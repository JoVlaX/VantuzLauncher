namespace Vantuz.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

/// <summary>
/// CQRS Query Context - read-only payload access.
/// F_doc: {context returns wrong type or null for existing key}
/// E_doc: Unit test with mock payload dictionary verifies Get returns correct typed value
/// </summary>
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

/// <summary>
/// CQRS Command Context - state mutation and abort signaling.
/// F_doc: {mutation is lost after command execution, or abort signal is ignored}
/// E_doc: Unit test verifies Set + GetMutations roundtrip and Abort sets IsAborted
/// </summary>
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
/// F_doc: {FileState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies FileState behavior

    public void Set<T>(string key, T value) where T : notnull => _mutations[key] = value;
    /// F_doc: {MoveOperation returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies MoveOperation behavior
    public T? Get<T>(string key) => _mutations.TryGetValue(key, out var val) && val is T typedVal ? typedVal : default;
    public IReadOnlyDictionary<string, object> GetMutations() => _mutations;
}

public delegate Task QueryDelegate(QueryContext context);
/// F_doc: {ModpackManifestResult returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ModpackManifestResult behavior
public delegate Task CommandDelegate(CommandContext context);

/// F_doc: {Version returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Version behavior
/// <summary>
/// F_doc: {MinecraftVersion returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies MinecraftVersion behavior
/// File metadata for delta-update manifest entries.
/// F_doc: {Files returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Files behavior
/// F_doc: {Hash collision or Size mismatch with actual file}
/// E_doc: Unit test computes hash of known file and asserts FileState.Hash matches
/// </summary>
public record FileState(string RelativePath, string Hash, long Size, string? Url);

/// <summary>
/// File move operation descriptor.
/// F_doc: {SourcePath does not exist or DestPath already exists}
/// E_doc: Unit test with mock filesystem verifies move succeeds and source no longer exists
/// </summary>
public record MoveOperation(string SourcePath, string DestPath);

/// <summary>
/// Result of modpack manifest loading for delta-updates.
/// F_doc: {manifest parsing fails or produces empty Files list on valid input}
/// E_doc: Unit test with sample JSON manifest asserts Version and Files.Count > 0
/// </summary>
public class ModpackManifestResult
{
    public string Version { get; set; } = "";
    public string MinecraftVersion { get; set; } = "";
    public List<FileState> Files { get; set; } = new();
    public List<string> RemovedFiles { get; set; } = new();
    public object? RawManifest { get; set; }  // Original manifest for advanced processing
}

/// <summary>
/// ARM005: CQRS Query plugin contract вЂ” read-only operations.
/// F_doc: {plugin writes to context mutations or filesystem}
/// E_doc: Static analysis verifies no 'Set' or 'File.Write' calls in Query plugin implementations
/// </summary>
public interface IQueryPlugin : IAsyncDisposable
{
    string Name { get; }
    Task<object?> ExecuteAsync(QueryContext context, JsonElement stepConfig);
}

/// <summary>
/// ARM005: CQRS Command plugin contract вЂ” state-mutating operations.
/// F_doc: {plugin returns Success=true but leaves no observable side effect}
/// E_doc: Unit test with mock context verifies ExecuteAsync calls Set or Abort
/// </summary>
public interface ICommandPlugin : IAsyncDisposable
{
    string Name { get; }
    Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig);
}

/// <summary>
/// Result of a command plugin execution.
/// F_doc: {Success=true but ErrorMessage is non-null, or Success=false with null ErrorMessage}
/// E_doc: Unit test asserts CommandResult(Success=true) has null ErrorMessage; Failure has non-null ErrorMessage
/// </summary>
public record CommandResult(bool Success, string? ErrorMessage = null);

/// <summary>
/// Handle to a running child process launched by the pipeline.
/// Per INVARIANT_THEORY.md §2.2: GUI must not know process specifics; pipeline owns lifecycle.
/// F_doc: {Cancel requested but child process continues after 5s}
/// E_doc: {Test asserts process.HasExited == true within 5s of Terminate()}
/// </summary>
public interface IRunningProcessHandle
{
    void Terminate();
    bool HasExited { get; }
    int? ExitCode { get; }
}

/// <summary>
/// Wrapper around System.Diagnostics.Process exposing only invariant-compliant operations.
/// F_doc: {ProcessHandle wraps null or disposed Process}
/// E_doc: {Unit test with mock Process verifies Terminate calls Kill and Dispose}
/// </summary>
public sealed class ProcessHandle : IRunningProcessHandle
{
    private readonly Process _process;
    public ProcessHandle(Process process) => _process = process;
    public bool HasExited => _process.HasExited;
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;
    public void Terminate()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(true); // entire process tree
                _process.Dispose();
            }
        }
        catch (InvalidOperationException) { /* already exited */ }
        catch (NotSupportedException) { /* platform limitation */ }
    }
}

// ============================================
// UNIVERSAL GAME PROVIDER ABSTRACTION
// Per Armatura:126 - isolate external dependencies
// Per Armatura:72 - Anticorruption Layer for game-specific APIs
// ============================================

