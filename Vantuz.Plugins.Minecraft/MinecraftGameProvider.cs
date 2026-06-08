#pragma warning disable ARM007 // MinecraftGameProvider is a helper class, not a pipeline plugin

namespace Vantuz.Plugins.Minecraft;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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

    // Internal hooks for unit testing the Forge install path.
    // Production code leaves these null; default behavior delegates to CmlLib.
    internal Func<MinecraftLauncher, ForgeInstaller>? ForgeInstallerFactory { get; set; }
    internal Func<MinecraftLauncher, string, Task>? LibraryInstaller { get; set; }
    internal Func<string, string, ForgeInstallOptions, Task<string>>? ForgeInstallOverride { get; set; }

    public Task<VersionCheckResult> CheckVersionAsync(string version, string installDir, CancellationToken ct)
    {
        try
        {
            var path = new MinecraftPath(installDir);
            var versionJsonPath = path.GetVersionJsonPath(version);
            var jsonExists = File.Exists(versionJsonPath);
            bool versionExists;

            if (IsForgeVersion(version))
            {
                // Forge does not create a version JAR; the version JSON references vanilla
                // client via inheritsFrom. We must verify ALL critical libraries from the JSON
                // (bootstraplauncher, securejarhandler, fmlloader) plus the vanilla client JAR.
                // Interrupted installs leave JSON but not all libraries, causing ClassNotFoundException.
                var (librariesOk, missingDetail) = VerifyForgeLibraries(version, installDir);
                versionExists = jsonExists && librariesOk;

                // Forge version check completed; versionExists determined by JSON + library verification
            }
            else
            {
                // Vanilla Minecraft: need both JSON descriptor and the client JAR
                var versionJarPath = path.GetVersionJarPath(version);
                var jarExists = File.Exists(versionJarPath);
                versionExists = jsonExists && jarExists;

                // Vanilla version check completed; versionExists determined by JSON + JAR existence
            }

            return Task.FromResult(new VersionCheckResult(versionExists));
        }
        catch (Exception ex)
        {
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
                        reporter.ReportState($"Forge установлен: {installedName}");

                        // ForgeInstaller.Install creates the version JSON and downloads fmlloader,
                        // but does NOT download all libraries referenced in the JSON. We must
                        // run CmlLib's library resolver to fetch the remaining artifacts
                        // (bootstraplauncher, securejarhandler, etc.) before launch.
                        reporter.ReportState($"Загрузка библиотек Forge для {installedName}...");
                        if (LibraryInstaller != null)
                            await LibraryInstaller(launcher, installedName);
                        else
                            await launcher.InstallAsync(installedName);

                        // Post-install verification: parse version JSON and verify all critical libraries.
                        var (librariesOk, missingDetail) = VerifyForgeLibraries(version, installDir);
                        if (!librariesOk)
                        {
                            return new InstallResult(false, $"Установка Forge завершилась, но не хватает критических библиотек: {missingDetail}");
                        }
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

    /// <summary>
    /// Verifies that all critical Forge libraries exist and are non-empty.
    /// Parses the version JSON to discover library paths (so version changes are handled automatically).
    /// Also checks the vanilla client JAR referenced via inheritsFrom.
    /// </summary>
    private static (bool AllExist, string? MissingDetail) VerifyForgeLibraries(string version, string installDir)
    {
        var path = new MinecraftPath(installDir);
        var versionJsonPath = path.GetVersionJsonPath(version);
        if (!File.Exists(versionJsonPath))
            return (false, "version JSON missing");

        string jsonText;
        try
        {
            jsonText = File.ReadAllText(versionJsonPath);
        }
        catch (Exception readEx)
        {
            return (false, $"version JSON read error: {readEx.Message}");
        }

        using var json = JsonDocument.Parse(jsonText);

        var requiredLibraries = new[] { "cpw.mods:bootstraplauncher", "cpw.mods:securejarhandler", "net.minecraftforge:fmlloader" };

        if (json.RootElement.TryGetProperty("libraries", out var librariesElement) && librariesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var libName in requiredLibraries)
            {
                bool found = false;
                foreach (var lib in librariesElement.EnumerateArray())
                {
                    if (lib.TryGetProperty("name", out var nameProp) && nameProp.GetString()?.StartsWith(libName + ":", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        if (lib.TryGetProperty("downloads", out var downloads) &&
                            downloads.TryGetProperty("artifact", out var artifact) &&
                            artifact.TryGetProperty("path", out var pathProp))
                        {
                            var artifactPath = pathProp.GetString();
                            if (!string.IsNullOrEmpty(artifactPath))
                            {
                                var fullPath = Path.Combine(installDir, "libraries", artifactPath.Replace('/', Path.DirectorySeparatorChar));
                                if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
                                {
                                    return (false, $"missing or empty library: {libName} at {fullPath}");
                                }
                                found = true;
                                break;
                            }
                        }
                    }
                }
                if (!found)
                {
                    return (false, $"library entry not found in JSON: {libName}");
                }
            }
        }
        else
        {
            return (false, "libraries array missing in version JSON");
        }

        // Check vanilla client JAR referenced via inheritsFrom
        if (json.RootElement.TryGetProperty("inheritsFrom", out var inheritsProp))
        {
            var inheritsFrom = inheritsProp.GetString();
            if (!string.IsNullOrEmpty(inheritsFrom))
            {
                var vanillaJarPath = path.GetVersionJarPath(inheritsFrom);
                if (!File.Exists(vanillaJarPath) || new FileInfo(vanillaJarPath).Length == 0)
                {
                    return (false, $"missing or empty vanilla client JAR: {vanillaJarPath}");
                }
            }
        }
        else
        {
            return (false, "inheritsFrom missing in version JSON");
        }

        return (true, null);
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
