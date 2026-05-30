namespace Vantuz.Host;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// Адаптер для миграции legacy IVantuzPlugin на QuantizedNode.
/// Позволяет старым плагинам работать в новой quantum-based архитектуре.
/// Оборачивает free-form async в один большой квант.
/// </summary>
/// <remarks>
/// Это временное решение для обратной совместимости.
/// Новые плагины должны наследоваться от QuantizedNode напрямую.
/// </remarks>
internal sealed class LegacyPluginAdapter : QuantizedNode
{
    private readonly IVantuzPlugin _legacyPlugin;
    private readonly Vantuz.Core.ExecutionContext _legacyContext;
    private TaskCompletionSource<bool>? _completionTcs;
    private MiddlewareDelegate? _nextCallback;

    public override string Name => _legacyPlugin.Name;

    public LegacyPluginAdapter(IVantuzPlugin legacyPlugin, Vantuz.Core.ExecutionContext context)
    {
        _legacyPlugin = legacyPlugin;
        _legacyContext = context;
    }

    public override async Task<QuantumResult> ExecuteQuantumAsync(
        IQuantumContext context,
        JsonElement stepConfig,
        CancellationToken ct)
    {
        // Для legacy плагинов выполняем всю работу в одном кванте
        // (без реального квантования, но с соблюдением интерфейса)
        try
        {
            // Создаём адаптер для обратного вызова next()
            _completionTcs = new TaskCompletionSource<bool>();
            _nextCallback = (ctx) =>
            {
                _completionTcs.TrySetResult(true);
                return Task.CompletedTask;
            };

            // Вызываем legacy плагин
            await _legacyPlugin.InvokeAsync(_legacyContext, stepConfig, _nextCallback);

            // Применяем мутации из legacy context к quantum context
            foreach (var kvp in _legacyContext.Payload)
            {
                context.Mutations.Set(kvp.Key, kvp.Value);
            }

            // Проверяем abort
            if (_legacyContext.IsAborted)
            {
                return QuantumResult.Abort(_legacyContext.AbortReason ?? "Legacy plugin aborted");
            }

            return QuantumResult.Complete();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return QuantumResult.Error(ex.Message);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await _legacyPlugin.DisposeAsync();
    }
}

/// <summary>
/// Адаптер IReadOnlyPayload для legacy ExecutionContext.Payload
/// </summary>
internal sealed class PayloadAdapter : IReadOnlyPayload
{
    private readonly IReadOnlyDictionary<string, object> _source;

    public PayloadAdapter(IReadOnlyDictionary<string, object> source)
    {
        _source = source;
    }

    public T? Get<T>(string key)
    {
        if (_source.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    public bool Contains(string key) => _source.ContainsKey(key);

    /// <summary>
    /// Возвращает все данные из source (используется CQRS адаптерами).
    /// </summary>
    internal IReadOnlyDictionary<string, object> GetAllInternal() => _source;
}
