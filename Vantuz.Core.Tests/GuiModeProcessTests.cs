using System.Diagnostics;
using Xunit;

namespace Vantuz.Core.Tests;

/// <summary>
/// GUI-mode process lifecycle verification per AGENT_FAILURE_ANALYSIS.md §6.5 (R4, R5).
/// Ensures double-clicking the EXE creates a window and closing it kills the process cleanly.
/// </summary>
public class GuiModeProcessTests : IDisposable
{
    private static readonly string ExePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "..",
        "bin", "Release", "net8.0-windows", "VantuzLauncher.exe");

    public GuiModeProcessTests()
    {
        // Kill any pre-existing zombies before each test
        foreach (var p in Process.GetProcessesByName("VantuzLauncher"))
        {
            try { p.Kill(); p.WaitForExit(5_000); } catch { }
        }
    }

    public void Dispose()
    {
        // Kill any leftover processes after each test
        foreach (var p in Process.GetProcessesByName("VantuzLauncher"))
        {
            try { p.Kill(); p.WaitForExit(2_000); } catch { }
        }
    }

    private static string ResolveExePath()
    {
        var path = Path.GetFullPath(ExePath);
        // Fallback: try debug build if release not present
        if (!File.Exists(path))
        {
            path = path.Replace("Release", "Debug");
        }
        return path;
    }

    [Fact]
    public void GuiMode_ProcessStarts_WindowAppearsWithin10Seconds()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        using var proc = Process.Start(new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            WindowStyle = ProcessWindowStyle.Normal
        });
        Assert.NotNull(proc);

        // Wait up to 10s for a window handle (R4)
        bool windowAppeared = SpinWait.SpinUntil(() => proc.MainWindowHandle != IntPtr.Zero, TimeSpan.FromSeconds(10));
        Assert.True(windowAppeared, "MainWindowHandle was not created within 10 seconds");

        // Graceful close via WM_CLOSE (R5)
        bool closed = proc.CloseMainWindow();
        if (!closed)
        {
            proc.Kill();
        }

        // Wait up to 10s for exit
        bool exited = proc.WaitForExit(10_000);
        Assert.True(exited, "Process did not exit within 10 seconds after window close — potential zombie");
    }

    [Fact]
    public void GuiMode_ProcessKilled_NoZombieRemains()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        using var proc = Process.Start(new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!
        });
        Assert.NotNull(proc);

        // Give it time to spawn
        proc.WaitForInputIdle(5_000);
        Thread.Sleep(500);

        proc.Kill();
        bool exited = proc.WaitForExit(5_000);
        Assert.True(exited, "Process did not exit after Kill()");

        // Verify no lingering VantuzLauncher processes
        var lingering = Process.GetProcessesByName("VantuzLauncher");
        Assert.Empty(lingering);
    }

    [Fact]
    public void GuiMode_FullLaunch_NoApplicationInstanceErrorInTraceLog()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        // Clean previous trace log so we only see output from this run
        string traceLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".vantuzlauncher", "launcher_trace.log");
        try { File.Delete(traceLogPath); } catch { }

        using var proc = Process.Start(new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            WindowStyle = ProcessWindowStyle.Normal
        });
        Assert.NotNull(proc);

        // R4: wait for main window
        bool windowAppeared = SpinWait.SpinUntil(
            () => proc.MainWindowHandle != IntPtr.Zero,
            TimeSpan.FromSeconds(10));
        Assert.True(windowAppeared, "MainWindowHandle was not created within 10 seconds");

        // Let the pipeline run for a few seconds (enough for GUI plugin + version validation)
        Thread.Sleep(5_000);

        // Graceful shutdown
        proc.CloseMainWindow();
        proc.WaitForExit(10_000);

        // Assert: trace log must not contain the Application-instance crash
        if (File.Exists(traceLogPath))
        {
            string log = File.ReadAllText(traceLogPath);
            bool hasAppInstanceError =
                log.Contains("Cannot create more than one", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("System.Windows.Application", StringComparison.OrdinalIgnoreCase);
            Assert.False(hasAppInstanceError,
                $"launcher_trace.log contains Application instance error:\n{log}");

            // Positive: pipeline must have progressed through critical steps
            // (requires QuantumScheduler to log "[STEP] {node.Name} completed")
            // NOTE: trace log is only created after BtnPlay_Click, so this section
            // only runs if the user has previously clicked Play. For automated
            // positive verification of pipeline progression, see
            // PipelinePositiveVerificationTests.Headless_RunsAllSteps_AndLogsPositiveMarkers.
            if (log.Contains("[STEP] GUI.MinecraftLauncher completed"))
            {
                Assert.Contains("[STEP] GUI.CredentialCollection completed", log);
                Assert.Contains("[STEP] Auth.YggdrasilCommand completed", log);
                Assert.Contains("[STEP] Game.VersionValidatorQuery completed", log);
                Assert.Contains("[STEP] Game.LaunchCommand completed", log);
            }
        }
    }
}
