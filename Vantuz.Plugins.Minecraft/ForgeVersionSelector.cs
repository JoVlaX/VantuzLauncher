#pragma warning disable ARM007

namespace Vantuz.Plugins.Minecraft;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// SRP: Single Responsibility - Select Forge version based on policy.
/// Per INVARIANT_THEORY.md §2.2 CQRS - Command/Decision, no queries.
/// Per INVARIANT_THEORY.md §11.1 Determinism - Temperature=0, unambiguous selection.
/// Per INVARIANT_THEORY.md §11.4 Context Evolution - explicit fallback with logging.
/// </summary>
public static class ForgeVersionSelector
{
    /// <summary>
    /// Selects Forge version using deterministic policy:
    /// 1. Use requested if available
    /// 2. Fallback to latest by semantic version (max)
    /// Per §11.5 Agentic - explicit decision rule, no ambiguity.
    /// </summary>
    public static string SelectVersion(
        string requestedVersion,
        IReadOnlyList<string> availableVersions,
        IStatusReporter reporter)
    {
        // Per §11.3 - explicit validation
        if (availableVersions == null || availableVersions.Count == 0)
        {
            reporter.ReportState($"[FORGE SELECT ERROR] No versions available");
            throw new InvalidOperationException("No Forge versions available for selection");
        }
        
        // Per §11.1 - Temperature=0: check if requested exists
        if (availableVersions.Contains(requestedVersion))
        {
            reporter.ReportState($"[FORGE SELECT] Using requested: {requestedVersion}");
            return requestedVersion;
        }
        
        // Per §11.1 - Deterministic fallback: max by semantic version
        var latest = availableVersions
            .Select(v => new { Version = v, Parsed = ParseSemanticVersion(v) })
            .OrderByDescending(x => x.Parsed)
            .First();
            
        // Per §11.4 - explicit evolution logging
        reporter.ReportState($"[FORGE SELECT] Requested {requestedVersion} unavailable");
        reporter.ReportState($"[FORGE SELECT] Available versions: {string.Join(", ", availableVersions.Take(5))}");
        reporter.ReportState($"[FORGE SELECT] Fallback to latest: {latest.Version} (per §11.1 Determinism)");
        
        return latest.Version;
    }
    
    /// <summary>
    /// Parses semantic version for deterministic comparison.
    /// Per §11.1 - single unambiguous interpretation.
    /// </summary>
    private static (int Major, int Minor, int Build, int Revision) ParseSemanticVersion(string version)
    {
        try
        {
            var parts = version.Split('.');
            var major = int.TryParse(parts.ElementAtOrDefault(0), out var m) ? m : 0;
            var minor = int.TryParse(parts.ElementAtOrDefault(1), out var mi) ? mi : 0;
            var build = int.TryParse(parts.ElementAtOrDefault(2), out var b) ? b : 0;
            var revision = int.TryParse(parts.ElementAtOrDefault(3), out var r) ? r : 0;
            return (major, minor, build, revision);
        }
        catch
        {
            return (0, 0, 0, 0);
        }
    }
}

#pragma warning restore ARM007
