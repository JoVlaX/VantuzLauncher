namespace Vantuz.Host;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// РџР»Р°РЅРёСЂРѕРІС‰РёРє РєРІР°РЅС‚РѕРІРѕРіРѕ РІС‹РїРѕР»РЅРµРЅРёСЏ РґР»СЏ QuantizedNode.
/// Р РµР°Р»РёР·СѓРµС‚:
/// - Task Bundling (Armatura:174)
/// - Cooperative yielding (Armatura:172)
/// - Host-controlled scheduling (Armatura:172)
/// - Continuous Proportional Backoff (Armatura:223-229)
/// </summary>
internal sealed class QuantumScheduler
{
    // РљРѕРЅС„РёРіСѓСЂР°С†РёСЏ РєРІР°РЅС‚РѕРІ - РјРѕР¶РµС‚ Р±С‹С‚СЊ РЅР°СЃС‚СЂРѕРµРЅР° С‡РµСЂРµР· manifest
    private readonly TimeSpan _defaultQuantum = TimeSpan.FromMilliseconds(16); // ~60 FPS
    private readonly int _maxYieldsPerNode = 100; // Р—Р°С‰РёС‚Р° РѕС‚ Р±РµСЃРєРѕРЅРµС‡РЅРѕРіРѕ Yield

    private readonly IStatusReporter _reporter;
    private readonly Dictionary<string, object> _globalPayload;

