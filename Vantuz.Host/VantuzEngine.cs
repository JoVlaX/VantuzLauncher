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
/// Согласно .traerules:96-98 и .traerules:169-174.
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
                // 4. Подготовка payload
                var payload = new Dictionary<string, object>();
                if (manifest.Variables != null)
                {
                    foreach (var kvp in manifest.Variables) payload[kvp.Key] = kvp.Value;
                }
                if (initialPayload != null)
                {
                    foreach (var kvp in initialPayload) payload[kvp.Key] = kvp.Value;
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
