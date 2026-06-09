using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Vantuz.Builder;

/// <summary>
/// Visualizes pipeline dependency graph from boot manifests.
/// Per COMPOSITUM_SPECIFICATION.md В§3.2: Pipeline dependency graph must be explicit.
/// </summary>
/// F_doc: {PipelineVisualizer returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies PipelineVisualizer behavior
public static class PipelineVisualizer
{
    /// F_doc: {Run returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Run behavior
    public static void Run(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project Vantuz.Builder -- verify <boot.json> <plugins-dir>");
            Console.WriteLine("  dotnet run --project Vantuz.Builder -- visualize <boot.json>");
            return;
        }

        var command = args[0].ToLowerInvariant();

        if (command == "verify")
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: dotnet run --project Vantuz.Builder -- verify <boot.json> <plugins-dir>");
                Environment.Exit(1);
                return;
            }
            Environment.Exit(PluginNameVerifier.VerifyManifest(args[1], args[2]));
            return;
        }

        if (command == "visualize")
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: dotnet run --project Vantuz.Builder -- visualize <boot.json>");
                return;
            }
            var manifestPath = args[1];
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
            return;
        }

        Console.Error.WriteLine($"Unknown command: {command}. Use 'verify' or 'visualize'.");
    }

    private static void VisualizePipeline(List<PipelineStep> steps)
    {
        Console.WriteLine();
        Console.WriteLine("в•”в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•—");
        Console.WriteLine("в•‘           PIPELINE DEPENDENCY GRAPH                            в•‘");
        Console.WriteLine("в•љв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ќ");
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
            var arrow = i == 0 ? "   " : "в†“ ";
            
            Console.WriteLine($"{indent}{arrow}[{i + 1}] {step.PluginName}");
            
            if (produces[step.PluginName].Any())
            {
                Console.WriteLine($"{indent}     в””в”Ђ Produces: {string.Join(", ", produces[step.PluginName])}");
            }
            
            if (consumes[step.PluginName].Any())
            {
                Console.WriteLine($"{indent}     в””в”Ђ Consumes: {string.Join(", ", consumes[step.PluginName])}");
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
                Console.WriteLine($"    в””в”Ђ Depends on: {string.Join(" в†’ ", deps)}");
            }
            else
            {
                Console.WriteLine($"  {step.PluginName} (root)");
            }
        }

        Console.WriteLine();
        Console.WriteLine("в•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђв•ђ");

        // DAG verification per INVARIANT_THEORY В§2.1
        var cycle = DetectCycle(dependencies);
        if (cycle != null)
        {
            Console.Error.WriteLine("[ARM-BUILD-021] DAG VIOLATION: Cycle detected in pipeline dependency graph.");
            Console.Error.WriteLine($"  Cycle: {string.Join(" в†’ ", cycle)} в†’ {cycle[0]}");
            throw new InvalidOperationException("Pipeline is not a DAG вЂ” cycle detected.");
        }
        else
        {
            Console.WriteLine("[VERIFY] DAG check passed: |C| = 0 (no cycles)");
        }
    }

    private static List<string>? DetectCycle(Dictionary<string, List<string>> dependencies)
    {
        var inDegree = new Dictionary<string, int>();
        var allNodes = new HashSet<string>(dependencies.Keys);
        foreach (var deps in dependencies.Values)
            foreach (var d in deps)
                allNodes.Add(d);

        foreach (var n in allNodes)
            inDegree[n] = 0;

        foreach (var kvp in dependencies)
            foreach (var dep in kvp.Value)
                if (inDegree.ContainsKey(dep))
                    inDegree[dep]++;

        var queue = new Queue<string>(allNodes.Where(n => inDegree[n] == 0));
        var visited = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            visited++;
            if (dependencies.TryGetValue(current, out var deps))
            {
                foreach (var neighbor in deps)
                {
                    if (inDegree.ContainsKey(neighbor))
                    {
                        inDegree[neighbor]--;
                        if (inDegree[neighbor] == 0)
                            queue.Enqueue(neighbor);
                    }
                }
            }
        }

        if (visited == allNodes.Count)
            return null; // No cycle

        // Find a node that is part of a cycle
        var cycleNode = allNodes.First(n => inDegree.ContainsKey(n) && inDegree[n] > 0);
        var cycle = new List<string>();
        var currentInCycle = cycleNode;
        var visitedInCycle = new HashSet<string>();

        do
        {
            cycle.Add(currentInCycle);
            visitedInCycle.Add(currentInCycle);
            var next = dependencies[currentInCycle].FirstOrDefault(d => inDegree.ContainsKey(d) && inDegree[d] > 0 && !visitedInCycle.Contains(d));
            if (string.IsNullOrEmpty(next))
                next = dependencies[currentInCycle].FirstOrDefault(d => inDegree.ContainsKey(d) && inDegree[d] > 0);
            if (string.IsNullOrEmpty(next))
                break;
            currentInCycle = next;
        } while (currentInCycle != cycleNode && !visitedInCycle.Contains(currentInCycle));

        return cycle.Count > 0 ? cycle : new List<string> { cycleNode };
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
        /// F_doc: {Pipeline returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Pipeline behavior
        public List<PipelineStep> Pipeline { get; set; } = new();
    }

    private class PipelineStep
    {
        /// F_doc: {PluginName returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies PluginName behavior
        public string PluginName { get; set; } = "";
        /// F_doc: {Config returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Config behavior
        public Dictionary<string, JsonElement> Config { get; set; } = new();
    }
}
