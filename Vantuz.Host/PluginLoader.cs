using System; 
using System.Collections.Generic; 
using System.IO; 
using System.Linq; 
using System.Reflection; 
using System.Runtime.Loader; 
using Vantuz.Core; 

namespace Vantuz.Host 
{ 
    public class PluginLoader : IDisposable
    { 
        // РЈР”Р•Р Р–РђРќРР• (Rooting) - Р·Р°С‰РёС‚Р° РѕС‚ СЃР±РѕСЂС‰РёРєР° РјСѓСЃРѕСЂР° 
        private readonly List<AssemblyLoadContext> _activeContexts = new(); 
        private readonly List<string> _shadowDirs = new();
        private readonly string[] _sharedAssemblies; 

        public PluginLoader(string[] sharedAssemblies) 
        { 
            _sharedAssemblies = sharedAssemblies; 
        } 


        /// <summary>
        /// Р—Р°РіСЂСѓР¶Р°РµС‚ QuantizedNode РёР· РґРёСЂРµРєС‚РѕСЂРёРё РїР»Р°РіРёРЅРѕРІ.
        /// РЎРѕРіР»Р°СЃРЅРѕ Armatura:98 - РЅРѕРІС‹Р№ РїР°С‚С‚РµСЂРЅ РІРјРµСЃС‚Рѕ free-form async.
        /// </summary>
        /// F_doc: {LoadQuantizedNodesFromDirectory returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies LoadQuantizedNodesFromDirectory behavior
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
                    throw new Exception($"[Р”РРђР“РќРћРЎРўРРљРђ] РћС€РёР±РєР° ReflectionTypeLoadException РІ Р±РёР±Р»РёРѕС‚РµРєРµ {dllName}:\n{loaderErrors}", ex);
                }

