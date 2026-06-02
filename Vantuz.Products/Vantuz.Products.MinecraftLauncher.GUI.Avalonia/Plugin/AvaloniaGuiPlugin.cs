using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;
using Vantuz.Host;
using Vantuz.Products.MinecraftLauncher.GUI.Avalonia.Services;
using Vantuz.Products.MinecraftLauncher.GUI.Avalonia.ViewModels;
using Vantuz.Products.MinecraftLauncher.GUI.Avalonia.Views;

namespace Vantuz.Products.MinecraftLauncher.GUI.Avalonia.Plugin;

/// <summary>
/// Avalonia-based GUI plugin for Minecraft Launcher.
/// Per INVARIANT_THEORY.md §1.2: Native UI stack for measurability.
/// Per COMPOSITUM.md §4: Host-agnostic plugin implementation.
/// </summary>
public class AvaloniaGuiPlugin : ICommandPlugin
{
    public string Name => "GUI.MinecraftLauncher";
    
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;
    private Application? _avaloniaApp;
    
    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        string workspacePath = context.Get<string>("workspace_path") ?? 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".vantuzlauncher");
        
        Directory.CreateDirectory(workspacePath);
        
        // Create reporter for UI updates
        var reporter = new GUIProgressReporter();
        
        // Create ViewModel with credential provider capabilities
        _viewModel = new MainWindowViewModel(reporter);
        
        // Register in context for downstream plugins
        context.Set("gui_reporter", reporter);
        context.Set("gui.credential_provider", (ICredentialProvider)_viewModel);
        context.Set("gui.window", _viewModel); // For future extensions
        context.Set("workspace_path", workspacePath);
        
        reporter.ReportState("[GUI] Avalonia Minecraft Launcher initialized");
        
        // Initialize and show Avalonia UI
        await InitializeAvaloniaAsync(_viewModel, context);
        
        // Wait for credentials or cancellation
        try
        {
            var credentials = await _viewModel.GetCredentialsAsync(CancellationToken.None);
            
            // Set credentials in context for downstream plugins
            context.Set("username", credentials.Username);
            context.Set("password", credentials.Password);
            context.Set("auth.username", credentials.Username);
            context.Set("auth.password", credentials.Password);
            
            reporter.ReportState($"[GUI] Credentials received for user: {credentials.Username}");
            
            // Keep UI alive for progress reporting
            await Task.Delay(-1, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            reporter.ReportState("[GUI] Credentials input cancelled");
            return new CommandResult(false, "User cancelled credentials input");
        }
        
        return new CommandResult(true);
    }
    
    private async Task InitializeAvaloniaAsync(MainWindowViewModel viewModel, CommandContext context)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        // Check if Avalonia is already initialized
        if (Application.Current != null)
        {
            // Use existing Avalonia application
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _mainWindow = new MainWindow(viewModel);
                _mainWindow.Show();
                tcs.SetResult(true);
            });
        }
        else
        {
            // Initialize Avalonia on this thread
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
            
            builder.SetupWithoutStarting();
            _avaloniaApp = builder.Instance;
            
            // Create window on UI thread
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                _mainWindow = new MainWindow(viewModel);
                _mainWindow.Show();
                tcs.SetResult(true);
            });
            
            // Note: We don't call Run() here - we're in plugin context
            // The window stays alive because we keep the reference
        }
        
        await tcs.Task;
    }
    
    public ValueTask DisposeAsync()
    {
        _mainWindow?.Close();
        return ValueTask.CompletedTask;
    }
}
