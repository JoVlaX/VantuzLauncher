#pragma warning disable ARM007 // ExternalAbstraction, not pipeline plugin

namespace Vantuz.Plugins.Minecraft;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Vantuz.Core;

/// <summary>
/// CQRS Query facet of Minecraft game provider.
/// Per INVARIANT_THEORY.md §2.2 — read-only operations only.
/// </summary>
public class MinecraftGameQueryProvider : IGameQueryProvider
{
    public string ProviderName => "Minecraft";

    public Task<VersionCheckResult> CheckVersionAsync(string version, string installDir, CancellationToken ct)
    {
        try
        {
            var path = new MinecraftPath(installDir);
            var versionJsonPath = path.GetVersionJsonPath(version);
            var jsonExists = File.Exists(versionJsonPath);
            bool versionExists;

            if (IsForgeVersion(version))
            {
                var (librariesOk, missingDetail) = VerifyForgeLibraries(version, installDir);
                versionExists = jsonExists && librariesOk;
            }
            else
            {
                var versionJarPath = path.GetVersionJarPath(version);
                var jarExists = File.Exists(versionJarPath);
                versionExists = jsonExists && jarExists;
            }

            return Task.FromResult(new VersionCheckResult(versionExists));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new VersionCheckResult(false, ex.Message));
        }
    }

    public async Task<LaunchParameters> BuildLaunchParametersAsync(
        string version,
        string installDir,
        LaunchOptions options,
        CancellationToken ct)
    {
        var path = new MinecraftPath(installDir);
        var launcher = new MinecraftLauncher(path);

        var session = new MSession
        {
            Username = options.PlayerName,
            UUID = options.Uuid ?? "",
            AccessToken = options.AccessToken ?? "",
            UserType = "mojang"
        };

        var launchOption = new MLaunchOption
        {
            Session = session,
            MaximumRamMb = options.RamMb,
            JavaPath = options.JavaPath ?? "java"
        };

        if (options.ExtraOptions != null)
        {
            if (options.ExtraOptions.TryGetValue("authlibPath", out var authlibPathObj) &&
                options.ExtraOptions.TryGetValue("authlibUrl", out var authlibUrlObj))
            {
                var authlibPath = authlibPathObj?.ToString();
                var authlibUrl = authlibUrlObj?.ToString();

                if (!string.IsNullOrEmpty(authlibPath) && !string.IsNullOrEmpty(authlibUrl))
                {
                    if (!File.Exists(authlibPath))
                    {
                        throw new InvalidOperationException(
                            $"authlib-injector.jar not found at {authlibPath}. " +
                            "Ensure the download step ran before launch.");
                    }

                    launchOption.ExtraJvmArguments = new[]
                    {
                        new MArgument($"-javaagent:{authlibPath}={authlibUrl}")
                    };
                }
            }
        }

        var process = await launcher.BuildProcessAsync(version, launchOption);

        return new LaunchParameters(
            process.StartInfo.FileName,
            process.StartInfo.Arguments,
            process.StartInfo.WorkingDirectory
        );
    }

    internal static (bool AllExist, string? MissingDetail) VerifyForgeLibraries(string version, string installDir)
    {
        var path = new MinecraftPath(installDir);
        var versionJsonPath = path.GetVersionJsonPath(version);
        if (!File.Exists(versionJsonPath))
            return (false, "version JSON missing");

        string jsonText;
        try
        {
            jsonText = File.ReadAllText(versionJsonPath);
        }
        catch (Exception readEx)
        {
            return (false, $"version JSON read error: {readEx.Message}");
        }

        using var json = JsonDocument.Parse(jsonText);

        var requiredLibraries = new[] { "cpw.mods:bootstraplauncher", "cpw.mods:securejarhandler", "net.minecraftforge:fmlloader" };

        if (json.RootElement.TryGetProperty("libraries", out var librariesElement) && librariesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var libName in requiredLibraries)
            {
                bool found = false;
                foreach (var lib in librariesElement.EnumerateArray())
                {
                    if (lib.TryGetProperty("name", out var nameProp) && nameProp.GetString()?.StartsWith(libName + ":", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        if (lib.TryGetProperty("downloads", out var downloads) &&
                            downloads.TryGetProperty("artifact", out var artifact) &&
                            artifact.TryGetProperty("path", out var pathProp))
                        {
                            var artifactPath = pathProp.GetString();
                            if (!string.IsNullOrEmpty(artifactPath))
                            {
                                var fullPath = Path.Combine(installDir, "libraries", artifactPath.Replace('/', Path.DirectorySeparatorChar));
                                if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
                                {
                                    return (false, $"missing or empty library: {libName} at {fullPath}");
                                }
                                found = true;
                                break;
                            }
                        }
                    }
                }
                if (!found)
                {
                    return (false, $"library entry not found in JSON: {libName}");
                }
            }
        }
        else
        {
            return (false, "libraries array missing in version JSON");
        }

        if (json.RootElement.TryGetProperty("inheritsFrom", out var inheritsProp))
        {
            var inheritsFrom = inheritsProp.GetString();
            if (!string.IsNullOrEmpty(inheritsFrom))
            {
                var vanillaJarPath = path.GetVersionJarPath(inheritsFrom);
                if (!File.Exists(vanillaJarPath) || new FileInfo(vanillaJarPath).Length == 0)
                {
                    return (false, $"missing or empty vanilla client JAR: {vanillaJarPath}");
                }
            }
        }
        else
        {
            return (false, "inheritsFrom missing in version JSON");
        }

        return (true, null);
    }

    internal static bool IsForgeVersion(string version)
    {
        return version.Contains("forge", StringComparison.OrdinalIgnoreCase);
    }

    internal static (string McVersion, string ForgeVersion) ParseForgeVersion(string version)
    {
        var idx = version.IndexOf("-forge-", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var mcVersion = version.Substring(0, idx);
            var forgeVersion = version.Substring(idx + "-forge-".Length);
            return (mcVersion, forgeVersion);
        }

        var allParts = version.Split('-');
        if (allParts.Length >= 3)
        {
            return (allParts[0], string.Join("-", allParts.Skip(2)));
        }

        throw new InvalidOperationException($"Невозможно разобрать Forge-версию из строки: {version}");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

#pragma warning restore ARM007
