using System; 
using System.Collections.Generic; 
using System.IO; 
using System.Linq; 
using System.Reflection; 
using System.Runtime.Loader; 
using Vantuz.Core; 

namespace Vantuz.Host 
{ 
    public class PluginLoader 
    { 
        // УДЕРЖАНИЕ (Rooting) - защита от сборщика мусора 
        private readonly List<AssemblyLoadContext> _activeContexts = new(); 
        private readonly string[] _sharedAssemblies; 

        public PluginLoader(string[] sharedAssemblies) 
        { 
            _sharedAssemblies = sharedAssemblies; 
        } 

        public IEnumerable<IVantuzPlugin> LoadPluginsFromDirectory(string pluginsPath) 
        { 
            var plugins = new List<IVantuzPlugin>(); 
            if (!Directory.Exists(pluginsPath)) return plugins; 

            foreach (var dllPath in Directory.GetFiles(pluginsPath, "*.dll")) 
            { 
                string shadowPath = PrepareShadowWorkspace(dllPath); 
                var context = new PluginLoadContext(shadowPath, _sharedAssemblies); 
                
                // Спасаем от GC 
                _activeContexts.Add(context); 

                // ЖАДНАЯ ЗАГРУЗКА (Eager Loading) для обхода слепоты NuGet deps.json 
                EagerLoadAssemblies(context, Path.GetDirectoryName(shadowPath)!); 

                // Ищем плагины в загруженном контексте 
                foreach (var assembly in context.Assemblies) 
                { 
                    var types = assembly.GetTypes() 
                        .Where(t => typeof(IVantuzPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract); 
                    
                    foreach (var type in types) 
                    { 
                        if (Activator.CreateInstance(type) is IVantuzPlugin plugin) 
                        { 
                            plugins.Add(plugin); 
                        } 
                    } 
                } 
            } 
            return plugins; 
        } 

        private void EagerLoadAssemblies(PluginLoadContext context, string shadowDir) 
        { 
            foreach (var file in Directory.GetFiles(shadowDir, "*.dll")) 
            { 
                var assemblyName = AssemblyName.GetAssemblyName(file); 
                if (!_sharedAssemblies.Contains(assemblyName.Name)) 
                { 
                    try { context.LoadFromAssemblyPath(file); } catch { /* Игнорируем конфликты нативных DLL */ } 
                } 
            } 
        } 

        private string PrepareShadowWorkspace(string originalPluginFilePath) 
        { 
            string originalDir = Path.GetDirectoryName(originalPluginFilePath) ?? string.Empty; 
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
                File.Copy(file, Path.Combine(shadowDir, Path.GetFileName(file)), true); 
            } 
            return Path.Combine(shadowDir, Path.GetFileName(originalPluginFilePath)); 
        } 
    } 
} 
