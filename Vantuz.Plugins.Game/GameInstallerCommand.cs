namespace Vantuz.Plugins.Game;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Command: Universal game version installer using IGameProvider.
/// Per Armatura:126 - no external dependencies, works with any game.
/// </summary>
public class GameInstallerCommand : ICommandPlugin
{
    public string Name => "Game.InstallerCommand";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        try
        {
            // TEST MODE: Deterministic behavior per INVARIANT_THEORY.md §1.1
            bool isTestMode = stepConfig.TryGetProperty("_testMode", out var testModeProp) && testModeProp.GetBoolean();
            if (isTestMode)
            {
                context.Reporter.ReportState("[TEST MODE] GameInstallerCommand - simulating installation");
                context.Set("InstallSuccess", true);
                context.Set("InstallTestMode", true);
                return new CommandResult(true);
            }

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

            // Per INVARIANT_THEORY.md §3.2 Nomadic - extract variables for path interpolation
            var variables = ExtractVariables(context);
            
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
                checkResult = await provider.CheckVersionAsync(versionName, installDir, variables, context.CancellationToken);
            }

            // Skip if already exists
            if (checkResult.Exists)
            {
                context.Reporter.ReportState($"Версия {versionName} уже установлена, пропуск установки.");
                context.Set("InstallSkipped", true);
                return new CommandResult(true);
            }

            context.Reporter.ReportState($"[GameInstaller] Starting installation for {versionName}...");

            // Resolve provider
            var gameProvider = ResolveProvider(context, providerName);
            if (gameProvider == null)
            {
                context.Reporter.ReportState($"[GameInstaller ERROR] Provider '{providerName}' not found in context");
                return new CommandResult(false, $"Game provider '{providerName}' not found");
            }
            context.Reporter.ReportState($"[GameInstaller] Provider resolved: {providerName}");

            // Install version with variables per INVARIANT_THEORY.md §3.2 Nomadic
            var installResult = await gameProvider.InstallVersionAsync(
                versionName, 
                installDir,
                variables,
                context.Reporter, 
                context.CancellationToken
            );

            if (!installResult.Success)
            {
                context.Reporter.ReportState($"[GameInstaller ERROR] Installation failed: {installResult.ErrorMessage}");
                return new CommandResult(false, installResult.ErrorMessage ?? "Installation failed");
            }

            context.Reporter.ReportState($"[GameInstaller] Version {versionName} installed successfully");
            context.Set("InstallSuccess", true);
            
            return new CommandResult(true);
        }
        catch (Exception ex)
        {
            context.Reporter.ReportState($"[GameInstaller EXCEPTION] {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                context.Reporter.ReportState($"[GameInstaller EXCEPTION] Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
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
    
    /// <summary>
    /// Extracts variables from context mutations per INVARIANT_THEORY.md §3.2 Nomadic Invariant.
    /// Variables travel with manifest, not hardcoded in code.
    /// </summary>
    private static Dictionary<string, string> ExtractVariables(CommandContext context)
    {
        var variables = new Dictionary<string, string>();
        
        // Extract string variables from context mutations
        var mutations = context.GetMutations();
        foreach (var kvp in mutations)
        {
            if (kvp.Value is string strValue)
            {
                variables[kvp.Key] = strValue;
            }
        }
        
        // Per INVARIANT_THEORY.md §3.2 - ensure critical variables have fallbacks
        // These should normally come from manifest, but we provide defaults for robustness
        if (!variables.ContainsKey("mcDir"))
        {
            variables["mcDir"] = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".vantuzlauncher");
        }
        
        return variables;
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
