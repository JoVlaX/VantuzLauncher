namespace Vantuz.Core;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public interface IStatusReporter
{
    void ReportProgress(string taskName, double percentage);
    void ReportState(string message);
}

// CQRS Query Context - только чтение данных
public sealed class QueryContext
{
    public IReadOnlyDictionary<string, object> Payload { get; }
    public CancellationToken CancellationToken { get; }
    public IStatusReporter Reporter { get; }

    public QueryContext(IReadOnlyDictionary<string, object> payload, CancellationToken cancellationToken, IStatusReporter reporter)
    {
        Payload = payload;
        CancellationToken = cancellationToken;
        Reporter = reporter;
    }

    public T? Get<T>(string key) => Payload.TryGetValue(key, out var val) && val is T typedVal ? typedVal : default;
}

// CQRS Command Context - только запись/модификация состояния
public sealed class CommandContext
{
    private readonly Dictionary<string, object> _mutations = new();
    public bool IsAborted { get; private set; }
    public string? AbortReason { get; private set; }
    public CancellationToken CancellationToken { get; }
    public IStatusReporter Reporter { get; }

    public CommandContext(CancellationToken cancellationToken, IStatusReporter reporter)
    {
        CancellationToken = cancellationToken;
        Reporter = reporter;
    }

    public void Abort(string reason)
    {
        IsAborted = true;
        AbortReason = reason;
    }

    public void Set<T>(string key, T value) where T : notnull => _mutations[key] = value;
    public T? Get<T>(string key) => _mutations.TryGetValue(key, out var val) && val is T typedVal ? typedVal : default;
    public IReadOnlyDictionary<string, object> GetMutations() => _mutations;
}

public delegate Task QueryDelegate(QueryContext context);
public delegate Task CommandDelegate(CommandContext context);

public record FileState(string RelativePath, string Hash, long Size, string? Url);
public record MoveOperation(string SourcePath, string DestPath);

/// <summary>
/// Result of modpack manifest loading for delta-updates
/// </summary>
public class ModpackManifestResult
{
    public string Version { get; set; } = "";
    public string MinecraftVersion { get; set; } = "";
    public List<FileState> Files { get; set; } = new();
    public List<string> RemovedFiles { get; set; } = new();
    public object? RawManifest { get; set; }  // Original manifest for advanced processing
}

// ARM005: Строгое разделение CQRS - Query плагины только читают
public interface IQueryPlugin : IAsyncDisposable
{
    string Name { get; }
    Task<object?> ExecuteAsync(QueryContext context, JsonElement stepConfig);
}

// ARM005: Строгое разделение CQRS - Command плагины только пишут
public interface ICommandPlugin : IAsyncDisposable
{
    string Name { get; }
    Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig);
}

public record CommandResult(bool Success, string? ErrorMessage = null);

// ============================================
// UNIVERSAL GAME PROVIDER ABSTRACTION
// Per Armatura:126 - isolate external dependencies
// Per Armatura:72 - Anticorruption Layer for game-specific APIs
// ============================================

/// <summary>
/// Universal game provider contract. Implementations are game-specific (Minecraft, Terraria, etc.)
/// </summary>
public interface IGameProvider : IAsyncDisposable
{
    string ProviderName { get; }
    
    /// <summary>
    /// Check if version exists locally
    /// Per INVARIANT_THEORY.md §3.2 Nomadic - variables from manifest, not hardcoded
    /// </summary>
    Task<VersionCheckResult> CheckVersionAsync(
        string version, 
        string installDir, 
        Dictionary<string, string> variables,
        CancellationToken ct);
    
    /// <summary>
    /// Install/update the specified version
    /// Per INVARIANT_THEORY.md §3.2 Nomadic - variables from manifest, not hardcoded
    /// </summary>
    Task<InstallResult> InstallVersionAsync(
        string version, 
        string installDir, 
        Dictionary<string, string> variables,
        IStatusReporter reporter, 
        CancellationToken ct);
    
    /// <summary>
    /// Build launch parameters for OS.Executor
    /// Per INVARIANT_THEORY.md §3.2 Nomadic - variables from manifest, not hardcoded
    /// </summary>
    Task<LaunchParameters> BuildLaunchParametersAsync(
        string version, 
        string installDir, 
        Dictionary<string, string> variables,
        LaunchOptions options, 
        CancellationToken ct);
}

/// <summary>
/// Result of version check operation
/// </summary>
public record VersionCheckResult(
    bool Exists, 
    string? ErrorMessage = null
);

/// <summary>
/// Result of install operation
/// </summary>
public record InstallResult(
    bool Success, 
    string? ErrorMessage = null
);

/// <summary>
/// Launch parameters for OS.Executor
/// </summary>
public record LaunchParameters(
    string ExecutablePath,
    string Arguments,
    string WorkingDirectory,
    Dictionary<string, string>? EnvironmentVariables = null
);

/// <summary>
/// Options for launching a game
/// </summary>
public record LaunchOptions(
    string PlayerName,
    string? AccessToken = null,
    string? Uuid = null,
    int RamMb = 4096,
    string? JavaPath = null,
    Dictionary<string, object>? ExtraOptions = null
);

// ============================================
// GUI CREDENTIAL PROVIDER CONTRACTS
// Per INVARIANT_THEORY.md §1.2: Shared types must be in Core for cross-plugin measurability
// Per COMPOSITUM_SPECIFICATION.md §4.2: Interface in shared scope, implementation in plugin
// ============================================

/// <summary>
/// Credentials container for authentication.
/// Per INVARIANT_THEORY.md §2.2 CQRS: immutable data transfer object.
/// </summary>
public sealed class Credentials
{
    public string Username { get; }
    public string Password { get; }
    public bool RememberMe { get; }
    public int RamMb { get; }

    public Credentials(string username, string password, bool rememberMe, int ramMb)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Password = password ?? throw new ArgumentNullException(nameof(password));
        RememberMe = rememberMe;
        RamMb = ramMb;
    }
}

/// <summary>
/// Event args for credential submission.
/// Per COMPOSITUM_SPECIFICATION.md §4.2: Plugin capability notification contract.
/// </summary>
public sealed class CredentialsSubmittedEventArgs : EventArgs
{
    public Credentials Credentials { get; }
    public CredentialsSubmittedEventArgs(Credentials credentials) => Credentials = credentials;
}

/// <summary>
/// Interface for collecting user credentials through GUI.
/// Per COMPOSITUM_SPECIFICATION.md §4.2: Plugin-provided capability, not Core concern.
/// Per INVARIANT_THEORY.md §1.2: Interface must be in shared scope (Core) for cross-plugin compatibility.
/// </summary>
public interface ICredentialProvider
{
    /// <summary>
    /// Asynchronously collect credentials from user.
    /// Blocks until user submits or cancels.
    /// </summary>
    Task<Credentials> CollectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signal that credentials have been collected and auth is in progress.
    /// Transitions UI from "input mode" to "progress mode".
    /// </summary>
    void ShowProgress();

    /// <summary>
    /// Update status message during authentication flow.
    /// </summary>
    void UpdateStatus(string message);

    /// <summary>
    /// Event raised when credentials are submitted by user.
    /// </summary>
    event EventHandler<CredentialsSubmittedEventArgs>? CredentialsSubmitted;

    /// <summary>
    /// Event raised when user cancels credential collection.
    /// </summary>
    event EventHandler? CredentialsCancelled;
}

