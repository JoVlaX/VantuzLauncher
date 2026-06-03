using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Vantuz.Builder;

/// <summary>
/// Build-time verification: cross-references boot.json pipeline pluginNames
/// against discovered plugin class Name properties via static IL analysis.
/// Per INVARIANT_THEORY.md §1.2 Measurability — no runtime instantiation.
/// </summary>
public static class PluginNameVerifier
{
    public static int VerifyManifest(string bootJsonPath, string pluginsDir)
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
        var manifest = JsonSerializer.Deserialize<BootManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (manifest?.Pipeline == null)
        {
            Console.Error.WriteLine("[VERIFY] Invalid boot.json format");
            return 1;
        }

        var expectedNames = manifest.Pipeline.Select(s => s.PluginName).ToList();
        var (discoveredNames, _) = DiscoverPluginNames(pluginsDir);

        var mismatches = expectedNames.Where(e => !discoveredNames.Contains(e)).ToList();
        if (mismatches.Count > 0)
        {
            Console.Error.WriteLine($"[VERIFY] ARM-BUILD-020: Pipeline pluginName mismatch detected in {Path.GetFileName(bootJsonPath)}.");
            foreach (var m in mismatches)
            {
                Console.Error.WriteLine($"  Missing plugin name: '{m}'");
            }
            return 1;
        }

        Console.WriteLine($"[VERIFY] PASS: All {expectedNames.Count} pipeline names verified against {discoveredNames.Count} discovered plugin classes.");
        return 0;
    }

    public static int VerifyDirectory(string dirPath, string pluginsDir)
    {
        if (!Directory.Exists(dirPath))
        {
            Console.Error.WriteLine($"[VERIFY] Directory not found: {dirPath}");
            return 1;
        }

        var manifests = Directory.GetFiles(dirPath, "boot*.json")
            .Where(f => !Path.GetFileName(f).Equals("boot.template.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        if (manifests.Count == 0)
        {
            Console.Error.WriteLine($"[VERIFY] No boot manifests found in {dirPath}");
            return 1;
        }

        int overallExit = 0;
        foreach (var manifestPath in manifests)
        {
            int result = VerifyManifest(manifestPath, pluginsDir);
            if (result != 0) overallExit = 1;
        }
        return overallExit;
    }

    private static (List<string> Names, Dictionary<string, string> Map) DiscoverPluginNames(string pluginsDir)
    {
        var discoveredNames = new List<string>();
        var discoveredMap = new Dictionary<string, string>();

        foreach (var dllPath in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            try
            {
                using var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadWrite = false });
                foreach (var type in asm.MainModule.Types)
                {
                    if (type.IsInterface || type.IsAbstract || type.IsValueType)
                        continue;

                    var nameProp = type.Properties.FirstOrDefault(p =>
                        p.Name == "Name" &&
                        p.PropertyType.FullName == "System.String");

                    if (nameProp == null) continue;

                    var getter = nameProp.GetMethod;
                    if (getter?.Body?.Instructions.Count >= 2)
                    {
                        var first = getter.Body.Instructions[0];
                        if (first.OpCode == OpCodes.Ldstr && first.Operand is string nameVal)
                        {
                            if (!string.IsNullOrEmpty(nameVal))
                            {
                                discoveredNames.Add(nameVal);
                                discoveredMap[nameVal] = Path.GetFileName(dllPath);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Skip unreadable/native assemblies silently
            }
        }

        return (discoveredNames, discoveredMap);
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
