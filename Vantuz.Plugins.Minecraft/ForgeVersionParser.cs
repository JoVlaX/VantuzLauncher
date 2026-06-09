#pragma warning disable ARM007

namespace Vantuz.Plugins.Minecraft;

using System;

/// <summary>
/// Immutable record representing parsed Forge version.
/// Per INVARIANT_THEORY.md В§1.1 Determinism - parsing is deterministic.
/// F_doc: {parsing empty string or non-semver input returns invalid version}
/// E_doc: Unit test with null, empty, and malformed version strings
/// </summary>
public readonly record struct ForgeVersion(string MinecraftVersion, string ForgeVersionNumber)
{
    /// <summary>
    /// Returns true if this is a valid Forge version (both parts non-empty)
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(MinecraftVersion) && !string.IsNullOrEmpty(ForgeVersionNumber);
    
    /// <summary>
    /// Returns the full version string: 1.20.1-forge-47.2.20
    /// </summary>
    public override string ToString() => $"{MinecraftVersion}-forge-{ForgeVersionNumber}";
}

/// <summary>
/// SRP: Single Responsibility - Parse Forge version strings only.
/// Per INVARIANT_THEORY.md В§498 Explicitness - no side effects, pure function.
/// </summary>
public static class ForgeVersionParser
{
    /// <summary>
    /// Detects and parses Forge version format: 1.20.1-forge-47.2.20
    /// Per INVARIANT_THEORY.md В§1.2 Measurability - returns struct with all data
    /// </summary>
    /// <param name="version">Version string to parse</param>
    /// <returns>ForgeVersion struct (check IsValid)</returns>
    /// F_doc: {Parse returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Parse behavior
    public static ForgeVersion Parse(string version)
    {
        if (string.IsNullOrEmpty(version))
            return default;
        
        // Format: 1.20.1-forge-47.2.20
        var parts = version.Split("-forge-", System.StringSplitOptions.None);
        
        if (parts.Length == 2)
        {
            var mcVersion = parts[0];
            var forgeVersion = parts[1];
            
            // Validate non-empty
            if (!string.IsNullOrWhiteSpace(mcVersion) && !string.IsNullOrWhiteSpace(forgeVersion))
            {
                return new ForgeVersion(mcVersion, forgeVersion);
            }
        }
        
        return default;
    }
    
    /// <summary>
    /// Quick check if string matches Forge version format
    /// </summary>
    /// F_doc: {IsForgeVersion returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies IsForgeVersion behavior
    public static bool IsForgeVersion(string version)
    {
        return !string.IsNullOrEmpty(version) && version.Contains("-forge-");
    }
}

#pragma warning restore ARM007
