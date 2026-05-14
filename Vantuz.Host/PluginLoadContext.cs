using System; 
 using System.IO; 
 using System.Reflection; 
 using System.Runtime.Loader; 
 
 namespace Vantuz.Host 
 { 
     public class PluginLoadContext : AssemblyLoadContext 
     { 
         private readonly string _pluginDirectory; 
 
         public PluginLoadContext(string pluginDirectory) : base(isCollectible: true) 
         { 
             _pluginDirectory = pluginDirectory; 
             // Подписываемся на событие для загрузки транзитивных зависимостей 
             this.Resolving += OnResolving; 
         } 
 
         protected override Assembly? Load(AssemblyName assemblyName) 
         { 
             // Строго null. Запрет на LoadFromStream внутри Load() от создателей .NET. 
             return null; 
         } 
 
         private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName) 
         { 
             string assemblyPath = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll"); 
             
             if (File.Exists(assemblyPath)) 
             { 
                 // Читаем файл целиком в массив байт и передаем в поток. 
                 // Это гарантирует нулевую блокировку файла на диске (Memory Loading). 
                 byte[] assemblyBytes = File.ReadAllBytes(assemblyPath); 
                 using var ms = new MemoryStream(assemblyBytes); 
                 return context.LoadFromStream(ms); 
             } 
             
             return null; 
         } 
     } 
 } 
