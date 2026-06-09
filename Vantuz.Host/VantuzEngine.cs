namespace Vantuz.Host;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;
/// F_doc: {BootManifest returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies BootManifest behavior

public record BootManifest(Dictionary<string, string>? Variables, Dictionary<string, string> Plugins, List<StepConfig> Pipeline);
/// F_doc: {StepConfig returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies StepConfig behavior
public record StepConfig(string PluginName, JsonElement Config);

/// <summary>
/// VantuzEngine СЃ РїРѕРґРґРµСЂР¶РєРѕР№ QuantizedNode (РєРІР°РЅС‚РѕРІР°РЅРЅРѕРіРѕ РІС‹РїРѕР»РЅРµРЅРёСЏ).
/// РЎРѕРіР»Р°СЃРЅРѕ Armatura:96-98 Рё .traerules:169-174.
/// </summary>
/// F_doc: {VantuzEngine returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies VantuzEngine behavior
public class VantuzEngine
{
    private readonly string _pluginsFolder;
    private readonly IStatusReporter _reporter;
    private readonly string _crashLogPath;

    public VantuzEngine(string pluginsFolder, IStatusReporter reporter, string crashLogPath)
    {
        _pluginsFolder = pluginsFolder;
        _reporter = reporter;
        _crashLogPath = crashLogPath;
    }

