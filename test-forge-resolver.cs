// Standalone test for ForgeVersionResolver per INVARIANT_THEORY.md §1.2
using System;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Plugins.Minecraft;
using Vantuz.Core;

class TestReporter : IStatusReporter
{
    public void ReportState(string message) => Console.WriteLine($"[STATE] {message}");
    public void ReportProgress(string operation, double percent) => Console.WriteLine($"[PROGRESS] {operation}: {percent:F1}%");
}

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== Forge Version Resolver Test ===");
        Console.WriteLine("Per INVARIANT_THEORY.md §1.2 Measurability");
        Console.WriteLine();

        var reporter = new TestReporter();
        var ct = CancellationToken.None;

        try
        {
            // Test 1: Query available versions for MC 1.20.1
            Console.WriteLine("[TEST 1] Querying Forge versions for Minecraft 1.20.1...");
            var versions = await ForgeVersionResolver.GetAvailableVersionsAsync("1.20.1", reporter, ct);
            
            if (versions.Count == 0)
            {
                Console.WriteLine("[FAIL] No versions returned - Forge API may be unreachable");
                return 1;
            }
            
            Console.WriteLine($"[PASS] Found {versions.Count} versions");
            Console.WriteLine($"[INFO] First 5 versions: {string.Join(", ", versions.Take(5))}");

            // Test 2: SelectVersion with exact match
            Console.WriteLine("\n[TEST 2] Testing exact version match...");
            var exactVersion = versions[0];
            var selected1 = ForgeVersionSelector.SelectVersion(exactVersion, versions, reporter);
            if (selected1 == exactVersion)
            {
                Console.WriteLine($"[PASS] Exact match works: {exactVersion}");
            }
            else
            {
                Console.WriteLine($"[FAIL] Expected {exactVersion}, got {selected1}");
                return 1;
            }

            // Test 3: SelectVersion with unavailable version (fallback to latest)
            Console.WriteLine("\n[TEST 3] Testing fallback to latest...");
            var unavailableVersion = "99.99.99";
            var selected2 = ForgeVersionSelector.SelectVersion(unavailableVersion, versions, reporter);
            
            if (versions.Contains(selected2))
            {
                Console.WriteLine($"[PASS] Fallback works: {unavailableVersion} -> {selected2}");
            }
            else
            {
                Console.WriteLine($"[FAIL] Fallback returned invalid version: {selected2}");
                return 1;
            }

            // Test 4: Parse Forge version format
            Console.WriteLine("\n[TEST 4] Testing ForgeVersionParser...");
            var parsed = ForgeVersionParser.Parse("1.20.1-forge-47.3.0");
            if (parsed.IsValid && parsed.MinecraftVersion == "1.20.1" && parsed.ForgeVersionNumber == "47.3.0")
            {
                Console.WriteLine($"[PASS] Parser works: MC={parsed.MinecraftVersion}, Forge={parsed.ForgeVersionNumber}");
            }
            else
            {
                Console.WriteLine($"[FAIL] Parser failed for valid version");
                return 1;
            }

            Console.WriteLine("\n=== ALL TESTS PASSED ===");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ERROR] {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[STACK] {ex.StackTrace}");
            return 1;
        }
    }
}
