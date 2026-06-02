using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using Vantuz.Core;
using Vantuz.Host;

namespace VantuzLauncher;

/// <summary>
/// Minimal application bootstrap.
/// Per COMPOSITUM_SPECIFICATION.md §2.2: Core is NOT Application.
/// GUI is loaded as plugin through VantuzEngine pipeline.
/// </summary>
public partial class App : Application
{
    private Mutex? _instanceMutex;
    private static bool _ownsMutex;

    public static string WorkspacePath { get; private set; } = string.Empty;
    private static bool _isHeadless = false;

    public static string DetermineWorkspace()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
        // Явный маркер портативности
        if (File.Exists(Path.Combine(baseDir, ".portable"))) return baseDir;

        // Стабильный идентификатор (не меняется при переименовании .exe)
        string productId = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name?.ToLowerInvariant() ?? "vantuzlauncher";
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "." + productId);
        Directory.CreateDirectory(appData);
        return appData;
    }

    private static string CalculateHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            if (_isHeadless)
            {
                Console.Error.WriteLine($"CRITICAL: {ex.Message}\n{ex.StackTrace}");
                Environment.Exit(2);
            }
            else
            {
                MessageBox.Show($"Критическая ошибка:\n{ex.Message}\n{ex.StackTrace}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        WorkspacePath = DetermineWorkspace();

        // Проверяем headless-режим по аргументам (per INVARIANT_THEORY.md §1.2 Measurability)
        if (TryParseHeadlessArgs(e.Args, out var headlessOptions))
        {
            _isHeadless = true;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RunHeadlessMode(headlessOptions);
            Shutdown();
            return;
        }

        // GUI Mode: Launch through VantuzEngine (GUI is a plugin, not Core)
        try
        {
            string testFile = Path.Combine(WorkspacePath, ".access_test");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show($"Ошибка доступа! Нет прав на запись в рабочую папку:\n{WorkspacePath}\nЗапустите от имени Администратора или удалите файл .portable.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(); return;
        }

        // Блокируем саму рабочую директорию, а не exe-файл
        if (!InitializeSingleInstanceLock(WorkspacePath)) { Shutdown(); return; }

        base.OnStartup(e);
        RunGuiMode();
    }

    /// <summary>
    /// GUI mode: Execute through VantuzEngine with GUI plugin pipeline.
    /// Per COMPOSITUM_SPECIFICATION.md §4.1: GUI is a plugin category capability.
    /// </summary>
    private async void RunGuiMode()
    {
        string bootJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot.gui.json");
        string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        string crashLogPath = Path.Combine(WorkspacePath, "crash.log");

        // Fallback to standard boot.json if GUI-specific doesn't exist
        if (!File.Exists(bootJsonPath))
        {
            bootJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot.json");
        }

        if (!File.Exists(bootJsonPath))
        {
            MessageBox.Show("Configuration file not found (boot.json or boot.gui.json).", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Minimal console reporter for bootstrap phase
        var reporter = new SimpleConsoleReporter();
        var engine = new VantuzEngine(pluginsDir, reporter, crashLogPath);

        try
        {
            using var cts = new CancellationTokenSource();
            var initialPayload = new Dictionary<string, object>
            {
                ["workspace_path"] = WorkspacePath,
                ["host_executable"] = System.Diagnostics.Process.GetCurrentProcess().ProcessName
            };

            var result = await engine.RunAsync(bootJsonPath, cts.Token, initialPayload);

            if (!result.Success)
            {
                MessageBox.Show($"Pipeline execution failed:\n{result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Critical error:\n{ex.Message}\n\nDetails in crash.log", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            File.AppendAllText(crashLogPath, $"[{DateTime.Now}] GUI Mode Error: {ex}\n");
        }
        finally
        {
            Shutdown();
        }
    }

    private static bool TryParseHeadlessArgs(string[] args, out HeadlessRunner.HeadlessOptions options)
    {
        options = new HeadlessRunner.HeadlessOptions();

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        // Headless режим активируется только при явном флаге --headless
        if (!dict.ContainsKey("headless"))
            return false;

        options = new HeadlessRunner.HeadlessOptions
        {
            Username = dict.GetValueOrDefault("username", "test"),
            Password = dict.GetValueOrDefault("password", "test"),
            RamMb = int.TryParse(dict.GetValueOrDefault("ram", "4096"), out var ram) ? ram : 4096,
            TestMode = dict.ContainsKey("test-mode") || dict.ContainsKey("test"),  // Phase 2: Nomadic testing
            BootPath = dict.GetValueOrDefault("boot", null)  // Per INVARIANT_THEORY.md §498 - explicit boot file
        };

        return true;
    }

    private void RunHeadlessMode(HeadlessRunner.HeadlessOptions options)
    {
        // Отключаем создание окон для headless-режима
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var result = await HeadlessRunner.RunAsync(options, cts.Token);

                // Сохраняем результат
                string outputPath = Path.Combine(WorkspacePath, "test-result.json");
                HeadlessRunner.SaveResult(result, outputPath);

                // Выводим в консоль
                Console.WriteLine($"\n=== TEST RESULT ===");
                Console.WriteLine($"Status: {result.Status}");
                Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F2}s");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    Console.WriteLine($"Error: {result.ErrorMessage}");
                Console.WriteLine($"Output: {outputPath}");

                // Exit code: 0 = success, 1 = failure
                Environment.Exit(result.Success ? 0 : 1);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Headless runner error: {ex.Message}");
                Environment.Exit(2);
            }
        }).Wait(); // Wait for headless execution to complete (per INVARIANT_THEORY.md §11.5)
    }

    private bool InitializeSingleInstanceLock(string targetWorkspace)
    {
        string mutexName = $"Local\\VantuzLauncher_{CalculateHash(targetWorkspace)}";
        try
        {
            _instanceMutex = new Mutex(true, mutexName, out _ownsMutex);
            if (!_ownsMutex) { MessageBox.Show("Лаунчер уже работает с этим профилем данных."); return false; }
        }
        catch (UnauthorizedAccessException) { return false; }
        catch { return false; }
        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex && _instanceMutex != null)
        {
            try { _instanceMutex.ReleaseMutex(); } catch { }
        }
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}

/// <summary>
/// Minimal console reporter for bootstrap phase.
/// Per INVARIANT_THEORY.md §1.2 Measurability: simple deterministic output.
/// </summary>
internal sealed class SimpleConsoleReporter : IStatusReporter
{
    public void ReportState(string message)
    {
        Console.WriteLine($"[BOOT] {message}");
    }

    public void ReportProgress(string operationId, double percent)
    {
        Console.WriteLine($"[BOOT] [{operationId}] {percent:F1}%");
    }
}
