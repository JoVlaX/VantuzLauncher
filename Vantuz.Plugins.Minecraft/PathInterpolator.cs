#pragma warning disable ARM007

namespace Vantuz.Plugins.Minecraft;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// SRP: Single Responsibility - Variable interpolation for paths only.
/// Per INVARIANT_THEORY.md §498 Explicitness - no hidden logic, pure function.
/// </summary>
public static class PathInterpolator
{
    /// <summary>
    /// Interpolates path variables like {{mcDir}}, ${special:ApplicationData}
    /// Per INVARIANT_THEORY.md §3.2 Nomadic - variables travel with manifest
    /// </summary>
    public static string Interpolate(string path, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(path)) return path;
        
        var result = path;
        
        // Replace {{variable}} patterns from manifest
        foreach (var kvp in variables)
        {
            result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        }
        
        // Replace ${special:...} system variables for backward compatibility
        result = ReplaceSpecialFolders(result);
        
        return Path.GetFullPath(result);
    }
    
    private static string ReplaceSpecialFolders(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        
        // Per INVARIANT_THEORY.md §3.2 - these are fallbacks only
        // Primary resolution should come from manifest variables
        if (path.Contains("${special:ApplicationData}"))
        {
            path = path.Replace("${special:ApplicationData}", 
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        }
        
        if (path.Contains("${special:LocalApplicationData}"))
        {
            path = path.Replace("${special:LocalApplicationData}", 
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }
        
        if (path.Contains("${special:UserProfile}"))
        {
            path = path.Replace("${special:UserProfile}", 
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }
        
        return path;
    }
}

#pragma warning restore ARM007
