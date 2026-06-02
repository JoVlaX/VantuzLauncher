using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vantuz.Core;

namespace Vantuz.Products.MinecraftLauncher.GUI;

/// <summary>
/// Main window for Minecraft Launcher GUI.
/// Per INVARIANT_THEORY.md §498: Explicit UI component, loaded only when needed.
/// Per COMPOSITUM_SPECIFICATION.md §4.1: Implements ICredentialProvider as plugin capability.
/// </summary>
public partial class MainWindow : Window, ICredentialProvider
{
    private readonly GUIProgressReporter _reporter;
    private readonly List<ProgressBar> _operationBars = new();
    private readonly Dictionary<string, TextBlock> _operationTexts = new();
    private readonly TaskCompletionSource<Credentials> _credentialsTcs = new();
    private readonly string _configPath;

    public event EventHandler<Vantuz.Core.CredentialsSubmittedEventArgs>? CredentialsSubmitted;
    public event EventHandler? CredentialsCancelled;

    public MainWindow(GUIProgressReporter reporter, string workspacePath)
    {
        _reporter = reporter;
        _configPath = Path.Combine(workspacePath, "launcher_config.json");
        InitializeComponent();

        // Subscribe to reporter updates
        reporter.StateChanged += OnStateChanged;
        reporter.ProgressChanged += OnProgressChanged;

        // Initialize UI
        InitializeRamSlider();
        LoadSavedConfig();

        // Wire up events
        RamSlider.ValueChanged += RamSlider_ValueChanged;

        // Initial state
        LogTextBox.Text = $"[{DateTime.Now:HH:mm:ss}] GUI initialized\n";
        StatusText.Text = "Введите логин и пароль";
        VersionText.Text = $"v{DateTime.Now:yyyyMMdd.HHmm}";
    }

    #region ICredentialProvider Implementation

    public Task<Credentials> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.Register(() => _credentialsTcs.TrySetCanceled());
        return _credentialsTcs.Task;
    }

    public void ShowProgress()
    {
        Dispatcher.Invoke(() =>
        {
            // Hide login controls, show progress
            PlayButton.IsEnabled = false;
            PlayButton.Opacity = 0.5;
            CancelButton.Visibility = Visibility.Visible;
            ProgressPanel.Visibility = Visibility.Visible;
        });
    }

    public void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
            AppendToLog($"[STATUS] {message}");
        });
    }

    #endregion

    #region UI Event Handlers

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        string username = UsernameBox.Text.Trim();
        string password = PasswordBox.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            MessageBox.Show("Пожалуйста, введите логин и пароль.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveConfig();

        var credentials = new Credentials(username, password, RememberMeBox.IsChecked ?? false, (int)RamSlider.Value);

        AppendToLog($"[ACTION] Credentials submitted for user: {username}");
        CredentialsSubmitted?.Invoke(this, new CredentialsSubmittedEventArgs(credentials));
        _credentialsTcs.TrySetResult(credentials);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        AppendToLog("[ACTION] User requested cancellation");
        CredentialsCancelled?.Invoke(this, EventArgs.Empty);
        _credentialsTcs.TrySetCanceled();
    }

    private void RamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int value = (int)e.NewValue;
        RamText.Text = $"Выделено: {value} МБ";
    }

    #endregion

    #region Reporter Event Handlers

    private void OnStateChanged(object? sender, StatusUpdateEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = e.Message;
            AppendToLog($"[STATE] {e.Message}");
            Title = $"Vantuz Minecraft Launcher - {e.Message}";
        });
    }

    private void OnProgressChanged(object? sender, ProgressUpdateEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            MainProgressBar.Value = e.Percent;
            ProgressPercentText.Text = $"{e.Percent:F1}%";
            UpdateOperationProgress(e.OperationId, e.Percent);
            AppendToLog($"[PROGRESS] {e.OperationId}: {e.Percent:F1}%");
        });
    }

    private void UpdateOperationProgress(string operationId, double percent)
    {
        if (!_operationTexts.ContainsKey(operationId))
        {
            var stack = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };
            var text = new TextBlock
            {
                Text = operationId,
                Foreground = Brushes.White,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 2)
            };
            var bar = new ProgressBar
            {
                Height = 8,
                Background = Brushes.DarkGray,
                Foreground = Brushes.LightGreen,
                BorderThickness = new Thickness(0)
            };
            stack.Children.Add(text);
            stack.Children.Add(bar);
            OperationsPanel.Children.Add(stack);
            _operationBars.Add(bar);
            _operationTexts[operationId] = text;
        }

        var index = _operationTexts.Keys.ToList().IndexOf(operationId);
        if (index >= 0 && index < _operationBars.Count)
        {
            _operationBars[index].Value = percent;
            _operationTexts[operationId].Text = $"{operationId}: {percent:F0}%";
        }
    }

    #endregion

    #region Configuration Management

    private void InitializeRamSlider()
    {
        try
        {
            long totalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            int totalRamMb = (int)(totalBytes / 1024 / 1024);
            int totalGb = totalRamMb / 1024;
            RamSlider.Maximum = Math.Min(totalGb * 1024, 32768);
            RamSlider.Minimum = 1024;
            RamSlider.Value = Math.Clamp(totalRamMb / 2, 1024, 8192);
            RamSlider.TickFrequency = 512;
        }
        catch
        {
            RamSlider.Maximum = 8192;
            RamSlider.Minimum = 1024;
            RamSlider.Value = 4096;
        }
        RamText.Text = $"Выделено: {(int)RamSlider.Value} МБ";
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
                    int ramValue = Math.Clamp(config.RamMb, (int)RamSlider.Minimum, (int)RamSlider.Maximum);
                    RamSlider.Value = ramValue;
                    RamText.Text = $"Выделено: {ramValue} МБ";
                }
            }
        }
        catch { /* Silent fail - user will enter credentials manually */ }
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
                RamMb = (int)RamSlider.Value
            };
            File.WriteAllText(_configPath, JsonSerializer.Serialize(config));
        }
        catch { /* Silent fail */ }
    }

    #endregion

    #region Helpers

    private void AppendToLog(string message)
    {
        LogTextBox.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        LogScrollViewer.ScrollToEnd();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        _reporter.StateChanged -= OnStateChanged;
        _reporter.ProgressChanged -= OnProgressChanged;
        _credentialsTcs.TrySetCanceled();
        base.OnClosing(e);
    }

    #endregion
}

