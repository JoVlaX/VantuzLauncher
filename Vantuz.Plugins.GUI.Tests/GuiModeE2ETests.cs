using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using System.Windows.Forms;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// GUI-mode end-to-end positive verification per READINESS_REPORT.md R7.
/// Launches VantuzLauncher.exe from a temporary directory with a test manifest,
/// enters credentials in both root and plugin windows via UI Automation,
/// clicks Play in both windows, and asserts all pipeline step completion markers
/// are present in launcher_trace.log.
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
            "bin", "Release", "net8.0-windows", "VantuzLauncher.exe");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            path = path.Replace("Release", "Debug");
        return path;
    }

    private static AutomationElement? FindWindowByName(string name, int expectedProcessId, int timeoutMs = 15_000)
    {
        AutomationElement? result = null;
        SpinWait.SpinUntil(() =>
        {
            var candidates = AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, name));
            for (int i = 0; i < candidates.Count; i++)
            {
                try
                {
                    var candidate = candidates[i];
                    if (candidate.Current.ProcessId == expectedProcessId)
                    {
                        result = candidate;
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }, TimeSpan.FromMilliseconds(timeoutMs));
        return result;
    }

    private static AutomationElement? FindDescendant(AutomationElement parent, string automationId, int timeoutMs = 15_000)
    {
        AutomationElement? result = null;
        SpinWait.SpinUntil(() =>
        {
            try
            {
                result = parent.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                    .Cast<AutomationElement>()
                    .FirstOrDefault(e =>
                    {
                        try { return e.Current.AutomationId == automationId; }
                        catch { return false; }
                    });
            }
            catch { }
            return result != null;
        }, TimeSpan.FromMilliseconds(timeoutMs));
        return result;
    }

    private static void BringToFront(AutomationElement window)
    {
        if (window.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern))
        {
            ((WindowPattern)pattern).SetWindowVisualState(WindowVisualState.Normal);
        }
    }

    [StaFact]
    public void FullGuiPipeline_ClickPlayInBothWindows_AllStepsCompleted()
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
            foreach (var file in Directory.GetFiles(Path.Combine(exeDir, "plugins")))
            {
                File.Copy(file, Path.Combine(pluginsDir, Path.GetFileName(file)), true);
            }

            // 2. Copy test manifest as boot.gui.json and create .portable marker
            string testManifest = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
                "boot.gui.test.json");
            testManifest = Path.GetFullPath(testManifest);
            Assert.True(File.Exists(testManifest), $"boot.gui.test.json not found at {testManifest}");
            File.Copy(testManifest, Path.Combine(tempDir, "boot.gui.json"), true);
            File.WriteAllText(Path.Combine(tempDir, ".portable"), "");

            // 3. Launch process
            var proc = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(tempDir, "VantuzLauncher.exe"),
                WorkingDirectory = tempDir,
                WindowStyle = ProcessWindowStyle.Normal
            });
            Assert.NotNull(proc);
            _processes.Add(proc);

            // 4. Wait for root window
            bool windowAppeared = SpinWait.SpinUntil(() =>
            {
                try { return proc.MainWindowHandle != IntPtr.Zero; }
                catch { return false; }
            }, TimeSpan.FromSeconds(15));
            Assert.False(proc.HasExited,
                $"VantuzLauncher.exe exited prematurely. " +
                $"TempDir={tempDir}, Workspace={AppDomain.CurrentDomain.BaseDirectory}");
            Assert.True(windowAppeared,
                $"MainWindowHandle was not created within 15 seconds. Process alive={!proc.HasExited}");

            var rootWindow = AutomationElement.FromHandle(proc.MainWindowHandle);
            Assert.NotNull(rootWindow);
            Thread.Sleep(5_000); // Allow WPF layout and UI Automation registration to complete

            // 5. Enter credentials and click Play in root window
            var usernameBox = FindDescendant(rootWindow, "RootUsernameBox");
            var passwordBox = FindDescendant(rootWindow, "RootPasswordBox");
            var playBtn = FindDescendant(rootWindow, "RootBtnPlay");

            if (usernameBox == null || passwordBox == null || playBtn == null)
            {
                var all = rootWindow.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                    .Cast<AutomationElement>();
                var desc = string.Join("\n", all.Select(e =>
                {
                    try { return $"{e.Current.ClassName} | AutomationId='{e.Current.AutomationId}' | Name='{e.Current.Name}'"; }
                    catch { return "(exception reading properties)"; }
                }));
                Assert.Fail($"Failed to locate root window controls.\nUI Automation tree:\n{desc}");
            }

            BringToFront(rootWindow);
            usernameBox.SetFocus();
            SendKeys.SendWait("^a");
            SendKeys.SendWait("test_user");

            passwordBox.SetFocus();
            SendKeys.SendWait("^a");
            SendKeys.SendWait("test_password");

            BringToFront(rootWindow);
            playBtn.SetFocus();
            var invokePattern = (InvokePattern)playBtn.GetCurrentPattern(InvokePattern.Pattern);
            invokePattern.Invoke();

            // 6. Wait for plugin window
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

            // 7. Enter credentials and click Play in plugin window
            var pluginUserBox = FindDescendant(pluginWindow, "UsernameBox");
            var pluginPassBox = FindDescendant(pluginWindow, "PasswordBox");
            var pluginPlayBtn = FindDescendant(pluginWindow, "PlayButton");

            Assert.NotNull(pluginUserBox);
            Assert.NotNull(pluginPassBox);
            Assert.NotNull(pluginPlayBtn);

            BringToFront(pluginWindow);
            pluginUserBox.SetFocus();
            SendKeys.SendWait("^a");
            SendKeys.SendWait("test_user");

            pluginPassBox.SetFocus();
            SendKeys.SendWait("^a");
            SendKeys.SendWait("test_password");

            BringToFront(pluginWindow);
            pluginPlayBtn.SetFocus();
            var pluginInvoke = (InvokePattern)pluginPlayBtn.GetCurrentPattern(InvokePattern.Pattern);
            pluginInvoke.Invoke();

            Thread.Sleep(2_000); // Allow BtnPlay_Click to initialize engine and create trace log

            // 8. Wait for pipeline completion via trace log
            string? traceLog = null;
            bool completed = SpinWait.SpinUntil(() =>
            {
                if (File.Exists(traceLogPath))
                {
                    try
                    {
                        using var fs = new FileStream(traceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs);
                        traceLog = sr.ReadToEnd();
                        return traceLog.Contains("[STEP] GUI.MinecraftLauncher completed") &&
                               traceLog.Contains("[STEP] GUI.CredentialCollection completed") &&
                               traceLog.Contains("[STEP] Auth.TestAuthCommand completed") &&
                               traceLog.Contains("[STEP] Game.VersionValidatorQuery completed") &&
                               traceLog.Contains("[STEP] Game.LaunchCommand completed");
                    }
                    catch { }
                }
                return false;
            }, TimeSpan.FromSeconds(45));

            Assert.True(completed,
                $"Pipeline did not complete within 45 seconds. " +
                $"Last trace log content: {(traceLog != null ? traceLog.Substring(Math.Max(0, traceLog.Length - 2000)) : "(not found)")}");

            // 9. Assert no Application instance error
            if (traceLog != null)
            {
                bool hasAppInstanceError =
                    traceLog.Contains("Cannot create more than one", StringComparison.OrdinalIgnoreCase) ||
                    traceLog.Contains("System.Windows.Application", StringComparison.OrdinalIgnoreCase);
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
