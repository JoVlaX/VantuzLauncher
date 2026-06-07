using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// Recidivism prevention test: verifies that GUI-mode pipeline with OS.ExecuteCommand
/// (waitForExit=false) does NOT hang the launcher. Per INVARIANT_THEORY.md §1.2 and §17.
/// </summary>
[Collection("GUI Sequential")]
public class GuiModeFireAndForgetRecidivismTests : IDisposable
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

    private static string ResolveBootGuiTestJson()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "boot.gui.test.json");
        return Path.GetFullPath(path);
    }

    [StaFact]
    public void GuiMode_FireAndForget_ExecuteCommand_PipelineDoesNotHang()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        string exeDir = Path.GetDirectoryName(exe)!;
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_gui_fireforget_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);

        string traceLogPath = Path.Combine(tempDir, "launcher_trace.log");

        try
        {
            // 1. Copy EXE and dependencies (skip boot manifests; we'll inject our own)
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

            // 2. Load boot.gui.test.json, modify pipeline, and save as boot.gui.json
            string testManifestPath = ResolveBootGuiTestJson();
            Assert.True(File.Exists(testManifestPath), $"boot.gui.test.json not found at {testManifestPath}");
            var bootJson = File.ReadAllText(testManifestPath);
            var doc = JsonDocument.Parse(bootJson);
            var root = doc.RootElement;

            var pipeline = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                root.GetProperty("pipeline").GetRawText())!;

            // Remove dry-run Game.LaunchCommand
            pipeline.RemoveAll(step =>
                step.TryGetValue("pluginName", out var name) && name is string s && s == "Game.LaunchCommand");

            // Add MockGameLaunch that sets gameCommand/gameArgs for OS.ExecuteCommand
            pipeline.Add(new Dictionary<string, object>
            {
                ["pluginName"] = "Test.MockGameLaunch",
                ["config"] = new Dictionary<string, object>
                {
                    ["command"] = "cmd.exe",
                    ["arguments"] = "/c echo gui-fire-and-forget-test",
                    ["workDir"] = tempDir
                }
            });

            // Add OS.ExecuteCommand with waitForExit=false (the fire-and-forget scenario)
            pipeline.Add(new Dictionary<string, object>
            {
                ["pluginName"] = "OS.ExecuteCommand",
                ["config"] = new Dictionary<string, object>
                {
                    ["fileName"] = "{{gameCommand}}",
                    ["arguments"] = "{{gameArgs}}",
                    ["workDir"] = "{{gameWorkDir}}",
                    ["waitForExit"] = false
                }
            });

            var modifiedManifest = new Dictionary<string, object>
            {
                ["_description"] = root.TryGetProperty("_description", out var desc)
                    ? desc.GetString()! : "GUI fire-and-forget recidivism manifest",
                ["variables"] = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    root.GetProperty("variables").GetRawText())!,
                ["plugins"] = JsonSerializer.Deserialize<Dictionary<string, object>>(
                    root.GetProperty("plugins").GetRawText())!,
                ["pipeline"] = pipeline
            };

            string bootJsonPath = Path.Combine(tempDir, "boot.gui.json");
            File.WriteAllText(bootJsonPath,
                JsonSerializer.Serialize(modifiedManifest, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(tempDir, ".portable"), "");

            Assert.True(File.Exists(bootJsonPath), "boot.gui.json was not written to tempDir");
            string writtenBootJson = File.ReadAllText(bootJsonPath);
            Assert.Contains("Test.MockGameLaunch", writtenBootJson);
            Assert.DoesNotContain("Auth.YggdrasilCommand", writtenBootJson);

            // 3. Launch process in GUI mode (no --headless)
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

            // 4. Wait for pipeline to run (GUI init + auto-submit + mock launch + fire-and-forget execute)
            Thread.Sleep(10_000); // 10 seconds is enough for the whole pipeline + 2s grace period

            Assert.False(proc.HasExited,
                $"VantuzLauncher.exe crashed or exited unexpectedly. TempDir={tempDir}");

            // 5. Assert pipeline reached OS.ExecuteCommand and did not hang
            Assert.True(File.Exists(traceLogPath),
                $"Trace log not found at {traceLogPath}");

            string traceLog;
            using (var fs = new FileStream(traceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                traceLog = sr.ReadToEnd();
            }

            Assert.Contains("[ExecuteCommand] waitForExit=False", traceLog);
            Assert.Contains("[STEP] OS.ExecuteCommand completed", traceLog);
            Assert.DoesNotContain("Pipeline failed", traceLog);
            Assert.DoesNotContain("Cannot create more than one", traceLog, StringComparison.OrdinalIgnoreCase);
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
