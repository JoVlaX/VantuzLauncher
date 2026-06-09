namespace Vantuz.Core;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// ARM007: QuantizedNode - Р±Р°Р·РѕРІС‹Р№ РєР»Р°СЃСЃ РґР»СЏ РєРІР°РЅС‚РѕРІР°РЅРЅРѕРіРѕ РІС‹РїРѕР»РЅРµРЅРёСЏ РїР»Р°РіРёРЅРѕРІ.
/// Р—Р°РјРµРЅСЏРµС‚ free-form async Task РјРµС‚РѕРґС‹ РЅР° СЃС‚СЂРѕРіРѕ РєРѕРЅС‚СЂРѕР»РёСЂСѓРµРјС‹Рµ РєРІР°РЅС‚С‹ РІС‹РїРѕР»РЅРµРЅРёСЏ.
/// РЎРѕРіР»Р°СЃРЅРѕ Armatura:96-98 Рё Armatura:169-174.
/// </summary>
public abstract class QuantizedNode : IAsyncDisposable
{
    /// <summary>
    /// РЈРЅРёРєР°Р»СЊРЅРѕРµ РёРјСЏ СѓР·Р»Р° РґР»СЏ СЂРѕСѓС‚РёРЅРіР°
    /// </summary>
    /// F_doc: {Name returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Name behavior
    public abstract string Name { get; }

    /// <summary>
    /// Р’С‹РїРѕР»РЅСЏРµС‚ РѕРґРёРЅ РєРІР°РЅС‚ СЂР°Р±РѕС‚С‹.
    /// РњРµС‚РѕРґ Р”РћР›Р–Р•Рќ Р·Р°РІРµСЂС€РёС‚СЊСЃСЏ РІ С‚РµС‡РµРЅРёРµ РІС‹РґРµР»РµРЅРЅРѕРіРѕ РєРІР°РЅС‚Р° РІСЂРµРјРµРЅРё.
    /// Р•СЃР»Рё СЂР°Р±РѕС‚Р° РЅРµ Р·Р°РІРµСЂС€РµРЅР° - РІРѕР·РІСЂР°С‰Р°РµС‚ Yield РґР»СЏ РїСЂРѕРґРѕР»Р¶РµРЅРёСЏ.
    /// </summary>
    /// <param name="context">РљРѕРЅС‚РµРєСЃС‚ РІС‹РїРѕР»РЅРµРЅРёСЏ СЃ РѕСЃС‚Р°РІС€РёРјСЃСЏ РІСЂРµРјРµРЅРµРј РєРІР°РЅС‚Р°</param>
    /// <param name="stepConfig">РљРѕРЅС„РёРіСѓСЂР°С†РёСЏ С€Р°РіР° pipeline</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Р РµР·СѓР»СЊС‚Р°С‚ РєРІР°РЅС‚Р°: Complete, Yield РёР»Рё Error</returns>
    /// F_doc: {ExecuteQuantumAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ExecuteQuantumAsync behavior
    public abstract Task<QuantumResult> ExecuteQuantumAsync(
        IQuantumContext context,
        JsonElement stepConfig,
        CancellationToken ct);

