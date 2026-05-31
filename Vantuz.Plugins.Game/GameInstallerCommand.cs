namespace Vantuz.Plugins.Game;

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Command: Universal game version installer using IGameProvider.
/// Per Armatura:126 - no external dependencies, works with any game.
/// </summary>
public class GameInstallerCommand : ICommandPlugin
{
    public string Name => "Game.Installer";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        try
        {
            // Get configuration from stepConfig per Armatura:44-45
            string providerName = stepConfig.TryGetProperty("provider", out var prov)
                ? Interpolate(prov.GetString() ?? "", context)
                : throw new InvalidOperationException("provider is missing in stepConfig");

            string versionName = stepConfig.TryGetProperty("version", out var vn)
                ? Interpolate(vn.GetString() ?? "", context)
                : throw new InvalidOperationException("version is missing in stepConfig");

            string installDir = stepConfig.TryGetProperty("installDir", out var id)
                ? Interpolate(id.GetString() ?? "", context)
                : throw new InvalidOperationException("installDir is missing in stepConfig");

            installDir = Path.GetFullPath(installDir.Replace('/', Path.DirectorySeparatorChar));

            // Check previous validator result
            var checkResult = context.Get<VersionCheckResult>("Game.VersionValidator.Result");
            if (checkResult == null)
            {
                // No validator ran, run check ourselves
                context.Reporter.ReportState($"Проверка версии {versionName} перед установкой...");
                var provider = ResolveProvider(context, providerName);
                if (provider == null)
                {
                    return new CommandResult(false, $"Game provider '{providerName}' not found");
                }
                checkResult = await provider.CheckVersionAsync(versionName, installDir, context.CancellationToken);
            }

            // Skip if already exists
            if (checkResult.Exists)
            {
                context.Reporter.ReportState($"Версия {versionName} уже установлена, пропуск установки.");
                context.Set("InstallSkipped", true);
                return new CommandResult(true);
            }

            context.Reporter.ReportState($"Установка версии {versionName}...");

            // Resolve provider
            var gameProvider = ResolveProvider(context, providerName);
            if (gameProvider == null)
            {
                return new CommandResult(false, $"Game provider '{providerName}' not found");
            }

            // Install version
            var installResult = await gameProvider.InstallVersionAsync(
                versionName, 
                installDir, 
                context.Reporter, 
                context.CancellationToken
            );

            if (!installResult.Success)
            {
                return new CommandResult(false, installResult.ErrorMessage ?? "Installation failed");
            }

            context.Reporter.ReportState($"Версия {versionName} успешно установлена.");
            context.Set("InstallSuccess", true);
            
            return new CommandResult(true);
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Resolves IGameProvider from context mutations.
    /// Providers register themselves with key "GameProvider.{ProviderName}"
    /// </summary>
    private static IGameProvider? ResolveProvider(CommandContext context, string providerName)
    {
        var key = $"GameProvider.{providerName}";
        return context.Get<IGameProvider>(key);
    }

    private static string Interpolate(string text, CommandContext context)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var mutations = context.GetMutations();
        foreach (var kvp in mutations)
        {
            text = text.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
        }
        return text;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
