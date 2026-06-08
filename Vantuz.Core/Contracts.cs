namespace Vantuz.Core;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Pipeline status reporter contract.
/// F_doc: {reporter not receiving progress updates or state messages during pipeline execution}
/// E_doc: Mock reporter assertion verifying ReportProgress/ReportState calls in test pipeline
/// </summary>
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
/// CQRS Query facet: read-only game provider operations.
/// Per INVARIANT_THEORY.md §2.2 - pure query, no side effects.
/// F_doc: {query returns stale version info or missing installDir}
/// E_doc: Unit test with mock file system verifying CheckVersionAsync and BuildLaunchParametersAsync
/// </summary>
public interface IGameQueryProvider
{
    string ProviderName { get; }

    /// <summary>
    /// Check if version exists locally
    /// </summary>
    Task<VersionCheckResult> CheckVersionAsync(string version, string installDir, CancellationToken ct);

    /// <summary>
    /// Build launch parameters for OS.Executor
    /// </summary>
    Task<LaunchParameters> BuildLaunchParametersAsync(string version, string installDir, LaunchOptions options, CancellationToken ct);
}

/// <summary>
/// CQRS Command facet: state-mutating game provider operations.
/// Per INVARIANT_THEORY.md §2.2 - only writes/modifies state.
/// F_doc: {install fails or times out, leaving partial filesystem state}
/// E_doc: Unit test with mock installer verifying InstallVersionAsync rollback
/// </summary>
public interface IGameCommandProvider
{
    string ProviderName { get; }

    /// <summary>
    /// Install/update the specified version
    /// </summary>
    Task<InstallResult> InstallVersionAsync(string version, string installDir, IStatusReporter reporter, CancellationToken ct, TimeSpan? timeout = null);
}

/// <summary>
/// Composite game provider contract. Implementations are game-specific (Minecraft, Terraria, etc.)
/// Per INVARIANT_THEORY.md §2.2 CQRS - composed of pure Query + pure Command facets.
/// Deviation: DEVIATION-009 resolved 2026-06-08 by splitting into IGameQueryProvider + IGameCommandProvider.
/// </summary>
public interface IGameProvider : IGameQueryProvider, IGameCommandProvider, IAsyncDisposable
{
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
    string? ErrorMessage = null,
    string? InstalledVersionName = null
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

/// <summary>
/// Credential provider interface for UI and headless modes.
/// Per INVARIANT_THEORY.md: Shared contract to avoid AssemblyLoadContext isolation issues.
/// F_doc: {ICredentialProvider implementation missing CollectAsync, ShowProgress, or UpdateStatus}
/// E_doc: Interface implementation test via typeof(ICredentialProvider).GetMethods()
/// </summary>
public interface ICredentialProvider
{
    Task<Credentials> CollectAsync(CancellationToken cancellationToken = default);
    void ShowProgress();
    void UpdateStatus(string message);
    event EventHandler<CredentialsSubmittedEventArgs>? CredentialsSubmitted;
    event EventHandler? CredentialsCancelled;
}

/// <summary>
/// User credentials for authentication
/// </summary>
public record Credentials(
    string Username,
    string Password,
    bool RememberMe = false,
    int RamMb = 4096
);

/// <summary>
/// Event args for credential submission
/// </summary>
public class CredentialsSubmittedEventArgs : EventArgs
{
    public required Credentials Credentials { get; init; }
}

