namespace Vantuz.Plugins.Minecraft;

using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// Plugin entry point that registers MinecraftGameProvider for use by universal game plugins.
/// Per Armatura:44 - explicit declaration in manifest.
/// Per Armatura:76-78 - Command plugin for state mutation (provider registration).
/// </summary>
public class MinecraftProviderCommand : ICommandPlugin
{
    public string Name => "Game.MinecraftProvider";
    
    private readonly MinecraftGameProvider _provider = new();

    /// <summary>
    /// Registers the IGameProvider instance for use by Game.* plugins.
    /// Per INVARIANT_THEORY.md §2.2: registers under both Query and Command facets.
    /// </summary>
    public Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        // Register provider under composite key for backward compat, and facet keys for CQRS purity
        context.Set($"GameProvider.{_provider.ProviderName}", _provider);
        context.Set($"GameQueryProvider.{_provider.ProviderName}", _provider);
        context.Set($"GameCommandProvider.{_provider.ProviderName}", _provider);
        context.Reporter.ReportState($"Провайдер {_provider.ProviderName} зарегистрирован.");
        return Task.FromResult(new CommandResult(true));
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
