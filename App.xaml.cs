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

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            MessageBox.Show($"Критическая ошибка:\n{ex.Message}\n{ex.StackTrace}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        if (!InitializeSingleInstanceLock())
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        new MainWindow().Show();
    }

    private bool InitializeSingleInstanceLock()
    {
        string basePath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/').ToLowerInvariant();
        string mutexName = $"Local\\VantuzLauncher_{CalculateStringMD5(basePath)}";

        try
        {
            _instanceMutex = new Mutex(true, mutexName, out _ownsMutex);

            if (!_ownsMutex)
            {
                MessageBox.Show("Лаунчер уже запущен.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Лаунчер уже запущен с другими правами.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка блокировки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        return true;
    }

    private static string CalculateStringMD5(string input)
    {
        using var md5 = MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);
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
