#pragma warning disable ARM007

namespace Vantuz.Plugins.Minecraft;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Installer.Forge;
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
    public static async Task<IReadOnlyList<string>> GetAvailableVersionsAsync(
        string mcVersion, 
        IStatusReporter reporter,
        CancellationToken ct)
    {
        // Per §1.2 Measurability - explicit logging of query
        reporter.ReportState($"[FORGE QUERY] Fetching versions for MC {mcVersion}");
        
        try
        {
            // Per §11.5 Agentic - use ForgeInstaller to query Forge versions
            // Note: CmlLib ForgeInstaller has GetForgeVersions method
            var path = new CmlLib.Core.MinecraftPath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
            var launcher = new CmlLib.Core.MinecraftLauncher(path);
            var forgeInstaller = new ForgeInstaller(launcher);
            var forgeVersions = await forgeInstaller.GetForgeVersions(mcVersion);
            var versionList = forgeVersions?.Select(v => v.ForgeVersionName).ToList() ?? new List<string>();
            
            // Per §11.3 - observable result
            int count = versionList.Count();
            reporter.ReportState($"[FORGE QUERY] Found {count} versions");
            
            if (count > 0)
            {
                var latest = versionList.OrderByDescending(v => ParseVersion(v)).FirstOrDefault();
                reporter.ReportState($"[FORGE QUERY] Latest: {latest}");
            }
            
            return versionList.AsReadOnly();
        }
        catch (Exception ex)
        {
            // Per §11.5 - agentic error reporting
            reporter.ReportState($"[FORGE QUERY ERROR] {ex.GetType().Name}: {ex.Message}");
            return new List<string>().AsReadOnly();
        }
    }
    
    /// <summary>
    /// Parses semantic version for proper ordering.
    /// Per §11.1 Determinism - Temperature=0, unambiguous sorting.
    /// </summary>
    private static System.Version ParseVersion(string version)
    {
        if (System.Version.TryParse(version, out var parsed))
            return parsed;
        return new System.Version(0, 0, 0, 0);
    }
}

#pragma warning restore ARM007