/// <summary>
/// CQRS Query facet: read-only game provider operations.
/// Per INVARIANT_THEORY.md В§2.2 - pure query, no side effects.
/// F_doc: {query returns stale version info or missing installDir}
/// E_doc: Unit test with mock file system verifying CheckVersionAsync and BuildLaunchParametersAsync
/// </summary>
public interface IGameQueryProvider
{
    /// <summary>
    /// Game provider display name for logging and UI.
    /// F_doc: {Name is null or empty}
    /// E_doc: Unit test asserts ProviderName is non-empty string
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Check if version exists locally.
    /// F_doc: {version exists but returns false, or version missing but returns true}
    /// E_doc: Unit test with mock file system: existing files return Exists=true; missing return Exists=false
    /// </summary>
    Task<VersionCheckResult> CheckVersionAsync(string version, string installDir, CancellationToken ct);

    /// <summary>
    /// Build launch parameters for OS.Executor.
    /// F_doc: {launch parameters contain invalid paths or missing authlib}
    /// E_doc: Unit test with mock path verifies LaunchParameters.ExecutablePath is non-empty
    /// </summary>
    Task<LaunchParameters> BuildLaunchParametersAsync(string version, string installDir, LaunchOptions options, CancellationToken ct);
}

/// <summary>
/// CQRS Command facet: state-mutating game provider operations.
/// Per INVARIANT_THEORY.md В§2.2 - only writes/modifies state.
/// F_doc: {install fails or times out, leaving partial filesystem state}
/// E_doc: Unit test with mock installer verifying InstallVersionAsync rollback
/// </summary>
public interface IGameCommandProvider
{
    /// <summary>
    /// Game provider display name for logging and UI.
    /// F_doc: {Name is null or empty}
    /// E_doc: Unit test asserts ProviderName is non-empty string
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Install/update the specified version.
    /// F_doc: {install fails silently, times out, or leaves partial filesystem state}
    /// E_doc: Unit test with mock installer verifying InstallVersionAsync rollback on failure
    /// </summary>
    Task<InstallResult> InstallVersionAsync(string version, string installDir, IStatusReporter reporter, CancellationToken ct, TimeSpan? timeout = null);
}

/// <summary>
/// Composite game provider contract. Implementations are game-specific (Minecraft, Terraria, etc.)
/// Per INVARIANT_THEORY.md В§2.2 CQRS - composed of pure Query + pure Command facets.
/// Deviation: DEVIATION-009 resolved 2026-06-08 by splitting into IGameQueryProvider + IGameCommandProvider.
/// F_doc: {implementation mixes Query and Command in single class}
/// E_doc: Builder ARM-BUILD-022 verifies no class implements IGameProvider without also implementing IGameQueryProvider and IGameCommandProvider separately
/// </summary>
public interface IGameProvider : IGameQueryProvider, IGameCommandProvider, IAsyncDisposable
{
}

/// <summary>
/// Result of version check operation.
/// F_doc: {Exists=true but ErrorMessage is non-null, or Exists=false with null ErrorMessage}
/// E_doc: Unit test asserts VersionCheckResult(true) has null ErrorMessage; failure has non-null ErrorMessage
/// </summary>
public record VersionCheckResult(
    bool Exists, 
    string? ErrorMessage = null
);

/// <summary>
/// Result of install operation.
/// F_doc: {Success=true but InstalledVersionName is null or empty}
/// E_doc: Unit test asserts InstallResult(true, null, "1.20.1").InstalledVersionName is non-null
/// </summary>
public record InstallResult(
    bool Success,
    string? ErrorMessage = null,
    string? InstalledVersionName = null
);

/// <summary>
/// Launch parameters for OS.Executor.
/// F_doc: {ExecutablePath is null, empty, or points to non-existent file}
/// E_doc: Unit test asserts LaunchParameters.ExecutablePath is non-empty and File.Exists returns true
/// </summary>
public record LaunchParameters(
    string ExecutablePath,
    string Arguments,
    string WorkingDirectory,
    Dictionary<string, string>? EnvironmentVariables = null
);

/// <summary>
/// Options for launching a game.
/// F_doc: {PlayerName is null or empty, or RamMb is negative}
/// E_doc: Unit test asserts LaunchOptions("test").PlayerName is non-empty and RamMb >= 0
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
/// User credentials for authentication.
/// F_doc: {Username or Password is null or empty}
/// E_doc: Unit test asserts Credentials("user", "pass").Username is non-empty
/// </summary>
public record Credentials(
    string Username,
    string Password,
    bool RememberMe = false,
    int RamMb = 4096
);

/// <summary>
/// Event args for credential submission.
/// F_doc: {Credentials property is null or contains invalid data}
/// E_doc: Unit test asserts new CredentialsSubmittedEventArgs { Credentials = valid }.Credentials is non-null
/// </summary>
public class CredentialsSubmittedEventArgs : EventArgs
{
    public required Credentials Credentials { get; init; }
}