    /// <summary>
    /// Р—Р°РїСѓСЃРєР°РµС‚ pipeline СЃ РєРІР°РЅС‚РѕРІР°РЅРЅС‹Рј РІС‹РїРѕР»РЅРµРЅРёРµРј (QuantizedNode).
    /// РЎРѕРіР»Р°СЃРЅРѕ .traerules:98 - РµРґРёРЅСЃС‚РІРµРЅРЅС‹Р№ РјРµС‚РѕРґ Р·Р°РїСѓСЃРєР°.
    /// </summary>
    public async Task<QuantumExecutionResult> RunAsync(
        string bootJsonPath,
        CancellationToken cancellationToken,
        IDictionary<string, object>? initialPayload = null)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<BootManifest>(
                await File.ReadAllTextAsync(bootJsonPath, cancellationToken),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new Exception("Invalid boot.json");

            // 1. Р’Р°Р»РёРґР°С†РёСЏ С…СЌС€РµР№ Р±РµР·РѕРїР°СЃРЅРѕСЃС‚Рё
            ValidateManifestHashes(manifest.Plugins);

            // 2. Р—Р°РіСЂСѓР·РєР° РїР»Р°РіРёРЅРѕРІ
            string[] shared = new[]
            {
                typeof(QuantizedNode).Assembly.GetName().Name!
            };
            var loader = new PluginLoader(shared);
            var allowedDlls = manifest.Plugins.Keys.ToList();

            // 3. Р—Р°РіСЂСѓР·РєР° QuantizedNode (РІРєР»СЋС‡Р°СЏ CQRS РїР»Р°РіРёРЅС‹ С‡РµСЂРµР· Р°РґР°РїС‚РµСЂС‹)
            var quantizedNodes = loader.LoadQuantizedNodesFromDirectory(_pluginsFolder, allowedDlls).ToList();
            var cqrsNodes = loader.LoadCqrsPluginsFromDirectory(_pluginsFolder, allowedDlls).ToList();
            quantizedNodes.AddRange(cqrsNodes);

            try
            {
                // 4. РџРѕРґРіРѕС‚РѕРІРєР° payload СЃ РёРЅС‚РµСЂРїРѕР»СЏС†РёРµР№ РїРµСЂРµРјРµРЅРЅС‹С…
                var payload = new Dictionary<string, object>();

                // РЎРЅР°С‡Р°Р»Р° РґРѕР±Р°РІР»СЏРµРј initialPayload (runtime Р·РЅР°С‡РµРЅРёСЏ РёРјРµСЋС‚ РїСЂРёРѕСЂРёС‚РµС‚)
                if (initialPayload != null)
                {
                    foreach (var kvp in initialPayload) payload[kvp.Key] = kvp.Value;
                }

                // РРЅС‚РµСЂРїРѕР»РёСЂСѓРµРј РїРµСЂРµРјРµРЅРЅС‹Рµ РёР· manifest РёСЃРїРѕР»СЊР·СѓСЏ payload
                if (manifest.Variables != null)
                {
                    var interpolatedVars = InterpolateVariables(manifest.Variables, payload);
                    foreach (var kvp in interpolatedVars) payload[kvp.Key] = kvp.Value;
                }

                string exeName = Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "VantuzLauncher.exe");
                payload["hostExecutable"] = exeName;

                // 5. Р’С‹РїРѕР»РЅРµРЅРёРµ С‡РµСЂРµР· QuantumScheduler
                var scheduler = new QuantumScheduler(_reporter, payload);
                var pipeline = BuildQuantumPipeline(manifest.Pipeline, quantizedNodes);

                var result = await scheduler.ExecutePipelineAsync(pipeline, cancellationToken);

                return new QuantumExecutionResult
                {
                    Success = result.IsSuccess,
                    Payload = result.FinalPayload,
                    ErrorMessage = result.ErrorMessage
                };
            }
            finally
            {
                foreach (var node in quantizedNodes) await node.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            string errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRITICAL SYSTEM CRASH\n" +
                                  $"Message: {ex.Message}\nStackTrace:\n{ex.StackTrace}\n" +
                                  $"InnerException: {ex.InnerException?.Message}\n" + new string('-', 50) + "\n";
            File.AppendAllText(_crashLogPath, errorMessage);
            throw;
        }
    }

    private void ValidateManifestHashes(Dictionary<string, string> pluginsConfig)
    {
        foreach (var (dllName, expectedHash) in pluginsConfig)
        {
            // РџСЂРѕРїСѓСЃРєР°РµРј РІР°Р»РёРґР°С†РёСЋ РµСЃР»Рё С…СЌС€ РїСѓСЃС‚РѕР№ (dev-СЂРµР¶РёРј)
            if (string.IsNullOrWhiteSpace(expectedHash))
                continue;

            string fullPath = Path.Combine(_pluginsFolder, Path.GetFileName(dllName));
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"Plugin not found: {fullPath}");

            using var fs = File.OpenRead(fullPath);
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(fs);
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            if (actualHash != expectedHash.ToLowerInvariant())
                throw new Exception($"HASH MISMATCH for {dllName}");
        }
    }

    /// <summary>
    /// РРЅС‚РµСЂРїРѕР»РёСЂСѓРµС‚ РїРµСЂРµРјРµРЅРЅС‹Рµ РІРёРґР° {{key}} РёСЃРїРѕР»СЊР·СѓСЏ Р·РЅР°С‡РµРЅРёСЏ РёР· payload.
    /// РџРѕРґРґРµСЂР¶РёРІР°РµС‚ ${env:VAR} Рё ${special:Folder} РґР»СЏ Nomadic РєРѕРЅС„РёРіСѓСЂР°С†РёРё.
    /// РЎРѕРіР»Р°СЃРЅРѕ Armatura:42 (Explicit Input Payloads) Рё :65 (No hardcoded paths).
    /// </summary>
    internal static Dictionary<string, string> InterpolateVariables(
        Dictionary<string, string> variables,
        Dictionary<string, object> payload)
    {
        var result = new Dictionary<string, string>();

        foreach (var kvp in variables)
        {
            string value = kvp.Value;

            // 1. Р—Р°РјРµРЅСЏРµРј ${env:VAR} РЅР° Р·РЅР°С‡РµРЅРёСЏ РїРµСЂРµРјРµРЅРЅС‹С… РѕРєСЂСѓР¶РµРЅРёСЏ
            value = InterpolateEnvironmentVariables(value);

            // 2. Р—Р°РјРµРЅСЏРµРј ${special:Folder} РЅР° РїСѓС‚Рё SpecialFolder
            value = InterpolateSpecialFolders(value);

            // 3. Р—Р°РјРµРЅСЏРµРј РІСЃРµ placeholder-С‹ {{key}} РЅР° Р·РЅР°С‡РµРЅРёСЏ РёР· payload
            foreach (var payloadKvp in payload)
            {
                string placeholder = "{{" + payloadKvp.Key + "}}";
                if (value.Contains(placeholder))
                {
                    string replacement = payloadKvp.Value?.ToString() ?? string.Empty;
                    value = value.Replace(placeholder, replacement);
                }
            }

            // 4. Р—Р°РјРµРЅСЏРµРј placeholder-С‹ РЅР° СѓР¶Рµ РёРЅС‚РµСЂРїРѕР»РёСЂРѕРІР°РЅРЅС‹Рµ РїРµСЂРµРјРµРЅРЅС‹Рµ (Р·Р°РІРёСЃРёРјРѕСЃС‚Рё РІРёРґР° A в†’ B)
            foreach (var resultKvp in result)
            {
                string placeholder = "{{" + resultKvp.Key + "}}";
                if (value.Contains(placeholder))
                {
                    string replacement = resultKvp.Value?.ToString() ?? string.Empty;
                    value = value.Replace(placeholder, replacement);
                }
            }

            result[kvp.Key] = value;
        }

        return result;
    }

    /// <summary>
    /// Р—Р°РјРµРЅСЏРµС‚ ${env:VAR} РЅР° Р·РЅР°С‡РµРЅРёРµ РїРµСЂРµРјРµРЅРЅРѕР№ РѕРєСЂСѓР¶РµРЅРёСЏ.
    /// </summary>
    private static string InterpolateEnvironmentVariables(string value)
    {
        int startIndex = 0;
        while (true)
        {
            int envStart = value.IndexOf("${env:", startIndex);
            if (envStart == -1) break;

            int envEnd = value.IndexOf("}", envStart);
            if (envEnd == -1) break;

            string varName = value.Substring(envStart + 6, envEnd - envStart - 6);
            string varValue = Environment.GetEnvironmentVariable(varName) ?? string.Empty;

            value = value.Substring(0, envStart) + varValue + value.Substring(envEnd + 1);
            startIndex = envStart + varValue.Length;
        }
        return value;
    }

    /// <summary>
    /// Р—Р°РјРµРЅСЏРµС‚ ${special:Folder} РЅР° РїСѓС‚СЊ SpecialFolder.
    /// </summary>
    private static string InterpolateSpecialFolders(string value)
    {
        var specialFolders = new Dictionary<string, Environment.SpecialFolder>(StringComparer.OrdinalIgnoreCase)
        {
            ["ApplicationData"] = Environment.SpecialFolder.ApplicationData,
            ["LocalApplicationData"] = Environment.SpecialFolder.LocalApplicationData,
            ["UserProfile"] = Environment.SpecialFolder.UserProfile,
            ["MyDocuments"] = Environment.SpecialFolder.MyDocuments,
            ["Desktop"] = Environment.SpecialFolder.Desktop,
            ["ProgramFiles"] = Environment.SpecialFolder.ProgramFiles,
            ["ProgramFilesX86"] = Environment.SpecialFolder.ProgramFilesX86,
            ["System"] = Environment.SpecialFolder.System,
            ["Windows"] = Environment.SpecialFolder.Windows
        };

        int startIndex = 0;
        while (true)
        {
            int specialStart = value.IndexOf("${special:", startIndex);
            if (specialStart == -1) break;

            int specialEnd = value.IndexOf("}", specialStart);
            if (specialEnd == -1) break;

            string folderName = value.Substring(specialStart + 10, specialEnd - specialStart - 10);
            string folderPath = specialFolders.TryGetValue(folderName, out var folder)
                ? Environment.GetFolderPath(folder)
                : string.Empty;

            value = value.Substring(0, specialStart) + folderPath + value.Substring(specialEnd + 1);
            startIndex = specialStart + folderPath.Length;
        }
        return value;
    }

    /// <summary>
    /// РЎРѕР±РёСЂР°РµС‚ pipeline РёР· QuantizedNode.
    /// </summary>
    private List<(QuantizedNode Node, JsonElement Config)> BuildQuantumPipeline(
        List<StepConfig> steps,
        List<QuantizedNode> quantizedNodes)
    {
        var result = new List<(QuantizedNode, JsonElement)>();

        foreach (var step in steps)
        {
            var node = quantizedNodes.FirstOrDefault(n => n.Name == step.PluginName);
            if (node != null)
            {
                result.Add((node, step.Config));
                continue;
            }

            throw new Exception($"Plugin {step.PluginName} not found");
        }

        return result;
    }
}

/// <summary>
/// Р РµР·СѓР»СЊС‚Р°С‚ РєРІР°РЅС‚РѕРІР°РЅРЅРѕРіРѕ РІС‹РїРѕР»РЅРµРЅРёСЏ
/// </summary>
public readonly record struct QuantumExecutionResult
{
    /// F_doc: {Success returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Success behavior
    public bool Success { get; init; }
    public IReadOnlyDictionary<string, object>? Payload { get; init; }
    public string? ErrorMessage { get; init; }
}
