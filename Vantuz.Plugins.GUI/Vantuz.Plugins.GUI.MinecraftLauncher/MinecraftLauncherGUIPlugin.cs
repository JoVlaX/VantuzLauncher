using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
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
    private Application? _app;
    private bool _ownsApplication = false;

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        // Extract workspace path from context (provided by Host or boot manifest)
        string workspacePath = context.Get<string>("workspace_path") ?? 
                               Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".vantuzlauncher");
        Directory.CreateDirectory(workspacePath);

        // Phase 1: Detect hosted vs standalone mode
        bool isHosted = Application.Current != null;

        if (isHosted)
        {
            // Hosted mode: reuse existing Application (VantuzLauncher host)
            // Per DEVIATION-003: plugin adapts to existing WPF context
            _app = Application.Current;
            _ownsApplication = false;

            // Ensure WPF Pack URI resolution targets plugin assembly
            if (Application.ResourceAssembly != typeof(MainWindow).Assembly)
            {
                try
                {
                    Application.ResourceAssembly = typeof(MainWindow).Assembly;
                }
                catch (InvalidOperationException)
                {
                    // Host has already pinned ResourceAssembly; resources may still
                    // resolve if host assembly contains them. Per DEVIATION-003.
                }
            }

            // Initialize on the host's dispatcher thread
            await _app!.Dispatcher.InvokeAsync(() =>
            {
                _reporter = new GUIProgressReporter();
                _mainWindow = new MainWindow(_reporter, workspacePath);
                _mainWindow.Show();

                // Set capabilities in context for downstream plugins
                context.Set("gui_reporter", _reporter);
                context.Set("gui_window", _mainWindow);
                context.Set("gui.credential_provider", (ICredentialProvider)_mainWindow);
                context.Set("workspace_path", workspacePath);

                context.Reporter.ReportState("[GUI] Minecraft Launcher initialized (hosted mode)");
            });
        }
        else
        {
            // Standalone mode: create new Application on dedicated STA thread
            _ownsApplication = true;
            var tcs = new TaskCompletionSource<bool>();

            var thread = new Thread(() =>
            {
                try
                {
                    _app = new Application();
                    _app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    // DEVIATION-003 RESOLVED: Ensure WPF Pack URI resolution targets plugin assembly
                    if (Application.ResourceAssembly != typeof(MainWindow).Assembly)
                    {
                        try
                        {
                            Application.ResourceAssembly = typeof(MainWindow).Assembly;
                        }
                        catch (InvalidOperationException)
                        {
                            // Test framework or host has already pinned ResourceAssembly
                        }
                    }

                    _reporter = new GUIProgressReporter();
                    _mainWindow = new MainWindow(_reporter, workspacePath);
                    _mainWindow.Show();

                    context.Set("gui_reporter", _reporter);
                    context.Set("gui_window", _mainWindow);
                    context.Set("gui.credential_provider", (ICredentialProvider)_mainWindow);
                    context.Set("workspace_path", workspacePath);

                    context.Reporter.ReportState("[GUI] Minecraft Launcher initialized (standalone mode)");

                    tcs.SetResult(true);
                    _app.Run();
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

        // Phase 2: Return immediately — GUI stays alive via Application.Current (hosted)
        // or dedicated STA thread (standalone). Pipeline proceeds to downstream steps.
        context.Reporter.ReportState("[GUI] Minecraft Launcher initialized and running");
        return new CommandResult(true);
    }

    private async Task ShutdownGUIAsync()
    {
        if (_app == null) return;

        await _app.Dispatcher.InvokeAsync(() =>
        {
            _mainWindow?.Close();
            // Only shutdown Application if we created it (standalone mode)
            // Hosted mode: never shutdown the host Application
            if (_ownsApplication)
            {
                _app.Shutdown();
            }
        });
    }

    public ValueTask DisposeAsync()
    {
        _ = ShutdownGUIAsync();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Status reporter that marshals updates to WPF UI thread.
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
    public string Message { get; }
    public StatusUpdateEventArgs(string message) => Message = message;
}

public class ProgressUpdateEventArgs : EventArgs
{
    public string OperationId { get; }
    public double Percent { get; }
    public ProgressUpdateEventArgs(string operationId, double percent)
    {
        OperationId = operationId;
        Percent = percent;
    }
}
