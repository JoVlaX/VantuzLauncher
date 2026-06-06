using System.IO;
using System.Text.Json;
using Vantuz.Host;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

public class ManifestSerializationTests
{
    [Fact]
    public void ModifiedManifest_ContainsTwoDownloadCommands()
    {
        string bootGuiPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "boot.gui.json");
        bootGuiPath = Path.GetFullPath(bootGuiPath);
        Assert.True(File.Exists(bootGuiPath));

        string bootJson = File.ReadAllText(bootGuiPath);
        var doc = JsonDocument.Parse(bootJson);
        var root = doc.RootElement;

        var pipeline = root.GetProperty("pipeline");
        var modifiedSteps = new List<Dictionary<string, object>>();
        foreach (var step in pipeline.EnumerateArray())
        {
            string pluginName = step.GetProperty("pluginName").GetString()!;
            if (pluginName == "Game.InstallerCommand")
            {
                var config = step.GetProperty("config");
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(config.GetRawText())!;
                dict["dryRun"] = false;
                modifiedSteps.Add(new Dictionary<string, object>
                {
                    ["pluginName"] = pluginName,
                    ["config"] = dict
                });
            }
            else if (pluginName == "Game.LaunchCommand")
            {
                var config = step.GetProperty("config");
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(config.GetRawText())!;
                dict["dryRun"] = false;
                modifiedSteps.Add(new Dictionary<string, object>
                {
                    ["pluginName"] = pluginName,
                    ["config"] = dict
                });
            }
            else
            {
                var stepDict = JsonSerializer.Deserialize<Dictionary<string, object>>(step.GetRawText())!;
                modifiedSteps.Add(stepDict);
            }
        }

        var modifiedManifest = new Dictionary<string, object>
        {
            ["variables"] = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetProperty("variables").GetRawText()),
            ["plugins"] = JsonSerializer.Deserialize<Dictionary<string, object>>(root.GetProperty("plugins").GetRawText()),
            ["pipeline"] = modifiedSteps
        };

        string manifestJson = JsonSerializer.Serialize(modifiedManifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText("C:\\temp\\debug_manifest.json", manifestJson);

        int count = manifestJson.Split("Net.DownloadCommand").Length - 1;
        Assert.True(count >= 2,
            $"Expected at least 2 Net.DownloadCommand steps, found {count}.\nManifest:\n{manifestJson}");

        // Also verify VantuzEngine can deserialize it back into a BootManifest with both steps
        var deserializedManifest = JsonSerializer.Deserialize<BootManifest>(manifestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(deserializedManifest);
        Assert.NotNull(deserializedManifest.Pipeline);
        int pipelineDownloadCount = deserializedManifest.Pipeline.Count(s => s.PluginName == "Net.DownloadCommand");
        Assert.True(pipelineDownloadCount >= 2,
            $"Deserialized BootManifest has {pipelineDownloadCount} Net.DownloadCommand steps, expected >= 2.");
    }
}
