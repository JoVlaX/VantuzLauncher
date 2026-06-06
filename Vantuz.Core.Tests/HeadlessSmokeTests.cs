using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Vantuz.Core.Tests;

public class HeadlessSmokeTests
{
    [Fact]
    public void HeadlessSmokeTest_ExitsWithoutCriticalError()
    {
        // Per INVARIANT_THEORY.md §1.2 Measurability: runtime behavior must be empirically testable.
        // E_doc: Process exit code MUST NOT be 2 (critical unhandled exception).
        // F_doc: Exit code == 2 indicates crash in App.xaml.cs UnhandledException handler.
        // Search from solution root for the built executable
        var searchDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "bin", "Debug", "net8.0-windows")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "bin", "Debug", "net8.0-windows")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "bin", "Debug", "net8.0-windows")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "bin", "Release", "net8.0-windows")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "bin", "Release", "net8.0-windows")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "bin", "Release", "net8.0-windows")),
        };
        var exePath = searchDirs.Select(d => Path.Combine(d, "VantuzLauncher.exe")).FirstOrDefault(File.Exists);
        Assert.True(exePath != null, $"VantuzLauncher.exe not found. Searched: {string.Join(", ", searchDirs)}");

        var psi = new ProcessStartInfo(exePath, "--headless --test-mode --boot=boot.test.json")
        {
            WorkingDirectory = Path.GetDirectoryName(exePath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var proc = Process.Start(psi);
        Assert.NotNull(proc);
        proc.WaitForExit(30000);
        Assert.True(proc.HasExited, "Process did not exit within 30 seconds");

        // Exit code 2 = critical unhandled exception per App.xaml.cs
        // Exit code 0 or 1 = normal completion (success or test failure)
        Assert.NotEqual(2, proc.ExitCode);
    }

    [Fact]
    public void BootJson_ParsesWithoutNullReference()
    {
        // E_doc: boot.json loads and deserializes without throwing NullReferenceException.
        // F_doc: JsonException or NullReferenceException during deserialization.
        var searchDirs = new[]
        {
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..")),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..")),
        };
        var bootPath = searchDirs.Select(d => Path.Combine(d, "boot.test.json")).FirstOrDefault(File.Exists);
        Assert.True(bootPath != null, $"boot.test.json not found");

        var json = File.ReadAllText(bootPath);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(JsonValueKind.Object, doc.ValueKind);
        Assert.True(doc.TryGetProperty("plugins", out var plugins));
        Assert.True(doc.TryGetProperty("pipeline", out var pipeline));
    }
}
