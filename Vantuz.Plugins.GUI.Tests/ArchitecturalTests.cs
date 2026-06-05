using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// Architectural regression tests per COMPOSITUM_SPECIFICATION.md §4.1 Component Scope Invariant.
/// Ensures the Product (VantuzLauncher) contains NO UI dependencies or types.
/// GUI is exclusively a Category (plugin) concern.
/// </summary>
public class ArchitecturalTests
{
    private static string ResolveProductProjectPath()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "VantuzLauncher.csproj");
        return Path.GetFullPath(path);
    }

    private static string ResolveProductAssemblyPath()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "bin", "Release", "net8.0-windows", "VantuzLauncher.dll");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            path = path.Replace("Release", "Debug");
        return path;
    }

    [Fact]
    public void ProductCsproj_DoesNotReferenceWpf()
    {
        string csprojPath = ResolveProductProjectPath();
        Assert.True(File.Exists(csprojPath), $"VantuzLauncher.csproj not found at {csprojPath}");

        var xml = XDocument.Load(csprojPath);
        string content = xml.ToString();

        Assert.DoesNotContain("UseWPF", content);
        Assert.DoesNotContain("PresentationCore", content);
        Assert.DoesNotContain("PresentationFramework", content);
        Assert.DoesNotContain("WindowsBase", content); // WPF-only reference indicator
    }

    [Fact]
    public void ProductAssembly_DoesNotContainWpfTypes()
    {
        string assemblyPath = ResolveProductAssemblyPath();
        Assert.True(File.Exists(assemblyPath), $"VantuzLauncher assembly not found at {assemblyPath}");

        // Load the assembly in a reflection-only context
        var assembly = Assembly.LoadFrom(assemblyPath);

        // Check for any types inheriting from WPF types
        var wpfBaseTypes = new[]
        {
            "System.Windows.Window",
            "System.Controls.Control",
            "System.Windows.Controls.UserControl",
            "System.Windows.Controls.Page",
            "System.Windows.Application"
        };

        foreach (var type in assembly.GetTypes())
        {
            foreach (var wpfBase in wpfBaseTypes)
            {
                string? baseName = type.BaseType?.FullName;
                Assert.True(
                    baseName != wpfBase,
                    $"Product type '{type.FullName}' inherits from '{wpfBase}'. " +
                    "GUI types must live in plugin projects, not Product.");
            }
        }
    }

    [Fact]
    public void ProductAssembly_DoesNotReferenceWpfAssemblies()
    {
        string assemblyPath = ResolveProductAssemblyPath();
        Assert.True(File.Exists(assemblyPath), $"VantuzLauncher assembly not found at {assemblyPath}");

        var assembly = Assembly.LoadFrom(assemblyPath);
        var referencedAssemblies = assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        Assert.DoesNotContain("PresentationCore", referencedAssemblies);
        Assert.DoesNotContain("PresentationFramework", referencedAssemblies);
        Assert.DoesNotContain("WindowsBase", referencedAssemblies);
    }

    [Fact]
    public void ProductCsproj_DoesNotContainXamlFiles()
    {
        string csprojPath = ResolveProductProjectPath();
        var productDir = Path.GetDirectoryName(csprojPath)!;

        var xamlFiles = Directory.GetFiles(productDir, "*.xaml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(productDir, "*.axaml", SearchOption.TopDirectoryOnly))
            .ToList();

        Assert.Empty(xamlFiles);
    }
}
