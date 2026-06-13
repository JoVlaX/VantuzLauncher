#pragma warning disable ARM007 // ExternalAbstraction, not pipeline plugin

namespace Vantuz.Plugins.Minecraft;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installers;
using Vantuz.Core;

/// <summary>
/// CQRS Command facet of Minecraft game provider.
/// Per INVARIANT_THEORY.md В§2.2 вЂ” state-mutating operations only.
/// F_doc: {class contains read-only query operations without side effects}
/// E_doc: Static analysis grep for 'public Task.*Async' shows only InstallVersionAsync
/// </summary>
public class MinecraftGameCommandProvider : IGameCommandProvider
{
    /// <summary>
    /// Provider identifier for context registration.
    /// F_doc: {Name is null or empty string}
    /// E_doc: Unit test asserts ProviderName == "Minecraft"
    /// </summary>
    public string ProviderName => "Minecraft";

    // Internal hooks for unit testing the Forge install path.
    // Production code leaves these null; default behavior delegates to CmlLib.
    internal Func<MinecraftLauncher, ForgeInstaller>? ForgeInstallerFactory { get; set; }
    internal Func<MinecraftLauncher, string, Task>? LibraryInstaller { get; set; }
    internal Func<string, string, ForgeInstallOptions, Task<string>>? ForgeInstallOverride { get; set; }

    /// <summary>
    /// Install/update the specified Minecraft version.
    /// F_doc: {install fails silently, times out, or leaves partial filesystem state}
    /// E_doc: Unit test with mock installer verifying InstallVersionAsync returns Success=false on timeout
    /// </summary>
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

            launcher.FileProgressChanged += (sender, args) =>
            {
                var progress = args.TotalTasks > 0
                    ? (double)args.ProgressedTasks / args.TotalTasks * 100
                    : 0;
                reporter.ReportProgress($"Downloading {args.Name}", progress);
            };

            reporter.ReportState($"Installing Minecraft {version}...");

            if (MinecraftGameQueryProvider.IsForgeVersion(version))
            {
                reporter.ReportState($"РћР±РЅР°СЂСѓР¶РµРЅР° Forge-РІРµСЂСЃРёСЏ {version}. РЈСЃС‚Р°РЅРѕРІРєР° Forge...");
                var (mcVersion, forgeVersion) = MinecraftGameQueryProvider.ParseForgeVersion(version);

                var absInstallDir = Path.GetFullPath(installDir);

                var forgeInstaller = ForgeInstallerFactory?.Invoke(launcher) ?? new ForgeInstaller(launcher);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
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
                        reporter.ReportState($"РЈСЃС‚Р°РЅРѕРІРєР° Forge {forgeVersion} РІ РїСЂРѕС†РµСЃСЃРµвЂ¦ РїСЂРѕС€Р»Рѕ {elapsed:mm\\:ss}");
                    }
                }, cts.Token);

                try
                {
                    var installTask = Task.Run(async () =>
                    {
                        try
                        {
                            var forgeOptions = new ForgeInstallOptions
                            {
                                FileProgress = new Progress<InstallerProgressChangedEventArgs>(args =>
                                {
                                    lastProgressTime = DateTime.UtcNow;
                                    var progress = args.TotalTasks > 0
                                        ? args.ProgressedTasks / (double)args.TotalTasks * 100
                                        : 0;
                                    reporter.ReportProgress($"РЈСЃС‚Р°РЅРѕРІРєР° Forge {forgeVersion}", progress);
                                }),
                                ByteProgress = new Progress<ByteProgress>(args =>
                                {
                                    lastProgressTime = DateTime.UtcNow;
                                    var progress = args.TotalBytes > 0
                                        ? args.ProgressedBytes / (double)args.TotalBytes * 100
                                        : 0;
                                    reporter.ReportProgress($"РЎРєР°С‡РёРІР°РЅРёРµ Forge {forgeVersion}", progress);
                                }),
                                SkipIfAlreadyInstalled = true
                            };
                            var installedName = ForgeInstallOverride != null
                                ? await ForgeInstallOverride(mcVersion, forgeVersion, forgeOptions)
                                : await forgeInstaller.Install(mcVersion, forgeVersion, forgeOptions);
                            return installedName;
                        }
                        catch
                        {
                            throw;
                        }
                    }, cts.Token);

                    var completedTask = await Task.WhenAny(installTask, heartbeatTask);
                    if (completedTask == installTask)
                    {
                        var installedName = await installTask;
                        reporter.ReportState($"Forge СѓСЃС‚Р°РЅРѕРІР»РµРЅ: {installedName}");

                        reporter.ReportState($"Р—Р°РіСЂСѓР·РєР° Р±РёР±Р»РёРѕС‚РµРє Forge РґР»СЏ {installedName}...");
                        if (LibraryInstaller != null)
                            await LibraryInstaller(launcher, installedName);
                        else
                            await launcher.InstallAsync(installedName);

                        var (librariesOk, missingDetail) = MinecraftGameQueryProvider.VerifyForgeLibraries(version, installDir);
                        if (!librariesOk)
                        {
                            return new InstallResult(false, $"РЈСЃС‚Р°РЅРѕРІРєР° Forge Р·Р°РІРµСЂС€РёР»Р°СЃСЊ, РЅРѕ РЅРµ С…РІР°С‚Р°РµС‚ РєСЂРёС‚РёС‡РµСЃРєРёС… Р±РёР±Р»РёРѕС‚РµРє: {missingDetail}");
                        }
                        return new InstallResult(true, null, installedName);
                    }
                    else
                    {
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
                    try { await heartbeatTask; }
                    catch (Exception ex)
                    {
                        // F_doc: {Heartbeat task throws after cancellation} E_doc: {Cancelled task is expected; finally must complete regardless}
                        Console.Error.WriteLine($"Heartbeat cleanup: {ex.Message}");
                    }
                }
            }
            /// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior
            else
            {
                await launcher.InstallAsync(version);
            }

            return new InstallResult(true);
        }
        catch (Exception ex)
        {
            return new InstallResult(false, $"РћС€РёР±РєР° СѓСЃС‚Р°РЅРѕРІРєРё {version}: {ex.Message}");
        }
    }

    /// <summary>
    /// Dispose command provider resources.
    /// F_doc: {DisposeAsync throws or leaves unmanaged resources}
    /// E_doc: Unit test calls DisposeAsync twice without exception
    /// </summary>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

#pragma warning restore ARM007
