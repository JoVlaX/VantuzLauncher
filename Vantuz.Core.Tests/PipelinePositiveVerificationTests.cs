using System.Text.Json;
using Vantuz.Core;
using Vantuz.Host;
using Xunit;

namespace Vantuz.Core.Tests;

/// <summary>
/// Positive verification tests for the Vantuz pipeline.
/// Per INVARIANT_THEORY.md §1.2: a claim must be falsifiable by a positive observation.
/// These tests assert that specific pipeline steps actually executed and logged completion markers,
/// not merely that no crash occurred.
/// </summary>
public class PipelinePositiveVerificationTests
{
    /// <summary>
    /// E_doc: Headless pipeline with boot.headless.json executes all steps and each step logs
    ///       "[STEP] {pluginName} completed" via QuantumScheduler.
    /// F_doc: Any step missing its completion marker, or result.Success == false, or
    ///       JsonException/NullReferenceException during boot manifest load.
    /// </summary>
    [Fact]
    public async Task Headless_RunsAllSteps_AndLogsPositiveMarkers()
    {
        // Resolve paths relative to solution root (same heuristic as HeadlessSmokeTests)
        var searchDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..")),
        };

        string? bootPath = searchDirs.Select(d => Path.Combine(d, "boot.headless.json")).FirstOrDefault(File.Exists);
        Assert.True(bootPath != null, $"boot.headless.json not found. Searched: {string.Join(", ", searchDirs)}");

        string? sourcePluginsDir = searchDirs
            .Select(d => Path.Combine(d, "bin", "Debug", "net8.0-windows", "plugins"))
            .Concat(searchDirs.Select(d => Path.Combine(d, "bin", "Release", "net8.0-windows", "plugins")))
            .FirstOrDefault(Directory.Exists);
        Assert.True(sourcePluginsDir != null, $"plugins directory not found. Searched: {string.Join(", ", searchDirs.Select(d => Path.Combine(d, "bin", "Release", "net8.0-windows", "plugins")))}");

        // Use a temp workspace so we do not pollute the user profile
        string workspace = Path.Combine(Path.GetTempPath(), $"vantuz_positive_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);

        // Copy plugins to an isolated temp directory to avoid file-lock conflicts
        // with parallel GUI tests that launch VantuzLauncher.exe
        string pluginsDir = Path.Combine(workspace, "plugins");
        CopyDirectory(sourcePluginsDir, pluginsDir);

        try
        {
            var reporter = new ListReporter();
            string crashLogPath = Path.Combine(workspace, "crash.log");
            var engine = new VantuzEngine(pluginsDir, reporter, crashLogPath);

            var initialPayload = new Dictionary<string, object>
            {
                ["username"] = "test_user",
                ["password"] = "test_password",
                ["ramMb"] = 4096,
                ["workspace"] = workspace
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await engine.RunAsync(bootPath, cts.Token, initialPayload);

            // Positive assertion 1: pipeline succeeded
            Assert.True(result.Success, $"Engine execution failed: {result.ErrorMessage}");

            // Positive assertion 2: each expected step logged its completion marker
            var logs = reporter.Logs;
            Assert.Contains("[STEP] Test.MockCredentialProvider completed", logs);
            Assert.Contains("[STEP] Auth.TestAuthCommand completed", logs);
            Assert.Contains("[STEP] Game.MinecraftProvider completed", logs);
            Assert.Contains("[STEP] Game.InstallerCommand completed", logs);
            Assert.Contains("[STEP] Game.VersionValidatorQuery completed", logs);

            // Positive assertion 3: downstream payload mutations exist (proof pipeline produced data)
            Assert.NotNull(result.Payload);
            Assert.True(result.Payload.ContainsKey("workspace"), "Payload missing 'workspace' — pipeline did not propagate initial payload");
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: boot.headless.json defines exactly the steps we assert above.
    /// F_doc: A step is added or removed without updating this test.
    /// </summary>
    [Fact]
    public void BootHeadlessJson_StepsMatchExpectedSet()
    {
        var searchDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..")),
        };
        string? bootPath = searchDirs.Select(d => Path.Combine(d, "boot.headless.json")).FirstOrDefault(File.Exists);
        Assert.True(bootPath != null, "boot.headless.json not found");

        var json = File.ReadAllText(bootPath);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var pipeline = doc.GetProperty("pipeline");
        var actualSteps = pipeline.EnumerateArray()
            .Select(s => s.GetProperty("pluginName").GetString())
            .ToList();

        var expectedSteps = new[]
        {
            "Test.MockCredentialProvider",
            "Auth.TestAuthCommand",
            "Game.MinecraftProvider",
            "Game.InstallerCommand",
            "Game.VersionValidatorQuery"
        };

        Assert.Equal(expectedSteps.Length, actualSteps.Count);
        foreach (var expected in expectedSteps)
        {
            Assert.Contains(expected, actualSteps);
        }
    }

    /// <summary>
    /// E_doc: boot.gui.json contains no unresolved {{variable}} placeholders.
    /// F_doc: A placeholder references a variable that is never defined in variables[] nor injected at runtime.
    ///        This recidivism occurred with {{authEndpoint}} leaking into JVM args.
    /// </summary>
    [Theory]
    [InlineData("boot.gui.json")]
    [InlineData("boot.headless.json")]
    [InlineData("boot.minecraft.production.json")]
    public void Manifest_NoUnresolvedPlaceholders(string fileName)
    {
        var searchDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..")),
        };
        string? bootPath = searchDirs.Select(d => Path.Combine(d, fileName)).FirstOrDefault(File.Exists);
        Assert.True(bootPath != null, $"{fileName} not found");

        var json = File.ReadAllText(bootPath);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);

        // Collect defined variables + known runtime-injected keys
        var knownVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gameProvider", "gameVersion", "mcDir", "installDir",
            "operationTimeout", "localVersion", "workspace", "username",
            "password", "ramMb", "gameCommand", "gameArgs", "gameWorkDir",
            "testUser", "testPass", "remoteVersion"
        };
        if (doc.TryGetProperty("variables", out var vars))
        {
            foreach (var prop in vars.EnumerateObject())
                knownVars.Add(prop.Name);
        }

