using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;

namespace VantuzLauncher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private Mutex? _instanceMutex;
    private static bool _ownsMutex;

    public static string WorkspacePath { get; private set; }

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
            // логика логгирования
            MessageBox.Show($"Критическая ошибка:\n{ex.Message}\n{ex.StackTrace}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        WorkspacePath = DetermineWorkspace();

        // Проверка прав доступа до создания окон
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
