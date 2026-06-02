using System.IO;
using System.Diagnostics;
using Vantuz.Core;
using Vantuz.Host;

namespace Vantuz.Products.MinecraftLauncher.Core;

/// <summary>
/// Product entry point for Minecraft Launcher.
/// Per INVARIANT_THEORY.md §498: Explicit product bootstrap, separates GUI from headless.
/// </summary>
public class MinecraftLauncherEntryPoint : IProductEntryPoint
{
    public string Name => "Product.MinecraftLauncher";

    public async Task<int> RunAsync(string[] args, string bootJsonPath, string pluginsDir, string crashLogPath)
    {
        var reporter = new ConsoleStatusReporter();

        // Phase 1: Detect mode based on pipeline configuration
        // Per §1.2 Measurability: GUI presence is statically verifiable from boot.json
        var manifest = System.Text.Json.JsonSerializer.Deserialize<BootManifest>(
            await File.ReadAllTextAsync(bootJsonPath),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (manifest == null)
        {
            reporter.ReportState("[FATAL] Failed to parse boot.json");
            return 1;
        }

        var hasGui = manifest.Pipeline.Any(s => s.PluginName.StartsWith("GUI."));
        var headlessFlag = args.Contains("--headless") || args.Contains("--test-mode");

        if (hasGui && !headlessFlag)
        {
            // GUI Mode: Load GUI plugin dynamically
            reporter.ReportState("[BOOT] Starting in GUI mode");
            return await RunWithGUIAsync(bootJsonPath, pluginsDir, crashLogPath, reporter);
        }
        else
        {
            // Headless Mode: Direct engine execution
            reporter.ReportState("[BOOT] Starting in headless mode");
            return await RunHeadlessAsync(bootJsonPath, pluginsDir, crashLogPath, reporter);
        }
    }

    private async Task<int> RunWithGUIAsync(
        string bootJsonPath,
        string pluginsDir,
        string crashLogPath,
        IStatusReporter reporter)
    {
        // GUI plugin will be loaded by VantuzEngine from pipeline
        var engine = new VantuzEngine(pluginsDir, reporter, crashLogPath);

        try
        {
            var result = await engine.RunAsync(bootJsonPath, CancellationToken.None);
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            reporter.ReportState($"[FATAL] {ex.Message}");
            File.AppendAllText(crashLogPath, $"[{DateTime.Now}] {ex}\n");
            return 1;
        }
    }

    private async Task<int> RunHeadlessAsync(
        string bootJsonPath,
        string pluginsDir,
        string crashLogPath,
        IStatusReporter reporter)
    {
        var engine = new VantuzEngine(pluginsDir, reporter, crashLogPath);

        try
        {
            var result = await engine.RunAsync(bootJsonPath, CancellationToken.None);
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            reporter.ReportState($"[FATAL] {ex.Message}");
            File.AppendAllText(crashLogPath, $"[{DateTime.Now}] {ex}\n");
            return 1;
        }
    }
}

/// <summary>
/// Simple console reporter for bootstrap phase.
/// </summary>
public class ConsoleStatusReporter : IStatusReporter
{
    public void ReportState(string message)
    {
        Console.WriteLine(message);
    }

    public void ReportProgress(string operationId, double percent)
    {
        Console.WriteLine($"[{operationId}] {percent:F1}%");
    }
}

/// <summary>
/// Interface for product entry points.
/// Per INVARIANT_THEORY.md §11.5: Agent-executable composition.
/// </summary>
public interface IProductEntryPoint
{
    string Name { get; }
    Task<int> RunAsync(string[] args, string bootJsonPath, string pluginsDir, string crashLogPath);
}
