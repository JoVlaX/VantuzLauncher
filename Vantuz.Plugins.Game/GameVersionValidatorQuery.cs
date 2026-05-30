namespace Vantuz.Plugins.Game;

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Query: Universal version validator using IGameProvider.
/// Per .traerules:126 - no external dependencies, works with any game.
/// </summary>
public class GameVersionValidatorQuery : IQueryPlugin
{
    public string Name => "Game.VersionValidator";

    public async Task<object?> ExecuteAsync(QueryContext context, JsonElement stepConfig)
    {
        // Get provider name from config - explicit declaration per .traerules:44-45
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

        context.Reporter.ReportState($"Проверка версии {versionName}...");

        // Resolve provider from context (registered by provider plugin)
        var provider = ResolveProvider(context, providerName);
        if (provider == null)
        {
            throw new InvalidOperationException($"Game provider '{providerName}' not found. Ensure the provider plugin is loaded.");
        }

        // Check version using universal interface
        var result = await provider.CheckVersionAsync(versionName, installDir, context.CancellationToken);

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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