                // РС‰РµРј РєР»Р°СЃСЃС‹, РЅР°СЃР»РµРґСѓСЋС‰РёРµСЃСЏ РѕС‚ QuantizedNode
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
        /// Р—Р°РіСЂСѓР¶Р°РµС‚ CQRS РїР»Р°РіРёРЅС‹ (ICommandPlugin, IQueryPlugin) РёР· РґРёСЂРµРєС‚РѕСЂРёРё РїР»Р°РіРёРЅРѕРІ.
        /// РћР±РѕСЂР°С‡РёРІР°РµС‚ РёС… РІ QuantizedNode Р°РґР°РїС‚РµСЂС‹.
        /// РЎРѕРіР»Р°СЃРЅРѕ Armatura:98 - РІРѕР·РІСЂР°С‰Р°РµРј QuantizedNode РґР»СЏ РєРІР°РЅС‚РѕРІР°РЅРЅРѕРіРѕ РІС‹РїРѕР»РЅРµРЅРёСЏ.
        /// </summary>
        /// F_doc: {LoadCqrsPluginsFromDirectory returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies LoadCqrsPluginsFromDirectory behavior
        public IEnumerable<QuantizedNode> LoadCqrsPluginsFromDirectory(string pluginsPath, List<string> allowedDlls)
        {
            var nodes = new List<QuantizedNode>();
            if (!Directory.Exists(pluginsPath)) return nodes;

            string shadowDir = PrepareShadowWorkspace(pluginsPath);

            foreach (var dllName in allowedDlls)
            {
                // РђРІС‚РѕРјР°С‚РёС‡РµСЃРєРё РґРѕР±Р°РІР»СЏРµРј .dll РµСЃР»Рё РЅРµ СѓРєР°Р·Р°РЅРѕ СЂР°СЃС€РёСЂРµРЅРёРµ
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
                    throw new Exception($"[Р”РРђР“РќРћРЎРўРРљРђ] РћС€РёР±РєР° ReflectionTypeLoadException РІ Р±РёР±Р»РёРѕС‚РµРєРµ {dllName}:\n{loaderErrors}", ex);
                }

                // РС‰РµРј ICommandPlugin implementations
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

                // РС‰РµРј IQueryPlugin implementations
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
                    try { context.LoadFromAssemblyStream(file); } catch (Exception ex) { Console.WriteLine($"[PluginLoader] WARN: failed to load {file}: {ex.Message}"); } 
                } 
            } 
        } 

        private string PrepareShadowWorkspace(string originalDir) 
        { 
            string hashStr; 
            using (var md5 = System.Security.Cryptography.MD5.Create()) 
                hashStr = BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(originalDir.ToLowerInvariant()))).Replace("-", ""); 
 
            string baseShadowDir = Path.Combine(Path.GetTempPath(), "VantuzLauncher_Shadow_" + hashStr); 
 
            // РЎР±РѕСЂРєР° РјСѓСЃРѕСЂР°: С‡РёСЃС‚РёРј Р·Р°РІРёСЃС€РёРµ СЃРµСЃСЃРёРё Р­РўРћР“Рћ Р»Р°СѓРЅС‡РµСЂР° 
            if (Directory.Exists(baseShadowDir)) { 
                foreach (var dir in Directory.GetDirectories(baseShadowDir)) { 
                    try { Directory.Delete(dir, true); } catch (Exception ex) { Console.WriteLine($"[PluginLoader] WARN: failed to delete shadow dir {dir}: {ex.Message}"); } 
                } 
            } 
 
            string shadowDir = Path.Combine(baseShadowDir, Guid.NewGuid().ToString()); 
            Directory.CreateDirectory(shadowDir);
            _shadowDirs.Add(shadowDir); 
            
            // Р РµРєСѓСЂСЃРёРІРЅРѕРµ РєРѕРїРёСЂРѕРІР°РЅРёРµ РІСЃРµС… С„Р°Р№Р»РѕРІ Рё РїР°РїРѕРє (РІРєР»СЋС‡Р°СЏ runtimes Рё .deps.json)
            // РСЃРєР»СЋС‡Р°РµРј shared assemblies - РѕРЅРё Р·Р°РіСЂСѓР¶Р°СЋС‚СЃСЏ РІ default context
            foreach (string dirPath in System.IO.Directory.GetDirectories(originalDir, "*", System.IO.SearchOption.AllDirectories)) 
            { 
                System.IO.Directory.CreateDirectory(dirPath.Replace(originalDir, shadowDir)); 
            } 
            foreach (string newPath in System.IO.Directory.GetFiles(originalDir, "*.*", System.IO.SearchOption.AllDirectories)) 
            { 
                // РџСЂРѕРїСѓСЃРєР°РµРј shared assemblies - РѕРЅРё Р·Р°РіСЂСѓР¶Р°СЋС‚СЃСЏ РІ default context
                string fileName = Path.GetFileName(newPath);
                string assemblyName = Path.GetFileNameWithoutExtension(fileName);
                if (_sharedAssemblies.Contains(assemblyName))
                    continue;
                System.IO.File.Copy(newPath, newPath.Replace(originalDir, shadowDir), true); 
            } 
            return shadowDir; 
        }

        /// <summary>
        /// ARM003 Resource Lifecycle: cleans up all shadow directories created during plugin loading.
        /// Per INVARIANT_THEORY В§3.1 вЂ” ephemeral directories must not outlive the loader.
        /// </summary>
        /// F_doc: {Dispose returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Dispose behavior
        public void Dispose()
        {
            foreach (var dir in _shadowDirs)
            {
                try
                {
                    if (Directory.Exists(dir))
                        Directory.Delete(dir, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginLoader] WARN: failed to delete shadow dir {dir} during Dispose: {ex.Message}");
                }
            }
            _shadowDirs.Clear();

            foreach (var ctx in _activeContexts)
            {
                try { ctx.Unload(); } catch { /* best effort */ }
            }
            _activeContexts.Clear();
        }
    } 
} 
