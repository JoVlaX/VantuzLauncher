namespace VantuzLauncher;

using System;
using System.Windows;
using Vantuz.Core;

public class WpfStatusReporter : IStatusReporter
{
    private readonly Action<string, double> _updateUiAction;

    public WpfStatusReporter(Action<string, double> updateUiAction)
    {
        _updateUiAction = updateUiAction;
    }

    public void ReportProgress(string taskName, double percentage)
    {
        Application.Current.Dispatcher.InvokeAsync(() => _updateUiAction(taskName, percentage));
    }

    public void ReportState(string message)
    {
        // Передаем -1, чтобы показать, что это обновление текста, а не прогресс-бара
        Application.Current.Dispatcher.InvokeAsync(() => _updateUiAction(message, -1));
    }
}
