namespace Vantuz.Host;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// Реализация IQuantumContext для управления квантом выполнения.
/// Согласно .traerules:169-174 - Host-контролируемое scheduling.
/// </summary>
internal sealed class QuantumContext : IQuantumContext, IDisposable
{
    private readonly Stopwatch _quantumTimer;
    private readonly Dictionary<string, object> _mutations;
    private readonly Func<Task> _stepCallback;
    private readonly Func<Task> _yieldCallback;
    private bool _disposed;

    public TimeSpan RemainingQuantum => TotalQuantum - _quantumTimer.Elapsed;
    public TimeSpan TotalQuantum { get; }
    public CancellationToken CancellationToken { get; }
    public IStatusReporter Reporter { get; }
    public IReadOnlyPayload Payload { get; }
    public ICommandMutations Mutations { get; }

    public QuantumContext(
        TimeSpan totalQuantum,
        IReadOnlyPayload payload,
        IStatusReporter reporter,
        CancellationToken cancellationToken,
        Func<Task> stepCallback,
        Func<Task> yieldCallback)
    {
        TotalQuantum = totalQuantum;
        Payload = payload;
        Reporter = reporter;
        CancellationToken = cancellationToken;
        _stepCallback = stepCallback;
        _yieldCallback = yieldCallback;
        _mutations = new Dictionary<string, object>();
        Mutations = new MutationsWrapper(_mutations);
        _quantumTimer = Stopwatch.StartNew();
    }

    public Task YieldQuantumAsync() => _yieldCallback();
    public Task StepAsync() => _stepCallback();

    /// <summary>
    /// Получает все мутации, накопленные за время кванта
    /// </summary>
    internal IReadOnlyDictionary<string, object> GetMutations() => _mutations;

    /// <summary>
    /// Останавивает таймер и возвращает оставшееся время
    /// </summary>
    internal TimeSpan Stop() => RemainingQuantum;

    public void Dispose()
    {
        if (!_disposed)
        {
            _quantumTimer.Stop();
            _disposed = true;
        }
    }

    /// <summary>
    /// Wrapper для мутаций
    /// </summary>
    private sealed class MutationsWrapper : ICommandMutations
    {
        private readonly Dictionary<string, object> _mutations;

        public MutationsWrapper(Dictionary<string, object> mutations)
        {
            _mutations = mutations;
        }

        public void Set<T>(string key, T value) where T : notnull
        {
            _mutations[key] = value;
        }
    }
}

/// <summary>
/// Реализация IReadOnlyPayload на основе Dictionary
/// </summary>
internal sealed class ReadOnlyPayload : IReadOnlyPayload
{
    private readonly IReadOnlyDictionary<string, object> _data;

    public ReadOnlyPayload(IReadOnlyDictionary<string, object> data)
    {
        _data = data;
    }

    public T? Get<T>(string key)
    {
        if (_data.TryGetValue(key, out var value) && value is T typed)
            return typed;
        return default;
    }

    public bool Contains(string key) => _data.ContainsKey(key);

    /// <summary>
    /// Возвращает все данные payload (используется CQRS адаптерами).
    /// </summary>
    internal IReadOnlyDictionary<string, object> GetAllInternal() => _data;
}

/// <summary>
/// Адаптер IReadOnlyPayload для совместимости с Dictionary.
/// Используется для передачи существующих данных в QueryContext.
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

    internal IReadOnlyDictionary<string, object> GetAllInternal() => _source;
}
