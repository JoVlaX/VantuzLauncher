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

        /// <summary>
        /// Загружает сборку из файла через MemoryStream (stream-based загрузка согласно .traerules ARM001)
        /// </summary>
        public Assembly LoadFromAssemblyStream(string assemblyPath)
        {
            byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
            using var stream = new MemoryStream(assemblyBytes);
            return LoadFromStream(stream);
        }

        protected override Assembly? Load(AssemblyName assemblyName) 
        { 
            if (assemblyName.Name == null) return null; 
 
            // 1. Стандартный резолв через resolver 
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName); 
            if (assemblyPath != null) 
            { 
                return LoadFromAssemblyStream(assemblyPath); 
            } 
 
            // 2. АГРЕССИВНЫЙ РЕКУРСИВНЫЙ FALLBACK (stream-based) 
            if (!string.IsNullOrEmpty(_pluginDir)) 
            { 
                try 
                { 
                    string[] files = Directory.GetFiles(_pluginDir, assemblyName.Name + ".dll", SearchOption.AllDirectories); 
                    if (files.Length > 0 && files[0] != null) 
                    { 
                        return LoadFromAssemblyStream(files[0]); 
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
