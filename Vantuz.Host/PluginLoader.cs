using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Vantuz.Core;

public class PluginLoadDiagnostics
{
    public List<string> Logs { get; } = new();
    public void Log(string message) => Logs.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
} 

namespace Vantuz.Host 
{ 
    public class PluginLoader
    {
        // УДЕРЖАНИЕ (Rooting) - защита от сборщика мусора
        private readonly List<AssemblyLoadContext> _activeContexts = new();
        private readonly string[] _sharedAssemblies;
        public PluginLoadDiagnostics Diagnostics { get; } = new(); 

        public PluginLoader(string[] sharedAssemblies) 
        { 
            _sharedAssemblies = sharedAssemblies; 
        } 


        /// <summary>
        /// Загружает QuantizedNode из директории плагинов.
        /// Согласно Armatura:98 - новый паттерн вместо free-form async.
        /// </summary>
        public IEnumerable<QuantizedNode> LoadQuantizedNodesFromDirectory(string pluginsPath, List<string> allowedDlls)
        {
            var nodes = new List<QuantizedNode>();
            Diagnostics.Log($"[QuantizedNodes] Starting load from: {pluginsPath}, allowed DLLs: {allowedDlls.Count}");

            if (!Directory.Exists(pluginsPath))
            {
                Diagnostics.Log($"[QuantizedNodes] Directory not found: {pluginsPath}");
                return nodes;
            }

            string shadowDir = PrepareShadowWorkspace(pluginsPath);

            foreach (var dllName in allowedDlls)
            {
                string shadowPath = Path.Combine(shadowDir, dllName);
                if (!File.Exists(shadowPath))
                {
                    Diagnostics.Log($"[QuantizedNodes] DLL not found in shadow: {dllName}");
                    continue;
                }

                Diagnostics.Log($"[QuantizedNodes] Loading assembly: {dllName}");
                var context = new PluginLoadContext(shadowPath);
                _activeContexts.Add(context);

                EagerLoadAssemblies(context, shadowDir);

                var assembly = context.LoadFromAssemblyStream(shadowPath);
                Diagnostics.Log($"[QuantizedNodes] Assembly loaded: {assembly.FullName}");

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                    Diagnostics.Log($"[QuantizedNodes] Found {types.Length} types in {dllName}");
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    string loaderErrors = string.Join("\n", (ex.LoaderExceptions ?? Array.Empty<Exception>()).Where(e => e != null).Select(e => e!.Message));
                    Diagnostics.Log($"[QuantizedNodes] ERROR: ReflectionTypeLoadException in {dllName}: {loaderErrors}");
                    throw new Exception($"[ДИАГНОСТИКА] Ошибка ReflectionTypeLoadException в библиотеке {dllName}:\n{loaderErrors}", ex);
                }

                // Ищем классы, наследующиеся от QuantizedNode
                var nodeTypes = types.Where(t =>
                    typeof(Vantuz.Core.QuantizedNode).IsAssignableFrom(t) &&
                    !t.IsInterface &&
                    !t.IsAbstract).ToList();

                Diagnostics.Log($"[QuantizedNodes] Found {nodeTypes.Count} QuantizedNode types in {dllName}");

                foreach (var type in nodeTypes)
                {
                    Diagnostics.Log($"[QuantizedNodes] Instantiating: {type.FullName}");
                    if (Activator.CreateInstance(type) is Vantuz.Core.QuantizedNode node)
                    {
                        Diagnostics.Log($"[QuantizedNodes] Registered node: {node.Name}");
                        nodes.Add(node);
                    }
                }
            }
            Diagnostics.Log($"[QuantizedNodes] Total nodes loaded: {nodes.Count}");
            return nodes;
        }

