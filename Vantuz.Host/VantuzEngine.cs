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

public record BootManifest(Dictionary<string, string>? Variables, Dictionary<string, string> Plugins, List<StepConfig> Pipeline);
public record StepConfig(string PluginName, JsonElement Config);

/// <summary>
/// VantuzEngine с поддержкой QuantizedNode (квантованного выполнения).
/// Согласно Armatura:96-98 и .traerules:169-174.
/// </summary>
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
    /// Запускает pipeline с квантованным выполнением (QuantizedNode).
    /// Согласно .traerules:98 - единственный метод запуска.
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

            // 1. Валидация хэшей безопасности
            ValidateManifestHashes(manifest.Plugins);

            // 2. Загрузка плагинов
            string[] shared = new[]
            {
                typeof(QuantizedNode).Assembly.GetName().Name!
            };
            var loader = new PluginLoader(shared);
            var allowedDlls = manifest.Plugins.Keys.ToList();

            // 3. Загрузка QuantizedNode (включая CQRS плагины через адаптеры)
            var quantizedNodes = loader.LoadQuantizedNodesFromDirectory(_pluginsFolder, allowedDlls).ToList();
            var cqrsNodes = loader.LoadCqrsPluginsFromDirectory(_pluginsFolder, allowedDlls).ToList();
            quantizedNodes.AddRange(cqrsNodes);

            try
            {
                // 4. Подготовка payload с интерполяцией переменных
                var payload = new Dictionary<string, object>();

                // Сначала добавляем initialPayload (runtime значения имеют приоритет)
                if (initialPayload != null)
                {
                    foreach (var kvp in initialPayload) payload[kvp.Key] = kvp.Value;
                }

                // Интерполируем переменные из manifest используя payload
                if (manifest.Variables != null)
                {
                    var interpolatedVars = InterpolateVariables(manifest.Variables, payload);
                    foreach (var kvp in interpolatedVars) payload[kvp.Key] = kvp.Value;
                }

                string exeName = Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "VantuzLauncher.exe");
                payload["hostExecutable"] = exeName;

                // 5. Выполнение через QuantumScheduler
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
            // Пропускаем валидацию если хэш пустой (dev-режим)
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
    /// Интерполирует переменные вида {{key}} используя значения из payload.
    /// Поддерживает ${env:VAR} и ${special:Folder} для Nomadic конфигурации.
    /// Согласно Armatura:42 (Explicit Input Payloads) и :65 (No hardcoded paths).
    /// </summary>
    internal static Dictionary<string, string> InterpolateVariables(
        Dictionary<string, string> variables,
        Dictionary<string, object> payload)
    {
        var result = new Dictionary<string, string>();

        foreach (var kvp in variables)
        {
            string value = kvp.Value;

            // 1. Заменяем ${env:VAR} на значения переменных окружения
            value = InterpolateEnvironmentVariables(value);

            // 2. Заменяем ${special:Folder} на пути SpecialFolder
            value = InterpolateSpecialFolders(value);

            // 3. Заменяем все placeholder-ы {{key}} на значения из payload
            foreach (var payloadKvp in payload)
            {
                string placeholder = "{{" + payloadKvp.Key + "}}";
                if (value.Contains(placeholder))
                {
                    string replacement = payloadKvp.Value?.ToString() ?? string.Empty;
                    value = value.Replace(placeholder, replacement);
                }
            }

            // 4. Заменяем placeholder-ы на уже интерполированные переменные (зависимости вида A → B)
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
    /// Заменяет ${env:VAR} на значение переменной окружения.
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
    /// Заменяет ${special:Folder} на путь SpecialFolder.
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
    /// Собирает pipeline из QuantizedNode.
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
/// Результат квантованного выполнения
/// </summary>
public readonly record struct QuantumExecutionResult
{
    public bool Success { get; init; }
    public IReadOnlyDictionary<string, object>? Payload { get; init; }
    public string? ErrorMessage { get; init; }
}
