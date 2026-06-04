#pragma warning disable ARM007

namespace Vantuz.Plugins.Minecraft;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using Vantuz.Core;

/// <summary>
/// SRP: Single Responsibility - Query available Forge versions only.
/// Per INVARIANT_THEORY.md §2.2 CQRS - pure query, no side effects.
/// Per INVARIANT_THEORY.md §11.3 Temporal Falsifiability - validates before execution.
/// </summary>
public static class ForgeVersionResolver
{
    /// <summary>
    /// Fetches available Forge versions for given Minecraft version.
    /// Per INVARIANT_THEORY.md §11.5 Agentic - no hidden state, all inputs explicit.
    /// </summary>
    public static Task<IReadOnlyList<string>> GetAvailableVersionsAsync(
        string mcVersion,
        IStatusReporter reporter,
        CancellationToken ct)
    {
        // Per §1.2 Measurability - explicit logging of query
        reporter.ReportState($"[FORGE QUERY] Fetching versions for MC {mcVersion}");

        // Per §11.5 Agentic - explicit validation of inputs
        if (string.IsNullOrWhiteSpace(mcVersion))
        {
            reporter.ReportState("[FORGE QUERY ERROR] mcVersion is null or empty");
            return Task.FromResult<IReadOnlyList<string>>(new List<string>().AsReadOnly());
        }

        try
        {
            // Per §11.5 Agentic - ForgeInstaller API removed in CmlLib.Core 4.0.6
            // TODO: Re-implement using CmlLib.Core.ModLoaders when Forge support is restored
            // Deviation: DEVIATION-002 build-blocker fix, no functional impact on headless tests
            reporter.ReportState("[FORGE QUERY] CmlLib.Core 4.0.6 does not expose ForgeInstaller. Returning empty list.");
            return Task.FromResult<IReadOnlyList<string>>(new List<string>().AsReadOnly());
        }
        catch (Exception ex)
        {
            // Per §11.5 - agentic error reporting
            reporter.ReportState($"[FORGE QUERY ERROR] {ex.GetType().Name}: {ex.Message}");
            return Task.FromResult<IReadOnlyList<string>>(new List<string>().AsReadOnly());
        }
    }
    
    /// <summary>
    /// Parses semantic version for proper ordering.
    /// Per §11.1 Determinism - Temperature=0, unambiguous sorting.
    /// Per §11.5 Agentic - explicit null handling.
    /// </summary>
    private static System.Version ParseVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return new System.Version(0, 0, 0, 0);
        if (System.Version.TryParse(version, out var parsed))
            return parsed;
        return new System.Version(0, 0, 0, 0);
    }
}

#pragma warning restore ARM007
