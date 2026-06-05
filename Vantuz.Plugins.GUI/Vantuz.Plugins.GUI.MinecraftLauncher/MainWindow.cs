using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Vantuz.Plugins.GUI.MinecraftLauncher;

public class MainWindow : Window, ICredentialProvider
{
    private readonly GUIProgressReporter _reporter;
    private readonly List<ProgressBar> _operationBars = new();
    private readonly Dictionary<string, TextBlock> _operationTexts = new();
    private readonly TaskCompletionSource<Credentials> _credentialsTcs = new();
    private readonly string _configPath;

    private TextBox UsernameBox = null!;
    private TextBox PasswordBox = null!;
    private CheckBox RememberMeBox = null!;
    private Slider RamSlider = null!;
    private TextBlock RamText = null!;
    private Button PlayButton = null!;
    private Button CancelButton = null!;
    private StackPanel ProgressPanel = null!;
    private TextBlock StatusText = null!;
    private ProgressBar MainProgressBar = null!;
    private TextBlock ProgressPercentText = null!;
    private StackPanel OperationsPanel = null!;
    private ScrollViewer LogScrollViewer = null!;
    private TextBox LogTextBox = null!;
    private TextBlock VersionText = null!;

    public event EventHandler<CredentialsSubmittedEventArgs>? CredentialsSubmitted;
    public event EventHandler? CredentialsCancelled;

    public MainWindow(GUIProgressReporter reporter, string workspacePath, bool autoSubmitTestCredentials = false)
    {
        _reporter = reporter;
        _configPath = Path.Combine(workspacePath, "launcher_config.json");

        BuildUI();

        reporter.StateChanged += OnStateChanged;
        reporter.ProgressChanged += OnProgressChanged;

        InitializeRamSlider();
        LoadSavedConfig();

        LogTextBox.Text = $"[{DateTime.Now:HH:mm:ss}] GUI initialized\n";
        StatusText.Text = "Введите логин и пароль";
        VersionText.Text = $"v{DateTime.Now:yyyyMMdd.HHmm}";

        if (autoSubmitTestCredentials)
        {
            this.Opened += async (_, _) =>
            {
                await Task.Delay(500);
                UsernameBox.Text = "test_user";
                PasswordBox.Text = "test_password";
                PlayButton_Click(null, null!);
            };
        }
        else
        {
            this.Opened += (_, _) => Dispatcher.UIThread.Post(() => UsernameBox.Focus());
        }
    }

