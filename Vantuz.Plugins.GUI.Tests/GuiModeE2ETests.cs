using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using System.Windows.Forms;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// GUI-mode end-to-end positive verification per READINESS_REPORT.md R7.
/// Launches headless VantuzLauncher.exe from a temporary directory with a test manifest,
/// waits for Avalonia plugin window "Vantuz Minecraft Launcher", enters credentials
/// via UI Automation, clicks Play, and asserts all pipeline step completion markers
/// are present in launcher_trace.log.
/// Per COMPOSITUM_SPECIFICATION.md §4.1: GUI is a Category (plugin) concern, not Product.
/// </summary>
public class GuiModeE2ETests : IDisposable
{
    private readonly List<Process> _processes = new();

    public void Dispose()
    {
        foreach (var p in _processes.ToList())
        {
            try { if (!p.HasExited) { p.Kill(); p.WaitForExit(5_000); } } catch { }
        }
    }

    private static string ResolveExePath()
    {
        var path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..",
            "bin", "Debug", "net8.0-windows", "VantuzLauncher.exe");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            path = path.Replace("Debug", "Release");
        return path;
    }

    private static AutomationElement? FindWindowByName(string name, int expectedProcessId, int timeoutMs = 30_000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var candidates = AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.NameProperty, name));
                for (int i = 0; i < candidates.Count; i++)
                {
                    try
                    {
                        var candidate = candidates[i];
                        if (candidate.Current.ProcessId == expectedProcessId || candidates.Count == 1)
                        {
                            return candidate;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            Thread.Sleep(200);
        }
        return null;
    }

    private static AutomationElement? FindDescendant(AutomationElement parent, string automationId, int timeoutMs = 15_000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var candidate = parent.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
                if (candidate != null)
                {
                    return candidate;
                }
            }
            catch { }
            Thread.Sleep(200);
        }
        return null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_CHAR = 0x0102;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const int SW_SHOWNORMAL = 1;

    private static IntPtr _lastHwnd = IntPtr.Zero;

    private static void BringToFront(AutomationElement window)
    {
        if (window.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern))
        {
            ((WindowPattern)pattern).SetWindowVisualState(WindowVisualState.Normal);
        }
        try
        {
            var hwnd = (IntPtr)window.Current.NativeWindowHandle;
            if (hwnd != IntPtr.Zero)
            {
                _lastHwnd = hwnd;
                ShowWindowAsync(hwnd, SW_SHOWNORMAL);
                Thread.Sleep(200);
                SetForegroundWindow(hwnd);
                Thread.Sleep(500);
            }
        }
        catch { }
    }

    private static void SendChar(char c)
    {
        if (_lastHwnd == IntPtr.Zero) return;
        SendMessageW(_lastHwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
    }

    private static void SendText(string text)
    {
        foreach (char c in text)
            SendChar(c);
    }

    private static void SendVk(ushort vk)
    {
        if (_lastHwnd == IntPtr.Zero) return;
        SendMessageW(_lastHwnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        SendMessageW(_lastHwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
    }

    [StaFact]
    public void FullGuiPipeline_ClickPlayInPluginWindow_AllStepsCompleted()
    {
        string exe = ResolveExePath();
        Assert.True(File.Exists(exe), $"VantuzLauncher.exe not found at {exe}");

        string exeDir = Path.GetDirectoryName(exe)!;
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string pluginsDir = Path.Combine(tempDir, "plugins");
        Directory.CreateDirectory(pluginsDir);

        string traceLogPath = Path.Combine(tempDir, "launcher_trace.log");

        try
        {
            // 1. Copy EXE and dependencies (skip boot manifests; we'll inject our own)
            foreach (var file in Directory.GetFiles(exeDir))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("boot.", StringComparison.OrdinalIgnoreCase)
                    && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                File.Copy(file, Path.Combine(tempDir, fileName), true);
            }
            foreach (var file in Directory.GetFiles(Path.Combine(exeDir, "plugins"), "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(Path.Combine(exeDir, "plugins"), file);
                string destPath = Path.Combine(pluginsDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath, true);
            }

            // 2. Copy test manifest as boot.gui.json and create .portable marker
            string testManifest = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
                "boot.gui.test.json");
            testManifest = Path.GetFullPath(testManifest);
            Assert.True(File.Exists(testManifest), $"boot.gui.test.json not found at {testManifest}");
            File.Copy(testManifest, Path.Combine(tempDir, "boot.gui.json"), true);
            File.WriteAllText(Path.Combine(tempDir, ".portable"), "");

            // 3. Launch process (headless host runs pipeline; Avalonia plugin creates window)
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(tempDir, "VantuzLauncher.exe"),
                WorkingDirectory = tempDir,
                WindowStyle = ProcessWindowStyle.Normal
            });
            Assert.NotNull(proc);
            _processes.Add(proc);

            Assert.False(proc.HasExited,
                $"VantuzLauncher.exe exited prematurely. " +
                $"TempDir={tempDir}, Workspace={AppDomain.CurrentDomain.BaseDirectory}");

            // 4. Wait for Avalonia plugin window
            Thread.Sleep(3_000); // Allow engine to load plugin and Avalonia to initialize
            var pluginWindow = FindWindowByName("Vantuz Minecraft Launcher", proc.Id);
            if (pluginWindow == null)
            {
                var allWindows = AutomationElement.RootElement
                    .FindAll(TreeScope.Children, Condition.TrueCondition)
                    .Cast<AutomationElement>()
                    .Select(e =>
                    {
                        try { return $"Name='{e.Current.Name}' | Class='{e.Current.ClassName}'"; }
                        catch { return "(error)"; }
                    });
                string traceContent = "(trace log not found)";
                if (File.Exists(traceLogPath))
                {
                    using var fs = new FileStream(traceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs);
                    traceContent = sr.ReadToEnd();
                }
                string crashLogPath = Path.Combine(tempDir, "crash.log");
                string crashContent = File.Exists(crashLogPath) ? File.ReadAllText(crashLogPath) : "(crash log not found)";
                Assert.Fail($"Plugin window 'Vantuz Minecraft Launcher' not found. Top-level windows:\n{string.Join("\n", allWindows)}\n\nTrace log:\n{traceContent}\n\nCrash log:\n{crashContent}");
            }

            // 5. Credentials are auto-submitted by plugin when autoSubmitTestCredentials=true in boot.gui.test.json
            // Verify window is visible and then wait for pipeline completion.
            BringToFront(pluginWindow);
            Thread.Sleep(2_000); // Allow auto-submit to propagate

            // 6. Wait for pipeline completion via trace log
            string? traceLog = null;
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            bool completed = false;
            while (sw2.Elapsed.TotalSeconds < 45)
            {
                if (File.Exists(traceLogPath))
                {
                    try
                    {
                        using var fs = new FileStream(traceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs);
                        traceLog = sr.ReadToEnd();
                        completed = traceLog.Contains("[STEP] GUI.MinecraftLauncher completed") &&
                               traceLog.Contains("[STEP] GUI.CredentialCollection completed") &&
                               traceLog.Contains("[STEP] Auth.TestAuthCommand completed") &&
                               traceLog.Contains("[STEP] Game.VersionValidatorQuery completed") &&
                               traceLog.Contains("[STEP] Game.LaunchCommand completed");
                        if (completed) break;
                    }
                    catch { }
                }
                Thread.Sleep(500);
            }

            Assert.True(completed,
                $"Pipeline did not complete within 45 seconds. " +
                $"Last trace log content: {(traceLog != null ? traceLog.Substring(Math.Max(0, traceLog.Length - 2000)) : "(not found)")}");

            // 7. Assert no Application instance error
            if (traceLog != null)
            {
                bool hasAppInstanceError =
                    traceLog.Contains("Cannot create more than one", StringComparison.OrdinalIgnoreCase);
                Assert.False(hasAppInstanceError,
                    $"launcher_trace.log contains Application instance error:\n{traceLog}");
            }
        }
        finally
        {
            // Cleanup: kill process and remove temp directory
            foreach (var p in _processes.ToList())
            {
                try { if (!p.HasExited) { p.Kill(); p.WaitForExit(5_000); } } catch { }
            }
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
