#pragma warning disable ARM007 // MinecraftGameProvider is a helper class, not a pipeline plugin

namespace Vantuz.Plugins.Minecraft;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using Vantuz.Core;

/// <summary>
/// Minecraft-specific implementation of IGameProvider using CmlLib.Core.
/// Per Armatura:126 - CmlLib dependency isolated here only.
/// </summary>
public class MinecraftGameProvider : IGameProvider
{
    public string ProviderName => "Minecraft";

    public Task<VersionCheckResult> CheckVersionAsync(string version, string installDir, CancellationToken ct)
    {
        try
        {
            var path = new MinecraftPath(installDir);
            var versionJsonPath = path.GetVersionJsonPath(version);
            var versionExists = File.Exists(versionJsonPath);

            // DIAGNOSTIC: Log the exact path CmlLib uses for version detection.
            // This is critical because Forge may install under a different internal name.
            Console.WriteLine($"[DIAG CheckVersionAsync] version={version}, installDir={installDir}, versionJsonPath={versionJsonPath}, exists={versionExists}");

            return Task.FromResult(new VersionCheckResult(versionExists));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DIAG CheckVersionAsync] EXCEPTION: {ex}");
            return Task.FromResult(new VersionCheckResult(false, ex.Message));
        }
    }

    public async Task<InstallResult> InstallVersionAsync(
        string version,
        string installDir,
        IStatusReporter reporter,
        CancellationToken ct,
        TimeSpan? timeout = null)
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

            if (IsForgeVersion(version))
            {
                reporter.ReportState($"Обнаружена Forge-версия {version}. Установка Forge...");
                var (mcVersion, forgeVersion) = ParseForgeVersion(version);

                // DIAGNOSTIC: Log parsed components and install dir before calling ForgeInstaller.
                // These values determine whether CmlLib can locate existing Forge or needs to re-download.
                var absInstallDir = Path.GetFullPath(installDir);
                Console.WriteLine($"[DIAG InstallVersionAsync] Parsed: version={version}, mcVersion={mcVersion}, forgeVersion={forgeVersion}, installDir={absInstallDir}");
                Console.WriteLine($"[DIAG InstallVersionAsync] ForgeInstaller timeout={(timeout?.TotalMinutes ?? 5):F0} min, SkipIfAlreadyInstalled=true");

                var forgeInstaller = new ForgeInstaller(launcher);

                var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
                {
                    cts.CancelAfter(timeout.Value);
                }
                else
                {
                    cts.CancelAfter(TimeSpan.FromMinutes(5));
                }

                var startTime = DateTime.UtcNow;
                var lastProgressTime = DateTime.UtcNow;

                // Heartbeat + watchdog task
                var heartbeatTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }

                        var elapsed = DateTime.UtcNow - startTime;
                        reporter.ReportState($"Установка Forge {forgeVersion} в процессе… прошло {elapsed:mm\\:ss}");
                    }
                }, cts.Token);

                try
                {
                    var installTask = Task.Run(() =>
                    {
                        try
                        {
                            Console.WriteLine($"[DIAG ForgeInstaller] Calling Install(mcVersion={mcVersion}, forgeVersion={forgeVersion})");
                            var installedName = forgeInstaller.Install(mcVersion, forgeVersion, new ForgeInstallOptions
                            {
                                FileProgress = new Progress<InstallerProgressChangedEventArgs>(args =>
                                {
                                    lastProgressTime = DateTime.UtcNow;
                                    var progress = args.TotalTasks > 0
                                        ? args.ProgressedTasks / (double)args.TotalTasks * 100
                                        : 0;
                                    reporter.ReportProgress($"Установка Forge {forgeVersion}", progress);
                                }),
                                ByteProgress = new Progress<ByteProgress>(args =>
                                {
                                    lastProgressTime = DateTime.UtcNow;
                                    var progress = args.TotalBytes > 0
                                        ? args.ProgressedBytes / (double)args.TotalBytes * 100
                                        : 0;
                                    reporter.ReportProgress($"Скачивание Forge {forgeVersion}", progress);
                                }),
                                SkipIfAlreadyInstalled = true
                            });
                            Console.WriteLine($"[DIAG ForgeInstaller] Install completed, returned={installedName}");
                            return installedName;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DIAG ForgeInstaller] EXCEPTION during Install: {ex.GetType().Name}: {ex.Message}");
                            if (ex.InnerException != null)
                                Console.WriteLine($"[DIAG ForgeInstaller] INNER EXCEPTION: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                            throw;
                        }
                    }, cts.Token);

                    var completedTask = await Task.WhenAny(installTask, heartbeatTask);
                    if (completedTask == installTask)
                    {
                        var installedName = await installTask;
                        reporter.ReportState($"Forge установлен: {installedName}");
                        return new InstallResult(true, null, installedName);
                    }
                    else
                    {
                        // heartbeat completed only if cancelled
                        throw new OperationCanceledException();
                    }
                }
                catch (OperationCanceledException)
                {
                    return new InstallResult(
                        false,
                        $"Forge installation timed out ({timeout?.TotalMinutes ?? 5:F0} min). Check your network connection and try again.");
                }
                finally
                {
                    cts.Cancel();
                    try { await heartbeatTask; } catch { }
                }
            }
            else
            {
                await launcher.InstallAsync(version);
            }

            return new InstallResult(true);
        }
        catch (Exception ex)
        {
            return new InstallResult(false, $"Ошибка установки {version}: {ex.Message}");
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
                    if (!File.Exists(authlibPath))
                    {
                        throw new InvalidOperationException(
                            $"authlib-injector.jar not found at {authlibPath}. " +
                            "Ensure the download step ran before launch.");
                    }

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

    private static bool IsForgeVersion(string version)
    {
        return version.Contains("forge", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a combined Forge version string like "1.20.1-forge-47.3.0"
    /// into ("1.20.1", "47.3.0").
    /// </summary>
    internal static (string McVersion, string ForgeVersion) ParseForgeVersion(string version)
    {
        // Expected format: {mcVersion}-forge-{forgeVersion}
        var idx = version.IndexOf("-forge-", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var mcVersion = version.Substring(0, idx);
            var forgeVersion = version.Substring(idx + "-forge-".Length);
            return (mcVersion, forgeVersion);
        }

        // Fallback: try splitting by "-"
        var allParts = version.Split('-');
        if (allParts.Length >= 3)
        {
            // e.g. "1.20.1-forge-47.3.0" -> mc="1.20.1", forge="47.3.0"
            return (allParts[0], string.Join("-", allParts.Skip(2)));
        }

        throw new InvalidOperationException($"Невозможно разобрать Forge-версию из строки: {version}");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

#pragma warning restore ARM007
