#nullable disable 
using System; 
using System.Collections.Generic; 
using System.IO; 
using System.Management; 
using System.Security.Cryptography; 
using System.Text; 
using System.Text.Json; 
using System.Threading; 
using System.Threading.Tasks; 
using System.Windows; 
using System.Windows.Input; 
using Vantuz.Core; 
using Vantuz.Host; 
 
namespace VantuzLauncher 
{ 
    public partial class MainWindow : Window 
    { 
        private readonly string _mcDir; 
        private readonly string _configPath; 
        private int _currentRamMb; 
        private int _totalRamMb = 8192; 
        private CancellationTokenSource _cts; 
 
        public MainWindow() 
        { 
            InitializeComponent(); 
             
            _mcDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".vantuz"); 
            if (!Directory.Exists(_mcDir)) Directory.CreateDirectory(_mcDir); 
 
            _configPath = Path.Combine(_mcDir, "launcher_config.json"); 
             
            InitializeRamLimits(); 
            LoadSavedConfig(); 
        } 
 
        // ПАТТЕРН 1: Thread-Safe Dispatching 
        private class WpfReporter : IStatusReporter 
        { 
            private readonly Action<string> _updateState; 
            private readonly Action<string, double> _updateProgress; 
 
            public WpfReporter(Action<string> updateState, Action<string, double> updateProgress) 
            { 
                _updateState = updateState; 
                _updateProgress = updateProgress; 
            } 
 
            public void ReportState(string message) 
            { 
                Application.Current.Dispatcher.Invoke(() => _updateState(message)); 
            } 
 
            public void ReportProgress(string taskName, double percentage) 
            { 
                Application.Current.Dispatcher.Invoke(() => _updateProgress(taskName, percentage)); 
            } 
        } 
 
        private void InitializeRamLimits() 
        { 
            try 
            { 
                var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"); 
                foreach (var obj in searcher.Get()) 
                { 
                    long totalBytes = Convert.ToInt64(obj["TotalPhysicalMemory"]); 
                    _totalRamMb = (int)(totalBytes / 1024 / 1024); 
                    int totalGb = _totalRamMb / 1024; 
                    RamSlider.Maximum = totalGb * 1024; 
                    RamSlider.Minimum = 1024; 
                    _currentRamMb = Math.Clamp(_totalRamMb / 2, 1024, 4096); 
                    break; 
                } 
            } 
            catch 
            { 
                RamSlider.Maximum = 8192; 
                RamSlider.Minimum = 1024; 
                _currentRamMb = 4096; 
            } 
        } 
 
        private void LoadSavedConfig() 
        { 
            try 
            { 
                if (File.Exists(_configPath)) 
                { 
                    var json = File.ReadAllText(_configPath); 
                    var config = JsonSerializer.Deserialize<LauncherConfig>(json); 
                    if (config != null) 
                    { 
                        UsernameBox.Text = config.Username; 
                        PasswordBox.Password = CryptoHelper.Decrypt(config.Password); 
                        RememberMeBox.IsChecked = config.RememberMe; 
                        _currentRamMb = Math.Clamp(config.RamMb, (int)RamSlider.Minimum, (int)RamSlider.Maximum); 
                        RamSlider.Value = _currentRamMb; 
                        if (RamText != null) RamText.Text = $"Выделено: {_currentRamMb} МБ из {RamSlider.Maximum} МБ"; 
                    } 
                } 
            } 
            catch { } 
        } 
 
        private void SaveConfig() 
        { 
            try 
            { 
                var config = new LauncherConfig 
                { 
                    Username = RememberMeBox.IsChecked == true ? UsernameBox.Text : "", 
                    Password = RememberMeBox.IsChecked == true ? CryptoHelper.Encrypt(PasswordBox.Password) : "", 
                    RememberMe = RememberMeBox.IsChecked == true, 
                    RamMb = _currentRamMb 
                }; 
                File.WriteAllText(_configPath, JsonSerializer.Serialize(config)); 
            } 
            catch { } 
        } 
 
