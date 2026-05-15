namespace Vantuz.Core;

using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public interface IStatusReporter
{
    void ReportProgress(string taskName, double percentage);
    void ReportState(string message);
}

public class ExecutionContext
{
    public ConcurrentDictionary<string, object> Payload { get; } = new ();
    public bool IsAborted { get; private set; }
    public string? AbortReason { get; private set; }
    
    public CancellationToken CancellationToken { get; }
    public IStatusReporter Reporter { get; }

    public ExecutionContext(CancellationToken cancellationToken, IStatusReporter reporter)
    {
        CancellationToken = cancellationToken;
        Reporter = reporter;
    }

    public void Abort(string reason)
    {
        IsAborted = true;
        AbortReason = reason;
    }

    public T? Get<T>(string key) => Payload.TryGetValue(key, out var val) && val is T typedVal ? typedVal : default;
    public void Set<T>(string key, T value) where T : notnull => Payload[key] = value;
}

public delegate Task MiddlewareDelegate(ExecutionContext context);

public record FileState(string RelativePath, string Hash, long Size, string? Url);
public record MoveOperation(string SourcePath, string DestPath);

public interface IVantuzPlugin : IAsyncDisposable
{
    string Name { get; }
    Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next);
}
