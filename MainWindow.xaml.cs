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
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Windows.Input;
using System.IO.Compression;
using System.Linq;
using System.Management;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using CmlLib.Core.Installer.Forge;

namespace VantuzLauncher
{
    public partial class MainWindow : Window
    {
        private const string CurrentVersion = "BUILD_DATE_PLACEHOLDER"; // Текущая версия лаунчера
        private readonly string _apiUrl = "https://troglobit.webhm.pro/yggdrasil/authserver/authenticate";
        private readonly string _versionUrl = "https://troglobit.webhm.pro/launcher_version.txt";
        private readonly string _downloadUrl = "https://troglobit.webhm.pro/VantuzLauncher.exe";
        private readonly string _packwizBootstrapUrl = "https://github.com/packwiz/packwiz-installer-bootstrap/releases/latest/download/packwiz-installer-bootstrap.jar";
        
        private readonly string _mcDir;
        private readonly string _configPath;
        private int _currentRamMb;
        private int _totalRamMb = 8192;
        private MSession _session;
        private string _currentTelemetryUsername = "unknown";
        private bool _updateRequired = false;
        private List<FileSystemWatcher> _watchers = new List<FileSystemWatcher>();
        private static readonly object _logLock = new object();
        
        // Настраиваем HttpClient с заголовками браузера и игнорированием ошибок SSL
        private static readonly HttpClientHandler _handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true
        };
        private static readonly HttpClient _httpClient = new HttpClient(_handler);

        public MainWindow()
        {
            InitializeComponent();
            
            // Регистрируем провайдер кодировок для поддержки 866 (DOS)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }
            
            // Устанавливаем бесконечный таймаут для работы с большими файлами обновлений и модов
            _httpClient.Timeout = System.Threading.Timeout.InfiniteTimeSpan;

            _mcDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".vantuz");
            if (!Directory.Exists(_mcDir)) Directory.CreateDirectory(_mcDir);

            _configPath = Path.Combine(_mcDir, "launcher_config.json");
            
            InitializeRamLimits();
            VersionText.Text = $"v{CurrentVersion}";
            LoadSavedConfig();
            
            // Проверка обновлений при запуске
            _ = CheckForUpdatesAsync();
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
                    
                    // Округляем до ГБ вниз для лимита
                    int totalGb = _totalRamMb / 1024;
                    RamSlider.Maximum = totalGb * 1024;
                    RamSlider.Minimum = 1024;

                    // Динамический дефолт: половина от общей, но в пределах [1024, 4096]
                    _currentRamMb = Math.Clamp(_totalRamMb / 2, 1024, 4096);
                    break;
                }
            }
            catch
            {
                // Если не удалось получить инфо, ставим стандартные 8ГБ лимит
                _totalRamMb = 8192;
                RamSlider.Maximum = 8192;
                RamSlider.Minimum = 1024;
                _currentRamMb = 4096;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var serverVersion = (await _httpClient.GetStringAsync($"{_versionUrl}?t={DateTime.Now.Ticks}")).Trim();
                if (decimal.TryParse(serverVersion.Replace(".", ""), out decimal sVer) && 
                    decimal.TryParse(CurrentVersion.Replace(".", ""), out decimal cVer))
                {
                    if (sVer > cVer)
                    {
                        _updateRequired = true;
                        Dispatcher.Invoke(() =>
                        {
                            var btnText = (TextBlock)BtnPlay.FindName("BtnPlayText");
                            if (btnText != null) btnText.Text = "ОБНОВИТЬ ЛАУНЧЕР";

                            // Блокируем ввод, так как играть все равно нельзя 
                            UsernameBox.IsEnabled = false;
                            PasswordBox.IsEnabled = false;
                            RememberMeBox.IsEnabled = false;
                            UpdateStatus($"Доступна новая версия: {serverVersion}. Необходимо обновить.");
                        });
                    }
                }
            }
            catch { }
        }

        private async Task DownloadAndApplyUpdateAsync()
        {
            try
            {
                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule.FileName;
                string tempExePath = exePath + ".new";

                using (var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var buffer = new byte[8192];
                    var totalRead = 0L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempExePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        int read;
                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            if (totalBytes != -1)
                            {
                                double percentage = (double)totalRead / totalBytes * 100;
                                Dispatcher.Invoke(() => {
                                    UpdateStatus($"Загрузка обновления: {percentage:F1}%...");
                                    LauncherProgress.Value = percentage;
                                });
                            }
                        }
                    }
                }

                string batchScript = $@"
