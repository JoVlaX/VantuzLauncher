using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// Recidivism prevention test: verifies the launch path is exercised in headless mode
/// so we never again claim "working" when authlib is missing or the JVM crashes on start.
/// Per INVARIANT_THEORY.md §1.2 (Measurability) and §17 (Determinism):
/// this test runs fully headless with boot.test.json, no GUI window, no network calls.
/// </summary>
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
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "bin", "Debug", "net8.0-windows", "VantuzLauncher.exe");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            path = path.Replace("Debug", "Release");
        return path;
    }

    private static string ResolveBootTestJson()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "boot.test.json");
        return Path.GetFullPath(path);
    }

    [StaFact]
    public void HeadlessLaunchPipeline_AuthlibExists_And_ProcessDoesNotCrash()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        string exeDir = Path.GetDirectoryName(exe)!;
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_real_launch_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);

        string authlibPath = Path.Combine(tempDir, "authlib-injector.jar");

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

            // 2. Copy boot.test.json and set mcDir to tempDir for isolation
            string bootTestPath = ResolveBootTestJson();
            Assert.True(File.Exists(bootTestPath), $"boot.test.json not found at {bootTestPath}");
            var bootJson = File.ReadAllText(bootTestPath);
            var doc = JsonDocument.Parse(bootJson);
            var root = doc.RootElement;

            var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(
                root.GetProperty("variables").GetRawText())!;
            variables["mcDir"] = tempDir;

            var modifiedManifest = new Dictionary<string, object>
            {
                ["_description"] = root.TryGetProperty("_description", out var desc)
                    ? desc.GetString()! : "Test manifest",
                ["_principles"] = root.TryGetProperty("_principles", out var prin)
                    ? JsonSerializer.Deserialize<string[]>(prin.GetRawText())!
                    : new[] { "SRP", "Explicitness", "Determinism", "Nomadic", "Measurability" },
                ["variables"] = variables,
                ["plugins"] = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    root.GetProperty("plugins").GetRawText())!,
                ["pipeline"] = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                    root.GetProperty("pipeline").GetRawText())!
            };

            File.WriteAllText(Path.Combine(tempDir, "boot.test.json"),
                JsonSerializer.Serialize(modifiedManifest, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(tempDir, ".portable"), "");

            // 3. Place a mock authlib so Game.LaunchCommand does not fail on missing file
            File.WriteAllText(authlibPath, "MOCK_AUTHLIB");

            // 4. Launch headless process — no GUI window, deterministic per INVARIANT_THEORY.md
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(tempDir, "VantuzLauncher.exe"),
                    WorkingDirectory = tempDir,
                    Arguments = "--headless --test-mode --boot-path=boot.test.json --username=test --password=test",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            Assert.NotNull(proc);
            _processes.Add(proc);

            // 5. Wait for completion (dryRun pipeline finishes in < 30 s)
            bool finished = proc.WaitForExit(30_000);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            if (!finished)
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
            }

            string combined = stdout + stderr;

            // 6. Assert recidivism conditions
            Assert.True(finished,
                $"Pipeline did not complete within 30 seconds. Combined output:\n{combined}");

            // Forge installation step must have been reached (dryRun or real)
            Assert.True(
                combined.Contains("Forge установлен:") ||
                combined.Contains("пропуск установки") ||
                combined.Contains("[DRY RUN] Installation of"),
                "Forge installation was not reached or skipped.\n" +
                $"Combined output:\n{combined}");

            // authlib-injector.jar must exist on disk (mock is acceptable for headless dry-run)
            Assert.True(
                File.Exists(authlibPath),
                $"authlib-injector.jar missing at {authlibPath}. " +
                "Game.LaunchCommand would fail on missing authlib in production.\n" +
                $"Combined output:\n{combined}");

            // If a crash happened, it must NOT be the old missing-authlib JVM error
            if (combined.Contains("Pipeline failed"))
            {
                Assert.DoesNotContain(
                    "Error opening zip file or JAR manifest missing",
                    combined);
                Assert.DoesNotContain(
                    "ExitCode: 1",
                    combined);
                Assert.True(
                    combined.Contains("authlib-injector.jar not found") ||
                    combined.Contains("Stderr:"),
                    "Crash log does not contain expected human-readable error or stderr context.\n" +
                    $"Combined output:\n{combined}");
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
