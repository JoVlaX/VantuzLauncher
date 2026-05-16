using System; 
using System.IO; 
using System.Linq; 
using System.Reflection; 
using System.Runtime.Loader; 

namespace Vantuz.Host 
{ 
    public class PluginLoadContext : System.Runtime.Loader.AssemblyLoadContext 
    { 
        private System.Runtime.Loader.AssemblyDependencyResolver _resolver; 
        private string _pluginDir; 

        public PluginLoadContext(string pluginPath) : base(isCollectible: true) 
        { 
            _resolver = new System.Runtime.Loader.AssemblyDependencyResolver(pluginPath); 
            _pluginDir = System.IO.Path.GetDirectoryName(pluginPath) ?? string.Empty; 
        } 

        protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName) 
        { 
            if (assemblyName.Name == null) return null; 
 
            // 1. Делегирование общих сборок (используй свою логику с _sharedAssemblies, если она есть) 
            // if (_sharedAssemblies != null && _sharedAssemblies.Contains(assemblyName.Name)) return null; 
 
            // 2. Стандартный резолв 
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName); 
            if (assemblyPath != null) 
            { 
                return LoadFromAssemblyPath(assemblyPath); 
            } 
 
            // 3. АГРЕССИВНЫЙ РЕКУРСИВНЫЙ FALLBACK 
            if (!string.IsNullOrEmpty(_pluginDir)) 
            { 
                try 
                { 
                    // Ищем библиотеку во всех вложенных папках песочницы 
                    string[] files = System.IO.Directory.GetFiles(_pluginDir, assemblyName.Name + ".dll", System.IO.SearchOption.AllDirectories); 
                    if (files.Length > 0 && files[0] != null) 
                    { 
                        return LoadFromAssemblyPath(files[0]); 
                    } 
                } 
                catch 
                { 
                    // Игнорируем ошибки доступа при поиске 
                } 
            } 
 
            return null; 
        } 
    } 
} 
