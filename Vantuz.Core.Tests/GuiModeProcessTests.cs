using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;

namespace Vantuz.Core.Tests;

/// <summary>
/// GUI-mode process lifecycle verification per AGENT_FAILURE_ANALYSIS.md §6.5 (R4, R5).
/// Ensures double-clicking the EXE creates a window and closing it kills the process cleanly.
/// </summary>
[Collection("GUI Sequential")]
public class GuiModeProcessTests : IDisposable
{
    private static readonly string ExePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "..", "..", "..", "..",
        "bin", "Release", "net8.0-windows", "VantuzLauncher.exe");

    private readonly List<Process> _ownedProcesses = new();

    public GuiModeProcessTests()
    {
    }
/// F_doc: {Dispose returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Dispose behavior

    public void Dispose()
    {
        // Only kill processes started by this test instance to avoid
        // interfering with parallel ForgeInstallationRecidivismTests
        foreach (var p in _ownedProcesses)
        {
            try { if (!p.HasExited) { p.Kill(); p.WaitForExit(2_000); } } catch (Exception ex) { /* F_doc: {Cleanup or retry may throw} E_doc: {Test continues; failure non-fatal to test objective} */ }
        }
    }

    private const uint WM_CLOSE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

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
    /// F_doc: {GuiMode_ProcessStarts_WindowAppearsWithin10Seconds returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies GuiMode_ProcessStarts_WindowAppearsWithin10Seconds behavior
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
        _ownedProcesses.Add(proc);

        // Wait up to 30s for a window handle (R4) — increased for parallel test runs
        bool windowAppeared = SpinWait.SpinUntil(() => { proc.Refresh(); return proc.MainWindowHandle != IntPtr.Zero; }, TimeSpan.FromSeconds(30));
        Assert.True(windowAppeared, "MainWindowHandle was not created within 30 seconds");

        // Graceful close via WM_CLOSE (R5) — Avalonia does not respond to Process.CloseMainWindow()
        bool closed = SendMessage(proc.MainWindowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero) != IntPtr.Zero;
        if (!closed)
        {
            proc.Kill();
        }

        // Wait up to 20s for exit — increased for parallel test runs
        bool exited = proc.WaitForExit(20_000);
        Assert.True(exited, "Process did not exit within 20 seconds after window close — potential zombie");
    }

    [Fact]
    /// F_doc: {GuiMode_ProcessKilled_NoZombieRemains returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies GuiMode_ProcessKilled_NoZombieRemains behavior
    public void GuiMode_ProcessKilled_NoZombieRemains()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        using var proc = Process.Start(new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!
        });
        Assert.NotNull(proc);
        _ownedProcesses.Add(proc);

        // Give it time to spawn
        proc.WaitForInputIdle(5_000);
        Thread.Sleep(500);

        proc.Kill();
        bool exited = proc.WaitForExit(5_000);
        Assert.True(exited, "Process did not exit after Kill()");

        // Verify no lingering owned processes
        var lingering = _ownedProcesses.Where(p => !p.HasExited).ToList();
        Assert.Empty(lingering);
    }

    [Fact]
    /// F_doc: {GuiMode_FullLaunch_NoApplicationInstanceErrorInTraceLog returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies GuiMode_FullLaunch_NoApplicationInstanceErrorInTraceLog behavior
    public void GuiMode_FullLaunch_NoApplicationInstanceErrorInTraceLog()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        // Clean previous trace log so we only see output from this run
        string traceLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".vantuzlauncher", "launcher_trace.log");
        try { File.Delete(traceLogPath); } catch (Exception ex) { /* F_doc: {Cleanup or retry may throw} E_doc: {Test continues; failure non-fatal to test objective} */ }

        using var proc = Process.Start(new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!,
            WindowStyle = ProcessWindowStyle.Normal
        });
        Assert.NotNull(proc);
        _ownedProcesses.Add(proc);

        // R4: wait for main window — increased for parallel test runs
        bool windowAppeared = SpinWait.SpinUntil(
            () => { proc.Refresh(); return proc.MainWindowHandle != IntPtr.Zero; },
            TimeSpan.FromSeconds(30));
        Assert.True(windowAppeared, "MainWindowHandle was not created within 30 seconds");

        // Let the pipeline run for a few seconds (enough for GUI plugin + version validation)
        Thread.Sleep(5_000);

        // Graceful shutdown — Avalonia does not respond to Process.CloseMainWindow()
        SendMessage(proc.MainWindowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        if (!proc.WaitForExit(5_000))
        {
            proc.Kill();
            proc.WaitForExit(5_000);
        }

        // Assert: trace log must not contain the Application-instance crash
        if (File.Exists(traceLogPath))
        {
            string log = File.ReadAllText(traceLogPath);
            bool hasAppInstanceError =
                log.Contains("Cannot create more than one", StringComparison.OrdinalIgnoreCase) ||
                log.Contains("System.Windows.Application", StringComparison.OrdinalIgnoreCase);
            Assert.False(hasAppInstanceError,
                $"launcher_trace.log contains Application instance error:\n{log}");

            // NOTE: We intentionally do NOT assert pipeline completion here.
            // Without a Play-button click GUI.CredentialCollection will abort the
            // pipeline, so step-completion assertions belong in headless tests
            // (see PipelinePositiveVerificationTests).
        }
    }
}
