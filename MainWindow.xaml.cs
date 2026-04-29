#nullable disable
using System;
using System.Diagnostics;
using System.IO;
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

namespace VantuzLauncher
{
    public partial class MainWindow : Window
    {
        private readonly string _packwizUrl = "https://raw.githubusercontent.com/JoVlaX/sigmaivan/main/pack.toml";
        private readonly string _packwizUrl = "https://troglobit.webhm.pro/modpack/pack.toml";
        private readonly string _packwizBootstrapUrl = "https://github.com/packwiz/packwiz-installer-bootstrap/releases/latest/download/packwiz-installer-bootstrap.jar";
        
        private readonly string _mcDir;
        private static readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
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
                MessageBox.Show($"Произошла ошибка:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            UpdateStatus("Подготовка библиотек и ядра...");

            var launcher = new CMLauncher(new MinecraftPath(_mcDir));

            launcher.FileChanged += (e) =>
            {
                Dispatcher.Invoke(() => {
                    UpdateStatus($"Загрузка: {e.FileName}");
                    LauncherProgress.Maximum = e.TotalFileCount;
                    LauncherProgress.Value = e.ProgressedFileCount;
                });
            };

            var versions = await launcher.GetAllVersionsAsync();
            string targetVersion = "1.20.1"; 

            foreach (var v in versions)
            {
                if (v.Name.ToLower().Contains("fabric") || v.Name.ToLower().Contains("forge"))
                {
                    targetVersion = v.Name;
                    break;
                }
            }

            var launchOption = new MLaunchOption
            {
                Session = MSession.GetOfflineSession(sessionUsername),
                MaximumRamMb = 4096
            };

            UpdateStatus("Запуск процесса игры...");
            var process = await launcher.CreateProcessAsync(targetVersion, launchOption);
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