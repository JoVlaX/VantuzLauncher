namespace Vantuz.Host;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// Р РµР°Р»РёР·Р°С†РёСЏ IQuantumContext РґР»СЏ СѓРїСЂР°РІР»РµРЅРёСЏ РєРІР°РЅС‚РѕРј РІС‹РїРѕР»РЅРµРЅРёСЏ.
/// РЎРѕРіР»Р°СЃРЅРѕ Armatura:169-174 - Host-РєРѕРЅС‚СЂРѕР»РёСЂСѓРµРјРѕРµ scheduling.
/// </summary>
internal sealed class QuantumContext : IQuantumContext, IDisposable
{
    private readonly Stopwatch _quantumTimer;
    private readonly Dictionary<string, object> _mutations;
    private readonly Func<Task> _stepCallback;
    private readonly Func<Task> _yieldCallback;
    private bool _disposed;

    public TimeSpan RemainingQuantum => TotalQuantum - _quantumTimer.Elapsed;
    /// F_doc: {TotalQuantum returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies TotalQuantum behavior
    public TimeSpan TotalQuantum { get; }
    /// F_doc: {CancellationToken returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies CancellationToken behavior
    public CancellationToken CancellationToken { get; }
    /// F_doc: {Reporter returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Reporter behavior
    public IStatusReporter Reporter { get; }
    /// F_doc: {Payload returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Payload behavior
    public IReadOnlyPayload Payload { get; }
    /// F_doc: {Mutations returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Mutations behavior
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
/// F_doc: {YieldQuantumAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies YieldQuantumAsync behavior

    public Task YieldQuantumAsync() => _yieldCallback();
    /// F_doc: {StepAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies StepAsync behavior
    public Task StepAsync() => _stepCallback();

    /// <summary>
    /// РџРѕР»СѓС‡Р°РµС‚ РІСЃРµ РјСѓС‚Р°С†РёРё, РЅР°РєРѕРїР»РµРЅРЅС‹Рµ Р·Р° РІСЂРµРјСЏ РєРІР°РЅС‚Р°
    /// </summary>
    internal IReadOnlyDictionary<string, object> GetMutations() => _mutations;

    /// <summary>
    /// РћСЃС‚Р°РЅР°РІРёРІР°РµС‚ С‚Р°Р№РјРµСЂ Рё РІРѕР·РІСЂР°С‰Р°РµС‚ РѕСЃС‚Р°РІС€РµРµСЃСЏ РІСЂРµРјСЏ
    /// </summary>
    internal TimeSpan Stop() => RemainingQuantum;
/// F_doc: {Dispose returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Dispose behavior

    public void Dispose()
    {
        if (!_disposed)
        {
            _quantumTimer.Stop();
            _disposed = true;
        }
    }

    /// <summary>
    /// Wrapper РґР»СЏ РјСѓС‚Р°С†РёР№
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
/// Р РµР°Р»РёР·Р°С†РёСЏ IReadOnlyPayload РЅР° РѕСЃРЅРѕРІРµ Dictionary
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
/// F_doc: {Contains returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Contains behavior

    public bool Contains(string key) => _data.ContainsKey(key);

    /// <summary>
    /// Р’РѕР·РІСЂР°С‰Р°РµС‚ РІСЃРµ РґР°РЅРЅС‹Рµ payload (РёСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ CQRS Р°РґР°РїС‚РµСЂР°РјРё).
    /// </summary>
    internal IReadOnlyDictionary<string, object> GetAllInternal() => _data;
}

/// <summary>
/// РђРґР°РїС‚РµСЂ IReadOnlyPayload РґР»СЏ СЃРѕРІРјРµСЃС‚РёРјРѕСЃС‚Рё СЃ Dictionary.
/// РСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ РґР»СЏ РїРµСЂРµРґР°С‡Рё СЃСѓС‰РµСЃС‚РІСѓСЋС‰РёС… РґР°РЅРЅС‹С… РІ QueryContext.
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
/// F_doc: {Contains returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Contains behavior

    public bool Contains(string key) => _source.ContainsKey(key);

    internal IReadOnlyDictionary<string, object> GetAllInternal() => _source;
}
