#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.Net; // Добавлено для SecurityProtocol
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;

namespace VantuzLauncher
{
    public partial class MainWindow : Window
    {
        private readonly string _apiUrl = "https://troglobit.webhm.pro/api.php";
        private readonly string _packwizUrl = "https://raw.githubusercontent.com/JoVlaX/sigmaivan/main/pack.toml";
        private readonly string _packwizBootstrapUrl = "https://github.com/packwiz/packwiz-installer-bootstrap/releases/latest/download/packwiz-installer-bootstrap.jar";
        
        private readonly string _mcDir;
        
        // Настраиваем HttpClient с заголовками браузера и игнорированием ошибок SSL (на время тестов)
        private static readonly HttpClientHandler _handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };
        private static readonly HttpClient _httpClient = new HttpClient(_handler);

        public MainWindow()
        {
            InitializeComponent();
            
            // Форсируем использование современных протоколов безопасности для устранения EOF
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            
            // Притворяемся браузером, чтобы сервера загрузок не сбрасывали соединение
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }

            _mcDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".vantuz");
            if (!Directory.Exists(_mcDir)) Directory.CreateDirectory(_mcDir);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Пожалуйста, введите логин и пароль.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetUIState(true);
            UpdateStatus("Авторизация...");

            try
            {
                var authResponse = await AuthenticateUserAsync(username, password);
                
                if (authResponse == null || !string.Equals(authResponse.status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Неверный логин или пароль.", "Ошибка авторизации");
                    SetUIState(false);
                    return;
                }

                if (!authResponse.has_access)
                {
                    MessageBox.Show("Доступ закрыт. Оплатите активацию на сайте.", "Отказ в доступе", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetUIState(false);
                    return;
                }

                UpdateStatus("Проверка Packwiz...");
                await SyncModpackAsync();

                UpdateStatus("Инициализация Minecraft...");
                await LaunchGameAsync(authResponse.username ?? username);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка:\n{ex.Message}\n\nВнутренняя ошибка: {ex.InnerException?.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                SetUIState(false);
            }
        }

        private async Task<AuthResponse> AuthenticateUserAsync(string username, string password)
        {
            var payload = new { username = username, password = password };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();

            var responseText = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<AuthResponse>(responseText);
        }

        private async Task SyncModpackAsync()
        {
            string bootstrapPath = Path.Combine(_mcDir, "packwiz-installer-bootstrap.jar");

            if (!File.Exists(bootstrapPath))
            {
                UpdateStatus("Загрузка установщика модов...");
                var bytes = await _httpClient.GetByteArrayAsync(_packwizBootstrapUrl);
                await File.WriteAllBytesAsync(bootstrapPath, bytes);
            }

            UpdateStatus("Синхронизация модов (Packwiz)...");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-jar \"{bootstrapPath}\" \"{_packwizUrl}\"",
                WorkingDirectory = _mcDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processStartInfo))
            {
                if (process == null) throw new Exception("Не удалось запустить Java. Убедитесь, что она установлена.");
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Ошибка при загрузке модов: {error}");
                }
            }
        }

        private async Task LaunchGameAsync(string sessionUsername)
        {
            UpdateStatus("Подготовка библиотек...");

            var path = new MinecraftPath(_mcDir);
            var launcher = new MinecraftLauncher(path);

            launcher.FileProgressChanged += (sender, args) =>
            {
                Dispatcher.Invoke(() => {
                    UpdateStatus($"Загрузка: {args.Name}");
                    LauncherProgress.Maximum = args.TotalTasks;
                    LauncherProgress.Value = args.ProgressedTasks;
                });
            };

            UpdateStatus("Установка Forge 47.4.10...");
            
            var forge = new ForgeInstaller(launcher);
            // Пытаемся установить Forge. Библиотека сама должна обработать редиректы.
            string targetVersion = await forge.Install("1.20.1", "47.4.10");

            var launchOption = new MLaunchOption
            {
                Session = MSession.CreateOfflineSession(sessionUsername),
                MaximumRamMb = 4096
            };

            UpdateStatus("Запуск процесса игры...");
            var process = await launcher.InstallAndBuildProcessAsync(targetVersion, launchOption);
            process.Start();

            Application.Current.Shutdown();
        }

        private void SetUIState(bool isProcessing)
        {
            BtnPlay.IsEnabled = !isProcessing;
            BtnPlay.Opacity = isProcessing ? 0.5 : 1.0;
            
            var btnText = (TextBlock)BtnPlay.FindName("BtnPlayText");
            if (btnText != null) btnText.Text = isProcessing ? "ПОДОЖДИТЕ..." : "ИГРАТЬ";

            UsernameBox.IsEnabled = !isProcessing;
            PasswordBox.IsEnabled = !isProcessing;
            
            ProgressPanel.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            if (!isProcessing)
            {
                LauncherProgress.Value = 0;
                StatusText.Text = "";
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }
    }

    public class AuthResponse
    {
        [JsonPropertyName("status")]
        public string status { get; set; }

        [JsonPropertyName("username")]
        public string username { get; set; }

        [JsonPropertyName("has_access")]
        public bool has_access { get; set; }
    }
}