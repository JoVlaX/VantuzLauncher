namespace Vantuz.Plugins.Game;

using System;
using System.Collections.Generic;
using System.IO;
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
            // Get configuration from stepConfig per Armatura:44-45
            string providerName = stepConfig.TryGetProperty("provider", out var prov)
                ? Interpolate(prov.GetString() ?? "", context)
                : throw new InvalidOperationException("provider is missing in stepConfig");

            string configVersionName = stepConfig.TryGetProperty("version", out var vn)
                ? Interpolate(vn.GetString() ?? "", context)
                : throw new InvalidOperationException("version is missing in stepConfig");

            // If Forge installer produced an InstalledVersionName, use it (Forge normalizes the name)
            string versionName = context.Get<string>("installedVersion") ?? configVersionName;

            string installDir = stepConfig.TryGetProperty("installDir", out var id)
                ? Interpolate(id.GetString() ?? "", context)
                : throw new InvalidOperationException("installDir is missing in stepConfig");

            installDir = Path.GetFullPath(installDir.Replace('/', Path.DirectorySeparatorChar));

            // Check dryRun mode per INVARIANT_THEORY.md §1.2 Measurability - test must not mutate state
            bool dryRun = stepConfig.TryGetProperty("dryRun", out var dr) && dr.GetBoolean();
            if (dryRun)
            {
                context.Reporter.ReportState($"[DRY RUN] Launch of {versionName} would occur here. No process started.");
                context.Set("gameCommand", "dry-run");
                context.Set("gameArgs", "--dry-run");
                context.Set("gameWorkDir", installDir);
                return new CommandResult(true);
            }

            // Read launch options from context (set by Auth or other plugins)
            string playerName = context.Get<string>("playerName") ?? "Player";
            string? accessToken = context.Get<string>("accessToken");
            string? uuid = context.Get<string>("uuid");
            int ramMb = context.Get<int>("ramMb");
            if (ramMb == 0) ramMb = 4096;
            string? javaPath = context.Get<string>("javaPath");

            // Build extra options from stepConfig
            var extraOptions = new Dictionary<string, object>();
            if (stepConfig.TryGetProperty("authlibPath", out var alp))
            {
                string authlibPathValue = Interpolate(alp.GetString() ?? "", context);
                if (authlibPathValue.Contains("{{"))
                {
                    context.Reporter.ReportState($"[WARN] authlibPath contains unresolved variable: {authlibPathValue}");
                }
                else
                {
                    extraOptions["authlibPath"] = authlibPathValue;
                }
            }
            if (stepConfig.TryGetProperty("authlibUrl", out var au))
            {
                string authlibUrlValue = Interpolate(au.GetString() ?? "", context);
                if (!string.IsNullOrWhiteSpace(authlibUrlValue) && !authlibUrlValue.Contains("example.com", StringComparison.OrdinalIgnoreCase))
                {
                    extraOptions["authlibUrl"] = authlibUrlValue;
                }
                else if (authlibUrlValue.Contains("example.com", StringComparison.OrdinalIgnoreCase))
                {
                    context.Reporter.ReportState("[WARN] authlibUrl contains placeholder domain 'example.com'. Skipping authlib injection.");
                }
            }

            context.Reporter.ReportState($"Генерация аргументов запуска {versionName}...");

            // Pre-flight validation: ensure critical paths exist before attempting launch
            if (!Directory.Exists(installDir))
            {
                return new CommandResult(false, $"Install directory not found: {installDir}");
            }

            string effectiveJavaPath = javaPath ?? "java";
            if (!File.Exists(effectiveJavaPath) && !IsInPath(effectiveJavaPath))
            {
                return new CommandResult(false, $"Java executable not found: {effectiveJavaPath}. Please install Java or set javaPath in context.");
            }

            if (extraOptions.TryGetValue("authlibPath", out var authlibPathObj) &&
                authlibPathObj?.ToString() is string authlibPathResolved &&
                !string.IsNullOrEmpty(authlibPathResolved) &&
                !File.Exists(authlibPathResolved))
            {
                return new CommandResult(false, $"authlib-injector not found at {authlibPathResolved}. Ensure the download step ran before launch.");
            }

            // Resolve provider
            var provider = ResolveProvider(context, providerName);
            if (provider == null)
            {
                return new CommandResult(false, $"Game provider '{providerName}' not found");
            }

            // Build launch parameters using universal interface
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
    /// Checks whether an executable name (without path) can be resolved via PATH.
    /// </summary>
    private static bool IsInPath(string fileName)
    {
        if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains('/'))
            return false;

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extensions = new[] { "" };
        if (OperatingSystem.IsWindows())
        {
            extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries);
        }

        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(dir, fileName + ext);
                if (File.Exists(fullPath)) return true;
            }
        }
        return false;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
