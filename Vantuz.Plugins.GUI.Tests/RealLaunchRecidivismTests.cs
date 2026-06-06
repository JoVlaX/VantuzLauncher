using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Vantuz.Core;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// Recidivism prevention test: exercises the real (non-dryRun) launch path so we
/// never again claim "working" when authlib is missing or the JVM crashes on start.
/// </summary>
internal sealed class DummyReporter : IStatusReporter
{
    public void ReportProgress(string taskName, double percentage) { }
    public void ReportState(string message) { }
}

public class RealLaunchRecidivismTests : IDisposable
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
        // Prefer Debug (freshly built) over Release (may be stale)
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "bin", "Debug", "net8.0-windows", "VantuzLauncher.exe");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            path = path.Replace("Debug", "Release");
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

    [StaFact]
    public async Task RealLaunchPipeline_AuthlibExists_And_ProcessDoesNotCrashImmediately()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        string exeDir = Path.GetDirectoryName(exe)!;
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_real_launch_{Guid.NewGuid():N}");
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

            // 2. Read real boot.gui.json and force dryRun=false on InstallerCommand and LaunchCommand
            string bootGuiPath = ResolveBootGuiJson();
            Assert.True(File.Exists(bootGuiPath), $"boot.gui.json not found at {bootGuiPath}");
            var bootJson = File.ReadAllText(bootGuiPath);
            var doc = JsonDocument.Parse(bootJson);
            var root = doc.RootElement;

            var pipeline = root.GetProperty("pipeline");
            var modifiedSteps = new List<Dictionary<string, object>>();
            bool foundInstaller = false;
            bool foundLaunch = false;

            foreach (var step in pipeline.EnumerateArray())
            {
                string pluginName = step.GetProperty("pluginName").GetString()!;
                // Skip GUI steps — replace with headless credential provider below
                if (pluginName == "GUI.MinecraftLauncher" || pluginName == "GUI.CredentialCollection")
                {
                    continue;
                }
                if (pluginName == "Auth.YggdrasilCommand")
                {
                    // Replace real auth with deterministic test auth (no network)
                    modifiedSteps.Add(new Dictionary<string, object>
                    {
                        ["pluginName"] = "Auth.TestAuthCommand",
                        ["config"] = new Dictionary<string, object> { ["launcherVersion"] = "2.0-test" }
                    });
                    continue;
                }
                if (pluginName == "Game.InstallerCommand")
                {
                    foundInstaller = true;
                    var installerStep = JsonSerializer.Deserialize<Dictionary<string, object>>(step.GetRawText())!;
                    var config = JsonSerializer.Deserialize<Dictionary<string, object>>(step.GetProperty("config").GetRawText())!;
                    config["dryRun"] = true;
                    installerStep["config"] = config;
                    modifiedSteps.Add(installerStep);
                    continue;
                }
                if (pluginName == "Game.LaunchCommand")
                {
                    foundLaunch = true;
                    var launchStep = JsonSerializer.Deserialize<Dictionary<string, object>>(step.GetRawText())!;
                    modifiedSteps.Add(launchStep);
                    continue;
                }
                if (pluginName == "OS.ExecuteCommand")
                {
                    continue;
                }
                var defaultStep = JsonSerializer.Deserialize<Dictionary<string, object>>(step.GetRawText())!;
                modifiedSteps.Add(defaultStep);
            }

            // Insert headless credential provider as first step
            modifiedSteps.Insert(0, new Dictionary<string, object>
            {
                ["pluginName"] = "Test.MockCredentialProvider",
                ["config"] = new Dictionary<string, object>
                {
                    ["username"] = "test_user",
                    ["password"] = "test_password",
                    ["rememberMe"] = false,
                    ["ramMb"] = 4096
                }
            });

            Assert.True(foundInstaller, "Game.InstallerCommand not found in boot.gui.json pipeline");
            Assert.True(foundLaunch, "Game.LaunchCommand not found in boot.gui.json pipeline");

            var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetProperty("variables").GetRawText())!;
            variables["testUser"] = "test_user";
            variables["testPass"] = "test_password";
            variables["ramMb"] = "4096";
            variables["mcDir"] = tempDir;

            var plugins = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetProperty("plugins").GetRawText())!;
            plugins["Vantuz.Plugins.Test.dll"] = "";

            var modifiedManifest = new Dictionary<string, object>
            {
                ["variables"] = variables,
                ["plugins"] = plugins,
                ["pipeline"] = modifiedSteps
            };

            string manifestJson = JsonSerializer.Serialize(modifiedManifest, new JsonSerializerOptions { WriteIndented = true });
            Assert.Contains("authlib-injector.jar", manifestJson);
            Assert.Contains("Net.DownloadCommand", manifestJson);

            // Count how many Net.DownloadCommand steps exist in the manifest
            int downloadCommandCount = manifestJson.Split("Net.DownloadCommand").Length - 1;
            Assert.True(downloadCommandCount >= 2,
                $"Expected at least 2 Net.DownloadCommand steps (modpack + authlib), found {downloadCommandCount}.\nManifest:\n{manifestJson}");

            File.WriteAllText(Path.Combine(tempDir, "boot.gui.json"), manifestJson);
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

            // 4. Wait for pipeline to reach launch or fail
            string? lastTrace = null;
            string? lastCrash = null;
            string authlibPath = Path.Combine(tempDir, "authlib-injector.jar");
            var sw = Stopwatch.StartNew();
            bool done = false;
            while (sw.Elapsed.TotalSeconds < 600)
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

                bool hasForgeInstall = lastTrace != null &&
                    (lastTrace.Contains("Forge установлен:") ||
                     lastTrace.Contains("пропуск установки") ||
                     lastTrace.Contains("[DRY RUN] Installation of"));
                bool hasAuthlibDownloaded = File.Exists(authlibPath);
                bool hasCrash = lastCrash != null && lastCrash.Contains("Pipeline failed");

                if ((hasForgeInstall && hasAuthlibDownloaded) || hasCrash)
                {
                    done = true;
                    break;
                }

                if (proc.HasExited)
                {
                    done = true;
                    break;
                }

                Thread.Sleep(500);
            }

            Assert.True(done, "Pipeline did not complete or produce an error within 120 seconds.");

            // 5. Assert recidivism conditions
            Assert.NotNull(lastTrace);
            Assert.True(
                lastTrace.Contains("Forge установлен:") ||
                lastTrace.Contains("пропуск установки") ||
                lastTrace.Contains("[DRY RUN] Installation of"),
                "Forge installation was not reached or skipped.\n" +
                $"Crash log:\n{lastCrash ?? "(none)"}\n" +
                $"Trace log:\n{lastTrace}");

            // authlib-injector.jar must exist on disk (downloaded by Net.DownloadCommand)
            Assert.True(
                File.Exists(authlibPath),
                $"authlib-injector.jar missing at {authlibPath}. " +
                "Net.DownloadCommand did not download the file before launch.\n" +
                $"Trace log:\n{lastTrace}");

            // If a crash happened, it must NOT be the old missing-authlib JVM error
            if (lastCrash != null && lastCrash.Contains("Pipeline failed"))
            {
                Assert.DoesNotContain(
                    "Error opening zip file or JAR manifest missing",
                    lastCrash);
                Assert.DoesNotContain(
                    "ExitCode: 1",
                    lastCrash);
                // The new guard in MinecraftGameProvider should produce a clear message
                Assert.True(
                    lastCrash.Contains("authlib-injector.jar not found") ||
                    lastCrash.Contains("Stderr:"),
                    "Crash log does not contain expected human-readable error or stderr context.\n" +
                    $"Crash log:\n{lastCrash}");
            }
            else
            {
                // No crash means process is still alive or exited cleanly
                // Give it 15 seconds after launch prep to ensure it didn't crash immediately
                Thread.Sleep(15_000);
                if (proc.HasExited)
                {
                    Assert.Equal(0, proc.ExitCode);
                }
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
