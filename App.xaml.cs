using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace VantuzLauncher;

/// <summary>
/// Interaction logic for App.xaml
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

        // Проверяем headless-режим по аргументам
        if (TryParseHeadlessArgs(e.Args, out var headlessOptions))
        {
            _isHeadless = true;
            RunHeadlessMode(headlessOptions);
            return;
        }

        // Обычный графический режим
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
        new MainWindow().Show();
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
            TestMode = dict.ContainsKey("test-mode") || dict.ContainsKey("test")  // Phase 2: Nomadic testing
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
        });
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

    private static string CalculateStringMD5(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = sha256.ComputeHash(inputBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
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
