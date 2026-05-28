namespace Vantuz.Core;

using System;
using System.Collections.Concurrent;
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

// LEGACY: Устаревший контекст для обратной совместимости во время миграции
[Obsolete("Используйте QueryContext или CommandContext вместо ExecutionContext согласно ARM005")]
public class ExecutionContext
{
    public ConcurrentDictionary<string, object> Payload { get; } = new();
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

[Obsolete("Используйте QueryDelegate или CommandDelegate согласно ARM005")]
public delegate Task MiddlewareDelegate(ExecutionContext context);

// LEGACY: Устаревший интерфейс для обратной совместимости
[Obsolete("Используйте IQueryPlugin или ICommandPlugin согласно ARM005")]
public interface IVantuzPlugin : IAsyncDisposable
{
    string Name { get; }
    Task InvokeAsync(ExecutionContext context, JsonElement stepConfig, MiddlewareDelegate next);
}