        /// <summary>
        /// Загружает CQRS плагины (ICommandPlugin, IQueryPlugin) из директории плагинов.
        /// Оборачивает их в QuantizedNode адаптеры.
        /// Согласно Armatura:98 - возвращаем QuantizedNode для квантованного выполнения.
        /// </summary>
        public IEnumerable<QuantizedNode> LoadCqrsPluginsFromDirectory(string pluginsPath, List<string> allowedDlls)
        {
            var nodes = new List<QuantizedNode>();
            Diagnostics.Log($"[CqrsPlugins] Starting load from: {pluginsPath}, allowed DLLs: {allowedDlls.Count}");

            if (!Directory.Exists(pluginsPath))
            {
                Diagnostics.Log($"[CqrsPlugins] Directory not found: {pluginsPath}");
                return nodes;
            }

            string shadowDir = PrepareShadowWorkspace(pluginsPath);

            foreach (var dllName in allowedDlls)
            {
                // Автоматически добавляем .dll если не указано расширение
                string actualDllName = dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? dllName
                    : dllName + ".dll";
                string shadowPath = Path.Combine(shadowDir, actualDllName);
                if (!File.Exists(shadowPath))
                {
                    Diagnostics.Log($"[CqrsPlugins] DLL not found in shadow: {actualDllName}");
                    continue;
                }

                Diagnostics.Log($"[CqrsPlugins] Loading assembly: {actualDllName}");
                var context = new PluginLoadContext(shadowPath);
                _activeContexts.Add(context);

                EagerLoadAssemblies(context, shadowDir);

                var assembly = context.LoadFromAssemblyStream(shadowPath);
                Diagnostics.Log($"[CqrsPlugins] Assembly loaded: {assembly.FullName}");

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                    Diagnostics.Log($"[CqrsPlugins] Found {types.Length} types in {actualDllName}");
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    string loaderErrors = string.Join("\n", (ex.LoaderExceptions ?? Array.Empty<Exception>()).Where(e => e != null).Select(e => e!.Message));
                    Diagnostics.Log($"[CqrsPlugins] ERROR: ReflectionTypeLoadException in {dllName}: {loaderErrors}");
                    throw new Exception($"[ДИАГНОСТИКА] Ошибка ReflectionTypeLoadException в библиотеке {dllName}:\n{loaderErrors}", ex);
                }

                // Ищем ICommandPlugin implementations
                var commandTypes = types.Where(t =>
                    typeof(Vantuz.Core.ICommandPlugin).IsAssignableFrom(t) &&
                    !t.IsInterface &&
                    !t.IsAbstract).ToList();

                Diagnostics.Log($"[CqrsPlugins] Found {commandTypes.Count} ICommandPlugin types in {actualDllName}");

                foreach (var type in commandTypes)
                {
                    Diagnostics.Log($"[CqrsPlugins] Instantiating ICommandPlugin: {type.FullName}");
                    if (Activator.CreateInstance(type) is Vantuz.Core.ICommandPlugin commandPlugin)
                    {
                        Diagnostics.Log($"[CqrsPlugins] Registered command: {commandPlugin.Name}");
                        nodes.Add(new CqrsCommandAdapter(commandPlugin));
                    }
                }

                // Ищем IQueryPlugin implementations
                var queryTypes = types.Where(t =>
                    typeof(Vantuz.Core.IQueryPlugin).IsAssignableFrom(t) &&
                    !t.IsInterface &&
                    !t.IsAbstract).ToList();

                Diagnostics.Log($"[CqrsPlugins] Found {queryTypes.Count} IQueryPlugin types in {actualDllName}");

                foreach (var type in queryTypes)
                {
                    Diagnostics.Log($"[CqrsPlugins] Instantiating IQueryPlugin: {type.FullName}");
                    if (Activator.CreateInstance(type) is Vantuz.Core.IQueryPlugin queryPlugin)
                    {
                        Diagnostics.Log($"[CqrsPlugins] Registered query: {queryPlugin.Name}");
                        nodes.Add(new CqrsQueryAdapter(queryPlugin));
                    }
                }
            }
            Diagnostics.Log($"[CqrsPlugins] Total CQRS nodes loaded: {nodes.Count}");
            return nodes;
        }

        private void EagerLoadAssemblies(PluginLoadContext context, string shadowDir) 
        { 
            foreach (var file in Directory.GetFiles(shadowDir, "*.dll")) 
            { 
                var assemblyName = AssemblyName.GetAssemblyName(file); 
                if (!_sharedAssemblies.Contains(assemblyName.Name)) 
                { 
                    try { context.LoadFromAssemblyStream(file); } catch { /* Игнорируем конфликты нативных DLL */ } 
                } 
            } 
        } 

        private string PrepareShadowWorkspace(string originalDir) 
        { 
            string hashStr; 
            using (var md5 = System.Security.Cryptography.MD5.Create()) 
                hashStr = BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(originalDir.ToLowerInvariant()))).Replace("-", ""); 
 
            string baseShadowDir = Path.Combine(Path.GetTempPath(), "VantuzLauncher_Shadow_" + hashStr); 
 
            // Сборка мусора: чистим зависшие сессии ЭТОГО лаунчера 
            if (Directory.Exists(baseShadowDir)) { 
                foreach (var dir in Directory.GetDirectories(baseShadowDir)) { 
                    try { Directory.Delete(dir, true); } catch { } 
                } 
            } 
 
            string shadowDir = Path.Combine(baseShadowDir, Guid.NewGuid().ToString()); 
            Directory.CreateDirectory(shadowDir); 
            
            // Рекурсивное копирование всех файлов и папок (включая runtimes и .deps.json)
            // Исключаем shared assemblies - они загружаются в default context
            foreach (string dirPath in System.IO.Directory.GetDirectories(originalDir, "*", System.IO.SearchOption.AllDirectories)) 
            { 
                System.IO.Directory.CreateDirectory(dirPath.Replace(originalDir, shadowDir)); 
            } 
            foreach (string newPath in System.IO.Directory.GetFiles(originalDir, "*.*", System.IO.SearchOption.AllDirectories)) 
            { 
                // Пропускаем shared assemblies - они загружаются в default context
                string fileName = Path.GetFileName(newPath);
                string assemblyName = Path.GetFileNameWithoutExtension(fileName);
                if (_sharedAssemblies.Contains(assemblyName))
                    continue;
                System.IO.File.Copy(newPath, newPath.Replace(originalDir, shadowDir), true); 
            } 
            return shadowDir; 
        } 
    } 
} 