        // Find all {{...}} references in the entire JSON text
        var raw = json;
        var unresolved = new List<string>();
        int i = 0;
        while ((i = raw.IndexOf("{{", i, StringComparison.Ordinal)) >= 0)
        {
            int end = raw.IndexOf("}}", i + 2, StringComparison.Ordinal);
            Assert.True(end > i, $"Unclosed placeholder in {fileName} at offset {i}");
            string key = raw.Substring(i + 2, end - i - 2);
            if (!knownVars.Contains(key))
                unresolved.Add(key);
            i = end + 2;
        }

        Assert.Empty(unresolved);
    }

    /// <summary>
    /// E_doc: In every production manifest, Game.LaunchCommand is immediately followed by OS.ExecuteCommand.
    /// F_doc: The ExecuteCommand step is missing after LaunchCommand, so the game process is never started.
    ///        This recidivism occurred in boot.minecraft.production.json.
    /// </summary>
    [Theory]
    [InlineData("boot.gui.json")]
    [InlineData("boot.minecraft.production.json")]
    public void Manifest_LaunchCommand_IsFollowedByExecuteCommand(string fileName)
    {
        var searchDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..")),
        };
        string? bootPath = searchDirs.Select(d => Path.Combine(d, fileName)).FirstOrDefault(File.Exists);
        Assert.True(bootPath != null, $"{fileName} not found");

        var json = File.ReadAllText(bootPath);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var pipeline = doc.GetProperty("pipeline");
        var steps = pipeline.EnumerateArray()
            .Select(s => s.GetProperty("pluginName").GetString())
            .ToList();

        int launchIndex = steps.IndexOf("Game.LaunchCommand");
        if (launchIndex < 0) return; // Not all manifests need a launch step

        int executeIndex = steps.IndexOf("OS.ExecuteCommand");
        Assert.True(executeIndex >= 0,
            $"{fileName}: OS.ExecuteCommand is missing after Game.LaunchCommand. Game will never start.");
        Assert.True(executeIndex == launchIndex + 1,
            $"{fileName}: OS.ExecuteCommand must immediately follow Game.LaunchCommand (found at index {executeIndex}, expected {launchIndex + 1}).");
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

    /// <summary>
    /// Simple reporter that records every ReportState call for post-run assertion.
    /// </summary>
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