    public QuantumScheduler(IStatusReporter reporter, Dictionary<string, object>? initialPayload = null)
    {
        _reporter = reporter;
        _globalPayload = initialPayload ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Р’С‹РїРѕР»РЅСЏРµС‚ pipeline РёР· QuantizedNode СЃ Р±Р°С‚С‡РёРЅРіРѕРј Рё РєРІР°РЅС‚РѕРІР°РЅРёРµРј.
    /// </summary>
    public async Task<ExecutionResult> ExecutePipelineAsync(
        IReadOnlyList<(QuantizedNode Node, JsonElement Config)> pipeline,
        CancellationToken ct)
    {
        var mergedPayload = new Dictionary<string, object>(_globalPayload);
        var stateSnapshots = new Dictionary<QuantizedNode, object?>();

        _reporter.ReportState($"[DEBUG] Pipeline has {pipeline.Count} steps: {string.Join(", ", pipeline.Select(p => p.Node.Name))}");

        for (int i = 0; i < pipeline.Count; i++)
        {
            var (node, config) = pipeline[i];
            var result = await ExecuteNodeAsync(
                node,
                config,
                mergedPayload,
                stateSnapshots.GetValueOrDefault(node),
                ct);

            switch (result.Status)
            {
                case QuantumStatus.Complete:
                    // РџСЂРёРјРµРЅСЏРµРј РјСѓС‚Р°С†РёРё Рє РіР»РѕР±Р°Р»СЊРЅРѕРјСѓ payload
                    if (result.Mutations != null)
                    {
                        foreach (var kvp in result.Mutations)
                        {
                            mergedPayload[kvp.Key] = kvp.Value;
                        }
                    }
                    stateSnapshots.Remove(node);
                    _reporter.ReportState($"[STEP] {node.Name} completed");
                    break;

                case QuantumStatus.Yield:
                    // РЎРѕС…СЂР°РЅСЏРµРј СЃРѕСЃС‚РѕСЏРЅРёРµ Рё РІРµСЂРЅС‘РјСЃСЏ Рє СЌС‚РѕРјСѓ СѓР·Р»Сѓ
                    stateSnapshots[node] = result.State;
                    i--; // РџРѕРІС‚РѕСЂРёРј СЌС‚РѕС‚ Р¶Рµ СѓР·РµР»
                    await Task.Yield(); // Р”Р°С‘Рј РїР»Р°РЅРёСЂРѕРІС‰РёРєСѓ РћРЎ РїРµСЂРµРєР»СЋС‡РёС‚СЊ РєРѕРЅС‚РµРєСЃС‚
                    break;

                case QuantumStatus.Error:
                    // РћС€РёР±РєР°, РЅРѕ РїСЂРѕРґРѕР»Р¶Р°РµРј pipeline
                    _reporter.ReportState($"[WARN] Node {node.Name} error: {result.ErrorMessage}");
                    stateSnapshots.Remove(node);
                    break;

                case QuantumStatus.Abort:
                    // РљСЂРёС‚РёС‡РµСЃРєР°СЏ РѕС€РёР±РєР°, РїСЂРµСЂС‹РІР°РµРј pipeline
                    return ExecutionResult.Failure(result.ErrorMessage ?? $"Node {node.Name} aborted");
            }

            ct.ThrowIfCancellationRequested();
        }

        return ExecutionResult.Success(mergedPayload);
    }

    /// <summary>
    /// Р’С‹РїРѕР»РЅСЏРµС‚ РѕРґРёРЅ СѓР·РµР» СЃ РєРІР°РЅС‚РѕРІР°РЅРёРµРј РґРѕ Complete/Error/Abort.
    /// </summary>
    private async Task<NodeExecutionResult> ExecuteNodeAsync(
        QuantizedNode node,
        JsonElement config,
        IReadOnlyDictionary<string, object> payload,
        object? previousState,
        CancellationToken ct)
    {
        var mutationsAccumulator = new Dictionary<string, object>();
        var readOnlyPayload = new ReadOnlyPayload(payload);
        int yieldCount = 0;
        object? currentState = previousState;

        // РРЅРёС†РёР°Р»РёР·Р°С†РёСЏ РїСЂРё РїРµСЂРІРѕРј Р·Р°РїСѓСЃРєРµ
        if (previousState == null)
        {
            await node.InitializeAsync(config, ct);
        }

        while (true)
        {
            if (yieldCount > _maxYieldsPerNode)
            {
                return new NodeExecutionResult
                {
                    Status = QuantumStatus.Abort,
                    ErrorMessage = $"Node {node.Name} exceeded max yields ({_maxYieldsPerNode})"
                };
            }

            // РЎРѕР·РґР°С‘Рј РєРѕРЅС‚РµРєСЃС‚ РґР»СЏ СЌС‚РѕРіРѕ РєРІР°РЅС‚Р°
            using var quantum = new QuantumContext(
                _defaultQuantum,
                readOnlyPayload,
                _reporter,
                ct,
                () => Task.CompletedTask,      // Step - РІС‹Р·С‹РІР°РµС‚СЃСЏ РїР»Р°РЅРёСЂРѕРІС‰РёРєРѕРј
                async () => await Task.Yield()   // Yield - РґР°С‘Рј РїР»Р°РЅРёСЂРѕРІС‰РёРєСѓ РћРЎ
            );

            QuantumResult result;
            try
            {
                result = await node.ExecuteQuantumAsync(quantum, config, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = QuantumResult.Error(ex.Message);
            }

            // РђРєРєСѓРјСѓР»РёСЂСѓРµРј РјСѓС‚Р°С†РёРё РёР· СЌС‚РѕРіРѕ РєРІР°РЅС‚Р°
            foreach (var kvp in quantum.GetMutations())
            {
                mutationsAccumulator[kvp.Key] = kvp.Value;
            }

            switch (result.Status)
            {
                case QuantumStatus.Complete:
                    return new NodeExecutionResult
                    {
                        Status = QuantumStatus.Complete,
                        Mutations = mutationsAccumulator
                    };

                case QuantumStatus.Yield:
                    yieldCount++;
                    currentState = result.State;
                    // РџСЂРѕРґРѕР»Р¶Р°РµРј РЅР° СЃР»РµРґСѓСЋС‰РµРј РєРІР°РЅС‚Рµ
                    break;

                case QuantumStatus.Error:
                    return new NodeExecutionResult
                    {
                        Status = QuantumStatus.Error,
                        ErrorMessage = result.ErrorMessage,
                        Mutations = mutationsAccumulator
                    };

                case QuantumStatus.Abort:
                    return new NodeExecutionResult
                    {
                        Status = QuantumStatus.Abort,
                        ErrorMessage = result.ErrorMessage
                    };
            }

            // Р•СЃР»Рё Yield Рё РјС‹ Р·РґРµСЃСЊ - РїР»Р°РЅРёСЂРѕРІС‰РёРє СѓР¶Рµ РІС‹Р·РІР°Р» Task.Yield() РІРЅСѓС‚СЂРё quantum
        }
    }

    /// <summary>
    /// Р РµР·СѓР»СЊС‚Р°С‚ РІС‹РїРѕР»РЅРµРЅРёСЏ РІСЃРµРіРѕ pipeline
    /// </summary>
    public readonly record struct ExecutionResult
    {
        /// F_doc: {IsSuccess returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies IsSuccess behavior
        public bool IsSuccess { get; init; }
        public IReadOnlyDictionary<string, object>? FinalPayload { get; init; }
        public string? ErrorMessage { get; init; }
/// F_doc: {Success returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Success behavior

        public static ExecutionResult Success(IReadOnlyDictionary<string, object> payload) =>
            new() { IsSuccess = true, FinalPayload = payload };
/// F_doc: {Failure returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Failure behavior

        public static ExecutionResult Failure(string message) =>
            new() { IsSuccess = false, ErrorMessage = message };
    }

    /// <summary>
    /// Р РµР·СѓР»СЊС‚Р°С‚ РІС‹РїРѕР»РЅРµРЅРёСЏ РѕРґРЅРѕРіРѕ СѓР·Р»Р° (РІРѕР·РјРѕР¶РЅРѕ, С‡РµСЂРµР· РЅРµСЃРєРѕР»СЊРєРѕ РєРІР°РЅС‚РѕРІ)
    /// </summary>
    private struct NodeExecutionResult
    {
        /// F_doc: {Status returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Status behavior
        public QuantumStatus Status { get; init; }
        public Dictionary<string, object>? Mutations { get; init; }
        public string? ErrorMessage { get; init; }
        public object? State { get; init; }
    }
}
