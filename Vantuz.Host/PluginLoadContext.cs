using System; 
 using System.IO; 
 using System.Linq; 
 using System.Reflection; 
 using System.Runtime.Loader; 
 
 namespace Vantuz.Host 
 { 
     public class PluginLoadContext : AssemblyLoadContext 
     { 
         private readonly AssemblyDependencyResolver _resolver; 
         private readonly string _shadowPluginPath; 
         private readonly string[] _sharedAssemblies; 
 
         public PluginLoadContext(string pluginFilePath, string[] sharedAssemblies) : base(isCollectible: true) 
         { 
             _sharedAssemblies = sharedAssemblies ?? Array.Empty<string>(); 
             
             string originalDir = Path.GetDirectoryName(pluginFilePath) ?? string.Empty; 
             string pluginFileName = Path.GetFileName(pluginFilePath); 
 
             string baseShadowDir = Path.Combine(originalDir, ".shadow"); 
             string shadowDir = Path.Combine(baseShadowDir, Guid.NewGuid().ToString()); 
             
             CleanupOldShadows(baseShadowDir); 
             Directory.CreateDirectory(shadowDir); 
 
             _shadowPluginPath = Path.Combine(shadowDir, pluginFileName); 
             File.Copy(pluginFilePath, _shadowPluginPath, true); 
 
             string depsFile = Path.ChangeExtension(pluginFilePath, ".deps.json"); 
             if (File.Exists(depsFile)) 
             { 
                 File.Copy(depsFile, Path.Combine(shadowDir, Path.GetFileName(depsFile)), true); 
             } 
 
             // Копируем зависимости, ИСКЛЮЧАЯ те, что помечены как общие (Shared) 
             foreach (var file in Directory.GetFiles(originalDir, "*.dll")) 
             { 
                 var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file); 
                 if (_sharedAssemblies.Contains(fileNameWithoutExt, StringComparer.OrdinalIgnoreCase)) 
                     continue; 
 
                 var dest = Path.Combine(shadowDir, Path.GetFileName(file)); 
                 if (!File.Exists(dest)) File.Copy(file, dest, true); 
             } 
 
             _resolver = new AssemblyDependencyResolver(_shadowPluginPath); 
         } 
 
         public Assembly LoadMainAssembly() 
         { 
             return LoadFromAssemblyPath(_shadowPluginPath); 
         } 
 
         protected override Assembly? Load(AssemblyName assemblyName) 
         { 
             // Динамическая проверка: если сборка общая, делегируем загрузку Default-контексту 
             if (assemblyName.Name != null && _sharedAssemblies.Contains(assemblyName.Name, StringComparer.OrdinalIgnoreCase)) 
             { 
                 return null; 
             } 
 
             string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName); 
             if (assemblyPath != null) 
             { 
                 return LoadFromAssemblyPath(assemblyPath); 
             } 
             return null; 
         } 
 
         protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) 
         { 
             string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName); 
             if (libraryPath != null) 
             { 
                 return LoadUnmanagedDllFromPath(libraryPath); 
             } 
             return IntPtr.Zero; 
         } 
 
         private void CleanupOldShadows(string baseShadowDir) 
         { 
             if (!Directory.Exists(baseShadowDir)) return; 
             try 
             { 
                 foreach (var dir in Directory.GetDirectories(baseShadowDir)) 
                 { 
                     try { Directory.Delete(dir, true); } catch { } 
                 } 
             } 
             catch { } 
         } 
     } 
 } 
