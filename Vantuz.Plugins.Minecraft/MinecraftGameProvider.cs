#pragma warning disable ARM007 // MinecraftGameProvider is a helper class, not a pipeline plugin

namespace Vantuz.Plugins.Minecraft;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.ProcessBuilder;
using Vantuz.Core;

/// <summary>
/// Minecraft-specific implementation of IGameProvider using CmlLib.Core.
/// Per Armatura:126 - CmlLib dependency isolated here only.
/// </summary>
public class MinecraftGameProvider : IGameProvider
{
    public string ProviderName => "Minecraft";

    public async Task<VersionCheckResult> CheckVersionAsync(
        string version, 
        string installDir, 
        Dictionary<string, string> variables,
        CancellationToken ct)
    {
        try
        {
            // Per INVARIANT_THEORY.md §498 Explicitness - interpolate from manifest variables
            installDir = PathInterpolator.Interpolate(installDir, variables);
            
            var path = new MinecraftPath(installDir);
            var versionJsonPath = path.GetVersionJsonPath(version);
            var versionExists = File.Exists(versionJsonPath);

            return new VersionCheckResult(versionExists);
        }
        catch (Exception ex)
        {
            return new VersionCheckResult(false, ex.Message);
        }
    }

    public async Task<InstallResult> InstallVersionAsync(
        string version, 
        string installDir, 
        Dictionary<string, string> variables,
        IStatusReporter reporter, 
        CancellationToken ct)
    {
        try
        {
            // Per INVARIANT_THEORY.md §498 Explicitness - interpolate from manifest variables
            installDir = PathInterpolator.Interpolate(installDir, variables);
            reporter.ReportState($"[INSTALL] Resolved installDir: {installDir}");
            
            reporter.ReportState($"[INSTALL] Initializing MinecraftPath for {version}...");
            var path = new MinecraftPath(installDir);
            reporter.ReportState($"[INSTALL] MinecraftPath created: {path.BasePath}");
            
            reporter.ReportState($"[INSTALL] Creating MinecraftLauncher...");
            var launcher = new MinecraftLauncher(path);
            reporter.ReportState($"[INSTALL] MinecraftLauncher created successfully");
            
            // Per SRP: Use ForgeVersionParser for version detection only
            var forgeVersion = ForgeVersionParser.Parse(version);
            if (forgeVersion.IsValid)
            {
                reporter.ReportState($"[FORGE] Detected Forge version: Minecraft={forgeVersion.MinecraftVersion}, Forge={forgeVersion.ForgeVersionNumber}");
                
                var forgeInstaller = new ForgeInstaller(launcher);
                reporter.ReportState($"[FORGE] Starting Forge installation...");
                
                await forgeInstaller.Install(forgeVersion.ForgeVersionNumber, forgeVersion.MinecraftVersion);
                
                reporter.ReportState($"[FORGE] Forge installation completed successfully");
                return new InstallResult(true);
            }

            // Wire up progress reporting
            launcher.FileProgressChanged += (sender, args) =>
            {
                var progress = args.TotalTasks > 0 
                    ? (double)args.ProgressedTasks / args.TotalTasks * 100 
                    : 0;
                reporter.ReportProgress($"Downloading {args.Name}", progress);
            };

            reporter.ReportState($"[INSTALL] Starting InstallAsync for {version}...");
            await launcher.InstallAsync(version);
            reporter.ReportState($"[INSTALL] InstallAsync completed successfully");
            
            return new InstallResult(true);
        }
        catch (OperationCanceledException)
        {
            reporter.ReportState($"[INSTALL ERROR] Installation cancelled (timeout or user abort)");
            throw;
        }
        catch (Exception ex)
        {
            reporter.ReportState($"[INSTALL ERROR] {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
            {
                reporter.ReportState($"[INSTALL ERROR] Inner: {ex.InnerException.Message}");
            }
            return new InstallResult(false, ex.Message);
        }
    }

    public async Task<LaunchParameters> BuildLaunchParametersAsync(
        string version, 
        string installDir, 
        Dictionary<string, string> variables,
        LaunchOptions options, 
        CancellationToken ct)
    {
        // Per INVARIANT_THEORY.md §498 Explicitness - interpolate from manifest variables
        installDir = PathInterpolator.Interpolate(installDir, variables);
        
        var path = new MinecraftPath(installDir);
        var launcher = new MinecraftLauncher(path);

        var session = new MSession
        {
            Username = options.PlayerName,
            UUID = options.Uuid ?? "",
            AccessToken = options.AccessToken ?? "",
            UserType = "mojang"
        };

        var launchOption = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = options.RamMb,
            JavaPath = options.JavaPath ?? "java"
        };

        // Handle authlib injector if provided in extra options
        if (options.ExtraOptions != null)
        {
            if (options.ExtraOptions.TryGetValue("authlibPath", out var authlibPathObj) &&
                options.ExtraOptions.TryGetValue("authlibUrl", out var authlibUrlObj))
            {
                var authlibPath = authlibPathObj?.ToString();
                var authlibUrl = authlibUrlObj?.ToString();
                
                if (!string.IsNullOrEmpty(authlibPath) && !string.IsNullOrEmpty(authlibUrl))
                {
                    launchOption.ExtraJvmArguments = new[]
                    {
                        new MArgument($"-javaagent:{authlibPath}={authlibUrl}")
                    };
                }
            }
        }

        var process = await launcher.BuildProcessAsync(version, launchOption);

        return new LaunchParameters(
            process.StartInfo.FileName,
            process.StartInfo.Arguments,
            process.StartInfo.WorkingDirectory
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

#pragma warning restore ARM007
