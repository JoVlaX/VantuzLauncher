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
    /// </summary>
    public Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        // Register provider for universal game plugins to resolve
        context.Set($"GameProvider.{_provider.ProviderName}", _provider);
        context.Reporter.ReportState($"Провайдер {_provider.ProviderName} зарегистрирован.");
        return Task.FromResult(new CommandResult(true));
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