@echo off
taskkill /F /IM VantuzLauncher.exe > nul 2>&1
timeout /t 5 /nobreak > nul
del /Q ""{exePath}""
move /Y ""{tempExePath}"" ""{exePath}""
start """" ""{exePath}""
del ""%~f0""";
                
                string batchPath = Path.Combine(Path.GetTempPath(), "vantuz_updater.bat");
                await File.WriteAllTextAsync(batchPath, batchScript, Encoding.GetEncoding(866));

                Process.Start(new ProcessStartInfo { 
                    FileName = "cmd.exe", 
                    Arguments = $"/c \"{batchPath}\"", 
                    CreateNoWindow = true, 
                    UseShellExecute = false 
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("");
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = Visibility.Visible;
        }

        private void BtnCloseSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
        }

        private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _currentRamMb = (int)e.NewValue;
            if (RamText != null) 
                RamText.Text = $"Выделено: {_currentRamMb} МБ из {RamSlider.Maximum} МБ";
            SaveConfig();
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
                        
                        _currentRamMb = config.RamMb;
                        
                        // Проверка на выход за границы при смене ПК
                        if (_currentRamMb > RamSlider.Maximum) _currentRamMb = (int)RamSlider.Maximum;
                        
                        RamSlider.Value = _currentRamMb;
                        RamText.Text = $"Выделено: {_currentRamMb} МБ из {RamSlider.Maximum} МБ";
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
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        private async Task<string> GetJavaPathAsync()
        {
            string runtimeDir = Path.Combine(_mcDir, "runtime");
            string javaExe = Path.Combine(runtimeDir, "bin", "java.exe");

            // Проверка целостности Java: наличие файла и размер > 0
            if (File.Exists(javaExe))
            {
                var fileInfo = new FileInfo(javaExe);
                if (fileInfo.Length > 0)
                    return javaExe;
                
                // Если файл 0 байт, удаляем папку для перекачивания
                try { Directory.Delete(runtimeDir, true); } catch { }
            }

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
            if (_updateRequired)
            {
                UpdateStatus("Загрузка обновления...");
                SetUIState(true);
                await DownloadAndApplyUpdateAsync();
                return;
            }

            SetUIState(true);
            UpdateStatus("Проверка актуальности лаунчера...");
            await CheckForUpdatesAsync();
            if (_updateRequired) return; // Если после проверки оказалось, что нужно обновиться 

            string username = UsernameBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Пожалуйста, введите логин и пароль.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentTelemetryUsername = username;

            SetUIState(true);
            UpdateStatus("Авторизация...");

            try
            {
                // Жесткая проверка: если папка была удалена перед нажатием кнопки, восстанавливаем её
                if (!Directory.Exists(_mcDir)) Directory.CreateDirectory(_mcDir);

                string javaPath = await GetJavaPathAsync();

                var authResponse = await AuthenticateUserAsync(username, password);
                
                if (authResponse == null || !string.IsNullOrEmpty(authResponse.error))
                {
                    string extraInfo = authResponse?.errorMessage ?? "Неизвестная ошибка";
                    MessageBox.Show($"Ошибка авторизации: {extraInfo}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetUIState(false);
                    return;
                }

                // Сохраняем учетные данные, если авторизация успешна
                SaveConfig();

                if (!authResponse.has_access)
                {
                    MessageBox.Show("Доступ закрыт. Оплатите активацию на сайте.", "Отказ в доступе", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetUIState(false);
                    return;
                }

                // Динамический выбор ссылки на Packwiz
                string baseUrl = "https://troglobit.webhm.pro/packs";
                string packwizUrl = $"{baseUrl}/main/pack.toml";
                if (authResponse.is_tester) packwizUrl = $"{baseUrl}/test/pack.toml";
                else if (authResponse.is_admin) packwizUrl = $"{baseUrl}/dev/pack.toml";

                UpdateStatus("Зачистка несанкционированных файлов...");
                string[] foldersToClean = { "mods", "resourcepacks", "shaderpacks" };
                foreach (var folder in foldersToClean)
                {
                    string folderPath = Path.Combine(_mcDir, folder);
                    if (Directory.Exists(folderPath))
                    {
                        var files = Directory.GetFiles(folderPath);
                        foreach (var file in files)
                        {
                            // Удаляем только архивы, чтобы не трогать папки или системные логи 
                            if (file.EndsWith(".jar") || file.EndsWith(".zip") || file.EndsWith(".rar"))
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }
                    }
                }

                UpdateStatus("Проверка Packwiz...");
                try 
                {
                    await SyncModpackAsync(packwizUrl, javaPath);
                }
                catch (Exception ex)
                {
                    LogException(ex, "modpack_sync_error");
                    throw new Exception("Ошибка синхронизации модов. Проверьте интернет или обратитесь к администрации.");
                }

                UpdateStatus("Чтение конфигурации модпака...");
                var (mcVersion, forgeVersion) = await GetPackVersionsAsync(packwizUrl);

                UpdateStatus("Инициализация Minecraft...");
                try 
                {
                    await LaunchGameAsync(authResponse, javaPath, mcVersion, forgeVersion);
                }
                catch (Exception ex)
                {
                    LogException(ex, "game_launch_error");
                    throw new Exception("Ошибка запуска Minecraft. Проверьте интернет или обратитесь к администрации.");
                }
            }
            catch (Exception ex)
            {
                // Если это наше "чистое" сообщение, не логируем его повторно (оно уже залогировано внутри)
                if (!ex.Message.Contains("обратитесь к администрации"))
                {
                    LogException(ex);
                    MessageBox.Show("Произошла ошибка запуска. Проверьте интернет или обратитесь к администрации.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                SetUIState(false);
            }
        }

        private async Task<AuthResponse> AuthenticateUserAsync(string username, string password)
        {
            var payload = new
            {
                username = username,
                password = password,
                clientToken = Guid.NewGuid().ToString("N"),
                launcherVersion = CurrentVersion // Добавлено поле версии 
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_apiUrl, content);
                var responseText = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<AuthResponse>(responseText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result != null)
                    return result;

                return new AuthResponse { error = "UnknownError", errorMessage = "Не удалось обработать ответ от сервера авторизации." };
            }
            catch (Exception ex)
            {
                return new AuthResponse { error = "NetworkError", errorMessage = $"Ошибка соединения с сервером Yggdrasil: {ex.Message}" };
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

        private async Task LaunchGameAsync(AuthResponse authInfo, string javaPath, string mcVersion, string targetVersion)
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

            UpdateStatus("Подготовка Yggdrasil (Authlib)...");
            string authlibPath = Path.Combine(_mcDir, "authlib-injector.jar");
            bool needDownloadAuthlib = true;

            if (File.Exists(authlibPath))
            {
                if (new FileInfo(authlibPath).Length > 10000) // Проверка: файл не пустой (> 10KB)
                    needDownloadAuthlib = false;
                else
                    File.Delete(authlibPath); // Удаляем битый файл
            }

            if (needDownloadAuthlib)
            {
                var response = await _httpClient.GetAsync("https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.5/authlib-injector-1.2.5.jar");
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(authlibPath, bytes);
            }

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
                        name.Contains("forge", StringComparison.OrdinalIgnoreCase) &&
                        name.Contains(targetVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        installedVersionName = name;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(installedVersionName))
            {
                string installerFileName = $"forge-{mcVersion}-{targetVersion}-installer.jar";
                string installerPath = Path.Combine(_mcDir, installerFileName);

                if (!File.Exists(installerPath))
                {
                    UpdateStatus($"Загрузка Forge {targetVersion}...");
                    string forgeDirectUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mcVersion}-{targetVersion}/{installerFileName}";
                    var response = await _httpClient.GetAsync(forgeDirectUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(installerPath, bytes);
                    }
                    else throw new Exception($"Не удалось скачать установщик Forge {targetVersion}.");
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
                    if (Directory.Exists(versionsDir))
                    {
                        var dirs = Directory.GetDirectories(versionsDir);
                        foreach (var dir in dirs)
                        {
                            string name = Path.GetFileName(dir);
                            if (name.Contains(mcVersion, StringComparison.OrdinalIgnoreCase) && 
                                name.Contains("forge", StringComparison.OrdinalIgnoreCase) &&
                                name.Contains(targetVersion, StringComparison.OrdinalIgnoreCase))
                            {
                                installedVersionName = name;
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains("обратитесь к администрации"))
                    {
                        LogException(ex, "forge_install_error");
                    }
                    throw;
                }
            }

            if (string.IsNullOrEmpty(installedVersionName))
            {
                installedVersionName = $"{mcVersion}-forge-{targetVersion}";
            }

            UpdateStatus($"Используется версия: {installedVersionName}. Запуск...");

            try 
            {
                // В CmlLib.Core v4 для обновления списка локальных версий 
                // можно просто создать новый экземпляр или вызвать GetVersionAsync
                // который сам проверит папку versions.
                var version = await launcher.GetVersionAsync(installedVersionName);

                // Проверяем и скачиваем недостающие библиотеки и ассеты Minecraft
                UpdateStatus("Загрузка библиотек Minecraft...");
                // В CmlLib.Core v4 правильный метод:
                await launcher.InstallAsync(version);

                _session = new MSession
                {
                    Username = authInfo.selectedProfile.name,
                    UUID = authInfo.selectedProfile.id,
                    AccessToken = authInfo.accessToken,
                    ClientToken = authInfo.clientToken,
                    UserType = "mojang"
                };

                var launchOption = new MLaunchOption
                {
                    Session = _session,
                    MaximumRamMb = _currentRamMb,
                    JavaPath = javaPath,
                    // ПРАВИЛЬНОЕ_СВОЙСТВО для CmlLib.Core v4.0.6
                    ExtraJvmArguments = new[] 
                    { 
                        new MArgument($"-javaagent:{authlibPath}=https://troglobit.webhm.pro/yggdrasil/") 
                    }
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
                    {
                        lock (_logLock) { File.AppendAllText(logPath, args.Data + Environment.NewLine); }
                    }
                };
                gameProcess.ErrorDataReceived += (s, args) => {
                    if (!string.IsNullOrEmpty(args.Data))
                    {
                        lock (_logLock) { File.AppendAllText(logPath, "[ERROR] " + args.Data + Environment.NewLine); }
                    }
                };

                gameProcess.Start();
                ChildProcessTracker.AddProcess(gameProcess);
                gameProcess.BeginOutputReadLine();
                gameProcess.BeginErrorReadLine();

                // Запуск античита: следим за папкой mods в реальном времени
                StartAntiCheatWatcher(_mcDir, gameProcess);

                await Task.Delay(5000);
                
                if (gameProcess.HasExited)
                {
                    string logTail = "Лог пуст.";
                    try {
                        var lines = File.ReadAllLines(logPath);
                        logTail = string.Join("\n", lines.Length > 10 ? lines[^10..] : lines);
                    } catch { }

                    var crashEx = new Exception($"Игра закрылась сразу после запуска (ExitCode: {gameProcess.ExitCode}).\nПоследние строки лога:\n{logTail}");
                    LogException(crashEx, "game_crash");
                    throw crashEx;
                }

                // Скрываем лаунчер, но оставляем его работать в фоне для античита 
                Application.Current.Dispatcher.Invoke(() => { this.Hide(); });

                // Ждем, пока игра не закроется 
                await gameProcess.WaitForExitAsync();

                // Только теперь закрываем лаунчер полностью 
                Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(); });
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("обратитесь к администрации"))
                {
                    LogException(ex, "game_launch_internal_error");
                }
                
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

        private async Task<(string mcVersion, string forgeVersion)> GetPackVersionsAsync(string packTomlUrl)
        {
            var content = await _httpClient.GetStringAsync(packTomlUrl);
            string mcVer = "1.20.1", forgeVer = "";
            foreach (var line in content.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("minecraft =")) mcVer = t.Split('=')[1].Trim(' ', '"', '\r');
                if (t.StartsWith("forge =")) forgeVer = t.Split('=')[1].Trim(' ', '"', '\r');
            }
            if (string.IsNullOrEmpty(forgeVer)) throw new Exception("Версия Forge не найдена в конфигурации сборки.");
            return (mcVer, forgeVer);
        }

        private void StartAntiCheatWatcher(string mcDir, Process gameProcess)
        {
            try
            {
                foreach (var w in _watchers) w?.Dispose();
                _watchers.Clear();

                string[] protectedFolders = { "mods", "resourcepacks", "shaderpacks" };

                Action<string, string> triggerAntiCheat = (path, changeType) =>
                {
                    if (!path.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
                        !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                        !path.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)) return;

                    try
                    {
                        if (!gameProcess.HasExited)
                        {
                            gameProcess.Kill();
                            _ = SendTelemetryAsync("anticheat_violation", $"Вмешательство во время игры: {path} ({changeType})");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("Обнаружено изменение критических файлов во время работы игры. Процесс принудительно остановлен.", "VantuzLauncher Античит", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _ = SendTelemetryAsync("anticheat_kill_error", ex.ToString());
                    }
                };

                FileSystemEventHandler onFileChanged = (s, e) => triggerAntiCheat(e.FullPath, e.ChangeType.ToString());
                RenamedEventHandler onFileRenamed = (s, e) => triggerAntiCheat(e.FullPath, "Renamed");

                foreach (var folder in protectedFolders)
                {
                    string fullPath = Path.Combine(mcDir, folder);
                    if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);

                    var watcher = new FileSystemWatcher(fullPath)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                        Filter = "*.*",
                        IncludeSubdirectories = true,
                        EnableRaisingEvents = true
                    };
                    watcher.Created += onFileChanged;
                    watcher.Changed += onFileChanged;
                    watcher.Renamed += onFileRenamed;
                    _watchers.Add(watcher);
                }

                gameProcess.EnableRaisingEvents = true;
                gameProcess.Exited += (s, e) =>
                {
                    foreach (var w in _watchers) w?.Dispose();
                    _watchers.Clear();
                };
            }
            catch (Exception ex)
            {
                _ = SendTelemetryAsync("anticheat_init_error", ex.ToString());
            }
        }

        private void LogException(Exception ex, string type = "launcher_error")
        {
            try
            {
                string logFile = Path.Combine(_mcDir, "launcher-errors.log");
                string errorMsg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR ({type}): {ex.Message}\n" +
                                 $"StackTrace: {ex.StackTrace}\n" +
                                 (ex.InnerException != null ? $"InnerException: {ex.InnerException.Message}\n" : "") +
                                 "--------------------------------------------------\n";
                File.AppendAllText(logFile, errorMsg);
                
                // Отправляем телеметрию
                _ = SendTelemetryAsync(type, errorMsg);
            }
            catch { }
        }

        private Task SendTelemetryAsync(string errorType, string logContent)
        {
            try
            {
                var payload = new
                {
                    username = _currentTelemetryUsername,
                    error_type = errorType,
                    log_content = logContent
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Отправляем без await ожиданий (fire and forget)
                _httpClient.PostAsync("https://troglobit.webhm.pro/api/telemetry.php", content);
            }
            catch
            {
                // Глушим любые ошибки сети. Телеметрия не должна ронять лаунчер.
            }
            return Task.CompletedTask;
        }
    }

    public class AuthResponse
    {
        [JsonPropertyName("accessToken")] public string accessToken { get; set; }
        [JsonPropertyName("clientToken")] public string clientToken { get; set; }
        [JsonPropertyName("selectedProfile")] public Profile selectedProfile { get; set; }
        [JsonPropertyName("error")] public string error { get; set; }
        [JsonPropertyName("errorMessage")] public string errorMessage { get; set; }
        [JsonPropertyName("has_access")] public bool has_access { get; set; }
        [JsonPropertyName("is_admin")] public bool is_admin { get; set; }
        [JsonPropertyName("is_tester")] public bool is_tester { get; set; }
    }

    public class Profile
    {
        [JsonPropertyName("id")] public string id { get; set; }
        [JsonPropertyName("name")] public string name { get; set; }
    }

    public class LauncherConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
        public int RamMb { get; set; }
    }

    public static class ChildProcessTracker
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr a, string lpName);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass, IntPtr lpJobObjectInfo, int cbJobObjectInfoLength);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        private static IntPtr s_jobHandle;

        static ChildProcessTracker()
        {
            s_jobHandle = CreateJobObject(IntPtr.Zero, null);
            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION { LimitFlags = 0x2000 }; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE 
            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION { BasicLimitInformation = info };

            int length = System.Runtime.InteropServices.Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr extendedInfoPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
            System.Runtime.InteropServices.Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

            SetInformationJobObject(s_jobHandle, 9, extendedInfoPtr, length);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(extendedInfoPtr);
        }

        public static void AddProcess(Process process)
        {
            if (s_jobHandle != IntPtr.Zero)
                AssignProcessToJobObject(s_jobHandle, process.Handle);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public long Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }
    }

    public static class CryptoHelper
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VantuzLauncher_Secure_Key_2026");

        public static string Encrypt(string clearText)
        {
            if (string.IsNullOrEmpty(clearText)) return clearText;
            try
            {
                byte[] clearBytes = Encoding.UTF8.GetBytes(clearText);
                using Aes aes = Aes.Create();
                using var rfc2898 = new Rfc2898DeriveBytes(Environment.MachineName + "Vantuz", Entropy, 1000, HashAlgorithmName.SHA256);
                aes.Key = rfc2898.GetBytes(aes.KeySize / 8);
                aes.IV = rfc2898.GetBytes(aes.BlockSize / 8);

                using MemoryStream ms = new MemoryStream();
                using CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
                cs.Write(clearBytes, 0, clearBytes.Length);
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
                using Aes aes = Aes.Create();
                using var rfc2898 = new Rfc2898DeriveBytes(Environment.MachineName + "Vantuz", Entropy, 1000, HashAlgorithmName.SHA256);
                aes.Key = rfc2898.GetBytes(aes.KeySize / 8);
                aes.IV = rfc2898.GetBytes(aes.BlockSize / 8);

                using MemoryStream ms = new MemoryStream();
                using CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
                cs.Write(cipherBytes, 0, cipherBytes.Length);
                cs.Close();
                return Encoding.UTF8.GetString(ms.ToArray());
            }
            catch { return ""; }
        }
    }
}