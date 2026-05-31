namespace Vantuz.Plugins.Game;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Query: Universal version validator using IGameProvider.
/// Per Armatura:126 - no external dependencies, works with any game.
/// </summary>
public class GameVersionValidatorQuery : IQueryPlugin
{
    public string Name => "Game.VersionValidatorQuery";

    public async Task<object?> ExecuteAsync(QueryContext context, JsonElement stepConfig)
    {
        // TEST MODE: Deterministic behavior per INVARIANT_THEORY.md §1.1
        bool isTestMode = stepConfig.TryGetProperty("_testMode", out var testModeProp) && testModeProp.GetBoolean();
        if (isTestMode)
        {
            context.Reporter.ReportState("[TEST MODE] GameVersionValidatorQuery - simulating version check");
            return new VersionCheckResult(true); // Pretend version exists
        }

        // Get provider name from config - explicit declaration per Armatura:44-45
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

        context.Reporter.ReportState($"Проверка версии {versionName}...");

        // Resolve provider from context (registered by provider plugin)
        var provider = ResolveProvider(context, providerName);
        if (provider == null)
        {
            throw new InvalidOperationException($"Game provider '{providerName}' not found. Ensure the provider plugin is loaded.");
        }

        // Check version using universal interface with variables
        var result = await provider.CheckVersionAsync(versionName, installDir, variables, context.CancellationToken);

        if (!result.Exists)
        {
            context.Reporter.ReportState($"Версия {versionName} не найдена. Требуется установка.");
        }
        else
        {
            context.Reporter.ReportState($"Версия {versionName} найдена.");
        }

        return result;
    }

    /// <summary>
    /// Resolves IGameProvider from context payload.
    /// Providers register themselves with key "GameProvider.{ProviderName}"
    /// </summary>
    private static IGameProvider? ResolveProvider(QueryContext context, string providerName)
    {
        var key = $"GameProvider.{providerName}";
        return context.Get<IGameProvider>(key);
    }

    private static string Interpolate(string text, QueryContext context)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var kvp in context.Payload)
        {
            text = text.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
        }
        return text;
    }
    
    /// <summary>
    /// Extracts variables from context payload per INVARIANT_THEORY.md §3.2 Nomadic Invariant.
    /// Variables travel with manifest, not hardcoded in code.
    /// </summary>
    private static Dictionary<string, string> ExtractVariables(QueryContext context)
    {
        var variables = new Dictionary<string, string>();
        
        // Extract string variables from context payload
        foreach (var kvp in context.Payload)
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
