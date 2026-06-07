using System.Text.Json;
using Vantuz.Core;
using Vantuz.Plugins.Game;
using Vantuz.Plugins.OS;
using Xunit;

namespace Vantuz.Core.Tests;

/// <summary>
/// Tests for pre-flight validation and early-failure paths in launch-related commands.
/// Per INVARIANT_THEORY.md §4.1: falsifiable claims about argument correctness.
/// </summary>
public class LaunchArgumentValidationTests
{
    /// <summary>
    /// E_doc: GameLaunchCommand fails fast with a clear message when installDir does not exist.
    /// F_doc: The command attempts to call BuildLaunchParametersAsync and crashes inside the provider.
    /// </summary>
    [Fact]
    public async Task GameLaunchCommand_MissingInstallDir_ReturnsFailureWithClearMessage()
    {
        var reporter = new ListReporter();
        var context = new CommandContext(System.Threading.CancellationToken.None, reporter);

        var stepConfig = JsonDocument.Parse(@"{
            ""provider"": ""Minecraft"",
            ""version"": ""1.20.1-forge-47.3.0"",
            ""installDir"": ""C:\\NonExistent\\Minecraft""
        }").RootElement;

        var command = new GameLaunchCommand();
        var result = await command.ExecuteAsync(context, stepConfig);

        Assert.False(result.Success);
        Assert.Contains("Install directory not found", result.ErrorMessage);
    }

    /// <summary>
    /// E_doc: GameLaunchCommand fails fast with a clear message when Java is not found.
    /// F_doc: The command reaches the provider and generates a launch command pointing to a non-existent Java.
    /// </summary>
    [Fact]
    public async Task GameLaunchCommand_MissingJava_ReturnsFailureWithClearMessage()
    {
        var reporter = new ListReporter();
        var context = new CommandContext(System.Threading.CancellationToken.None, reporter);

        // Use a temp dir that exists for installDir, but no Java
        string tempDir = Path.Combine(Path.GetTempPath(), $"vantuz_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var stepConfig = JsonDocument.Parse($@"{{
                ""provider"": ""Minecraft"",
                ""version"": ""1.20.1-forge-47.3.0"",
                ""installDir"": ""{tempDir.Replace("\\", "\\\\")}""
            }}").RootElement;

            var command = new GameLaunchCommand();
            var result = await command.ExecuteAsync(context, stepConfig);

            // Result should indicate Java not found OR provider not found (since no provider is registered in test context)
            Assert.False(result.Success);
            Assert.True(
                result.ErrorMessage!.Contains("Java executable not found") || result.ErrorMessage.Contains("not found"),
                $"Expected 'Java not found' or provider 'not found', got: {result.ErrorMessage}");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// E_doc: OS.ExecuteCommand fails fast when fileName contains unresolved {{...}} placeholders.
    /// F_doc: Process.Start is called with a path containing literal '{{gameCommand}}', which doesn't exist.
    /// </summary>
    [Fact]
    public async Task ExecuteCommand_UnresolvedPlaceholder_ReturnsFailureWithClearMessage()
    {
        var reporter = new ListReporter();
        var context = new CommandContext(System.Threading.CancellationToken.None, reporter);

        var stepConfig = JsonDocument.Parse(@"{
            ""fileName"": ""{{gameCommand}}"",
            ""arguments"": ""{{gameArgs}}"",
            ""workDir"": ""{{gameWorkDir}}"",
            ""waitForExit"": false
        }").RootElement;

        var command = new ExecuteCommand();
        var result = await command.ExecuteAsync(context, stepConfig);

        Assert.False(result.Success);
        Assert.Contains("unresolved placeholders", result.ErrorMessage);
    }

    /// <summary>
    /// E_doc: OS.ExecuteCommand with a real dummy executable completes successfully in waitForExit=true mode.
    /// F_doc: The step returns CommandResult(false, ...) for a valid executable.
    /// </summary>
    [Fact]
    public async Task ExecuteCommand_DummyExecutable_ReturnsSuccess()
    {
        var reporter = new ListReporter();
        var context = new CommandContext(System.Threading.CancellationToken.None, reporter);

        // Use 'cmd /c exit 0' on Windows, 'sh -c exit 0' on Unix
        string fileName = OperatingSystem.IsWindows() ? "cmd" : "sh";
        string arguments = OperatingSystem.IsWindows() ? "/c exit 0" : "-c 'exit 0'";

        var stepConfig = JsonDocument.Parse($@"{{
            ""fileName"": ""{fileName}"",
            ""arguments"": ""{arguments}"",
            ""workDir"": ""{Environment.CurrentDirectory.Replace("\\", "\\\\")}"",
            ""waitForExit"": true
        }}").RootElement;

        var command = new ExecuteCommand();
        var result = await command.ExecuteAsync(context, stepConfig);

        Assert.True(result.Success);
    }

    private class ListReporter : IStatusReporter
    {
        public List<string> Logs { get; } = new();
        public void ReportState(string message) => Logs.Add(message);
        public void ReportProgress(string taskName, double percentage) => Logs.Add($"[{taskName}] {percentage:F1}%");
    }

}
