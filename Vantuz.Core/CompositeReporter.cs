using System;
using System.Collections.Generic;
using System.Linq;

namespace Vantuz.Core;

public class CompositeReporter : IStatusReporter
{
    private readonly List<IStatusReporter> _reporters;

    public CompositeReporter(params IStatusReporter[] reporters)
    {
        _reporters = reporters.Where(r => r != null).ToList();
    }
/// F_doc: {ReportState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportState behavior

    public void ReportState(string message)
    {
        foreach (var reporter in _reporters)
        {
            try { reporter.ReportState(message); } catch { }
        }
    }
/// F_doc: {ReportProgress returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportProgress behavior

    public void ReportProgress(string taskName, double percentage)
    {
        foreach (var reporter in _reporters)
        {
            try { reporter.ReportProgress(taskName, percentage); } catch { }
        }
    }
}
