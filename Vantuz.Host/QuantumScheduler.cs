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
/// Планировщик квантового выполнения для QuantizedNode.
/// Реализует:
/// - Task Bundling (Armatura:174)
/// - Cooperative yielding (Armatura:172)
/// - Host-controlled scheduling (Armatura:172)
/// - Continuous Proportional Backoff (Armatura:223-229)
/// </summary>
internal sealed class QuantumScheduler
{
    // Конфигурация квантов - может быть настроена через manifest
    private readonly TimeSpan _defaultQuantum = TimeSpan.FromMilliseconds(16); // ~60 FPS
    private readonly int _maxYieldsPerNode = 100; // Защита от бесконечного Yield

    private readonly IStatusReporter _reporter;
    private readonly Dictionary<string, object> _globalPayload;

    public QuantumScheduler(IStatusReporter reporter, Dictionary<string, object>? initialPayload = null)
    {
        _reporter = reporter;
        _globalPayload = initialPayload ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Выполняет pipeline из QuantizedNode с батчингом и квантованием.
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
                    // Применяем мутации к глобальному payload
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
                    // Сохраняем состояние и вернёмся к этому узлу
                    stateSnapshots[node] = result.State;
                    i--; // Повторим этот же узел
                    await Task.Yield(); // Даём планировщику ОС переключить контекст
                    break;

                case QuantumStatus.Error:
                    // Ошибка, но продолжаем pipeline
                    _reporter.ReportState($"[WARN] Node {node.Name} error: {result.ErrorMessage}");
                    stateSnapshots.Remove(node);
                    break;

                case QuantumStatus.Abort:
                    // Критическая ошибка, прерываем pipeline
                    return ExecutionResult.Failure(result.ErrorMessage ?? $"Node {node.Name} aborted");
            }

            ct.ThrowIfCancellationRequested();
        }

        return ExecutionResult.Success(mergedPayload);
    }

    /// <summary>
    /// Выполняет один узел с квантованием до Complete/Error/Abort.
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

        // Инициализация при первом запуске
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

            // Создаём контекст для этого кванта
            using var quantum = new QuantumContext(
                _defaultQuantum,
                readOnlyPayload,
                _reporter,
                ct,
                () => Task.CompletedTask,      // Step - вызывается планировщиком
                async () => await Task.Yield()   // Yield - даём планировщику ОС
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

            // Аккумулируем мутации из этого кванта
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
                    // Продолжаем на следующем кванте
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

            // Если Yield и мы здесь - планировщик уже вызвал Task.Yield() внутри quantum
        }
    }

    /// <summary>
    /// Результат выполнения всего pipeline
    /// </summary>
    public readonly record struct ExecutionResult
    {
        public bool IsSuccess { get; init; }
        public IReadOnlyDictionary<string, object>? FinalPayload { get; init; }
        public string? ErrorMessage { get; init; }

        public static ExecutionResult Success(IReadOnlyDictionary<string, object> payload) =>
            new() { IsSuccess = true, FinalPayload = payload };

        public static ExecutionResult Failure(string message) =>
            new() { IsSuccess = false, ErrorMessage = message };
    }

    /// <summary>
    /// Результат выполнения одного узла (возможно, через несколько квантов)
    /// </summary>
    private struct NodeExecutionResult
    {
        public QuantumStatus Status { get; init; }
        public Dictionary<string, object>? Mutations { get; init; }
        public string? ErrorMessage { get; init; }
        public object? State { get; init; }
    }
}
