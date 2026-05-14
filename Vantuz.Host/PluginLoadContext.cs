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
             this.Resolving += OnResolving; 
         } 
 
         protected override Assembly? Load(AssemblyName assemblyName) 
         { 
             if (assemblyName.Name != null && _sharedAssemblies.Contains(assemblyName.Name)) return null; 
             return null; 
         } 
 
         private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName) 
         { 
             string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName); 
             
             if (assemblyPath == null && assemblyName.Name != null) 
             { 
                 string fallbackPath = Path.Combine(_shadowDir, $"{assemblyName.Name}.dll"); 
                 if (File.Exists(fallbackPath)) assemblyPath = fallbackPath; 
             } 
 
             if (assemblyPath != null) return LoadFromAssemblyPath(assemblyPath); 
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
