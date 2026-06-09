using System.Reflection;
using Xunit;

namespace Vantuz.Plugins.GUI.MinecraftLauncher.Tests;
/// F_doc: {ResourceAssemblyTests returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ResourceAssemblyTests behavior

public class ResourceAssemblyTests
{
    [Fact]
    /// F_doc: {PluginAssembly_ContainsMainWindowType returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies PluginAssembly_ContainsMainWindowType behavior
    public void PluginAssembly_ContainsMainWindowType()
    {
        // Per DEVIATION-003: plugin assembly must expose MainWindow for GUI resolution
        var pluginAssembly = typeof(MainWindow).Assembly;
        Assert.NotNull(pluginAssembly.GetType("Vantuz.Plugins.GUI.MinecraftLauncher.MainWindow"));
    }

    [Fact]
    /// F_doc: {PluginAssembly_ReferencesAvalonia returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies PluginAssembly_ReferencesAvalonia behavior
    public void PluginAssembly_ReferencesAvalonia()
    {
        // Avalonia plugin must reference Avalonia assemblies
        var pluginAssembly = typeof(MainWindow).Assembly;
        var refs = pluginAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.Contains("Avalonia.Controls", refs);
        Assert.Contains("Avalonia.Base", refs);
    }
}
