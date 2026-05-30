namespace Vantuz.Plugins.Minecraft;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Vantuz.Core;

/// <summary>
/// Minecraft-specific implementation of IGameProvider using CmlLib.Core.
/// Per .traerules:126 - CmlLib dependency isolated here only.
/// </summary>
public class MinecraftGameProvider : IGameProvider
{
    public string ProviderName => "Minecraft";

    public async Task<VersionCheckResult> CheckVersionAsync(string version, string installDir, CancellationToken ct)
    {
        try
        {
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

    public async Task<InstallResult> InstallVersionAsync(string version, string installDir, IStatusReporter reporter, CancellationToken ct)
    {
        try
        {
            var path = new MinecraftPath(installDir);
            var launcher = new MinecraftLauncher(path);

            // Wire up progress reporting
            launcher.FileProgressChanged += (sender, args) =>
            {
                var progress = args.TotalTasks > 0 
                    ? (double)args.ProgressedTasks / args.TotalTasks * 100 
                    : 0;
                reporter.ReportProgress($"Downloading {args.Name}", progress);
            };

            reporter.ReportState($"Installing Minecraft {version}...");
            await launcher.InstallAsync(version);
            
            return new InstallResult(true);
        }
        catch (Exception ex)
        {
            return new InstallResult(false, ex.Message);
        }
    }

    public async Task<LaunchParameters> BuildLaunchParametersAsync(
        string version, 
        string installDir, 
        LaunchOptions options, 
        CancellationToken ct)
    {
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
