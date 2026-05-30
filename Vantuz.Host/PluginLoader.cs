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


        /// <summary>
        /// Загружает QuantizedNode из директории плагинов.
        /// Согласно .traerules:98 - новый паттерн вместо free-form async.
        /// </summary>
        public IEnumerable<QuantizedNode> LoadQuantizedNodesFromDirectory(string pluginsPath, List<string> allowedDlls)
        {
            var nodes = new List<QuantizedNode>();
            if (!Directory.Exists(pluginsPath)) return nodes;

            string shadowDir = PrepareShadowWorkspace(pluginsPath);

            foreach (var dllName in allowedDlls)
            {
                string shadowPath = Path.Combine(shadowDir, dllName);
                if (!File.Exists(shadowPath)) continue;

                var context = new PluginLoadContext(shadowPath);
                _activeContexts.Add(context);

                EagerLoadAssemblies(context, shadowDir);

                var assembly = context.LoadFromAssemblyStream(shadowPath);

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    string loaderErrors = string.Join("\n", (ex.LoaderExceptions ?? Array.Empty<Exception>()).Where(e => e != null).Select(e => e!.Message));
                    throw new Exception($"[ДИАГНОСТИКА] Ошибка ReflectionTypeLoadException в библиотеке {dllName}:\n{loaderErrors}", ex);
                }

                // Ищем классы, наследующиеся от QuantizedNode
                var nodeTypes = types.Where(t =>
                    typeof(Vantuz.Core.QuantizedNode).IsAssignableFrom(t) &&
                    !t.IsInterface &&
                    !t.IsAbstract);

                foreach (var type in nodeTypes)
                {
                    if (Activator.CreateInstance(type) is Vantuz.Core.QuantizedNode node)
                    {
                        nodes.Add(node);
                    }
                }
            }
            return nodes;
        }

        /// <summary>
        /// Загружает CQRS плагины (ICommandPlugin, IQueryPlugin) из директории плагинов.
        /// Оборачивает их в QuantizedNode адаптеры.
        /// Согласно .traerules:98 - возвращаем QuantizedNode для квантованного выполнения.
        /// </summary>
        public IEnumerable<QuantizedNode> LoadCqrsPluginsFromDirectory(string pluginsPath, List<string> allowedDlls)
        {
            var nodes = new List<QuantizedNode>();
            if (!Directory.Exists(pluginsPath)) return nodes;

            string shadowDir = PrepareShadowWorkspace(pluginsPath);

            foreach (var dllName in allowedDlls)
            {
                // Автоматически добавляем .dll если не указано расширение
                string actualDllName = dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? dllName
                    : dllName + ".dll";
                string shadowPath = Path.Combine(shadowDir, actualDllName);
                if (!File.Exists(shadowPath)) continue;

                var context = new PluginLoadContext(shadowPath);
                _activeContexts.Add(context);

                EagerLoadAssemblies(context, shadowDir);

                var assembly = context.LoadFromAssemblyStream(shadowPath);

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    string loaderErrors = string.Join("\n", (ex.LoaderExceptions ?? Array.Empty<Exception>()).Where(e => e != null).Select(e => e!.Message));
                    throw new Exception($"[ДИАГНОСТИКА] Ошибка ReflectionTypeLoadException в библиотеке {dllName}:\n{loaderErrors}", ex);
                }

                // Ищем ICommandPlugin implementations
                var commandTypes = types.Where(t =>
                    typeof(Vantuz.Core.ICommandPlugin).IsAssignableFrom(t) &&
                    !t.IsInterface &&
                    !t.IsAbstract);

                foreach (var type in commandTypes)
                {
                    if (Activator.CreateInstance(type) is Vantuz.Core.ICommandPlugin commandPlugin)
                    {
                        nodes.Add(new CqrsCommandAdapter(commandPlugin));
                    }
                }

                // Ищем IQueryPlugin implementations
                var queryTypes = types.Where(t =>
                    typeof(Vantuz.Core.IQueryPlugin).IsAssignableFrom(t) &&
                    !t.IsInterface &&
                    !t.IsAbstract);

                foreach (var type in queryTypes)
                {
                    if (Activator.CreateInstance(type) is Vantuz.Core.IQueryPlugin queryPlugin)
                    {
                        nodes.Add(new CqrsQueryAdapter(queryPlugin));
                    }
                }
            }
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
