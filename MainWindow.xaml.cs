#nullable disable 
using System; 
using System.Collections.Generic; 
using System.IO; 
using System.Security.Cryptography; 
using System.Text; 
using System.Text.Json; 
using System.Threading; 
using System.Threading.Tasks; 
using System.Windows; 
using System.Windows.Controls;
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
        private Task _engineTask;

        public MainWindow() 
        { 
            InitializeComponent(); 
            _mcDir = App.WorkspacePath; // Берем готовый путь из ядра приложения 
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
 
            public void ReportState(string message) => 
                Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => _updateState(message))); 

            public void ReportProgress(string taskName, double percentage) => 
                Application.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => _updateProgress(taskName, percentage))); 
        } 

        private void InitializeRamLimits() 
        { 
            try 
            { 
                long totalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes; 
                _totalRamMb = (int)(totalBytes / 1024 / 1024); 
                int totalGb = _totalRamMb / 1024; 
                RamSlider.Maximum = totalGb * 1024; 
                RamSlider.Minimum = 1024; 
                _currentRamMb = Math.Clamp(_totalRamMb / 2, 1024, 4096); 
            } 
            catch { RamSlider.Maximum = 8192; RamSlider.Minimum = 1024; _currentRamMb = 4096; } 
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
 
        private async void BtnClose_Click(object sender, RoutedEventArgs e) 
        { 
            if (_cts != null && !_cts.IsCancellationRequested) 
            { 
                if (sender is Button btn) btn.IsEnabled = false; 
                StatusText.Text = "Безопасное завершение...";
                _cts.Cancel(); 
                if (_engineTask != null) { try { await _engineTask; } catch { } } 
            } 
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
            AsyncFileReporter fileReporter = null;

            try 
            { 
                // Инициализируем репортеры
                var uiReporter = new WpfReporter( 
                    msg => StatusText.Text = msg, 
                    (task, prog) => { 
                        StatusText.Text = $"{task}... {prog:F1}%"; 
                        LauncherProgress.Value = prog; 
                    } 
                ); 

                string logPath = Path.Combine(_mcDir, "launcher_trace.log");
                fileReporter = new AsyncFileReporter(logPath);

                var compositeReporter = new CompositeReporter(uiReporter, fileReporter);
 
                var initialPayload = new Dictionary<string, object>
                {
                    { "username", username },
                    { "password", password },
                    { "ramMb", _currentRamMb }
                    // mcDir теперь определяется в boot.json переменных (Nomadic-конфигурация)
                }; 
 
                string bootJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "boot.gui.json"); 
                string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins"); 
                 
                if (!File.Exists(bootJsonPath)) 
                    throw new FileNotFoundException("Файл манифеста boot.gui.json не найден!"); 
 
                // Запускаем тяжелый конвейер в фоновом пуле потоков 
                QuantumExecutionResult runResult = default;
                _engineTask = Task.Run(async () => 
                { 
                    string crashLogPath = Path.Combine(_mcDir, "crash.log"); 
                    var engine = new VantuzEngine(pluginsDir, compositeReporter, crashLogPath); 
                    runResult = await engine.RunAsync(bootJsonPath, _cts.Token, initialPayload); 
                }); 
                await _engineTask;

                if (!runResult.Success)
                {
                    StatusText.Text = "Ошибка конвейера";
                    MessageBox.Show($"Ошибка при запуске: {runResult.ErrorMessage ?? "Неизвестная ошибка"}", "Сбой", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetUIState(false);
                }
                else if (runResult.Payload != null && 
                    runResult.Payload.TryGetValue("UpdateReady", out var updateReadyObj) &&
                    updateReadyObj is bool updateReady && updateReady)
                {
                    string hostExe = runResult.Payload.TryGetValue("hostExecutable", out var hostExeObj) && hostExeObj is string he ? he : "VantuzLauncher.exe";
                    string updateScript = runResult.Payload.TryGetValue("UpdateScript", out var scriptObj) && scriptObj is string s ? s : null;
                    if (!string.IsNullOrEmpty(updateScript))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = updateScript,
                            Arguments = $"\"{hostExe}\"",
                            UseShellExecute = true,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                        });
                        Application.Current.Shutdown();
                        return;
                    }
                }
                else
                {
                    StatusText.Text = "Запуск успешно завершен!"; 
                    this.Hide();
                } 
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
                if (fileReporter != null) await fileReporter.DisposeAsync();
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
 
    /// <summary>
    /// Portable Cryptography с динамической случайной солью (согласно .traerules portable_cryptography_protocol)
    /// Соль генерируется динамически и хранится в открытом виде в заголовке зашифрованных данных.
    /// </summary>
    public static class CryptoHelper 
    { 
        private const int SaltSize = 16; // 128 bits
        private const int KeySize = 32;  // 256 bits
        private const int IvSize = 16;   // 128 bits
        private const int Iterations = 10000;
 
        public static string Encrypt(string clearText) 
        { 
            if (string.IsNullOrEmpty(clearText)) return clearText; 
            try 
            { 
                // Генерируем случайную соль для каждого шифрования (.traerules:199)
                byte[] salt = new byte[SaltSize];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }

                using Aes aes = Aes.Create(); 
                // Используем динамическую соль вместо хардкод (.traerules:199)
                using var rfc2898 = new Rfc2898DeriveBytes(
                    Environment.MachineName + Environment.UserName, // Номадный профиль
                    salt, 
                    Iterations, 
                    HashAlgorithmName.SHA256);
                aes.Key = rfc2898.GetBytes(KeySize); 
                aes.IV = rfc2898.GetBytes(IvSize); 
 
                using MemoryStream ms = new MemoryStream(); 
                // Записываем соль в открытом виде в заголовок (.traerules:199)
                ms.Write(salt, 0, salt.Length);
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
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                if (cipherBytes.Length < SaltSize) return "";

                // Извлекаем соль из заголовка (.traerules:199)
                byte[] salt = new byte[SaltSize];
                Buffer.BlockCopy(cipherBytes, 0, salt, 0, SaltSize);
                byte[] encryptedData = new byte[cipherBytes.Length - SaltSize];
                Buffer.BlockCopy(cipherBytes, SaltSize, encryptedData, 0, encryptedData.Length);

                using Aes aes = Aes.Create(); 
                // Используем извлеченную соль (.traerules:199)
                using var rfc2898 = new Rfc2898DeriveBytes(
                    Environment.MachineName + Environment.UserName, // Номадный профиль
                    salt, 
                    Iterations, 
                    HashAlgorithmName.SHA256);
                aes.Key = rfc2898.GetBytes(KeySize); 
                aes.IV = rfc2898.GetBytes(IvSize); 
 
                using MemoryStream ms = new MemoryStream(); 
                using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write); 
                cs.Write(encryptedData); 
                cs.Close(); 
                return Encoding.UTF8.GetString(ms.ToArray()); 
            } 
            catch { return ""; } 
        } 
    } 
} 
