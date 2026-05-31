namespace Vantuz.Plugins.Game;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Command: Universal game launch command using IGameProvider.
/// Per Armatura:126 - no external dependencies, works with any game.
/// </summary>
public class GameLaunchCommand : ICommandPlugin
{
    public string Name => "Game.LaunchCommand";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        try
        {
            // TEST MODE: Deterministic behavior per INVARIANT_THEORY.md §1.1
            bool isTestMode = stepConfig.TryGetProperty("_testMode", out var testModeProp) && testModeProp.GetBoolean();
            if (isTestMode)
            {
                context.Reporter.ReportState("[TEST MODE] GameLaunchCommand - simulating launch preparation");
                context.Set("gameCommand", "java");
                context.Set("gameArgs", "-cp test.jar TestMain");
                context.Set("gameWorkDir", System.AppContext.BaseDirectory);
                context.Set("LaunchTestMode", true);
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

            // Read launch options from context (set by Auth or other plugins)
            string playerName = context.Get<string>("playerName") ?? "Player";
            string? accessToken = context.Get<string>("accessToken");
            string? uuid = context.Get<string>("uuid");
            int ramMb = context.Get<int>("ramMb");
            if (ramMb == 0) ramMb = 4096;
            string? javaPath = context.Get<string>("javaPath");

            // Per INVARIANT_THEORY.md §3.2 Nomadic - extract variables for path interpolation
            var variables = ExtractVariables(context);

            // Build extra options from stepConfig
            var extraOptions = new Dictionary<string, object>();
            if (stepConfig.TryGetProperty("authlibPath", out var alp))
                extraOptions["authlibPath"] = Interpolate(alp.GetString() ?? "", context);
            if (stepConfig.TryGetProperty("authlibUrl", out var au))
                extraOptions["authlibUrl"] = Interpolate(au.GetString() ?? "", context);

            context.Reporter.ReportState($"Генерация аргументов запуска {versionName}...");

            // Resolve provider
            var provider = ResolveProvider(context, providerName);
            if (provider == null)
            {
                return new CommandResult(false, $"Game provider '{providerName}' not found");
            }

            // Build launch parameters using universal interface with variables
            var launchOptions = new LaunchOptions(
                PlayerName: playerName,
                AccessToken: accessToken,
                Uuid: uuid,
                RamMb: ramMb,
                JavaPath: javaPath,
                ExtraOptions: extraOptions
            );

            var launchParams = await provider.BuildLaunchParametersAsync(
                versionName, 
                installDir,
                variables,
                launchOptions, 
                context.CancellationToken
            );

            // Set mutations for downstream steps (OS.Executor)
            context.Set("gameCommand", launchParams.ExecutablePath);
            context.Set("gameArgs", launchParams.Arguments);
            context.Set("gameWorkDir", launchParams.WorkingDirectory);

            context.Reporter.ReportState("Аргументы запуска сгенерированы.");

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
        if (!variables.ContainsKey("mcDir"))
        {
            variables["mcDir"] = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                ".vantuzlauncher");
        }
        
        return variables;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