        private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) 
        { 
            _currentRamMb = (int)e.NewValue; 
            if (RamText != null) RamText.Text = $"Выделено: {_currentRamMb} МБ из {RamSlider.Maximum} МБ"; 
            SaveConfig(); 
        } 
 
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) 
        { 
            if (e.ChangedButton == MouseButton.Left) this.DragMove(); 
        } 
 
        private void BtnClose_Click(object sender, RoutedEventArgs e) 
        { 
            _cts?.Cancel(); 
            Application.Current.Shutdown(); 
        } 
 
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Visible; 
        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Collapsed; 
 
        // ПАТТЕРН 2 и 3: Fire and Forget Boundary + State Machine UI 
        private async void BtnPlay_Click(object sender, RoutedEventArgs e) 
        { 
            string username = UsernameBox.Text.Trim(); 
            string password = PasswordBox.Password; 
 
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) 
            { 
                MessageBox.Show("Пожалуйста, введите логин и пароль.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning); 
                return; 
            } 
 
            SaveConfig(); 
 
            // Блокируем UI (State: В работе) 
            SetUIState(true); 
            StatusText.Text = "Инициализация движка..."; 
            LauncherProgress.Value = 0; 
             
            _cts = new CancellationTokenSource(); 
 
            try 
            { 
                // Инициализируем потокобезопасный репортер 
                var reporter = new WpfReporter( 
                    msg => StatusText.Text = msg, 
                    (task, prog) => { 
                        StatusText.Text = $"{task}... {prog:F1}%"; 
                        LauncherProgress.Value = prog; 
                    } 
                ); 
 
                var initialPayload = new Dictionary<string, object> 
                { 
                    { "username", username }, 
                    { "password", password }, 
                    { "ramMb", _currentRamMb }, 
                    { "mcDir", _mcDir } 
                }; 
 
                string bootJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot.json"); 
                string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins"); 
                 
                if (!File.Exists(bootJsonPath)) 
                    throw new FileNotFoundException("Файл манифеста boot.json не найден!"); 
 
                // Запускаем тяжелый конвейер в фоновом пуле потоков 
                await Task.Run(async () => 
                { 
                    var engine = new VantuzEngine(pluginsDir, reporter); 
                    await engine.RunAsync(bootJsonPath, _cts.Token, initialPayload); 
                }); 
 
                StatusText.Text = "Запуск успешно завершен!"; 
                this.Hide(); 
            } 
            catch (Exception ex) 
            { 
                // Выводим пользователю понятную ошибку, а детали лежат в crash.log 
                MessageBox.Show($"Критическая ошибка при запуске.\n\n{ex.Message}\n\nПодробности в файле crash.log.", 
                                "Сбой Конвейера", MessageBoxButton.OK, MessageBoxImage.Error); 
                StatusText.Text = "Ошибка запуска"; 
                SetUIState(false); 
            } 
            finally 
            { 
                // Гарантированная разблокировка UI 
                BtnPlay.IsEnabled = true; 
                BtnPlay.Opacity = 1.0; 
            } 
        } 
 
        private void SetUIState(bool isProcessing) 
        { 
            BtnPlay.IsEnabled = !isProcessing; 
            BtnPlay.Opacity = isProcessing ? 0.5 : 1.0; 
            UsernameBox.IsEnabled = !isProcessing; 
            PasswordBox.IsEnabled = !isProcessing; 
            ProgressPanel.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed; 
             
            if (!isProcessing) 
            { 
                LauncherProgress.Value = 0; 
                StatusText.Text = ""; 
            } 
        } 
    } 
 
    public class LauncherConfig 
    { 
        public string Username { get; set; } 
        public string Password { get; set; } 
        public bool RememberMe { get; set; } 
        public int RamMb { get; set; } 
    } 
 
    public static class CryptoHelper 
    { 
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VantuzLauncher_Secure_Key_2026"); 
 
        public static string Encrypt(string clearText) 
        { 
            if (string.IsNullOrEmpty(clearText)) return clearText; 
            try 
            { 
                using Aes aes = Aes.Create(); 
                using var rfc2898 = new Rfc2898DeriveBytes(Environment.MachineName + "Vantuz", Entropy, 1000, HashAlgorithmName.SHA256); 
                aes.Key = rfc2898.GetBytes(aes.KeySize / 8); 
                aes.IV = rfc2898.GetBytes(aes.BlockSize / 8); 
 
                using MemoryStream ms = new MemoryStream(); 
                using CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write); 
                cs.Write(Encoding.UTF8.GetBytes(clearText)); 
                cs.Close(); 
                return Convert.ToBase64String(ms.ToArray()); 
            } 
            catch { return ""; } 
        } 
 
        public static string Decrypt(string cipherText) 
        { 
            if (string.IsNullOrEmpty(cipherText)) return cipherText; 
            try 
            { 
                using Aes aes = Aes.Create(); 
                using var rfc2898 = new Rfc2898DeriveBytes(Environment.MachineName + "Vantuz", Entropy, 1000, HashAlgorithmName.SHA256); 
                aes.Key = rfc2898.GetBytes(aes.KeySize / 8); 
                aes.IV = rfc2898.GetBytes(aes.BlockSize / 8); 
 
                using MemoryStream ms = new MemoryStream(); 
                using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write); 
                byte[] cipherBytes = Convert.FromBase64String(cipherText); 
                cs.Write(cipherBytes); 
                cs.Close(); 
                return Encoding.UTF8.GetString(ms.ToArray()); 
            } 
            catch { return ""; } 
        } 
    } 
} 
