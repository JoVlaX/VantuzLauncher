using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Vantuz.Core;
using Vantuz.Host;

namespace Vantuz.Plugins.GUI.MinecraftLauncher;

/// <summary>
/// Product-specific GUI plugin for Minecraft Launcher.
/// Per INVARIANT_THEORY.md В§498: Explicit side-effect only when included in pipeline.
/// </summary>
public class MinecraftLauncherGUIPlugin : ICommandPlugin
{
    public string Name => "GUI.MinecraftLauncher";

    private MainWindow? _mainWindow;
    private GUIProgressReporter? _reporter;
    private Avalonia.Application? _app;
    private bool _ownsApplication = false;

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        string workspacePath = context.Get<string>("workspace_path") ??
                               Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".vantuzlauncher");
        Directory.CreateDirectory(workspacePath);

        bool autoSubmit = stepConfig.TryGetProperty("autoSubmitTestCredentials", out var autoSubmitProp) && autoSubmitProp.GetBoolean();
        bool isHosted = Avalonia.Application.Current != null;

        if (isHosted)
        {
            _app = Avalonia.Application.Current;
            _ownsApplication = false;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _reporter = new GUIProgressReporter();
                _mainWindow = new MainWindow(_reporter, workspacePath, autoSubmit);
                _mainWindow.Show();

                context.Set("gui_reporter", _reporter);
                context.Set("gui_window", _mainWindow);
                context.Set("gui.credential_provider", (ICredentialProvider)_mainWindow);
                context.Set("workspace_path", workspacePath);

                context.Reporter.ReportState("[GUI] Minecraft Launcher initialized (hosted mode)");
            });
        }
        else
        {
            _ownsApplication = true;
            var tcs = new TaskCompletionSource<bool>();
            _reporter = new GUIProgressReporter();

            var thread = new Thread(() =>
            {
                try
                {
                    AppBuilder.Configure(() => new PluginApp(_reporter, workspacePath, context, tcs, autoSubmit))
                        .UsePlatformDetect()
                        .LogToTrace()
                        .StartWithClassicDesktopLifetime(Array.Empty<string>());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            await tcs.Task;
        }

        context.Reporter.ReportState("[GUI] Minecraft Launcher initialized and running");
        return new CommandResult(true);
    }

    private async Task ShutdownGUIAsync()
    {
        if (_ownsApplication)
        {
            if (_app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                await Dispatcher.UIThread.InvokeAsync(() => desktop.Shutdown());
            }
        }
        else if (_mainWindow != null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => _mainWindow.Close());
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownGUIAsync();
    }
}

public class PluginApp : Avalonia.Application
{
    private readonly GUIProgressReporter _reporter;
    private readonly string _workspacePath;
    private readonly CommandContext _context;
    private readonly TaskCompletionSource<bool> _tcs;

    private readonly bool _autoSubmit;

    public PluginApp(GUIProgressReporter reporter, string workspacePath, CommandContext context, TaskCompletionSource<bool> tcs, bool autoSubmitTestCredentials = false)
    {
        _reporter = reporter;
        _workspacePath = workspacePath;
        _context = context;
        _tcs = tcs;
        _autoSubmit = autoSubmitTestCredentials;
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    }
/// F_doc: {OnFrameworkInitializationCompleted returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies OnFrameworkInitializationCompleted behavior

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(_reporter, _workspacePath, _autoSubmit);
            desktop.MainWindow = window;
            window.Show();
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            _context.Set("gui_reporter", _reporter);
            _context.Set("gui_window", window);
            _context.Set("gui.credential_provider", (ICredentialProvider)window);
            _context.Set("workspace_path", _workspacePath);

            _context.Reporter.ReportState("[GUI] Minecraft Launcher initialized (standalone mode)");

            _tcs.SetResult(true);
        }
        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>
/// Status reporter that marshals updates to UI thread via SynchronizationContext.
/// Per В§2.2 CQRS: separates UI updates from business logic.
/// </summary>
public class GUIProgressReporter : IStatusReporter
{
    private readonly SynchronizationContext? _uiContext;
    private readonly Dictionary<string, double> _progress = new();
    private string _currentState = "";
    
    public event EventHandler<StatusUpdateEventArgs>? StateChanged;
    public event EventHandler<ProgressUpdateEventArgs>? ProgressChanged;

    public GUIProgressReporter()
    {
        _uiContext = SynchronizationContext.Current;
    }
/// F_doc: {ReportState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportState behavior

    public void ReportState(string message)
    {
        _currentState = message;
        
        if (_uiContext != null)
        {
            _uiContext.Post(_ => StateChanged?.Invoke(this, new StatusUpdateEventArgs(message)), null);
        }
        else
        {
            // Fallback for headless scenarios
            Console.WriteLine($"[GUI] {message}");
        }
    }
/// F_doc: {ReportProgress returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportProgress behavior

    public void ReportProgress(string operationId, double percent)
    {
        _progress[operationId] = percent;
        
        if (_uiContext != null)
        {
            _uiContext.Post(_ => ProgressChanged?.Invoke(this, new ProgressUpdateEventArgs(operationId, percent)), null);
        }
    }

    public string CurrentState => _currentState;
    public IReadOnlyDictionary<string, double> Progress => _progress;
}

public class StatusUpdateEventArgs : EventArgs
{
    /// F_doc: {Message returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Message behavior
    public string Message { get; }
    public StatusUpdateEventArgs(string message) => Message = message;
}

public class ProgressUpdateEventArgs : EventArgs
{
    /// F_doc: {OperationId returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies OperationId behavior
    public string OperationId { get; }
    /// F_doc: {Percent returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Percent behavior
    public double Percent { get; }
    public ProgressUpdateEventArgs(string operationId, double percent)
    {
        OperationId = operationId;
        Percent = percent;
    }
}
