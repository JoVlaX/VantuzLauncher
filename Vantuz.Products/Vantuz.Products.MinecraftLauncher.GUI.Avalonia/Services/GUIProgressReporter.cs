using System;
using Vantuz.Core;

namespace Vantuz.Products.MinecraftLauncher.GUI.Avalonia.Services;

public class GUIProgressReporter : IStatusReporter
{
    public void ReportState(string message)
    {
        // Status update without event - direct to UI via ViewModel
    }
    
    public void ReportProgress(string taskName, double percentage)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            ProgressChanged?.Invoke(this, new ProgressEventArgs(taskName, percentage)));
    }
    
    public event ProgressChangedHandler? ProgressChanged;
}