    private void BuildUI()
    {
        Title = "Vantuz Minecraft Launcher";
        Width = 900;
        Height = 600;
        MinWidth = 700;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = new Border { Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)), Padding = new Thickness(20, 15, 20, 15) };
        var headerStack = new StackPanel();
        headerStack.Children.Add(new TextBlock { Text = "VANTUZ", FontSize = 32, FontWeight = FontWeight.Black, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
        headerStack.Children.Add(new TextBlock { Text = "M I N E C R A F T  L A U N C H E R", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, FontWeight = FontWeight.SemiBold });
        header.Child = headerStack;
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        // Main content
        var content = new Grid { Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left panel
        var leftBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25)), BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D)), BorderThickness = new Thickness(0, 0, 1, 0) };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var leftStack = new StackPanel { Margin = new Thickness(20) };

        leftStack.Children.Add(new TextBlock { Text = "ВХОД", FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15), FontSize = 12 });

        leftStack.Children.Add(new TextBlock { Text = "ЛОГИН", Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), FontSize = 10, Margin = new Thickness(0, 0, 0, 5) });
        var uBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 12) };
        UsernameBox = new TextBox { Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(10, 8, 10, 8), FontSize = 13, CaretBrush = Brushes.White };
        AutomationProperties.SetAutomationId(UsernameBox, "UsernameBox");
        uBorder.Child = UsernameBox;
        leftStack.Children.Add(uBorder);

        leftStack.Children.Add(new TextBlock { Text = "ПАРОЛЬ", Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), FontSize = 10, Margin = new Thickness(0, 0, 0, 5) });
        var pBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 0, 10) };
        PasswordBox = new TextBox { Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(10, 8, 10, 8), FontSize = 13, CaretBrush = Brushes.White, PasswordChar = '*' };
        AutomationProperties.SetAutomationId(PasswordBox, "PasswordBox");
        pBorder.Child = PasswordBox;
        leftStack.Children.Add(pBorder);

        RememberMeBox = new CheckBox { Content = "Запомнить меня", Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), Margin = new Thickness(0, 0, 0, 25), FontSize = 11 };
        leftStack.Children.Add(RememberMeBox);

        leftStack.Children.Add(new TextBlock { Text = "НАСТРОЙКИ", FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15), FontSize = 12 });
        leftStack.Children.Add(new TextBlock { Text = "ВЫДЕЛЕНИЕ ОЗУ", Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), FontSize = 10, Margin = new Thickness(0, 0, 0, 8) });

        RamSlider = new Slider { Minimum = 1024, Maximum = 16384, SmallChange = 512, LargeChange = 1024, Value = 4096, TickFrequency = 512, Margin = new Thickness(0, 0, 0, 5) };
        leftStack.Children.Add(RamSlider);

        RamText = new TextBlock { Text = "Выделено: 4096 МБ", Foreground = Brushes.White, FontSize = 11, Margin = new Thickness(0, 0, 0, 20), FontWeight = FontWeight.SemiBold };
        leftStack.Children.Add(RamText);

        PlayButton = new Button { Content = "ИГРАТЬ", Height = 45, Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), Foreground = Brushes.White, FontWeight = FontWeight.Bold, FontSize = 14, BorderThickness = new Thickness(0) };
        AutomationProperties.SetAutomationId(PlayButton, "PlayButton");
        PlayButton.Click += PlayButton_Click;
        leftStack.Children.Add(PlayButton);

        CancelButton = new Button { Content = "Отмена", Height = 35, Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), FontSize = 12, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), Margin = new Thickness(0, 10, 0, 0), IsVisible = false };
        CancelButton.Click += CancelButton_Click;
        leftStack.Children.Add(CancelButton);

        ProgressPanel = new StackPanel { IsVisible = false, Margin = new Thickness(0, 20, 0, 0) };
        ProgressPanel.Children.Add(new TextBlock { Text = "СТАТУС", FontWeight = FontWeight.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), FontSize = 12 });

        var sBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), CornerRadius = new CornerRadius(4), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 10) };
        StatusText = new TextBlock { Text = "Подготовка...", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)), FontSize = 11 };
        sBorder.Child = StatusText;
        ProgressPanel.Children.Add(sBorder);

        MainProgressBar = new ProgressBar { Height = 8, Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), BorderThickness = new Thickness(0), Maximum = 100 };
        ProgressPanel.Children.Add(MainProgressBar);

        ProgressPercentText = new TextBlock { Text = "0%", Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0), FontSize = 11 };
        ProgressPanel.Children.Add(ProgressPercentText);

        OperationsPanel = new StackPanel { Margin = new Thickness(0, 15, 0, 0) };
        ProgressPanel.Children.Add(OperationsPanel);

        leftStack.Children.Add(ProgressPanel);

        scroll.Content = leftStack;
        leftBorder.Child = scroll;
        Grid.SetColumn(leftBorder, 0);
        content.Children.Add(leftBorder);

        // Right panel (log)
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var rightHeader = new Border { Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)), Padding = new Thickness(15, 10, 15, 10) };
        rightHeader.Child = new TextBlock { Text = "Activity Log", FontWeight = FontWeight.Bold, Foreground = Brushes.White };
        Grid.SetRow(rightHeader, 0);
        rightGrid.Children.Add(rightHeader);

        var rightContent = new Border { Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)) };
        LogScrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        LogTextBox = new TextBox { Background = Brushes.Transparent, Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), FontFamily = new FontFamily("Consolas"), FontSize = 12, BorderThickness = new Thickness(0), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(15), AcceptsReturn = true };
        LogScrollViewer.Content = LogTextBox;
        rightContent.Child = LogScrollViewer;
        Grid.SetRow(rightContent, 1);
        rightGrid.Children.Add(rightContent);

        Grid.SetColumn(rightGrid, 1);
        content.Children.Add(rightGrid);

        Grid.SetRow(content, 1);
        grid.Children.Add(content);

        // Footer
        var footerBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)), Padding = new Thickness(15, 10, 15, 10) };
        var footerGrid = new Grid();
        VersionText = new TextBlock { Text = "v1.0.0", Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)), FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
        footerGrid.Children.Add(VersionText);
        var footerRight = new TextBlock { Text = "Compositum Architecture", Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        footerGrid.Children.Add(footerRight);
        footerBorder.Child = footerGrid;
        Grid.SetRow(footerBorder, 2);
        grid.Children.Add(footerBorder);

        Content = grid;

        // Wire up slider
        RamSlider.PropertyChanged += (s, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                int value = (int)RamSlider.Value;
                RamText.Text = $"Выделено: {value} МБ";
            }
        };
    }

    #region ICredentialProvider Implementation

    public Task<Credentials> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.Register(() => _credentialsTcs.TrySetCanceled());
        return _credentialsTcs.Task;
    }

    public void ShowProgress()
    {
        Dispatcher.UIThread.Post(() =>
        {
            PlayButton.IsEnabled = false;
            PlayButton.Opacity = 0.5;
            CancelButton.IsVisible = true;
            ProgressPanel.IsVisible = true;
        });
    }

    public void UpdateStatus(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = message;
            AppendToLog($"[STATUS] {message}");
        });
    }

    #endregion

    #region UI Event Handlers

    private void PlayButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string username = UsernameBox.Text?.Trim() ?? "";
        string password = PasswordBox.Text ?? "";

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            // In Avalonia, show a simple message box via window or just log
            AppendToLog("[WARN] Пожалуйста, введите логин и пароль.");
            StatusText.Text = "Введите логин и пароль";
            return;
        }

        SaveConfig();

        var credentials = new Credentials(username, password, RememberMeBox.IsChecked ?? false, (int)RamSlider.Value);

        AppendToLog($"[ACTION] Credentials submitted for user: {username}");
        CredentialsSubmitted?.Invoke(this, new CredentialsSubmittedEventArgs(credentials));
        _credentialsTcs.TrySetResult(credentials);
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AppendToLog("[ACTION] User requested cancellation");
        CredentialsCancelled?.Invoke(this, EventArgs.Empty);
        _credentialsTcs.TrySetCanceled();
    }

    #endregion

    #region Reporter Event Handlers

    private void OnStateChanged(object? sender, StatusUpdateEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StatusText.Text = e.Message;
            AppendToLog($"[STATE] {e.Message}");
            Title = $"Vantuz Minecraft Launcher - {e.Message}";
        });
    }

    private void OnProgressChanged(object? sender, ProgressUpdateEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
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
                Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
                BorderThickness = new Thickness(0)
            };
            stack.Children.Add(text);
            stack.Children.Add(bar);
            OperationsPanel.Children.Add(stack);
            _operationBars.Add(bar);
            _operationTexts[operationId] = text;
        }

        var index = new List<string>(_operationTexts.Keys).IndexOf(operationId);
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
                    PasswordBox.Text = CryptoHelper.Decrypt(config.Password);
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
                Username = RememberMeBox.IsChecked == true ? UsernameBox.Text ?? "" : "",
                Password = RememberMeBox.IsChecked == true ? CryptoHelper.Encrypt(PasswordBox.Text ?? "") : "",
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

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _reporter.StateChanged -= OnStateChanged;
        _reporter.ProgressChanged -= OnProgressChanged;
        _credentialsTcs.TrySetCanceled();
        base.OnClosing(e);
    }

    #endregion
}

