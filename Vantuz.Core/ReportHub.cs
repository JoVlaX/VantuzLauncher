namespace Vantuz.Core;

/// <summary>
/// Global report hub that bridges host-side ConsoleReporter to plugin-side GUI.
/// Both host and plugin share Vantuz.Core in the default AssemblyLoadContext,
/// so events cross the boundary without coupling.
/// </summary>
public static class ReportHub
{
    public static event Action<string>? StateReported;
    public static event Action<string, double>? ProgressReported;

    public static void ReportState(string message)
    {
        StateReported?.Invoke(message);
    }

    public static void ReportProgress(string taskName, double percentage)
    {
        ProgressReported?.Invoke(taskName, percentage);
    }
}
