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

        // Extended invariant checks per DEVIATION-005
        int invariantResult = VerifyPluginInvariants(pluginsDir);
        if (invariantResult != 0) return invariantResult;

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

    private static int VerifyPluginInvariants(string pluginsDir)
    {
        int exitCode = 0;

        var cqrsViolations = VerifyCQRS(pluginsDir);
        if (cqrsViolations.Count > 0)
        {
            Console.Error.WriteLine("[VERIFY] ARM-BUILD-022: CQRS separation violations detected.");
            foreach (var v in cqrsViolations) Console.Error.WriteLine($"  {v}");
            exitCode = 1;
        }

        var resourceViolations = VerifyResources(pluginsDir);
        if (resourceViolations.Count > 0)
        {
            Console.Error.WriteLine("[VERIFY] ARM-BUILD-023: Resource category violations detected.");
            foreach (var v in resourceViolations) Console.Error.WriteLine($"  {v}");
            exitCode = 1;
        }

        var scopeViolations = VerifyScope(pluginsDir);
        if (scopeViolations.Count > 0)
        {
            Console.Error.WriteLine("[VERIFY] ARM-BUILD-024: Scope violations detected.");
            foreach (var v in scopeViolations) Console.Error.WriteLine($"  {v}");
            exitCode = 1;
        }

        var nomadicViolations = VerifyNomadic(pluginsDir);
        if (nomadicViolations.Count > 0)
        {
            Console.Error.WriteLine("[VERIFY] ARM-BUILD-026: Nomadic/Transdomain primitive violations detected.");
            foreach (var v in nomadicViolations) Console.Error.WriteLine($"  {v}");
            exitCode = 1;
        }

        return exitCode;
    }

    private static List<string> VerifyCQRS(string pluginsDir)
    {
        var violations = new List<string>();
        foreach (var dllPath in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            try
            {
                using var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadWrite = false });
                foreach (var type in asm.MainModule.Types)
                {
                    if (type.IsInterface || type.IsAbstract || type.IsValueType) continue;

                    bool hasCommand = type.Interfaces.Any(i =>
                        i.InterfaceType.Name.Contains("Command") ||
                        type.Methods.Any(m => m.Name.Contains("Command") || m.Name.Contains("Execute")));
                    bool hasQuery = type.Interfaces.Any(i =>
                        i.InterfaceType.Name.Contains("Query") ||
                        type.Methods.Any(m => m.Name.Contains("Query") || m.Name.Contains("Get")));

                    if (hasCommand && hasQuery)
                    {
                        violations.Add($"{type.FullName} in {Path.GetFileName(dllPath)}: mixes Command and Query characteristics");
                    }
                }
            }
            catch { /* Skip unreadable assemblies */ }
        }
        return violations;
    }

    private static readonly string[] ForbiddenResourceTypes = new[]
    {
        "System.IO.FileStream",
        "System.Net.Http.HttpClient",
        "System.Net.HttpWebRequest",
        "System.Diagnostics.Process"
    };

    private static List<string> VerifyResources(string pluginsDir)
    {
        var violations = new List<string>();
        foreach (var dllPath in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            try
            {
                using var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadWrite = false });
                foreach (var type in asm.MainModule.Types)
                {
                    if (type.IsInterface || type.IsAbstract || type.IsValueType) continue;

                    foreach (var method in type.Methods.Where(m => m.HasBody))
                    {
                        foreach (var instr in method.Body.Instructions)
                        {
                            if (instr.Operand is MethodReference mr)
                            {
                                var declType = mr.DeclaringType.FullName;
                                if (ForbiddenResourceTypes.Any(f => declType.StartsWith(f)))
                                {
                                    violations.Add($"{type.FullName}.{method.Name} in {Path.GetFileName(dllPath)}: references {declType}");
                                }
                            }
                            if (instr.Operand is TypeReference tr)
                            {
                                var typeName = tr.FullName;
                                if (ForbiddenResourceTypes.Any(f => typeName.StartsWith(f)))
                                {
                                    violations.Add($"{type.FullName}.{method.Name} in {Path.GetFileName(dllPath)}: references type {typeName}");
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Skip unreadable assemblies */ }
        }
        return violations;
    }

    private static List<string> VerifyScope(string pluginsDir)
    {
        var violations = new List<string>();
        var allowedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mscorlib", "System", "System.Core", "netstandard", "System.Runtime",
            "System.Collections", "System.Linq", "System.Text.Json", "Mono.Cecil"
        };

        foreach (var dllPath in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            try
            {
                using var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadWrite = false });
                var pluginAssemblyName = asm.Name.Name;
                var pluginDir = Path.GetDirectoryName(dllPath)!;
                var pluginDllNames = new HashSet<string>(
                    Directory.GetFiles(pluginDir, "*.dll")
                             .Select(f => Path.GetFileNameWithoutExtension(f)),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var type in asm.MainModule.Types)
                {
                    if (type.IsInterface || type.IsAbstract || type.IsValueType) continue;

                    foreach (var method in type.Methods.Where(m => m.HasBody))
                    {
                        foreach (var instr in method.Body.Instructions)
                        {
                            if (instr.Operand is MemberReference mr)
                            {
                                var refAssembly = mr.DeclaringType.Scope.Name;
                                if (!allowedAssemblies.Contains(refAssembly) &&
                                    !pluginDllNames.Contains(refAssembly) &&
                                    !refAssembly.Equals(pluginAssemblyName, StringComparison.OrdinalIgnoreCase))
                                {
                                    violations.Add($"{type.FullName}.{method.Name} in {Path.GetFileName(dllPath)}: references external assembly {refAssembly}");
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Skip unreadable assemblies */ }
        }
        return violations;
    }

    private static readonly string[] ForbiddenHostSpecificTypes = new[]
    {
        "System.Windows.Forms",
        "Microsoft.AspNetCore",
        "System.Web",
        "System.ServiceModel",
        "System.Drawing",
        "System.Web.UI",
        "Microsoft.Win32"
    };

    private static List<string> VerifyNomadic(string pluginsDir)
    {
        var violations = new List<string>();
        foreach (var dllPath in Directory.GetFiles(pluginsDir, "*.dll"))
        {
            try
            {
                using var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadWrite = false });
                foreach (var type in asm.MainModule.Types)
                {
                    if (type.IsInterface || type.IsAbstract || type.IsValueType) continue;

                    // Detect P/Invoke
                    foreach (var method in type.Methods)
                    {
                        if (method.IsPInvokeImpl)
                        {
                            violations.Add($"{type.FullName}.{method.Name} in {Path.GetFileName(dllPath)}: uses P/Invoke (platform-specific)");
                        }
                    }

                    // Detect custom attributes with host-specific names
                    foreach (var attr in type.CustomAttributes)
                    {
                        var attrName = attr.AttributeType.FullName;
                        if (attrName.Contains("HostSpecific") || attrName.Contains("Platform") || attrName.Contains("WindowsOnly"))
                        {
                            violations.Add($"{type.FullName} in {Path.GetFileName(dllPath)}: has host-specific attribute {attrName}");
                        }
                    }

                    // Detect forbidden host-specific references in method bodies
                    foreach (var method in type.Methods.Where(m => m.HasBody))
                    {
                        foreach (var instr in method.Body.Instructions)
                        {
                            if (instr.Operand is MethodReference mr)
                            {
                                var declType = mr.DeclaringType.FullName;
                                if (ForbiddenHostSpecificTypes.Any(f => declType.StartsWith(f)))
                                {
                                    violations.Add($"{type.FullName}.{method.Name} in {Path.GetFileName(dllPath)}: references host-specific type {declType}");
                                }
                            }
                            if (instr.Operand is TypeReference tr)
                            {
                                var typeName = tr.FullName;
                                if (ForbiddenHostSpecificTypes.Any(f => typeName.StartsWith(f)))
                                {
                                    violations.Add($"{type.FullName}.{method.Name} in {Path.GetFileName(dllPath)}: references host-specific type {typeName}");
                                }
                            }
                        }
                    }
                }
            }
            catch { /* Skip unreadable assemblies */ }
        }
        return violations;
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
