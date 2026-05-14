namespace Vantuz.Host; 
 
 using System; 
 using System.Collections.Generic; 
 using System.IO; 
 using System.Linq; 
 using System.Reflection; 
 using System.Security.Cryptography; 
 using System.Text.Json; 
 using System.Threading; 
 using System.Threading.Tasks; 
 using Vantuz.Core; 
 
 public record BootManifest( 
     Dictionary<string, string> Plugins, 
     List<StepConfig> Pipeline 
 ); 
 
 public record StepConfig(string PluginName, JsonElement Config); 
 
 public class VantuzEngine 
 { 
     private readonly string _pluginsFolder; 
     private readonly IStatusReporter _reporter; 
     private readonly List<IVantuzPlugin> _loadedPlugins = new (); 
 
     public VantuzEngine(string pluginsFolder, IStatusReporter reporter) 
     { 
         _pluginsFolder = pluginsFolder; 
         _reporter = reporter; 
     } 
 
     public async Task RunAsync(string bootJsonPath, CancellationToken cancellationToken, IDictionary<string, object>? initialPayload = null) 
     { 
         var manifest = JsonSerializer.Deserialize<BootManifest>( 
             await File.ReadAllTextAsync(bootJsonPath, cancellationToken), 
             new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
             ?? throw new Exception("Invalid boot.json"); 
 
         try 
         { 
             LoadPlugins(manifest.Plugins); 
             await ExecutePipelineAsync(manifest.Pipeline, cancellationToken, initialPayload); 
         } 
         finally 
         { 
             foreach (var plugin in _loadedPlugins) 
             { 
                 await plugin.DisposeAsync(); 
             } 
             _loadedPlugins.Clear(); 
         } 
     } 
 
     private void LoadPlugins(Dictionary<string, string> pluginsConfig) 
     { 
         // Динамическое определение имени сборки с контрактами 
         var shared = new[] { typeof(IVantuzPlugin).Assembly.GetName().Name! }; 
 
         foreach (var (dllName, expectedHash) in pluginsConfig) 
         { 
             var safeDllName = Path.GetFileName(dllName); 
             string fullPath = Path.Combine(_pluginsFolder, safeDllName); 
             
             if (!File.Exists(fullPath)) throw new FileNotFoundException($"Plugin not found: {fullPath}"); 
 
             using (var fs = File.OpenRead(fullPath)) 
             { 
                 ValidateHash(fs, expectedHash, safeDllName); 
             } 
             
             // Передаем динамический список shared-сборок 
             var context = new PluginLoadContext(fullPath, shared); 
             
             var assembly = context.LoadMainAssembly(); 
 
             IEnumerable<Type> pluginTypes; 
             try 
             { 
                 pluginTypes = assembly.GetTypes() 
                     .Where(t => typeof (IVantuzPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract); 
             } 
             catch (ReflectionTypeLoadException ex) 
             { 
                 var loaderErrors = string.Join("; ", ex.LoaderExceptions.Select(e => e?.Message).Where(m => m != null)); 
                 throw new Exception($"Критическая ошибка загрузки типов в {safeDllName}: {loaderErrors}"); 
             } 
 
             foreach (var type in pluginTypes) 
             { 
                 if (Activator.CreateInstance(type) is IVantuzPlugin plugin) 
                 { 
                     _loadedPlugins.Add(plugin); 
                 } 
             } 
         } 
     } 
 
     private void ValidateHash(Stream fileStream, string expectedHash, string fileName) 
     { 
         using var sha256 = SHA256.Create(); 
         var hashBytes = sha256.ComputeHash(fileStream); 
         var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); 
 
         if (actualHash != expectedHash.ToLowerInvariant()) 
             throw new Exception($"HASH MISMATCH for {fileName}. Possible tampering detected!"); 
     } 
 
     private async Task ExecutePipelineAsync(List<StepConfig> pipelineSteps, CancellationToken ct, IDictionary<string, object>? initialPayload) 
     { 
         var contextData = new Vantuz.Core.ExecutionContext(ct, _reporter); 
         
         if (initialPayload != null) 
         { 
             foreach (var kvp in initialPayload) 
                 contextData.Set(kvp.Key, kvp.Value); 
         } 
 
         MiddlewareDelegate pipeline = (ctx) => Task.CompletedTask; 
 
         for (int i = pipelineSteps.Count - 1; i >= 0; i--) 
         { 
             var step = pipelineSteps[i]; 
             var plugin = _loadedPlugins.FirstOrDefault(p => p.Name == step.PluginName) 
                 ?? throw new Exception($"Plugin {step.PluginName} not found in loaded DLLs"); 
 
             var next = pipeline; 
             pipeline = async (ctx) => 
             { 
                 if (ctx.IsAborted || ctx.CancellationToken.IsCancellationRequested) return; 
                 
                 try 
                 { 
                     ctx.Reporter.ReportState($"Executing {plugin.Name}..."); 
                     await plugin.InvokeAsync(ctx, step.Config, next); 
                 } 
                 catch (Exception ex) 
                 { 
                     ctx.Abort($"Plugin {plugin.Name} crashed: {ex.Message}"); 
                 } 
             }; 
         } 
 
         await pipeline(contextData); 
 
         if (contextData.IsAborted) 
         { 
             throw new Exception($"Конвейер прерван:\n{contextData.AbortReason}"); 
         } 
     } 
 } 
