using System; 
 using System.IO; 
 using System.Reflection; 
 using System.Runtime.Loader; 
 
 namespace Vantuz.Host 
 { 
     public class PluginLoadContext : AssemblyLoadContext 
     { 
         private readonly AssemblyDependencyResolver _resolver; 
         private readonly string _shadowDir; 
 
         public PluginLoadContext(string pluginFilePath) : base(isCollectible: true) 
         { 
             string originalDir = Path.GetDirectoryName(pluginFilePath) ?? string.Empty; 
             string pluginFileName = Path.GetFileName(pluginFilePath); 
 
             // 1. Кочевнический подход: создаем локальную теневую папку 
             string baseShadowDir = Path.Combine(originalDir, ".shadow"); 
             _shadowDir = Path.Combine(baseShadowDir, Guid.NewGuid().ToString()); 
             
             // 2. Жизнестойкость (Resilience): Очищаем старые теневые папки (оставшиеся после крашей) 
             CleanupOldShadows(baseShadowDir); 
             Directory.CreateDirectory(_shadowDir); 
 
             // 3. Копируем сам плагин и его .deps.json 
             string shadowPluginPath = Path.Combine(_shadowDir, pluginFileName); 
             File.Copy(pluginFilePath, shadowPluginPath, true); 
 
             string depsFile = Path.ChangeExtension(pluginFilePath, ".deps.json"); 
             if (File.Exists(depsFile)) 
             { 
                 File.Copy(depsFile, Path.Combine(_shadowDir, Path.GetFileName(depsFile)), true); 
             } 
 
             // 4. Копируем остальные DLL из папки (транзитивные зависимости) 
             foreach (var file in Directory.GetFiles(originalDir, "*.dll")) 
             { 
                 var dest = Path.Combine(_shadowDir, Path.GetFileName(file)); 
                 if (!File.Exists(dest)) File.Copy(file, dest, true); 
             } 
 
             // 5. Используем стандартный резолвер Microsoft, но нацеленный на ТЕНЕВУЮ копию 
             _resolver = new AssemblyDependencyResolver(shadowPluginPath); 
         } 
 
         protected override Assembly? Load(AssemblyName assemblyName) 
         { 
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
                     // Пытаемся удалить. Если папка заблокирована другим запущенным лаунчером - пропускаем. 
                     try { Directory.Delete(dir, true); } catch { } 
                 } 
             } 
             catch { } 
         } 
     } 
 } 
