using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;
using Vantuz.Host;

namespace VantuzLauncher;

class Program
{
    /// F_doc: {WorkspacePath returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies WorkspacePath behavior
    public static string WorkspacePath { get; private set; } = string.Empty;

    // Win32 MessageBox for WinExe error surfacing вЂ” console is invisible
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
    private const uint MB_OK = 0x0;
    private const uint MB_ICONERROR = 0x10;

    [STAThread]
    static async Task Main(string[] args)
    {
        WorkspacePath = DetermineWorkspace();

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = (Exception)e.ExceptionObject;
            string msg = $"CRITICAL: {ex.Message}\n{ex.StackTrace}";
            Console.Error.WriteLine(msg);
            MessageBoxW(0, msg, "Vantuz Launcher вЂ” Critical Error", MB_OK | MB_ICONERROR);
            Environment.Exit(2);
        };

        if (TryParseHeadlessArgs(args, out var headlessOptions))
        {
            await RunHeadlessAsync(headlessOptions);
            return;
        }

        await RunGuiModeAsync();
    }

    static async Task RunGuiModeAsync()
    {
        try
        {
            string testFile = Path.Combine(WorkspacePath, ".access_test");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
        }
        catch (UnauthorizedAccessException)
        {
            string msg = $"РћС€РёР±РєР° РґРѕСЃС‚СѓРїР°! РќРµС‚ РїСЂР°РІ РЅР° Р·Р°РїРёСЃСЊ РІ СЂР°Р±РѕС‡СѓСЋ РїР°РїРєСѓ:\n{WorkspacePath}\nР—Р°РїСѓСЃС‚РёС‚Рµ РѕС‚ РёРјРµРЅРё РђРґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂР° РёР»Рё СѓРґР°Р»РёС‚Рµ С„Р°Р№Р» .portable.";
            Console.Error.WriteLine(msg);
            MessageBoxW(0, msg, "Vantuz Launcher вЂ” РћС€РёР±РєР° РґРѕСЃС‚СѓРїР°", MB_OK | MB_ICONERROR);
            Environment.Exit(2);
        }

        string bootJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot.gui.json");
        string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        string crashLogPath = Path.Combine(WorkspacePath, "crash.log");

        if (!File.Exists(bootJsonPath))
        {
            string msg = $"boot.gui.json not found at {bootJsonPath}";
            Console.Error.WriteLine(msg);
            MessageBoxW(0, msg, "Vantuz Launcher вЂ” Missing Config", MB_OK | MB_ICONERROR);
            Environment.Exit(2);
        }

        var reporter = new ConsoleReporter();
        var engine = new VantuzEngine(pluginsDir, reporter, crashLogPath);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

        var result = await engine.RunAsync(bootJsonPath, cts.Token);

        if (!result.Success)
        {
            string error = result.ErrorMessage ?? "Unknown error";
            string detailedMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Pipeline failed: {error}";
            Console.Error.WriteLine(detailedMsg);

            // Persist to crash log for diagnostics first
            try
            {
                File.AppendAllText(crashLogPath, detailedMsg + "\n");
            }
            catch { }

            // Surface to user вЂ” WinExe hides console, so use Win32 MessageBox
            // Keep message user-friendly and actionable
            string userFriendly = $"РћС€РёР±РєР° Р·Р°РїСѓСЃРєР°:\n{error}\n\nРџРѕРґСЂРѕР±РЅРѕСЃС‚Рё Р·Р°РїРёСЃР°РЅС‹ РІ:\n{crashLogPath}";
            MessageBoxW(0, userFriendly, "Vantuz Launcher вЂ” РћС€РёР±РєР°", MB_OK | MB_ICONERROR);

            // Give the dialog a moment to render before the process exits
            await Task.Delay(100);
            Environment.Exit(1);
        }

        if (result.Payload != null &&
            result.Payload.TryGetValue("UpdateReady", out var updateReadyObj) &&
            updateReadyObj is bool updateReady && updateReady)
        {
            string hostExe = result.Payload.TryGetValue("hostExecutable", out var hostExeObj) && hostExeObj is string he ? he : "VantuzLauncher.exe";
            string updateScript = result.Payload.TryGetValue("UpdateScript", out var scriptObj) && scriptObj is string s ? s : null;
            if (!string.IsNullOrEmpty(updateScript))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = updateScript,
                    Arguments = $"\"{hostExe}\"",
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                });
                Environment.Exit(0);
            }
        }

        Console.WriteLine("Pipeline completed successfully.");
        // Let the GUI lifetime control process exit (ShutdownMode.OnMainWindowClose).
    }

    static async Task RunHeadlessAsync(HeadlessRunner.HeadlessOptions options)
    {
        options = options with { WorkspacePath = WorkspacePath };
        var result = await HeadlessRunner.RunAsync(options);

        string outputPath = Path.Combine(WorkspacePath, "test-result.json");
        HeadlessRunner.SaveResult(result, outputPath);

        Console.WriteLine($"\n=== TEST RESULT ===");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2}s");
        if (!string.IsNullOrEmpty(result.ErrorMessage))
            Console.WriteLine($"Error: {result.ErrorMessage}");
        Console.WriteLine($"Output: {outputPath}");

        Environment.Exit(result.Success ? 0 : 1);
    }

    static string DetermineWorkspace()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        if (File.Exists(Path.Combine(baseDir, ".portable"))) return baseDir;

        string productId = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name?.ToLowerInvariant() ?? "vantuzlauncher";
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "." + productId);
        Directory.CreateDirectory(appData);
        return appData;
    }

    static bool TryParseHeadlessArgs(string[] args, out HeadlessRunner.HeadlessOptions options)
    {
        options = new HeadlessRunner.HeadlessOptions();

        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var arg in args)
        {
            if (arg.StartsWith("--"))
            {
                var parts = arg.Substring(2).Split('=', 2);
                if (parts.Length == 2)
                    dict[parts[0]] = parts[1];
                else if (parts.Length == 1)
                    dict[parts[0]] = "true";
            }
            else if (arg.StartsWith("-"))
            {
                var key = arg.Substring(1);
                dict[key] = "true";
            }
        }

        if (!dict.ContainsKey("headless"))
            return false;

        options = new HeadlessRunner.HeadlessOptions
        {
            Username = dict.GetValueOrDefault("username", "test")!,
            Password = dict.GetValueOrDefault("password", "test")!,
            RamMb = int.TryParse(dict.GetValueOrDefault("ram", "4096"), out var ram) ? ram : 4096,
            TestMode = dict.ContainsKey("test-mode") || dict.ContainsKey("test"),
            BootPath = dict.GetValueOrDefault("boot-path", null) ?? dict.GetValueOrDefault("boot", null),
        };

        return true;
    }
}

class ConsoleReporter : IStatusReporter
{
    private readonly string? _logPath;

    public ConsoleReporter()
    {
        try
        {
            _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_trace.log");
        }
        catch { /* Silent fail - logging to console is sufficient */ }
    }
/// F_doc: {ReportProgress returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportProgress behavior

    public void ReportProgress(string taskName, double percentage)
    {
        string line = $"[{DateTime.UtcNow:HH:mm:ss}] {taskName}: {percentage:F1}%";
        Console.WriteLine(line);
        AppendToLog(line);
        ReportHub.ReportProgress(taskName, percentage);
    }
/// F_doc: {ReportState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportState behavior

    public void ReportState(string message)
    {
        string line = $"[{DateTime.UtcNow:HH:mm:ss}] {message}";
        Console.WriteLine(line);
        AppendToLog(line);
        ReportHub.ReportState(message);
    }

    private void AppendToLog(string line)
    {
        if (_logPath == null) return;
        try
        {
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch { /* Silent fail */ }
    }
}
