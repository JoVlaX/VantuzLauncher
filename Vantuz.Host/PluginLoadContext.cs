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

        protected override System.Reflection.Assembly Load(System.Reflection.AssemblyName assemblyName) 
        { 
            // 1. Попытка стандартного резолва (через .deps.json) 
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName); 
            if (assemblyPath != null) 
            { 
                return LoadFromAssemblyPath(assemblyPath); 
            } 

            // 2. АГРЕССИВНЫЙ FALLBACK: Ищем DLL физически в папке плагина 
            if (!string.IsNullOrEmpty(_pluginDir)) 
            { 
                string fallbackPath = System.IO.Path.Combine(_pluginDir, assemblyName.Name + ".dll"); 
                if (System.IO.File.Exists(fallbackPath)) 
                { 
                    return LoadFromAssemblyPath(fallbackPath); 
                } 
            } 

            return null; 
        } 
    } 
} 
