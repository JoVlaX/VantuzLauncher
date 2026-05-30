namespace Vantuz.Host;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// Адаптер для ICommandPlugin - оборачивает Command-операции в QuantizedNode.
/// Согласно .traerules:98 - наследуется от QuantizedNode, без free-form async.
/// </summary>
internal sealed class CqrsCommandAdapter : QuantizedNode
{
    private readonly ICommandPlugin _commandPlugin;

    public override string Name => _commandPlugin.Name;

    public CqrsCommandAdapter(ICommandPlugin commandPlugin)
    {
        _commandPlugin = commandPlugin ?? throw new ArgumentNullException(nameof(commandPlugin));
    }

    public override async Task<QuantumResult> ExecuteQuantumAsync(
        IQuantumContext context,
        JsonElement stepConfig,
        CancellationToken ct)
    {
        try
        {
            // Создаем CommandContext из IQuantumContext
            var commandContext = new CommandContext(context.CancellationToken, context.Reporter);

            // Копируем существующие мутации из payload как начальное состояние
            var payloadData = ExtractPayloadData(context.Payload);
            foreach (var kvp in payloadData)
            {
                commandContext.Set(kvp.Key, kvp.Value);
            }

            // Выполняем команду (траерулы:98 - один квант, без блокировок)
            var result = await _commandPlugin.ExecuteAsync(commandContext, stepConfig);

            // Проверяем отмену
            if (ct.IsCancellationRequested)
            {
                return QuantumResult.Error("Operation cancelled");
            }

            // Если команда неуспешна - прерываем pipeline
            if (!result.Success)
            {
                return QuantumResult.Abort(result.ErrorMessage ?? $"Command {Name} failed");
            }

            // Копируем мутации обратно в quantum context
            foreach (var kvp in commandContext.GetMutations())
            {
                context.Mutations.Set(kvp.Key, kvp.Value);
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
        await _commandPlugin.DisposeAsync();
    }

    /// <summary>
    /// Извлекает данные из IReadOnlyPayload используя pattern matching для внутренних типов.
    /// </summary>
    private static IReadOnlyDictionary<string, object> ExtractPayloadData(IReadOnlyPayload payload)
    {
        // Pattern matching для внутренних реализаций
        return payload switch
        {
            ReadOnlyPayload rop => rop.GetAllInternal(),
            PayloadAdapter pa => pa.GetAllInternal(),
            _ => new Dictionary<string, object>() // Fallback для внешних реализаций
        };
    }
}

/// <summary>
/// Адаптер для IQueryPlugin - оборачивает Query-операции в QuantizedNode.
/// Согласно .traerules:98 - наследуется от QuantizedNode.
/// Query только читает, не модифицирует состояние (traerules:76-79).
/// </summary>
internal sealed class CqrsQueryAdapter : QuantizedNode
{
    private readonly IQueryPlugin _queryPlugin;

    public override string Name => _queryPlugin.Name;

    public CqrsQueryAdapter(IQueryPlugin queryPlugin)
    {
        _queryPlugin = queryPlugin ?? throw new ArgumentNullException(nameof(queryPlugin));
    }

    public override async Task<QuantumResult> ExecuteQuantumAsync(
        IQuantumContext context,
        JsonElement stepConfig,
        CancellationToken ct)
    {
        try
        {
            // Создаем QueryContext из IQuantumContext (read-only)
            var payloadData = ExtractPayloadData(context.Payload);
            var queryContext = new QueryContext(payloadData, context.CancellationToken, context.Reporter);

            // Выполняем query (траерулы:98 - один квант)
            var result = await _queryPlugin.ExecuteAsync(queryContext, stepConfig);

            // Проверяем отмену
            if (ct.IsCancellationRequested)
            {
                return QuantumResult.Error("Operation cancelled");
            }

            // Если результат не null - сохраняем его в мутации для downstream шагов
            if (result != null)
            {
                context.Mutations.Set($"{Name}.Result", result);
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
        await _queryPlugin.DisposeAsync();
    }

    /// <summary>
    /// Извлекает данные из IReadOnlyPayload используя pattern matching для внутренних типов.
    /// </summary>
    private static IReadOnlyDictionary<string, object> ExtractPayloadData(IReadOnlyPayload payload)
    {
        // Pattern matching для внутренних реализаций
        return payload switch
        {
            ReadOnlyPayload rop => rop.GetAllInternal(),
            PayloadAdapter pa => pa.GetAllInternal(),
            _ => new Dictionary<string, object>() // Fallback для внешних реализаций
        };
    }
}
