using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;

namespace Vantuz.Core;
/// F_doc: {PathHelper returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies PathHelper behavior

public static class PathHelper
{
    /// F_doc: {CalculateHash returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies CalculateHash behavior
    public static string CalculateHash(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// РћР±РµСЃРїРµС‡РёРІР°РµС‚ Р±РµР·РѕРїР°СЃРЅС‹Р№ РїСѓС‚СЊ СЃ Р·Р°С‰РёС‚РѕР№ РѕС‚ Path Traversal Рё РїРѕРґРґРµСЂР¶РєРѕР№ MAX_PATH РЅР° Windows.
    /// </summary>
    /// F_doc: {GetSafePath returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies GetSafePath behavior
    public static string GetSafePath(string rootDir, string relativePath)
    {
        string fullRoot = Path.GetFullPath(rootDir);
        string combinedPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));

        // 1. Path Jailing (Р—Р°С‰РёС‚Р° РѕС‚ РІС‹С…РѕРґР° Р·Р° РїСЂРµРґРµР»С‹ РєРѕСЂРЅСЏ)
        if (!combinedPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"Attempted Path Traversal detected: {relativePath}");
        }

        // 2. РЎРѕР·РґР°РЅРёРµ СЂРѕРґРёС‚РµР»СЊСЃРєРёС… РґРёСЂРµРєС‚РѕСЂРёР№
        string? parentDir = Path.GetDirectoryName(combinedPath);
        if (parentDir != null && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        // 3. MAX_PATH Bypass for Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!combinedPath.StartsWith(@"\\?\"))
            {
                // Р”Р»СЏ СЃРµС‚РµРІС‹С… РїСѓС‚РµР№ РёСЃРїРѕР»СЊР·СѓРµС‚СЃСЏ \\?\UNC\server\share
                if (combinedPath.StartsWith(@"\\"))
                {
                    combinedPath = @"\\?\UNC\" + combinedPath.Substring(2);
                }
                else
                {
                    combinedPath = @"\\?\" + combinedPath;
                }
            }
        }

        return combinedPath;
    }
}
