namespace Vantuz.Core.Tests;

using System;
using System.Collections.Generic;
using Vantuz.Host;
using Xunit;

/// <summary>
/// Tests for VantuzEngine.InterpolateVariables — the root cause of the
/// "unresolved placeholders in arguments={{mcDir}}" crash (2026-06-07).
///
/// E_doc: Variables in boot.json can reference other variables (e.g. installDir: "{{mcDir}}\\.minecraft").
/// F_doc: InterpolateVariables only searched payload, not already-interpolated variables,
///        so {{mcDir}} in installDir was never resolved.
/// </summary>
public class VariableInterpolationTests
{
    /// <summary>
    /// E_doc: When installDir references mcDir, both resolve correctly.
    /// F_doc: installDir should contain the resolved mcDir path, not literal "{{mcDir}}".
    /// </summary>
    [Fact]
    public void InterpolateVariables_DependentVariables_ResolvesInOrder()
    {
        var variables = new Dictionary<string, string>
        {
            ["mcDir"] = "C:\\Users\\Test\\AppData\\Roaming\\.vantuzlauncher",
            ["installDir"] = "{{mcDir}}\\.minecraft",
            ["authlibPath"] = "{{mcDir}}\\authlib-injector.jar"
        };
        var payload = new Dictionary<string, object>();

        var result = VantuzEngine.InterpolateVariables(variables, payload);

        Assert.Equal("C:\\Users\\Test\\AppData\\Roaming\\.vantuzlauncher", result["mcDir"]);
        Assert.Equal("C:\\Users\\Test\\AppData\\Roaming\\.vantuzlauncher\\.minecraft", result["installDir"]);
        Assert.Equal("C:\\Users\\Test\\AppData\\Roaming\\.vantuzlauncher\\authlib-injector.jar", result["authlibPath"]);

        // Critical: no unresolved placeholders should remain
        foreach (var kvp in result)
        {
            Assert.DoesNotContain("{{", kvp.Value);
            Assert.DoesNotContain("}}", kvp.Value);
        }
    }

    /// <summary>
    /// E_doc: Payload runtime values override manifest variables of the same name.
    /// F_doc: If payload contains "mcDir", it takes precedence over manifest's "mcDir".
    /// </summary>
    [Fact]
    public void InterpolateVariables_PayloadOverridesVariable()
    {
        var variables = new Dictionary<string, string>
        {
            ["mcDir"] = "C:\\Default",
            ["installDir"] = "{{mcDir}}\\.minecraft"
        };
        var payload = new Dictionary<string, object>
        {
            ["mcDir"] = "D:\\Override"
        };

        var result = VantuzEngine.InterpolateVariables(variables, payload);

        // Payload mcDir should be used when resolving installDir
        Assert.Equal("D:\\Override\\.minecraft", result["installDir"]);
    }

    /// <summary>
    /// E_doc: Chained dependencies (A → B → C) resolve transitively.
    /// F_doc: "{{base}}\\foo\\{{bar}}" where bar references base — all resolve.
    /// </summary>
    [Fact]
    public void InterpolateVariables_ChainedDependencies_ResolvesTransitively()
    {
        var variables = new Dictionary<string, string>
        {
            ["base"] = "C:\\Root",
            ["level1"] = "{{base}}\\Level1",
            ["level2"] = "{{level1}}\\Level2",
            ["gamePath"] = "{{level2}}\\game.exe"
        };
        var payload = new Dictionary<string, object>();

        var result = VantuzEngine.InterpolateVariables(variables, payload);

        Assert.Equal("C:\\Root\\Level1\\Level2\\game.exe", result["gamePath"]);
    }

    /// <summary>
    /// E_doc: Circular dependencies do not cause infinite loops.
    /// F_doc: A references B, B references A — no crash, at least one remains unresolved.
    /// </summary>
    [Fact]
    public void InterpolateVariables_CircularDependency_DoesNotHang()
    {
        var variables = new Dictionary<string, string>
        {
            ["a"] = "{{b}}_suffix",
            ["b"] = "{{a}}_prefix"
        };
        var payload = new Dictionary<string, object>();

        // Should complete without hanging or throwing
        var result = VantuzEngine.InterpolateVariables(variables, payload);

        // At least one should still contain unresolved placeholders
        bool hasUnresolved = result["a"].Contains("{{") || result["b"].Contains("{{");
        Assert.True(hasUnresolved, "Circular dependency should leave at least one variable unresolved");
    }

    /// <summary>
    /// E_doc: Environment variables ${env:VAR} are resolved before manifest placeholders.
    /// F_doc: "${env:USERPROFILE}\\.mc" should contain the real user profile path.
    /// </summary>
    [Fact]
    public void InterpolateVariables_EnvironmentVariable_Resolves()
    {
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "C:\\Users\\Default";
        var variables = new Dictionary<string, string>
        {
            ["mcDir"] = "${env:USERPROFILE}\\.vantuzlauncher"
        };
        var payload = new Dictionary<string, object>();

        var result = VantuzEngine.InterpolateVariables(variables, payload);

        Assert.Equal($"{userProfile}\\.vantuzlauncher", result["mcDir"]);
    }

    /// <summary>
    /// E_doc: Special folders ${special:Folder} are resolved correctly.
    /// F_doc: "${special:ApplicationData}\\.vantuzlauncher" maps to AppData\Roaming.
    /// </summary>
    [Fact]
    public void InterpolateVariables_SpecialFolder_Resolves()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var variables = new Dictionary<string, string>
        {
            ["mcDir"] = "${special:ApplicationData}\\.vantuzlauncher"
        };
        var payload = new Dictionary<string, object>();

        var result = VantuzEngine.InterpolateVariables(variables, payload);

        Assert.Equal($"{appData}\\.vantuzlauncher", result["mcDir"]);
    }

    /// <summary>
    /// E_doc: boot.json realistic variable chain mcDir → installDir resolves cleanly.
    /// F_doc: Reproduces the exact crash scenario from 2026-06-07.
    /// </summary>
    [Fact]
    public void InterpolateVariables_RealisticBootJson_NoUnresolvedPlaceholders()
    {
        // Exact variables from boot.json
        var variables = new Dictionary<string, string>
        {
            ["localVersion"] = "2.0-dev",
            ["gameProvider"] = "Minecraft",
            ["gameVersion"] = "1.20.1-forge-47.3.0",
            ["mcDir"] = "${special:ApplicationData}\\.vantuzlauncher",
            ["installDir"] = "{{mcDir}}\\.minecraft"
        };
        var payload = new Dictionary<string, object>();

        var result = VantuzEngine.InterpolateVariables(variables, payload);

        // installDir must NOT contain literal "{{mcDir}}"
        Assert.DoesNotContain("{{mcDir}}", result["installDir"]);
        // installDir must contain the resolved ApplicationData path
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.Contains(appData, result["installDir"]);
        Assert.EndsWith("\\.minecraft", result["installDir"]);
    }
}
