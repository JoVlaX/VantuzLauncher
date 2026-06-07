using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// Recidivism prevention test: verifies the Forge installer path is exercised
/// in headless mode so we never again report "working" when Forge versions silently fail.
/// Per INVARIANT_THEORY.md §1.2 (Measurability) and §17 (Determinism):
/// this test runs fully headless with boot.test.json, no GUI window, no network calls.
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
    public void HeadlessForgePipeline_ProducesHumanReadableErrorOrSucceeds()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        string exeDir = Path.GetDirectoryName(exe)!;
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_forge_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);

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

            // 3. Launch headless process — no GUI window, deterministic per INVARIANT_THEORY.md
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

            // 4. Wait for completion (dryRun pipeline finishes in < 30 s)
            bool finished = proc.WaitForExit(30_000);
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            if (!finished)
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
            }

            string combined = stdout + stderr;

            // 5. Assert recidivism conditions
            Assert.True(finished,
                $"Pipeline did not complete within 30 seconds. Combined output:\n{combined}");

            Assert.True(
                combined.Contains("[DRY RUN] Installation of"),
                "The pipeline never attempted Forge installation. " +
                "GameInstallerCommand may have been skipped or failed silently.\n" +
                $"Combined output:\n{combined}");

            Assert.True(
                combined.Contains("Forge установлен:") ||
                combined.Contains("пропуск установки") ||
                combined.Contains("[DRY RUN] Installation of"),
                "Neither Forge installation nor skip/dry-run message found. " +
                "GameInstallerCommand may have failed before reaching the provider.\n" +
                $"Combined output:\n{combined}");

            if (combined.Contains("Pipeline failed"))
            {
                Assert.DoesNotContain("Cannot find 1.20.1-forge-47.3.0", combined);
                Assert.DoesNotContain("KeyNotFoundException", combined);
                Assert.DoesNotContain("ExitCode: 1", combined);
                Assert.True(
                    combined.Contains("Stderr:") || combined.Contains("Ошибка установки"),
                    "Crash log contains a bare ExitCode without stderr context.\n" +
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
