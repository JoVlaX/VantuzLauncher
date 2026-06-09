namespace Vantuz.Plugins.Net;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;

/// <summary>
/// ARM005 CQRS Query: Р—Р°РіСЂСѓР·РєР° РјР°РЅРёС„РµСЃС‚Р° РјРѕРґРїР°РєР° Рё РєРѕРЅРІРµСЂС‚Р°С†РёСЏ РІ TargetState.
/// Per Armatura:126 - no external dependencies in domain types.
/// </summary>
public class ModpackManifestQuery : IQueryPlugin
{
    public string Name => "Net.ModpackManifestQuery";
    private readonly HttpClient _httpClient;

    public ModpackManifestQuery()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "VantuzLauncher-ModpackManifest/2.0");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<object?> ExecuteAsync(QueryContext context, JsonElement stepConfig)
    {
        // Get URL from config per Armatura:44-45
        string manifestUrl = stepConfig.TryGetProperty("url", out var url)
            ? Interpolate(url.GetString() ?? "", context)
            : throw new InvalidOperationException("url is missing in stepConfig");

        string installDir = stepConfig.TryGetProperty("installDir", out var id)
            ? Interpolate(id.GetString() ?? "", context)
            : throw new InvalidOperationException("installDir is missing in stepConfig");

        bool ignoreSslErrors = stepConfig.TryGetProperty("ignoreSslErrors", out var sslProp) && sslProp.GetBoolean();
        installDir = Path.GetFullPath(installDir.Replace('/', Path.DirectorySeparatorChar));

        context.Reporter.ReportState("Р—Р°РіСЂСѓР·РєР° РјР°РЅРёС„РµСЃС‚Р° РјРѕРґРїР°РєР°...");

        try
        {
            using var handler = new HttpClientHandler();
            if (ignoreSslErrors)
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;
            }

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "VantuzLauncher-ModpackManifest/2.0");

            // Anti-cache
            manifestUrl = manifestUrl.Contains('?') 
                ? $"{manifestUrl}&t={DateTime.UtcNow.Ticks}" 
                : $"{manifestUrl}?t={DateTime.UtcNow.Ticks}";

            using var response = await httpClient.GetAsync(manifestUrl, context.CancellationToken);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(context.CancellationToken);
            var manifest = JsonSerializer.Deserialize<ModpackManifest>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (manifest == null)
            {
                throw new InvalidOperationException("Failed to parse modpack manifest");
            }

            context.Reporter.ReportState($"РњР°РЅРёС„РµСЃС‚ Р·Р°РіСЂСѓР¶РµРЅ: {manifest.Version}, {manifest.Files?.Count ?? 0} С„Р°Р№Р»РѕРІ");

            // Convert manifest files to FileState for DeltaAnalyzer
            var targetState = new List<FileState>();
            if (manifest.Files != null)
            {
                foreach (var file in manifest.Files)
                {
                    targetState.Add(new FileState(
                        file.Path,
                        file.Hash,
                        file.Size,
                        file.Url
                    ));
                }
            }

            // Build result
            var result = new ModpackManifestResult
            {
                Version = manifest.Version,
                MinecraftVersion = manifest.Minecraft,
                Files = targetState,
                RemovedFiles = manifest.RemovedFiles ?? new List<string>(),
                RawManifest = manifest
            };

            // Set TargetState for downstream OS.DeltaAnalyzer
            // Note: This requires the engine to support setting payload values from Query results
            // Alternatively, the engine should expose a way to pass Query results to next steps

            return result;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to download modpack manifest: {ex.Message}");
        }
    }

    private static string Interpolate(string text, QueryContext context)
    {
        if (string.IsNullOrEmpty(text)) return text;
        foreach (var kvp in context.Payload)
        {
            text = text.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "");
        }
        return text;
    }
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

    public ValueTask DisposeAsync()
    {
        _httpClient?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Structure of modpack.json from server
/// </summary>
/// F_doc: {ModpackManifest returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ModpackManifest behavior
public class ModpackManifest
{
    /// F_doc: {Version returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Version behavior
    public string Version { get; set; } = "";
    /// F_doc: {Minecraft returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Minecraft behavior
    public string Minecraft { get; set; } = "";
    public List<ModpackFile>? Files { get; set; }
    public List<string>? RemovedFiles { get; set; }
}
/// F_doc: {ModpackFile returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ModpackFile behavior

public class ModpackFile
{
    /// F_doc: {Path returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Path behavior
    public string Path { get; set; } = "";
    /// F_doc: {Hash returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Hash behavior
    public string Hash { get; set; } = "";
    /// F_doc: {Size returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Size behavior
    public long Size { get; set; }
    /// F_doc: {Url returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Url behavior
    public string Url { get; set; } = "";
}

// Note: ModpackManifestResult is defined in Vantuz.Core for cross-plugin compatibility
