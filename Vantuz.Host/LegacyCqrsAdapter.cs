namespace Vantuz.Host;

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// Адаптер для интеграции ICommandPlugin в legacy IVantuzPlugin pipeline.
/// Оборачивает CQRS Command плагин для совместимости с RunAsync().
/// </summary>
#pragma warning disable CS0618 // IVantuzPlugin is obsolete but needed for legacy compatibility
internal sealed class LegacyCqrsCommandAdapter : IVantuzPlugin
#pragma warning restore CS0618
{
    private readonly ICommandPlugin _commandPlugin;

    public string Name => _commandPlugin.Name;

    public LegacyCqrsCommandAdapter(ICommandPlugin commandPlugin)
    {
        _commandPlugin = commandPlugin ?? throw new ArgumentNullException(nameof(commandPlugin));
    }

    public async Task InvokeAsync(
        Vantuz.Core.ExecutionContext context,
        JsonElement stepConfig,
        MiddlewareDelegate next)
    {
        try
        {
            // Создаем CommandContext из ExecutionContext
            var commandContext = new CommandContext(context.CancellationToken, context.Reporter);

            // Копируем данные из ExecutionContext.Payload как начальное состояние
            foreach (var kvp in context.Payload)
            {
                commandContext.Set(kvp.Key, kvp.Value);
            }

            // Выполняем команду
            var result = await _commandPlugin.ExecuteAsync(commandContext, stepConfig);

            // Копируем мутации обратно в ExecutionContext.Payload
            foreach (var kvp in commandContext.GetMutations())
            {
                context.Set(kvp.Key, kvp.Value);
            }

            // Если команда неуспешна - прерываем
            if (!result.Success)
            {
                context.Abort(result.ErrorMessage ?? $"Command {Name} failed");
                return;
            }

            // Переходим к следующему шагу
            await next(context);
        }
        catch (Exception ex)
        {
            context.Abort($"Plugin {Name} crashed: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _commandPlugin.DisposeAsync();
    }
}

/// <summary>
/// Адаптер для интеграции IQueryPlugin в legacy IVantuzPlugin pipeline.
/// Оборачивает CQRS Query плагин для совместимости с RunAsync().
/// </summary>
#pragma warning disable CS0618 // IVantuzPlugin is obsolete but needed for legacy compatibility
internal sealed class LegacyCqrsQueryAdapter : IVantuzPlugin
#pragma warning restore CS0618
{
    private readonly IQueryPlugin _queryPlugin;

    public string Name => _queryPlugin.Name;

    public LegacyCqrsQueryAdapter(IQueryPlugin queryPlugin)
    {
        _queryPlugin = queryPlugin ?? throw new ArgumentNullException(nameof(queryPlugin));
    }

    public async Task InvokeAsync(
        Vantuz.Core.ExecutionContext context,
        JsonElement stepConfig,
        MiddlewareDelegate next)
    {
        try
        {
            // Создаем QueryContext из ExecutionContext (read-only)
            var queryContext = new QueryContext(
                context.Payload,
                context.CancellationToken,
                context.Reporter);

            // Выполняем query
            var result = await _queryPlugin.ExecuteAsync(queryContext, stepConfig);

            // Если результат не null - сохраняем его в Payload для downstream шагов
            if (result != null)
            {
                context.Set($"{Name}.Result", result);
            }

            // Переходим к следующему шагу
            await next(context);
        }
        catch (Exception ex)
        {
            context.Abort($"Plugin {Name} crashed: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _queryPlugin.DisposeAsync();
    }
}
