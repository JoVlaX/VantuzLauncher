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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            MessageBox.Show($"Критическая ошибка при запуске:\n{ex.Message}\n{ex.StackTrace}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        InitializeSingleInstanceLock();
    }

    private void InitializeSingleInstanceLock()
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "VantuzLauncher.exe";
            string mutexName = $"Local\\{CalculateMD5(exePath)}";

            _instanceMutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                MessageBox.Show("Лаунчер уже запущен.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Возможный конфликт UAC или прав доступа, игнорируем или логируем
        }
        catch (Exception)
        {
            // Глобальный фолбэк
        }
    }

    private static string CalculateMD5(string input)
    {
        using var md5 = MD5.Create();
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = md5.ComputeHash(inputBytes);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
