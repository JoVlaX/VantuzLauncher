namespace Vantuz.Core;

/// <summary>
/// Global report hub that bridges host-side ConsoleReporter to plugin-side GUI.
/// Both host and plugin share Vantuz.Core in the default AssemblyLoadContext,
/// so events cross the boundary without coupling.
/// </summary>
/// F_doc: {ReportHub returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportHub behavior
public static class ReportHub
{
    public static event Action<string>? StateReported;
    public static event Action<string, double>? ProgressReported;
/// F_doc: {ReportState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportState behavior

    public static void ReportState(string message)
    {
        StateReported?.Invoke(message);
    }
/// F_doc: {ReportProgress returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportProgress behavior

    public static void ReportProgress(string taskName, double percentage)
    {
        ProgressReported?.Invoke(taskName, percentage);
    }
}
