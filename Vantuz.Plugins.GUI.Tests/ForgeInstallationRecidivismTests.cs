using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// Recidivism prevention test: verifies the real (non-dryRun) Forge installer path
/// is exercised so we never again report "working" when Forge versions silently fail.
/// Per the previous bug: CmlLib.Core.Installer.Forge was missing, causing
/// "Cannot find 1.20.1-forge-47.3.0" with no user-visible error in WinExe mode.
/// </summary>
public class ForgeInstallationRecidivismTests : IDisposable
{
    private readonly List<Process> _processes = new();

    public void Dispose()
    {
        foreach (var p in _processes.ToList())
        {
            try { if (!p.HasExited) { p.Kill(); p.WaitForExit(5_000); } } catch { }
        }
    }

    private static string ResolveExePath()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "bin", "Release", "net8.0-windows", "VantuzLauncher.exe");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            path = path.Replace("Release", "Debug");
        return path;
    }

    private static string ResolveBootGuiJson()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "boot.gui.json");
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// E_doc: When boot.gui.json is used with dryRun=false on the InstallerCommand,
    ///        the pipeline either completes or fails with a human-readable error.
    /// F_doc: The pipeline aborts with "Cannot find 1.20.1-forge-47.3.0" or a
    ///        similar internal CmlLib error that is not surfaced to the user.
    /// </summary>
    [StaFact]
    public void RealForgePipeline_ProducesHumanReadableErrorOrSucceeds()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        string exeDir = Path.GetDirectoryName(exe)!;
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_forge_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);

        string crashLogPath = Path.Combine(tempDir, "crash.log");
        string traceLogPath = Path.Combine(tempDir, "launcher_trace.log");

        try
        {
            // 1. Copy EXE and dependencies
            foreach (var file in Directory.GetFiles(exeDir))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("boot.", StringComparison.OrdinalIgnoreCase)
                    && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                File.Copy(file, Path.Combine(tempDir, fileName), true);
            }
            foreach (var file in Directory.GetFiles(Path.Combine(exeDir, "plugins"), "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(Path.Combine(exeDir, "plugins"), file);
                string destPath = Path.Combine(pluginsDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, true);
            }

            // 2. Read real boot.gui.json and force dryRun=false on InstallerCommand + LaunchCommand
            string bootGuiPath = ResolveBootGuiJson();
            Assert.True(File.Exists(bootGuiPath), $"boot.gui.json not found at {bootGuiPath}");
            var bootJson = File.ReadAllText(bootGuiPath);
            var doc = JsonDocument.Parse(bootJson);
            var root = doc.RootElement;

            var pipeline = root.GetProperty("pipeline");
            var modifiedSteps = new List<JsonElement>();
            bool foundInstaller = false;
            bool foundLaunch = false;

            foreach (var step in pipeline.EnumerateArray())
            {
                string pluginName = step.GetProperty("pluginName").GetString()!;
                if (pluginName == "Game.InstallerCommand")
                {
                    foundInstaller = true;
                    var config = step.GetProperty("config");
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(config.GetRawText())!;
                    dict["dryRun"] = false; // Force real installation
                    var modifiedStep = new Dictionary<string, object>
                    {
                        ["pluginName"] = pluginName,
                        ["config"] = dict
                    };
                    modifiedSteps.Add(JsonDocument.Parse(JsonSerializer.Serialize(modifiedStep)).RootElement);
                }
                else if (pluginName == "Game.LaunchCommand")
                {
                    foundLaunch = true;
                    var config = step.GetProperty("config");
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(config.GetRawText())!;
                    dict["dryRun"] = true; // Don't actually launch the game in tests
                    var modifiedStep = new Dictionary<string, object>
                    {
                        ["pluginName"] = pluginName,
                        ["config"] = dict
                    };
                    modifiedSteps.Add(JsonDocument.Parse(JsonSerializer.Serialize(modifiedStep)).RootElement);
                }
                else
                {
                    modifiedSteps.Add(step);
                }
            }

            Assert.True(foundInstaller, "Game.InstallerCommand not found in boot.gui.json pipeline");
            Assert.True(foundLaunch, "Game.LaunchCommand not found in boot.gui.json pipeline");

            var modifiedManifest = new Dictionary<string, object>
            {
                ["variables"] = root.GetProperty("variables"),
                ["plugins"] = root.GetProperty("plugins"),
                ["pipeline"] = modifiedSteps
            };

            // Write modified manifest
            File.WriteAllText(Path.Combine(tempDir, "boot.gui.json"),
                JsonSerializer.Serialize(modifiedManifest, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(tempDir, ".portable"), "");

            // 3. Launch process
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(tempDir, "VantuzLauncher.exe"),
                WorkingDirectory = tempDir,
                WindowStyle = ProcessWindowStyle.Normal
            });
            Assert.NotNull(proc);
            _processes.Add(proc);

            Assert.False(proc.HasExited,
                $"VantuzLauncher.exe exited prematurely. TempDir={tempDir}");

            // 4. Wait for pipeline to complete (success or failure)
            string? lastTrace = null;
            string? lastCrash = null;
            var sw = Stopwatch.StartNew();
            bool done = false;
            while (sw.Elapsed.TotalSeconds < 120) // Forge install can take time
            {
                if (File.Exists(traceLogPath))
                {
                    try
                    {
                        using var fs = new FileStream(traceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs);
                        lastTrace = sr.ReadToEnd();
                    }
                    catch { }
                }

                if (File.Exists(crashLogPath))
                {
                    try { lastCrash = File.ReadAllText(crashLogPath); } catch { }
                }

                // Detect completion: either all steps completed OR a clear error was logged
                if (lastTrace != null)
                {
                    bool allStepsCompleted =
                        lastTrace.Contains("[STEP] GUI.MinecraftLauncher completed") &&
                        lastTrace.Contains("[STEP] GUI.CredentialCollection completed") &&
                        lastTrace.Contains("[STEP] Auth.YggdrasilCommand completed") &&
                        lastTrace.Contains("[STEP] Game.MinecraftProvider completed");

                    bool hasError = lastCrash != null && lastCrash.Contains("Pipeline failed");
                    bool hasForgeInstallAttempt = lastTrace.Contains("Обнаружена Forge-версия") ||
                                                   lastTrace.Contains("Installing Minecraft 1.20.1-forge-47.3.0");

                    if (allStepsCompleted && (hasError || hasForgeInstallAttempt))
                    {
                        done = true;
                        break;
                    }
                }

                // Also check if process exited
                if (proc.HasExited)
                {
                    done = true;
                    break;
                }

                Thread.Sleep(500);
            }

            // 5. Assert recidivism conditions
            Assert.True(done, "Pipeline did not complete or produce an error within 120 seconds.");

            // Must see evidence that Forge installation was actually attempted
            Assert.NotNull(lastTrace);
            Assert.True(
                lastTrace.Contains("Обнаружена Forge-версия") ||
                lastTrace.Contains("Installing Minecraft 1.20.1-forge-47.3.0") ||
                lastTrace.Contains("Установка Forge"),
                "The pipeline never attempted Forge installation. " +
                "This means the Forge-specific code path was skipped or the old 'Cannot find' error occurred silently.\n" +
                $"Trace log:\n{lastTrace}\nCrash log:\n{lastCrash ?? "(none)"}");

            // If it failed, the error must be human-readable (not raw CmlLib internals)
            if (lastCrash != null && lastCrash.Contains("Pipeline failed"))
            {
                Assert.DoesNotContain("Cannot find 1.20.1-forge-47.3.0", lastCrash);
                Assert.DoesNotContain("KeyNotFoundException", lastCrash);
            }
        }
        finally
        {
            foreach (var p in _processes.ToList())
            {
                try { if (!p.HasExited) { p.Kill(); p.WaitForExit(5_000); } } catch { }
            }
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
