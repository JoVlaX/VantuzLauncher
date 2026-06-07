namespace Vantuz.Core.Tests;

using System.IO;
using Vantuz.Plugins.Minecraft;
using Xunit;

/// <summary>
/// Tests for MinecraftGameProvider — the layer that CmlLib uses to check version
/// existence and parse Forge version strings.
/// Per INVARIANT_THEORY.md §1.2: falsifiable claims about version detection.
/// </summary>
public class MinecraftGameProviderTests
{
    /// <summary>
    /// E_doc: "1.20.1-forge-47.3.0" parses into mcVersion="1.20.1", forgeVersion="47.3.0".
    /// F_doc: This is the exact version string used in boot.json manifests.
    /// </summary>
    [Fact]
    public void ParseForgeVersion_StandardFormat_ReturnsCorrectTuple()
    {
        var (mcVersion, forgeVersion) = MinecraftGameProvider.ParseForgeVersion("1.20.1-forge-47.3.0");

        Assert.Equal("1.20.1", mcVersion);
        Assert.Equal("47.3.0", forgeVersion);
    }

    /// <summary>
    /// E_doc: Fallback split handles edge cases with multiple dashes.
    /// F_doc: "1.20.1-forge-47.3.0" also works via fallback.
    /// </summary>
    [Fact]
    public void ParseForgeVersion_FallbackSplit_ReturnsCorrectTuple()
    {
        // This format triggers the fallback path because it has 3+ parts
        var (mcVersion, forgeVersion) = MinecraftGameProvider.ParseForgeVersion("1.19.2-forge-43.2.0");

        Assert.Equal("1.19.2", mcVersion);
        Assert.Equal("43.2.0", forgeVersion);
    }

    /// <summary>
    /// E_doc: When the version JSON exists in the .minecraft/versions folder, CheckVersionAsync returns Exists=true.
    /// F_doc: CmlLib's MinecraftPath.GetVersionJsonPath must match the path we create.
    /// </summary>
    [Fact]
    public async Task CheckVersionAsync_ExistingVersion_ReturnsTrue()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string versionsDir = Path.Combine(tempDir, ".minecraft", "versions", "1.20.1-forge-47.3.0");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1-forge-47.3.0.json");
        await File.WriteAllTextAsync(versionJsonPath, "{\"id\":\"1.20.1-forge-47.3.0\"}");

        try
        {
            var provider = new MinecraftGameProvider();
            var result = await provider.CheckVersionAsync("1.20.1-forge-47.3.0", Path.Combine(tempDir, ".minecraft"), default);

            Assert.True(result.Exists, $"Expected Exists=true but got: {result.ErrorMessage}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When the version JSON is missing, CheckVersionAsync returns Exists=false.
    /// F_doc: This is the signal that triggers InstallVersionAsync in the pipeline.
    /// </summary>
    [Fact]
    public async Task CheckVersionAsync_MissingVersion_ReturnsFalse()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string mcDir = Path.Combine(tempDir, ".minecraft");
        Directory.CreateDirectory(mcDir);

        try
        {
            var provider = new MinecraftGameProvider();
            var result = await provider.CheckVersionAsync("1.20.1-forge-47.3.0", mcDir, default);

            Assert.False(result.Exists);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: When Forge version JSON already exists, GameInstallerCommand skips install and returns success.
    /// F_doc: This prevents unnecessary re-downloads and avoids the Forge installer timeout path entirely.
    /// </summary>
    [Fact]
    public async Task GameInstallerCommand_ForgeAlreadyInstalled_SkipsInstall()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_mc_test_{Guid.NewGuid():N}");
        string versionsDir = Path.Combine(tempDir, ".minecraft", "versions", "1.20.1-forge-47.3.0");
        Directory.CreateDirectory(versionsDir);
        string versionJsonPath = Path.Combine(versionsDir, "1.20.1-forge-47.3.0.json");
        await File.WriteAllTextAsync(versionJsonPath, "{\"id\":\"1.20.1-forge-47.3.0\"}");

        try
        {
            var reporter = new ListReporter();
            var context = new CommandContext(System.Threading.CancellationToken.None, reporter);
            context.Set("GameProvider.Minecraft", new MinecraftGameProvider());

            var stepConfig = System.Text.Json.JsonDocument.Parse($@"{{
                ""provider"": ""Minecraft"",
                ""version"": ""1.20.1-forge-47.3.0"",
                ""installDir"": ""{Path.Combine(tempDir, ".minecraft").Replace("\\", "\\\\")}""
            }}").RootElement;

            var command = new Vantuz.Plugins.Game.GameInstallerCommand();
            var result = await command.ExecuteAsync(context, stepConfig);

            Assert.True(result.Success);
            Assert.Contains("уже установлена", reporter.Logs[^1]); // "Версия ... уже установлена, пропуск установки."
            Assert.True(context.Get<bool>("InstallSkipped"));
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private class ListReporter : IStatusReporter
    {
        public List<string> Logs { get; } = new();
        public void ReportState(string message) => Logs.Add(message);
        public void ReportProgress(string taskName, double percentage) => Logs.Add($"[{taskName}] {percentage:F1}%");
    }
}
