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
 
 public record BootManifest(Dictionary<string, string> Plugins, List<StepConfig> Pipeline); 
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
         try 
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
                 foreach (var plugin in _loadedPlugins) await plugin.DisposeAsync(); 
                 _loadedPlugins.Clear(); 
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
 
     private void LoadPlugins(Dictionary<string, string> pluginsConfig) 
     { 
         string[] shared = new[] { typeof(IVantuzPlugin).Assembly.GetName().Name! }; 
 
         foreach (var (dllName, expectedHash) in pluginsConfig) 
         { 
             string fullPath = Path.Combine(_pluginsFolder, Path.GetFileName(dllName)); 
             if (!File.Exists(fullPath)) throw new FileNotFoundException($"Plugin not found: {fullPath}"); 
 
             using (var fs = File.OpenRead(fullPath)) ValidateHash(fs, expectedHash, dllName); 
             
             // Подготовка теневой копии (SRP) 
             string shadowPath = PrepareShadowWorkspace(fullPath); 
             
             var context = new PluginLoadContext(shadowPath, shared); 
             var assembly = context.LoadFromAssemblyPath(shadowPath); 
 
             var types = assembly.GetTypes() 
                 .Where(t => typeof(IVantuzPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract); 
 
             foreach (var type in types) 
             { 
                 if (Activator.CreateInstance(type) is IVantuzPlugin plugin) _loadedPlugins.Add(plugin); 
             } 
         } 
     } 
 
     private string PrepareShadowWorkspace(string originalPluginFilePath) 
     { 
         string originalDir = Path.GetDirectoryName(originalPluginFilePath) ?? string.Empty; 
         string pluginFileName = Path.GetFileName(originalPluginFilePath); 
         string baseShadowDir = Path.Combine(originalDir, ".shadow"); 
         string shadowDir = Path.Combine(baseShadowDir, Guid.NewGuid().ToString()); 
         
         if (Directory.Exists(baseShadowDir)) 
         { 
             foreach (var dir in Directory.GetDirectories(baseShadowDir)) 
             { 
                 try { Directory.Delete(dir, true); } catch { } 
             } 
         } 
 
         Directory.CreateDirectory(shadowDir); 
         foreach (var file in Directory.GetFiles(originalDir, "*.*")) 
         { 
             if (file.Contains(".shadow")) continue; 
             File.Copy(file, Path.Combine(shadowDir, Path.GetFileName(file)), true); 
         } 
 
         return Path.Combine(shadowDir, pluginFileName); 
     } 
 
     private void ValidateHash(Stream fileStream, string expectedHash, string fileName) 
     { 
         using var sha256 = SHA256.Create(); 
         var hashBytes = sha256.ComputeHash(fileStream); 
         var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant(); 
         if (actualHash != expectedHash.ToLowerInvariant()) 
             throw new Exception($"HASH MISMATCH for {fileName}"); 
     } 
 
     private async Task ExecutePipelineAsync(List<StepConfig> pipelineSteps, CancellationToken ct, IDictionary<string, object>? initialPayload) 
     { 
         var contextData = new Vantuz.Core.ExecutionContext(ct, _reporter); 
         if (initialPayload != null) foreach (var kvp in initialPayload) contextData.Set(kvp.Key, kvp.Value); 
 
         MiddlewareDelegate pipeline = (ctx) => Task.CompletedTask; 
         for (int i = pipelineSteps.Count - 1; i >= 0; i--) 
         { 
             var step = pipelineSteps[i]; 
             var plugin = _loadedPlugins.FirstOrDefault(p => p.Name == step.PluginName) 
                 ?? throw new Exception($"Plugin {step.PluginName} not found"); 
 
             var next = pipeline; 
             pipeline = async (ctx) => { 
                 if (ctx.IsAborted || ctx.CancellationToken.IsCancellationRequested) return; 
                 try { await plugin.InvokeAsync(ctx, step.Config, next); } 
                 catch (Exception ex) { ctx.Abort($"Plugin {plugin.Name} crashed: {ex.Message}"); } 
             }; 
         } 
         await pipeline(contextData); 
         if (contextData.IsAborted) throw new Exception(contextData.AbortReason); 
     } 
 } 
