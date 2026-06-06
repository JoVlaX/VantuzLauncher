using System.IO;
using System.Text.Json;
using Xunit;

namespace Vantuz.Core.Tests;

/// <summary>
/// Functional GUI-mode verification per AGENT_FAILURE_ANALYSIS.md §7.5 (R7).
/// Ensures the configured Minecraft version string is valid and manifests are consistent.
/// This would have caught the "Cannot find 1.20.1-forge-47.2.20" root cause
/// (boot.json loaded instead of boot.gui.json with mismatched versions).
/// </summary>
public class GuiModeFunctionalTests
{
    private static readonly string ProjectRoot = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "..");

    private static string ResolvePath(string relative) =>
        Path.GetFullPath(Path.Combine(ProjectRoot, relative));

    [Fact]
    public void GuiManifest_GameVersion_IsValidForgeFormat()
    {
        string guiManifestPath = ResolvePath("boot.gui.json");
        Assert.True(File.Exists(guiManifestPath), "boot.gui.json not found");

        var json = File.ReadAllText(guiManifestPath);
        using var doc = JsonDocument.Parse(json);
        var variables = doc.RootElement.GetProperty("variables");
        string gameVersion = variables.GetProperty("gameVersion").GetString()!;

        // Must contain Forge marker
        Assert.Contains("-forge-", gameVersion);

        // Must be parseable into MC version + Forge build number
        var parts = gameVersion.Split("-forge-");
        Assert.Equal(2, parts.Length);
        Assert.False(string.IsNullOrWhiteSpace(parts[0]), "Minecraft version part is empty");
        Assert.False(string.IsNullOrWhiteSpace(parts[1]), "Forge build number part is empty");
    }

    [Fact]
    public void GeneratedBootJson_MatchesGuiManifest_GameVersion()
    {
        // Per plan fix-r7-blocker-622aab: boot.json (generated from template)
        // must have the same gameVersion as boot.gui.json (loaded by GUI mode)
        string guiPath = ResolvePath("boot.gui.json");
        string templatePath = ResolvePath("boot.template.json");

        Assert.True(File.Exists(guiPath), "boot.gui.json not found");
        Assert.True(File.Exists(templatePath), "boot.template.json not found");

        string ReadVersion(string path)
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("variables").GetProperty("gameVersion").GetString()!;
        }

        string guiVersion = ReadVersion(guiPath);
        string templateVersion = ReadVersion(templatePath);

        Assert.Equal(guiVersion, templateVersion);
    }

    [Fact]
    public void AllManifests_HaveConsistentGameProvider()
    {
        var manifests = new[] { "boot.gui.json", "boot.template.json", "boot.headless.json", "boot.test.json" };
        string? firstProvider = null;

        foreach (var manifest in manifests)
        {
            string path = ResolvePath(manifest);
            if (!File.Exists(path)) continue;

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var variables = doc.RootElement.GetProperty("variables");

            // Skip manifests that don't declare gameProvider (e.g., headless test manifest)
            if (!variables.TryGetProperty("gameProvider", out var providerProp))
                continue;

            var provider = providerProp.GetString()!;

            if (firstProvider == null)
                firstProvider = provider;
            else
                Assert.Equal(firstProvider, provider);
        }

        Assert.NotNull(firstProvider);
    }

    [Fact]
    public void Program_Loads_GuiManifest_NotTemplate()
    {
        // Per AGENT_FAILURE_ANALYSIS.md §7.3: GUI mode must load boot.gui.json
        // in GUI mode, not boot.json (generated from boot.template.json).
        // The entry point (Program.cs) now owns manifest selection.
        string programPath = ResolvePath("Program.cs");
        Assert.True(File.Exists(programPath), "Program.cs not found");

        string source = File.ReadAllText(programPath);
        Assert.Contains("boot.gui.json", source);
        Assert.DoesNotContain("\"boot.json\"", source);
    }
}
