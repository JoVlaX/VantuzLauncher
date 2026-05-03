#nullable disable
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.IO.Compression;
using System.Linq;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;

namespace VantuzLauncher
{
    public partial class MainWindow : Window
    {
        private readonly string _apiUrl = "https://troglobit.webhm.pro/api.php";
        private readonly string _packwizBootstrapUrl = "https://github.com/packwiz/packwiz-installer-bootstrap/releases/latest/download/packwiz-installer-bootstrap.jar";
        
        private readonly string _mcDir;
        private readonly string _configPath;
        
        // Настраиваем HttpClient с заголовками браузера и игнорированием ошибок SSL
        private static readonly HttpClientHandler _handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };
        private static readonly HttpClient _httpClient = new HttpClient(_handler);

        public MainWindow()
        {
            InitializeComponent();
            
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }

            _mcDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".vantuz");
            if (!Directory.Exists(_mcDir)) Directory.CreateDirectory(_mcDir);

            _configPath = Path.Combine(_mcDir, "launcher_config.json");
            LoadSavedCredentials();
        }

        private void LoadSavedCredentials()
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
                        PasswordBox.Password = config.Password;
                        RememberMeBox.IsChecked = config.RememberMe;
                    }
                }
            }
            catch { }
        }

        private void SaveCredentials(string username, string password, bool remember)
        {
            try
            {
                var config = new LauncherConfig
                {
                    Username = remember ? username : "",
                    Password = remember ? password : "",
                    RememberMe = remember
                };
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        private async Task<string> GetJavaPathAsync()
        {
            string runtimeDir = Path.Combine(_mcDir, "runtime");
            string javaExe = Path.Combine(runtimeDir, "bin", "java.exe");

            if (File.Exists(javaExe))
                return javaExe;

            UpdateStatus("Загрузка автономной Java 17...");
            string zipPath = Path.Combine(_mcDir, "java.zip");
            string jreUrl = "https://api.adoptium.net/v3/binary/latest/17/ga/windows/x64/jre/hotspot/normal/eclipse?project=jdk";

            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(jreUrl);
                await File.WriteAllBytesAsync(zipPath, bytes);

                UpdateStatus("Распаковка Java...");
                if (Directory.Exists(runtimeDir))
                    Directory.Delete(runtimeDir, true);
                
                Directory.CreateDirectory(runtimeDir);

                // Распаковываем во временную папку, так как внутри архива есть корневая папка
                string tempExtract = Path.Combine(_mcDir, "temp_java");
                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                
                ZipFile.ExtractToDirectory(zipPath, tempExtract);

                // Перемещаем содержимое корневой папки архива в runtime
                string rootInZip = Directory.GetDirectories(tempExtract).FirstOrDefault();
                if (rootInZip != null)
                {
                    foreach (var dir in Directory.GetDirectories(rootInZip))
                        Directory.Move(dir, Path.Combine(runtimeDir, Path.GetFileName(dir)));
                    foreach (var file in Directory.GetFiles(rootInZip))
                        File.Move(file, Path.Combine(runtimeDir, Path.GetFileName(file)));
                }

                // Очистка
                Directory.Delete(tempExtract, true);
                File.Delete(zipPath);

                if (File.Exists(javaExe))
                    return javaExe;
                
                throw new Exception("Не удалось найти java.exe после распаковки.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при подготовке Java: {ex.Message}");
            }
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
                // Жесткая проверка: если папка была удалена перед нажатием кнопки, восстанавливаем её
                if (!Directory.Exists(_mcDir)) Directory.CreateDirectory(_mcDir);

                string javaPath = await GetJavaPathAsync();

                var authResponse = await AuthenticateUserAsync(username, password);
                
                if (authResponse == null || !string.Equals(authResponse.status, "success", StringComparison.OrdinalIgnoreCase))
                {
                    string extraInfo = "";
                    if (authResponse != null)
                    {
                        extraInfo = $"\nСтатус: {authResponse.status}";
                        if (!string.IsNullOrEmpty(authResponse.message))
                            extraInfo += $"\nСообщение от сервера: {authResponse.message}";
                    }
                    
                    MessageBox.Show($"Неверный логин или пароль.{extraInfo}", "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetUIState(false);
                    return;
                }

                // Сохраняем учетные данные, если авторизация успешна
                SaveCredentials(username, password, RememberMeBox.IsChecked == true);

                if (!authResponse.has_access)
                {
                    MessageBox.Show("Доступ закрыт. Оплатите активацию на сайте.", "Отказ в доступе", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetUIState(false);
                    return;
                }

                // Динамический выбор ссылки на Packwiz
                string packwizUrl = authResponse.is_admin 
                    ? "https://raw.githubusercontent.com/JoVlaX/sigmaivan/dev/pack.toml" 
                    : "https://raw.githubusercontent.com/JoVlaX/sigmaivan/main/pack.toml";

                UpdateStatus("Проверка Packwiz...");
                await SyncModpackAsync(packwizUrl, javaPath);

                UpdateStatus("Инициализация Minecraft...");
                await LaunchGameAsync(authResponse.username ?? username, javaPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Произошла ошибка:\n{ex.Message}\n\nВнутренняя ошибка: {ex.InnerException?.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                SetUIState(false);
            }
        }

        private async Task<AuthResponse> AuthenticateUserAsync(string username, string password)
        {
            // Попробуем сначала JSON формат (как было в оригинале), но с правильными заголовками
            var payload = new { username = username, password = password };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try 
            {
                var response = await _httpClient.PostAsync(_apiUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    try {
                        var result = JsonSerializer.Deserialize<AuthResponse>(responseText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (result != null && string.Equals(result.status, "success", StringComparison.OrdinalIgnoreCase))
                        {
                            return result;
                        }
                    } catch { }
                }

                // Если JSON не сработал, пробуем FormUrlEncoded
                var formData = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "username", username },
                    { "password", password }
                };
                var formContent = new FormUrlEncodedContent(formData);
                
                var formResponse = await _httpClient.PostAsync(_apiUrl, formContent);
                var formResponseText = await formResponse.Content.ReadAsStringAsync();

                try {
                    return JsonSerializer.Deserialize<AuthResponse>(formResponseText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                } catch (JsonException) {
                    // Если и это не JSON, возвращаем ошибку с текстом ответа
                    return new AuthResponse { status = "error", message = formResponseText };
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при связи с сервером авторизации: {ex.Message}", ex);
            }
        }

        private async Task SyncModpackAsync(string packwizUrl, string javaPath)
        {
            string bootstrapPath = Path.Combine(_mcDir, "packwiz-installer-bootstrap.jar");

            if (!File.Exists(bootstrapPath))
            {
                UpdateStatus("Загрузка установщика модов...");
                
                if (!Directory.Exists(_mcDir)) Directory.CreateDirectory(_mcDir);
                
                var bytes = await _httpClient.GetByteArrayAsync(_packwizBootstrapUrl);
                await File.WriteAllBytesAsync(bootstrapPath, bytes);
            }

            UpdateStatus("Синхронизация модов (Packwiz)...");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = $"-jar \"{bootstrapPath}\" \"{packwizUrl}\" --side client --no-gui",
                WorkingDirectory = _mcDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null) throw new Exception("Не удалось запустить Java. Убедитесь, что она установлена и добавлена в PATH.");
                    
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        var error = await process.StandardError.ReadToEndAsync();
                        // Если стандартная ошибка пуста, попробуем прочитать обычный вывод
                        if (string.IsNullOrEmpty(error)) error = await process.StandardOutput.ReadToEndAsync();
                        throw new Exception($"Ошибка при загрузке модов (ExitCode: {process.ExitCode}): {error}");
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new Exception("Команда 'java' не найдена. Пожалуйста, установите Java и добавьте её в переменную окружения PATH.");
            }
        }

        private async Task LaunchGameAsync(string sessionUsername, string javaPath)
        {
            UpdateStatus("Подготовка библиотек...");

            var path = new MinecraftPath(_mcDir);
            var launcher = new MinecraftLauncher(path);

            launcher.FileProgressChanged += (sender, args) =>
            {
                Dispatcher.Invoke(() => {
                    UpdateStatus($"Загрузка: {args.Name} ({args.ProgressedTasks}/{args.TotalTasks})");
                    LauncherProgress.Maximum = args.TotalTasks;
                    LauncherProgress.Value = args.ProgressedTasks;
                });
            };

            string mcVersion = "1.20.1";
            // Список версий Forge для 1.20.1, пробуем от самой новой к более старым
            string[] forgeVersions = { "47.3.0", "47.2.20", "47.2.0", "47.1.0" };
            string targetVersion = "";
            string installerPath = "";

            foreach (var forgeVersion in forgeVersions)
            {
                string installerFileName = $"forge-{mcVersion}-{forgeVersion}-installer.jar";
                installerPath = Path.Combine(_mcDir, installerFileName);

                if (File.Exists(installerPath))
                {
                    targetVersion = forgeVersion;
                    break;
                }

                UpdateStatus($"Проверка Forge {forgeVersion}...");
                string forgeDirectUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mcVersion}-{forgeVersion}/{installerFileName}";
                
                try 
                {
                    var response = await _httpClient.GetAsync(forgeDirectUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        UpdateStatus($"Загрузка Forge {forgeVersion}...");
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(installerPath, bytes);
                        targetVersion = forgeVersion;
                        break;
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(targetVersion))
            {
                throw new Exception("Не удалось найти или скачать подходящую версию Forge для 1.20.1. Проверьте интернет-соединение.");
            }

            UpdateStatus($"Установка Forge {targetVersion}...");
            
            try 
            {
                // Forge требует наличия файла launcher_profiles.json для работы установщика
                string profilesPath = Path.Combine(_mcDir, "launcher_profiles.json");
                if (!File.Exists(profilesPath))
                {
                    File.WriteAllText(profilesPath, "{ \"profiles\": {} }");
                }

                // Вместо forge.Install, который вызывает 404, используем прямой запуск установщика
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = $"-Djava.net.preferIPv4Stack=true -jar \"{installerPath}\" --installClient \"{_mcDir}\"",
                    WorkingDirectory = _mcDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null) throw new Exception("Не удалось запустить установщик Forge.");
                    UpdateStatus("Распаковка Forge (может занять 1-2 минуты)...");
                    
                    // Читаем вывод, чтобы видеть, если что-то пошло не так
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"Ошибка при установке Forge (ExitCode: {process.ExitCode}):\n{error}\n{output}");
                    }
                }

                // После установки Forge создает папку в versions. Нам нужно найти ее точное имя.
                UpdateStatus("Поиск установленной версии...");
                string versionsDir = Path.Combine(_mcDir, "versions");
                string installedVersionName = "";

                if (Directory.Exists(versionsDir))
                {
                    var dirs = Directory.GetDirectories(versionsDir);
                    // Ищем папку, которая содержит "forge" и нашу версию MC
                    foreach (var dir in dirs)
                    {
                        string name = Path.GetFileName(dir);
                        if (name.Contains(mcVersion, StringComparison.OrdinalIgnoreCase) && 
                            name.Contains("forge", StringComparison.OrdinalIgnoreCase))
                        {
                            installedVersionName = name;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(installedVersionName))
                {
                    installedVersionName = $"{mcVersion}-forge-{targetVersion}";
                }

                UpdateStatus($"Найдена версия: {installedVersionName}. Запуск...");

                // В CmlLib.Core v4 для обновления списка локальных версий 
                // можно просто создать новый экземпляр или вызвать GetVersionAsync
                // который сам проверит папку versions.
                var version = await launcher.GetVersionAsync(installedVersionName);

                // Проверяем и скачиваем недостающие библиотеки и ассеты Minecraft
                UpdateStatus("Загрузка библиотек Minecraft...");
                // В CmlLib.Core v4 правильный метод:
                await launcher.InstallAsync(version);

                var launchOption = new MLaunchOption
                {
                    Session = MSession.CreateOfflineSession(sessionUsername),
                    MaximumRamMb = 4096,
                    JavaPath = javaPath // Используем портативную Java
                };

                var gameProcess = await launcher.BuildProcessAsync(installedVersionName, launchOption);
                
                gameProcess.StartInfo.UseShellExecute = false;
                gameProcess.StartInfo.RedirectStandardOutput = true;
                gameProcess.StartInfo.RedirectStandardError = true;
                gameProcess.StartInfo.CreateNoWindow = true;

                string logPath = Path.Combine(_mcDir, "latest-game-log.txt");
                File.WriteAllText(logPath, $"--- Запуск игры: {DateTime.Now} ---\n");

                gameProcess.OutputDataReceived += (s, args) => {
                    if (!string.IsNullOrEmpty(args.Data))
                        File.AppendAllText(logPath, args.Data + Environment.NewLine);
                };
                gameProcess.ErrorDataReceived += (s, args) => {
                    if (!string.IsNullOrEmpty(args.Data))
                        File.AppendAllText(logPath, "[ERROR] " + args.Data + Environment.NewLine);
                };

                gameProcess.Start();
                gameProcess.BeginOutputReadLine();
                gameProcess.BeginErrorReadLine();

                await Task.Delay(5000);
                
                if (gameProcess.HasExited)
                {
                    string logTail = "Лог пуст.";
                    try {
                        var lines = File.ReadAllLines(logPath);
                        logTail = string.Join("\n", lines.Length > 10 ? lines[^10..] : lines);
                    } catch { }

                    throw new Exception($"Игра закрылась сразу после запуска (ExitCode: {gameProcess.ExitCode}).\nПоследние строки лога:\n{logTail}");
                }

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                string detail = $"Ошибка: {ex.Message}";
                if (ex.StackTrace != null) detail += $"\n\nСтек вызовов:\n{ex.StackTrace}";
                throw new Exception(detail);
            }
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

        [JsonPropertyName("is_admin")]
        public bool is_admin { get; set; }

        [JsonPropertyName("message")]
        public string message { get; set; }
    }

    public class LauncherConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
    }
}