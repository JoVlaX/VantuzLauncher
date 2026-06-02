using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Vantuz.Builder;

/// <summary>
/// Visualizes pipeline dependency graph from boot manifests.
/// Per COMPOSITUM_SPECIFICATION.md §3.2: Pipeline dependency graph must be explicit.
/// </summary>
public static class PipelineVisualizer
{
    public static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: dotnet run --project Vantuz.Builder -- visualize <boot.json>");
            return;
        }

        var manifestPath = args[0];
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"Error: Manifest not found: {manifestPath}");
            return;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<BootManifest>(json);
            
            if (manifest?.Pipeline == null)
            {
                Console.Error.WriteLine("Error: Invalid manifest format");
                return;
            }

            VisualizePipeline(manifest.Pipeline);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    private static void VisualizePipeline(List<PipelineStep> steps)
    {
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║           PIPELINE DEPENDENCY GRAPH                            ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Build dependency graph
        var dependencies = new Dictionary<string, List<string>>();
        var produces = new Dictionary<string, List<string>>();
        var consumes = new Dictionary<string, List<string>>();

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var stepName = step.PluginName;
            
            // Determine what this step produces
            var producesList = GetProduces(step, i);
            produces[stepName] = producesList;
            
            // Determine what this step consumes (from previous steps)
            var consumesList = GetConsumes(step, i, steps);
            consumes[stepName] = consumesList;
            
            // Build dependency list
            dependencies[stepName] = new List<string>();
            foreach (var consume in consumesList)
            {
                // Find which step produces this
                for (int j = 0; j < i; j++)
                {
                    if (produces[steps[j].PluginName].Contains(consume))
                    {
                        dependencies[stepName].Add(steps[j].PluginName);
                        break;
                    }
                }
            }
        }

        // Visualize as flow
        Console.WriteLine("Flow Diagram:");
        Console.WriteLine();
        
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var indent = new string(' ', i * 4);
            var arrow = i == 0 ? "   " : "↓ ";
            
            Console.WriteLine($"{indent}{arrow}[{i + 1}] {step.PluginName}");
            
            if (produces[step.PluginName].Any())
            {
                Console.WriteLine($"{indent}     └─ Produces: {string.Join(", ", produces[step.PluginName])}");
            }
            
            if (consumes[step.PluginName].Any())
            {
                Console.WriteLine($"{indent}     └─ Consumes: {string.Join(", ", consumes[step.PluginName])}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Dependency Chain:");
        Console.WriteLine();

        foreach (var step in steps)
        {
            var deps = dependencies[step.PluginName];
            if (deps.Any())
            {
                Console.WriteLine($"  {step.PluginName}");
                Console.WriteLine($"    └─ Depends on: {string.Join(" → ", deps)}");
            }
            else
            {
                Console.WriteLine($"  {step.PluginName} (root)");
            }
        }

        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════════════");
    }

    private static List<string> GetProduces(PipelineStep step, int index)
    {
        var result = new List<string>();
        
        // Common produces by plugin type
        if (step.PluginName.Contains("GUI"))
        {
            if (step.PluginName.Contains("MinecraftLauncher"))
            {
                result.AddRange(new[] { "gui_reporter", "gui_window", "gui.credential_provider" });
            }
            if (step.PluginName.Contains("CredentialCollection"))
            {
                result.AddRange(new[] { "username", "password", "auth.credentials" });
            }
        }
        
        if (step.PluginName.Contains("Auth"))
        {
            result.Add("auth.token");
        }
        
        if (step.PluginName.Contains("ApiReader"))
        {
            result.Add("remoteVersion");
        }

        return result;
    }

    private static List<string> GetConsumes(PipelineStep step, int index, List<PipelineStep> allSteps)
    {
        var result = new List<string>();
        
        // Common consumes by plugin type
        if (step.PluginName.Contains("CredentialCollection"))
        {
            result.Add("gui.credential_provider");
        }
        
        if (step.PluginName.Contains("Auth"))
        {
            result.AddRange(new[] { "username", "password" });
        }
        
        if (step.PluginName.Contains("Update"))
        {
            result.AddRange(new[] { "localVersion", "remoteVersion" });
        }

        return result;
    }

    private class BootManifest
    {
        public List<PipelineStep> Pipeline { get; set; } = new();
    }

    private class PipelineStep
    {
        public string PluginName { get; set; } = "";
        public Dictionary<string, JsonElement> Config { get; set; } = new();
    }
}
