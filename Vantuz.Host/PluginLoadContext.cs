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
        private readonly string[] _sharedAssemblies; 
        private readonly string _shadowDir; 

        public PluginLoadContext(string shadowPluginFilePath, string[] sharedAssemblies) : base(isCollectible: true) 
        { 
            _sharedAssemblies = sharedAssemblies ?? Array.Empty<string>(); 
            _shadowDir = Path.GetDirectoryName(shadowPluginFilePath) ?? string.Empty; 
            _resolver = new AssemblyDependencyResolver(shadowPluginFilePath); 
        } 

        protected override Assembly? Load(AssemblyName assemblyName) 
        { 
            // 1. Делегируем общие сборки (контракты) системному загрузчику 
            if (assemblyName.Name != null && _sharedAssemblies.Contains(assemblyName.Name)) 
                return null; 

            // 2. Официальный резолв через .deps.json 
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName); 
            
            // 3. Прямой Fallback: если манифест слеп, ищем файл физически в теневой папке 
            if (assemblyPath == null && assemblyName.Name != null) 
            { 
                string fallbackPath = Path.Combine(_shadowDir, $"{assemblyName.Name}.dll"); 
                if (File.Exists(fallbackPath)) 
                    assemblyPath = fallbackPath; 
            } 

            // 4. Безопасная загрузка (LoadFromAssemblyPath ЛЕГАЛЕН внутри Load) 
            if (assemblyPath != null) 
                return LoadFromAssemblyPath(assemblyPath); 

            return null; 
        } 

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName) 
        { 
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName); 
            if (libraryPath != null) return LoadUnmanagedDllFromPath(libraryPath); 
            return IntPtr.Zero; 
        } 
    } 
} 
