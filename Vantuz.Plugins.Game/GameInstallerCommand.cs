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
    public string Name => "Game.InstallerCommand";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
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

        TimeSpan timeout = TimeSpan.FromMinutes(5);
        if (stepConfig.TryGetProperty("operationTimeout", out var otProp))
        {
            if (TimeSpan.TryParse(otProp.GetString(), out var parsedTimeout) && parsedTimeout > TimeSpan.Zero)
            {
                timeout = parsedTimeout;
            }
        }

        try
        {
            // Check dryRun mode per INVARIANT_THEORY.md В§1.2 Measurability - test must not mutate state
            bool dryRun = stepConfig.TryGetProperty("dryRun", out var dr) && dr.GetBoolean();
            if (dryRun)
            {
                context.Reporter.ReportState($"[DRY RUN] Installation of {versionName} would occur here. No state changes.");
                context.Set("InstallSuccess", true);
                context.Set("InstallDryRun", true);
                return new CommandResult(true);
            }

            // Check previous validator result (Query adapter stores under plugin name + ".Result")
            var checkResult = context.Get<VersionCheckResult>("Game.VersionValidatorQuery.Result");
            if (checkResult == null)
            {
                // No validator ran, run check ourselves
                context.Reporter.ReportState($"РџСЂРѕРІРµСЂРєР° РІРµСЂСЃРёРё {versionName} РїРµСЂРµРґ СѓСЃС‚Р°РЅРѕРІРєРѕР№...");
                var queryProvider = ResolveReadProvider(context, providerName);
                if (queryProvider == null)
                {
                    return new CommandResult(false, $"Game query provider '{providerName}' not found");
                }
                checkResult = await queryProvider.CheckVersionAsync(versionName, installDir, context.CancellationToken);
            }

            // Skip if already exists
            if (checkResult.Exists)
            {
                context.Reporter.ReportState($"Р’РµСЂСЃРёСЏ {versionName} СѓР¶Рµ СѓСЃС‚Р°РЅРѕРІР»РµРЅР°, РїСЂРѕРїСѓСЃРє СѓСЃС‚Р°РЅРѕРІРєРё.");
                context.Set("InstallSkipped", true);
                return new CommandResult(true);
            }

            context.Reporter.ReportState($"РЈСЃС‚Р°РЅРѕРІРєР° РІРµСЂСЃРёРё {versionName}...");

            // Resolve command provider
            var commandProvider = ResolveCommandProvider(context, providerName);
            if (commandProvider == null)
            {
                return new CommandResult(false, $"Game command provider '{providerName}' not found");
            }

            // Enforce operation timeout via CancellationTokenSource so every provider respects it
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(timeout);

            // Install version
            var installResult = await commandProvider.InstallVersionAsync(
                versionName,
                installDir,
                context.Reporter,
                cts.Token,
                timeout
            );

            if (!installResult.Success)
            {
                return new CommandResult(false, installResult.ErrorMessage ?? "Installation failed");
            }

            context.Reporter.ReportState($"Р’РµСЂСЃРёСЏ {versionName} СѓСЃРїРµС€РЅРѕ СѓСЃС‚Р°РЅРѕРІР»РµРЅР°.");
            context.Set("InstallSuccess", true);
            if (!string.IsNullOrEmpty(installResult.InstalledVersionName))
            {
                context.Set("installedVersion", installResult.InstalledVersionName);
            }

            return new CommandResult(true);
        }
        catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
        {
            return new CommandResult(false, $"Installation timed out after {timeout.TotalMinutes:F0} minutes. Check your network connection and try again.");
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Resolves IGameQueryProvider from context mutations.
    /// Per INVARIANT_THEORY.md В§2.2: Query facet for version checking.
    /// </summary>
    private static IGameQueryProvider? ResolveReadProvider(CommandContext context, string providerName)
    {
        var queryKey = $"GameQueryProvider.{providerName}";
        var legacyKey = $"GameProvider.{providerName}";
        return context.Get<IGameQueryProvider>(queryKey)
            ?? context.Get<IGameQueryProvider>(legacyKey);
    }

    /// <summary>
    /// Resolves IGameCommandProvider from context mutations.
    /// Per INVARIANT_THEORY.md В§2.2: Command facet for installation.
    /// </summary>
    private static IGameCommandProvider? ResolveCommandProvider(CommandContext context, string providerName)
    {
        var commandKey = $"GameCommandProvider.{providerName}";
        var legacyKey = $"GameProvider.{providerName}";
        return context.Get<IGameCommandProvider>(commandKey)
            ?? context.Get<IGameCommandProvider>(legacyKey);
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
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
