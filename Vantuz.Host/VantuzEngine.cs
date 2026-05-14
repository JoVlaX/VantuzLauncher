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

public record BootManifest(Dictionary<string, string> Plugins, List<StepConfig> Pipeline); 
public record StepConfig(string PluginName, JsonElement Config); 

public class VantuzEngine 
{ 
    private readonly string _pluginsFolder; 
    private readonly IStatusReporter _reporter; 

    public VantuzEngine(string pluginsFolder, IStatusReporter reporter) 
    { 
        _pluginsFolder = pluginsFolder; 
        _reporter = reporter; 
    } 

    public async Task RunAsync(string bootJsonPath, CancellationToken cancellationToken, IDictionary<string, object>? initialPayload = null) 
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
            var loadedPlugins = loader.LoadPluginsFromDirectory(_pluginsFolder).ToList(); 

            try 
            { 
                // 3. Выполнение конвейера 
                await ExecutePipelineAsync(loadedPlugins, manifest.Pipeline, cancellationToken, initialPayload); 
            } 
            finally 
            { 
                foreach (var plugin in loadedPlugins) await plugin.DisposeAsync(); 
                loadedPlugins.Clear(); 
            } 
        } 
        catch (Exception ex) 
        { 
            // Глобальный Краш-логгер (Observability) 
            string crashLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"); 
            string errorMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] CRITICAL SYSTEM CRASH\n" + 
                                  $"Message: {ex.Message}\n" + 
                                  $"StackTrace:\n{ex.StackTrace}\n" + 
                                  $"InnerException: {ex.InnerException?.Message}\n" + 
                                  new string('-', 50) + "\n"; 
            File.AppendAllText(crashLogPath, errorMessage); 
            throw; 
        } 
    } 

    private void ValidateManifestHashes(Dictionary<string, string> pluginsConfig) 
    { 
        foreach (var (dllName, expectedHash) in pluginsConfig) 
        { 
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

    private async Task ExecutePipelineAsync(List<IVantuzPlugin> loadedPlugins, List<StepConfig> pipelineSteps, CancellationToken ct, IDictionary<string, object>? initialPayload) 
    { 
        var contextData = new Vantuz.Core.ExecutionContext(ct, _reporter); 
        if (initialPayload != null) foreach (var kvp in initialPayload) contextData.Set(kvp.Key, kvp.Value); 

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

        // ДОБАВЛЕНО: Внешняя мутация (External Bootstrapper) 
        if (contextData.Get<bool>("UpdateReady")) 
        { 
            string scriptPath = contextData.Get<string>("UpdateScript")!; 
            if (File.Exists(scriptPath)) 
            { 
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo 
                { 
                    FileName = scriptPath, 
                    UseShellExecute = true, 
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden 
                }); 
                 
                // Жестко завершаем текущий процесс, чтобы ОС сняла блокировки с файлов 
                Environment.Exit(0); 
            } 
        } 
    } 
} 
