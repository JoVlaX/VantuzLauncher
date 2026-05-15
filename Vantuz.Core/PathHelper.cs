using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;

namespace Vantuz.Core;

public static class PathHelper
{
    public static string CalculateHash(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Обеспечивает безопасный путь с защитой от Path Traversal и поддержкой MAX_PATH на Windows.
    /// </summary>
    public static string GetSafePath(string rootDir, string relativePath)
    {
        string fullRoot = Path.GetFullPath(rootDir);
        string combinedPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));

        // 1. Path Jailing (Защита от выхода за пределы корня)
        if (!combinedPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"Attempted Path Traversal detected: {relativePath}");
        }

        // 2. Создание родительских директорий
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
                // Для сетевых путей используется \\?\UNC\server\share
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
