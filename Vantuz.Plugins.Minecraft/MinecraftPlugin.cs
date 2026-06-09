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

    private readonly MinecraftGameQueryProvider _queryProvider = new();
    private readonly MinecraftGameCommandProvider _commandProvider = new();

    /// <summary>
    /// Registers the Minecraft game providers for use by Game.* plugins.
    /// Per INVARIANT_THEORY.md В§2.2: registers Query and Command facets separately.
    /// </summary>
    /// F_doc: {ExecuteAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ExecuteAsync behavior
    public Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        context.Set($"GameQueryProvider.{_queryProvider.ProviderName}", _queryProvider);
        context.Set($"GameCommandProvider.{_commandProvider.ProviderName}", _commandProvider);
        context.Reporter.ReportState($"РџСЂРѕРІР°Р№РґРµСЂ {_queryProvider.ProviderName} Р·Р°СЂРµРіРёСЃС‚СЂРёСЂРѕРІР°РЅ (Query + Command).");
        return Task.FromResult(new CommandResult(true));
    }

    public async ValueTask DisposeAsync()
    {
        await _queryProvider.DisposeAsync();
        await _commandProvider.DisposeAsync();
    }
}
