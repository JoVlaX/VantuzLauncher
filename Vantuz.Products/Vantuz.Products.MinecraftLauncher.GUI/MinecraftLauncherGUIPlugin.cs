using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Vantuz.Core;
using Vantuz.Host;

namespace Vantuz.Products.MinecraftLauncher.GUI;

/// <summary>
/// Product-specific GUI plugin for Minecraft Launcher.
/// Per INVARIANT_THEORY.md §498: Explicit side-effect only when included in pipeline.
/// </summary>
public class MinecraftLauncherGUIPlugin : ICommandPlugin
{
    public string Name => "GUI.MinecraftLauncher";

    private MainWindow? _mainWindow;
    private GUIProgressReporter? _reporter;
    private Application? _app;

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        // Extract workspace path from context (provided by Host or boot manifest)
        string workspacePath = context.Get<string>("workspace_path") ?? 
                               Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".vantuzlauncher");
        Directory.CreateDirectory(workspacePath);

        // Phase 1: Initialize WPF Application (UI thread)
        var tcs = new TaskCompletionSource<bool>();

        var thread = new Thread(() =>
        {
            try
            {
                // Per §11.5 Agentic: explicit initialization
                _app = new Application();
                _app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Create reporter that marshals to UI thread
                _reporter = new GUIProgressReporter();

                // Create main window with credential provider capability
                _mainWindow = new MainWindow(_reporter, workspacePath);
                _mainWindow.Show();

                // Set capabilities in context for downstream plugins
                context.Set("gui_reporter", _reporter);
                context.Set("gui_window", _mainWindow);
                context.Set("gui.credential_provider", (ICredentialProvider)_mainWindow);  // Sync with CredentialCollectionStep.cs:27
                context.Set("workspace_path", workspacePath);

                // Subscribe to context updates
                context.Reporter.ReportState("[GUI] Minecraft Launcher initialized");

                tcs.SetResult(true);

                // Run message loop
                _app.Run();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        // Wait for initialization
        await tcs.Task;
        
        // Phase 2: Wait for pipeline completion signal
        var cts = new CancellationTokenSource();
        if (context.Get<CancellationToken>("cancellation_token") is CancellationToken parentToken)
        {
            parentToken.Register(() => cts.Cancel());
        }
        
        try
        {
            // Keep GUI alive until explicitly closed or pipeline completes
            await Task.Delay(-1, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        
        // Phase 3: Graceful shutdown
        await ShutdownGUIAsync();
        
        return new CommandResult(true);
    }

    private async Task ShutdownGUIAsync()
    {
        if (_app == null) return;
        
        await _app.Dispatcher.InvokeAsync(() =>
        {
            _mainWindow?.Close();
            _app.Shutdown();
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
/// Per §2.2 CQRS: separates UI updates from business logic.
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
