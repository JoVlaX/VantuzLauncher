using System.Reflection;
using Xunit;

namespace Vantuz.Plugins.GUI.MinecraftLauncher.Tests;

public class ResourceAssemblyTests
{
    [Fact]
    public void PluginAssembly_ContainsMainWindowType()
    {
        // Per DEVIATION-003: plugin assembly must expose MainWindow for GUI resolution
        var pluginAssembly = typeof(MainWindow).Assembly;
        Assert.NotNull(pluginAssembly.GetType("Vantuz.Plugins.GUI.MinecraftLauncher.MainWindow"));
    }

    [Fact]
    public void PluginAssembly_ReferencesAvalonia()
    {
        // Avalonia plugin must reference Avalonia assemblies
        var pluginAssembly = typeof(MainWindow).Assembly;
        var refs = pluginAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.Contains("Avalonia.Controls", refs);
        Assert.Contains("Avalonia.Base", refs);
    }
}