    /// <summary>
    /// РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ СѓР·Р»Р° РїРµСЂРµРґ РїРµСЂРІС‹Рј РєРІР°РЅС‚РѕРј.
    /// Р’С‹Р·С‹РІР°РµС‚СЃСЏ РѕРґРёРЅ СЂР°Р· РїРµСЂРµРґ РЅР°С‡Р°Р»РѕРј РІС‹РїРѕР»РЅРµРЅРёСЏ.
    /// </summary>
    /// F_doc: {InitializeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies InitializeAsync behavior
    public virtual ValueTask InitializeAsync(JsonElement stepConfig, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// РћС‡РёСЃС‚РєР° СЂРµСЃСѓСЂСЃРѕРІ РїСЂРё Р·Р°РІРµСЂС€РµРЅРёРё СЂР°Р±РѕС‚С‹ СѓР·Р»Р°.
    /// </summary>
    /// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior
    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// РљРѕРЅС‚РµРєСЃС‚ РІС‹РїРѕР»РЅРµРЅРёСЏ РєРІР°РЅС‚Р° СЃ РєРѕРЅС‚СЂРѕР»РµРј РІСЂРµРјРµРЅРё Рё backpressure
/// </summary>
/// F_doc: {IQuantumContext returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies IQuantumContext behavior
public interface IQuantumContext
{
    /// <summary>
    /// РћСЃС‚Р°РІС€РµРµСЃСЏ РІСЂРµРјСЏ С‚РµРєСѓС‰РµРіРѕ РєРІР°РЅС‚Р°
    /// </summary>
    TimeSpan RemainingQuantum { get; }

    /// <summary>
    /// РџРѕР»РЅС‹Р№ СЂР°Р·РјРµСЂ РєРІР°РЅС‚Р° (РґР»СЏ СЂР°СЃС‡С‘С‚Р° РїСЂРѕС†РµРЅС‚РѕРІ)
    /// </summary>
    TimeSpan TotalQuantum { get; }

    /// <summary>
    /// CancellationToken РґР»СЏ РѕС‚РјРµРЅС‹ РѕРїРµСЂР°С†РёРё
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Р РµРїРѕСЂС‚С‘СЂ РґР»СЏ РёРЅС„РѕСЂРјРёСЂРѕРІР°РЅРёСЏ Рѕ РїСЂРѕРіСЂРµСЃСЃРµ
    /// </summary>
    IStatusReporter Reporter { get; }

    /// <summary>
    /// Payload РґР°РЅРЅС‹Рµ С‚РѕР»СЊРєРѕ РґР»СЏ С‡С‚РµРЅРёСЏ (Query)
    /// </summary>
    IReadOnlyPayload Payload { get; }

    /// <summary>
    /// РњСѓС‚Р°С†РёРё РґР»СЏ Р·Р°РїРёСЃРё СЂРµР·СѓР»СЊС‚Р°С‚РѕРІ (Command)
    /// </summary>
    ICommandMutations Mutations { get; }

    /// <summary>
    /// РџСЂРёРЅСѓРґРёС‚РµР»СЊРЅРѕ СѓСЃС‚СѓРїРёС‚СЊ РѕСЃС‚Р°РІС€РёР№СЃСЏ РєРІР°РЅС‚ Рё РІРµСЂРЅСѓС‚СЊСЃСЏ РІ РїР»Р°РЅРёСЂРѕРІС‰РёРє.
    /// РСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ РґР»СЏ РєРѕРѕРїРµСЂР°С‚РёРІРЅРѕР№ РјРЅРѕРіРѕР·Р°РґР°С‡РЅРѕСЃС‚Рё.
    /// </summary>
    Task YieldQuantumAsync();

    /// <summary>
    /// РџРµСЂРµР№С‚Рё Рє СЃР»РµРґСѓСЋС‰РµРјСѓ С€Р°РіСѓ pipeline.
    /// РњРѕР¶РµС‚ Р±С‹С‚СЊ РІС‹Р·РІР°РЅ С‚РѕР»СЊРєРѕ РїСЂРё Complete СЂРµР·СѓР»СЊС‚Р°С‚Рµ.
    /// </summary>
    Task StepAsync();
}

/// <summary>
/// Р РµР·СѓР»СЊС‚Р°С‚ РІС‹РїРѕР»РЅРµРЅРёСЏ РєРІР°РЅС‚Р°
/// </summary>
public readonly record struct QuantumResult
{
    /// F_doc: {Status returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Status behavior
    public QuantumStatus Status { get; init; }
    public object? State { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// РљРІР°РЅС‚ Р·Р°РІРµСЂС€С‘РЅ СѓСЃРїРµС€РЅРѕ, РјРѕР¶РЅРѕ РїРµСЂРµС…РѕРґРёС‚СЊ Рє СЃР»РµРґСѓСЋС‰РµРјСѓ С€Р°РіСѓ
    /// </summary>
    /// F_doc: {Complete returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Complete behavior
    public static QuantumResult Complete() => new() { Status = QuantumStatus.Complete };

    /// <summary>
    /// РўСЂРµР±СѓРµС‚СЃСЏ РґРѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Р№ РєРІР°РЅС‚ РґР»СЏ РїСЂРѕРґРѕР»Р¶РµРЅРёСЏ СЂР°Р±РѕС‚С‹.
    /// State СЃРѕС…СЂР°РЅСЏРµС‚СЃСЏ Рё РїРµСЂРµРґР°С‘С‚СЃСЏ РІ СЃР»РµРґСѓСЋС‰РёР№ РєРІР°РЅС‚.
    /// </summary>
    /// F_doc: {Yield returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Yield behavior
    public static QuantumResult Yield(object? state = null) => new()
    {
        Status = QuantumStatus.Yield,
        State = state
    };

    /// <summary>
    /// РћС€РёР±РєР° РІС‹РїРѕР»РЅРµРЅРёСЏ РєРІР°РЅС‚Р°
    /// </summary>
    /// F_doc: {Error returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Error behavior
    public static QuantumResult Error(string message) => new()
    {
        Status = QuantumStatus.Error,
        ErrorMessage = message
    };

    /// <summary>
    /// РџСЂРµСЂРІР°С‚СЊ РІС‹РїРѕР»РЅРµРЅРёРµ pipeline
    /// </summary>
    /// F_doc: {Abort returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Abort behavior
    public static QuantumResult Abort(string reason) => new()
    {
        Status = QuantumStatus.Abort,
        ErrorMessage = reason
    };
}
/// F_doc: {QuantumStatus returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies QuantumStatus behavior

public enum QuantumStatus
{
    /// <summary>РљРІР°РЅС‚ СѓСЃРїРµС€РЅРѕ Р·Р°РІРµСЂС€С‘РЅ</summary>
    Complete,
    /// <summary>РўСЂРµР±СѓРµС‚СЃСЏ РµС‰С‘ РѕРґРёРЅ РєРІР°РЅС‚</summary>
    Yield,
    /// <summary>РћС€РёР±РєР°, РЅРѕ РјРѕР¶РЅРѕ РїСЂРѕРґРѕР»Р¶РёС‚СЊ pipeline</summary>
    Error,
    /// <summary>РљСЂРёС‚РёС‡РµСЃРєР°СЏ РѕС€РёР±РєР°, РїСЂРµСЂРІР°С‚СЊ pipeline</summary>
    Abort
}

/// <summary>
/// РРЅС‚РµСЂС„РµР№СЃ РґР»СЏ С‡С‚РµРЅРёСЏ payload (Query side CQRS)
/// </summary>
/// F_doc: {IReadOnlyPayload returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies IReadOnlyPayload behavior
public interface IReadOnlyPayload
{
    T? Get<T>(string key);
    bool Contains(string key);
}

/// <summary>
/// РРЅС‚РµСЂС„РµР№СЃ РґР»СЏ РјСѓС‚Р°С†РёР№ (Command side CQRS)
/// </summary>
/// F_doc: {ICommandMutations returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ICommandMutations behavior
public interface ICommandMutations
{
    void Set<T>(string key, T value) where T : notnull;
}
