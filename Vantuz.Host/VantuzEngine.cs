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

    public async Task<Vantuz.Core.ExecutionContext> RunAsync(string bootJsonPath, CancellationToken cancellationToken, IDictionary<string, object>? initialPayload = null) 
    { 
        try 
        { 
            var manifest = JsonSerializer.Deserialize<BootManifest>( 
                await File.ReadAllTextAsync(bootJsonPath, cancellationToken), 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                ?? throw new Exception("Invalid boot.json"); 

            // 1. Валидация хэшей безопасности 
            ValidateManifestHashes(manifest.Plugins); 

            // 2. Изолированная загрузка плагинов через новый провайдер 
            string[] shared = new[] { typeof(IVantuzPlugin).Assembly.GetName().Name! }; 
            var loader = new PluginLoader(shared); 
            var allowedDlls = manifest.Plugins.Keys.ToList();
            var loadedPlugins = loader.LoadPluginsFromDirectory(_pluginsFolder, allowedDlls).ToList();

            // 2.5 Загрузка CQRS плагинов (ICommandPlugin, IQueryPlugin) для legacy пути
            var cqrsPlugins = loader.LoadLegacyCqrsPluginsFromDirectory(_pluginsFolder, allowedDlls).ToList();
            loadedPlugins.AddRange(cqrsPlugins);

            try 
            { 
                // 3. Выполнение конвейера 
                return await ExecutePipelineAsync(loadedPlugins, manifest.Pipeline, manifest.Variables, cancellationToken, initialPayload); 
            } 
            finally 
            { 
                foreach (var plugin in loadedPlugins) await plugin.DisposeAsync(); 
                loadedPlugins.Clear(); 
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

    private async Task<Vantuz.Core.ExecutionContext> ExecutePipelineAsync(List<IVantuzPlugin> loadedPlugins, List<StepConfig> pipelineSteps, Dictionary<string, string>? manifestVariables, CancellationToken ct, IDictionary<string, object>? initialPayload) 
    { 
        var contextData = new Vantuz.Core.ExecutionContext(ct, _reporter); 
        
        // 0. Инъекция системных переменных 
        if (manifestVariables != null) 
        { 
            foreach (var kvp in manifestVariables) contextData.Set(kvp.Key, kvp.Value); 
        } 

        // 2. Затем вливаем Payload из UI (он имеет приоритет и перезапишет ключи манифеста) 
        if (initialPayload != null) 
        { 
            foreach (var kvp in initialPayload) contextData.Set(kvp.Key, kvp.Value); 
        } 

        string exeName = System.IO.Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "VantuzLauncher.exe");
        contextData.Set("hostExecutable", exeName);

        MiddlewareDelegate pipeline = (ctx) => Task.CompletedTask; 
        for (int i = pipelineSteps.Count - 1; i >= 0; i--) 
        { 
            var step = pipelineSteps[i]; 
            var plugin = loadedPlugins.FirstOrDefault(p => p.Name == step.PluginName) 
                ?? throw new Exception($"Plugin {step.PluginName} not found"); 

            var next = pipeline; 
            pipeline = async (ctx) => { 
                if (ctx.IsAborted || ctx.CancellationToken.IsCancellationRequested || ctx.Get<bool>("UpdateReady")) return; 
                try { await plugin.InvokeAsync(ctx, step.Config, next); } 
                catch (Exception ex) { ctx.Abort($"Plugin {plugin.Name} crashed: {ex.Message}"); } 
            }; 
        } 
        await pipeline(contextData); 
        if (contextData.IsAborted) throw new Exception(contextData.AbortReason); 

        return contextData; 
    }

    /// <summary>
    /// Запускает pipeline с квантованным выполнением (QuantizedNode).
    /// Это предпочтительный метод согласно .traerules:98.
    /// </summary>
    public async Task<QuantumExecutionResult> RunQuantumAsync(
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
                typeof(IVantuzPlugin).Assembly.GetName().Name!,
                typeof(QuantizedNode).Assembly.GetName().Name!
            };
            var loader = new PluginLoader(shared);
            var allowedDlls = manifest.Plugins.Keys.ToList();
            var loadedPlugins = loader.LoadPluginsFromDirectory(_pluginsFolder, allowedDlls).ToList();

            // 3. Загрузка QuantizedNode (новый паттерн)
            var quantizedNodes = loader.LoadQuantizedNodesFromDirectory(_pluginsFolder, allowedDlls).ToList();

            // 4. Загрузка CQRS плагинов (ICommandPlugin, IQueryPlugin) через адаптеры
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
                var pipeline = BuildQuantumPipeline(manifest.Pipeline, loadedPlugins, quantizedNodes);

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
                foreach (var plugin in loadedPlugins) await plugin.DisposeAsync();
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

    /// <summary>
    /// Собирает pipeline из QuantizedNode и legacy плагинов.
    /// Legacy плагины оборачиваются в LegacyPluginAdapter.
    /// </summary>
    private List<(QuantizedNode Node, JsonElement Config)> BuildQuantumPipeline(
        List<StepConfig> steps,
        List<IVantuzPlugin> legacyPlugins,
        List<QuantizedNode> quantizedNodes)
    {
        var result = new List<(QuantizedNode, JsonElement)>();

        foreach (var step in steps)
        {
            // Сначала ищем QuantizedNode
            var node = quantizedNodes.FirstOrDefault(n => n.Name == step.PluginName);
            if (node != null)
            {
                result.Add((node, step.Config));
                continue;
            }

            // Затем ищем legacy плагин и оборачиваем его
            var legacy = legacyPlugins.FirstOrDefault(p => p.Name == step.PluginName);
            if (legacy != null)
            {
                // Создаём временный ExecutionContext для адаптера
                var context = new Vantuz.Core.ExecutionContext(CancellationToken.None, _reporter);
                var adapter = new LegacyPluginAdapter(legacy, context);
                result.Add((adapter, step.Config));
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
