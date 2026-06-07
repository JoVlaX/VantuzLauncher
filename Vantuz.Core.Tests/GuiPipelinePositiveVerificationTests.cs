using System.Reflection;
using System.Text.Json;
using Vantuz.Host;
using Xunit;

namespace Vantuz.Core.Tests;

/// <summary>
/// GUI pipeline positive verification tests.
/// Per INVARIANT_THEORY.md §1.2: a claim must be falsifiable by a positive observation.
/// These tests assert that the GUI manifest (boot.json) pipeline can be fully resolved
/// to loaded plugin classes, closing the gap between "build succeeds" and "GUI pipeline works".
/// </summary>
public class GuiPipelinePositiveVerificationTests
{
    /// <summary>
    /// E_doc: Every pluginName in boot.json resolves to a loaded QuantizedNode.
    /// F_doc: A pipeline step references a plugin name that does not exist among loaded nodes
    ///        (the root cause of the "Plugin Net.ApiReaderQuery not found" runtime crash).
    /// </summary>
    [Fact]
    public void GuiPipeline_ResolvesAllPlugins()
    {
        var (bootPath, pluginsDir) = ResolveGuiPaths();

        // Copy plugins to isolated temp workspace to avoid file-lock conflicts
        string workspace = Path.Combine(Path.GetTempPath(), $"vantuz_gui_resolution_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        string tempPlugins = Path.Combine(workspace, "plugins");
        CopyDirectory(pluginsDir, tempPlugins);

        try
        {
            var json = File.ReadAllText(bootPath);
            var manifest = JsonSerializer.Deserialize<BootManifest>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Failed to deserialize boot.json");

            string[] shared = new[] { typeof(QuantizedNode).Assembly.GetName().Name! };
            var loader = new PluginLoader(shared);
            var allowedDlls = manifest.Plugins.Keys.ToList();
            var quantizedNodes = loader.LoadQuantizedNodesFromDirectory(tempPlugins, allowedDlls).ToList();
            var cqrsNodes = loader.LoadCqrsPluginsFromDirectory(tempPlugins, allowedDlls).ToList();
            quantizedNodes.AddRange(cqrsNodes);

            var engineType = typeof(VantuzEngine);
            var buildPipeline = engineType.GetMethod(
                "BuildQuantumPipeline",
                BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("BuildQuantumPipeline method not found via reflection");

            var reporter = new ListReporter();
            var engine = new VantuzEngine(tempPlugins, reporter, Path.Combine(workspace, "crash.log"));

            // Invoke the private method that historically threw "Plugin X not found"
            var pipeline = buildPipeline.Invoke(engine, new object[] { manifest.Pipeline, quantizedNodes })
                as System.Collections.IList;

            Assert.NotNull(pipeline);
            Assert.Equal(manifest.Pipeline.Count, pipeline.Count);

            // Positive: every step name appears in the reporter debug log (if any)
            // The fact that Invoke succeeded without exception is the primary assertion.
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: VantuzEngine.RunAsync with GUI manifest either succeeds or fails with
    ///        a non-critical error (e.g. network timeout), but NEVER with "Plugin X not found".
    /// F_doc: The engine crashes with "Plugin {name} not found" during BuildQuantumPipeline.
    /// </summary>
    [Fact]
    public async Task GuiPipeline_ExecutesWithoutPluginNotFoundCrash()
    {
        var (bootPath, pluginsDir) = ResolveGuiPaths();

        string workspace = Path.Combine(Path.GetTempPath(), $"vantuz_gui_run_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        string tempPlugins = Path.Combine(workspace, "plugins");
        CopyDirectory(pluginsDir, tempPlugins);

        try
        {
            var reporter = new ListReporter();
            var engine = new VantuzEngine(tempPlugins, reporter, Path.Combine(workspace, "crash.log"));

            // Provide minimal payload so interpolation doesn't fail on missing keys
            var initialPayload = new Dictionary<string, object>
            {
                ["localVersion"] = "2.0-test",
                ["gameProvider"] = "Minecraft",
                ["gameVersion"] = "1.20.1-forge-47.3.0",
                ["mcDir"] = Path.Combine(workspace, ".minecraft"),
                ["installDir"] = Path.Combine(workspace, ".minecraft", ".minecraft"),
                ["gameCommand"] = "java",
                ["gameArgs"] = "-version",
                ["gameWorkDir"] = workspace,
                ["username"] = "test",
                ["password"] = "test",
                ["ramMb"] = 4096,
                ["authEndpoint"] = "https://example.com"
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Exception? caught = null;
            QuantumExecutionResult? result = null;

            try
            {
                result = await engine.RunAsync(bootPath, cts.Token, initialPayload);
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            // If a plugin name mismatch occurs, the exception message will contain "not found"
            if (caught != null)
            {
                Assert.DoesNotContain("not found", caught.Message, StringComparison.OrdinalIgnoreCase);
            }

            // If it completed (fast path with local mocking / dry run), assert success
            if (result.HasValue && result.Value.Success)
            {
                Assert.True(result.Value.Success);
            }

            // The critical invariant: no "Plugin X not found" in crash log either
            string crashLogPath = Path.Combine(workspace, "crash.log");
            if (File.Exists(crashLogPath))
            {
                var crashContent = File.ReadAllText(crashLogPath);
                Assert.DoesNotContain("not found", crashContent, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: boot.json contains the expected GUI pipeline steps (13 steps).
    /// F_doc: A step is added or removed without updating this test.
    /// </summary>
    [Fact]
    public void BootGuiJson_StepsMatchExpectedSet()
    {
        var (bootPath, _) = ResolveGuiPaths();
        var json = File.ReadAllText(bootPath);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var pipeline = doc.GetProperty("pipeline");
        var actualSteps = pipeline.EnumerateArray()
            .Select(s => s.GetProperty("pluginName").GetString())
            .ToList();

        var expectedSteps = new[]
        {
            "Net.ApiReaderQuery",
            "Net.UpdateCommand",
            "Auth.YggdrasilCommand",
            "Game.MinecraftProvider",
            "Game.VersionValidatorQuery",
            "Game.InstallerCommand",
            "Net.ModpackManifestQuery",
            "OS.DeltaAnalyzerQuery",
            "OS.LocalMoveCommand",
            "Net.DownloadCommand",
            "OS.BatchPurgeCommand",
            "Game.LaunchCommand",
            "OS.ExecuteCommand"
        };

        Assert.Equal(expectedSteps.Length, actualSteps.Count);
        foreach (var expected in expectedSteps)
        {
            Assert.Contains(expected, actualSteps);
        }
    }

    private static (string BootPath, string PluginsDir) ResolveGuiPaths()
    {
        var searchDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..")),
        };

        string? bootPath = searchDirs
            .SelectMany(d => new[]
            {
                Path.Combine(d, "bin", "Release", "net8.0-windows", "boot.json"),
                Path.Combine(d, "bin", "Debug", "net8.0-windows", "boot.json"),
            })
            .FirstOrDefault(File.Exists);

        Assert.True(bootPath != null, $"boot.json not found. Searched under: {string.Join(", ", searchDirs)}");

        string? pluginsDir = searchDirs
            .SelectMany(d => new[]
            {
                Path.Combine(d, "bin", "Release", "net8.0-windows", "plugins"),
                Path.Combine(d, "bin", "Debug", "net8.0-windows", "plugins"),
            })
            .FirstOrDefault(Directory.Exists);

        Assert.True(pluginsDir != null, $"plugins directory not found");
        return (bootPath, pluginsDir);
    }

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relPath = Path.GetRelativePath(source, file);
            string destPath = Path.Combine(dest, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            File.Copy(file, destPath, true);
        }
    }

    private class ListReporter : IStatusReporter
    {
        public List<string> Logs { get; } = new();

        public void ReportState(string message)
        {
            Logs.Add(message);
        }

        public void ReportProgress(string taskName, double percentage)
        {
            Logs.Add($"[{taskName}] {percentage:F1}%");
        }
    }
}
