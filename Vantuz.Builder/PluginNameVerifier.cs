using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Vantuz.Builder;

/// <summary>
/// Build-time verification: cross-references boot.json pipeline pluginNames
/// against discovered plugin class Name properties.
/// Per INVARIANT_THEORY.md §1.2 Measurability.
/// </summary>
public static class PluginNameVerifier
{
    public static int Verify(string bootJsonPath, string pluginsDir)
    {
        if (!File.Exists(bootJsonPath))
        {
            Console.Error.WriteLine($"[VERIFY] boot.json not found: {bootJsonPath}");
            return 1;
        }
        if (!Directory.Exists(pluginsDir))
        {
            Console.Error.WriteLine($"[VERIFY] plugins directory not found: {pluginsDir}");
            return 1;
        }

        var json = File.ReadAllText(bootJsonPath);
        var manifest = JsonSerializer.Deserialize<BootManifest>(json);
        if (manifest?.Pipeline == null)
        {
            Console.Error.WriteLine("[VERIFY] Invalid boot.json format");
            return 1;
        }

        var expectedNames = manifest.Pipeline.Select(s => s.PluginName).ToList();

        // Pre-load Vantuz.Core so plugin assemblies resolve interface references
        var hostDir = Path.GetDirectoryName(pluginsDir)!;
        var coreDll = Path.Combine(hostDir, "Vantuz.Core.dll");
        if (File.Exists(coreDll))
        {
            try { Assembly.LoadFrom(coreDll); } catch { /* best effort */ }
        }

        // Also load all other DLLs in host dir to satisfy transitive deps
        foreach (var dll in Directory.GetFiles(hostDir, "*.dll"))
        {
            try { Assembly.LoadFrom(dll); } catch { /* skip native/unloadable */ }
        }

        var discoveredNames = new List<string>();
        var discoveredMap = new Dictionary<string, string>();

        foreach (var dllPath in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(dllPath);
                foreach (var type in asm.GetTypes())
                {
                    var prop = type.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null || prop.PropertyType != typeof(string))
                        continue;

                    // Must have parameterless ctor
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor == null)
                        continue;

                    var instance = ctor.Invoke(null);
                    var name = prop.GetValue(instance) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        discoveredNames.Add(name);
                        discoveredMap[name] = Path.GetFileName(dllPath);
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                var msg = string.Join("; ", ex.LoaderExceptions?.Where(e => e != null).Select(e => e!.Message) ?? Array.Empty<string>());
                Console.Error.WriteLine($"[VERIFY] ReflectionTypeLoadException in {Path.GetFileName(dllPath)}: {msg}");
            }
            catch
            {
                // Skip native/unloadable assemblies
            }
        }

        var mismatches = expectedNames.Where(e => !discoveredNames.Contains(e)).ToList();
        if (mismatches.Count > 0)
        {
            Console.Error.WriteLine("[VERIFY] ARM-BUILD-020: Pipeline pluginName mismatch detected.");
            foreach (var m in mismatches)
            {
                Console.Error.WriteLine($"  Missing plugin name: '{m}'");
            }
            return 1;
        }

        Console.WriteLine($"[VERIFY] PASS: All {expectedNames.Count} pipeline names verified against {discoveredNames.Count} discovered plugin classes.");
        return 0;
    }

    private class BootManifest
    {
        public List<PipelineStep> Pipeline { get; set; } = new();
    }

    private class PipelineStep
    {
        public string PluginName { get; set; } = "";
    }
}
