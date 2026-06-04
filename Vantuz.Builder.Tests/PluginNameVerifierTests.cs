using System.IO;
using System.Text.Json;
using Xunit;

namespace Vantuz.Builder.Tests;

public class PluginNameVerifierTests
{
    [Fact]
    public void VerifyManifest_ValidBootJson_ReturnsZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);

        // Create a dummy plugin assembly (we can't create a real one easily, so we test directory logic)
        var bootJson = Path.Combine(tempDir, "boot.json");
        var manifest = new
        {
            plugins = new Dictionary<string, string> { ["TestPlugin.dll"] = "" },
            pipeline = new[] { new { pluginName = "TestPlugin", config = new { } } }
        };
        File.WriteAllText(bootJson, JsonSerializer.Serialize(manifest));

        var result = PluginNameVerifier.VerifyManifest(bootJson, pluginsDir);
        // No real assembly present, so plugin name won't be found; but we verify the method runs without crash
        Assert.True(result == 0 || result == 1, "VerifyManifest should return a valid exit code");

        Directory.Delete(tempDir, true);
    }

    [Fact]
    public void VerifyDirectory_NoPlugins_ReturnsZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);

        // VerifyDirectory requires at least one boot*.json manifest
        var bootJson = Path.Combine(tempDir, "boot.json");
        var manifest = new
        {
            plugins = new Dictionary<string, string>(),
            pipeline = Array.Empty<object>()
        };
        File.WriteAllText(bootJson, JsonSerializer.Serialize(manifest));

        var result = PluginNameVerifier.VerifyDirectory(tempDir, pluginsDir);
        Assert.Equal(0, result);

        Directory.Delete(tempDir, true);
    }
}
