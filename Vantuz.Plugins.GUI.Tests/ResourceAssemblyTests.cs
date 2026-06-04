using System.Reflection;
using Xunit;

namespace Vantuz.Plugins.GUI.MinecraftLauncher.Tests;

public class ResourceAssemblyTests
{
    [Fact]
    public void PluginAssembly_ContainsMainWindowType()
    {
        // Per DEVIATION-003: plugin assembly must expose MainWindow for WPF Pack URI resolution
        var pluginAssembly = typeof(MainWindow).Assembly;
        Assert.NotNull(pluginAssembly.GetType("Vantuz.Plugins.GUI.MinecraftLauncher.MainWindow"));
    }

    [Fact]
    public void PluginAssembly_HasBamlResources()
    {
        // Verify XAML resources are embedded in the plugin assembly (required for Pack URI)
        var pluginAssembly = typeof(MainWindow).Assembly;
        var resourceNames = pluginAssembly.GetManifestResourceNames();
        Assert.Contains(resourceNames, r => r.EndsWith(".g.resources"));
    }
}
