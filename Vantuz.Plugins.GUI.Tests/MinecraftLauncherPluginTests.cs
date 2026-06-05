using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using Vantuz.Core;
using Vantuz.Host;
using Vantuz.Plugins.GUI.MinecraftLauncher;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// Unit tests for MinecraftLauncherGUIPlugin.
/// Per INVARIANT_THEORY.md §1.2: plugin must return immediately so scheduler
/// can proceed to downstream steps (CredentialCollection, Auth, etc.).
/// </summary>
public class MinecraftLauncherPluginTests
{
    /// <summary>
    /// E_doc: In standalone mode (Application.Current == null), ExecuteAsync returns
    ///       within 1 second and publishes gui.credential_provider to context.
    /// F_doc: Plugin blocks pipeline with await Task.Delay(-1) or fails to publish
    ///       gui.credential_provider, causing downstream "Credential provider not available" error.
    /// </summary>
    [StaFact]
    public async Task ExecuteAsync_StandaloneMode_ReturnsImmediately_And_PublishesCredentialProvider()
    {
        // Arrange: ensure NO Application.Current (standalone mode simulation)
        // If another test leaked an Application, shut it down first.
        if (System.Windows.Application.Current != null)
        {
            System.Windows.Application.Current.Shutdown();
            await Task.Delay(300);
        }

        var reporter = new TestReporter();
        var context = new CommandContext(CancellationToken.None, reporter);
        var plugin = new MinecraftLauncherGUIPlugin();

        // Act: plugin must return within 1 second (previously blocked forever)
        var sw = Stopwatch.StartNew();
        var result = await plugin.ExecuteAsync(context, JsonDocument.Parse("{}").RootElement);
        sw.Stop();

        try
        {
            // Assert: returned quickly — the critical fix
            Assert.True(result.Success, $"Plugin failed: {result.ErrorMessage}");
            Assert.True(sw.ElapsedMilliseconds < 5000,
                $"Plugin blocked for {sw.ElapsedMilliseconds}ms — expected immediate return (<5000ms). " +
                "If this fails, the plugin still contains await Task.Delay(-1) which blocks the QuantumScheduler pipeline.");

            // Assert: published credential provider to context for downstream CredentialCollection step
            var credentialProvider = context.Get<Vantuz.Plugins.GUI.MinecraftLauncher.ICredentialProvider>("gui.credential_provider");
            Assert.NotNull(credentialProvider);
        }
        finally
        {
            // Cleanup: close window and shutdown standalone Application
            await plugin.DisposeAsync();
            // Give the standalone STA thread time to process Shutdown
            await Task.Delay(500);
        }
    }

    private class TestReporter : IStatusReporter
    {
        public List<string> Logs { get; } = new();
        public void ReportState(string message) => Logs.Add(message);
        public void ReportProgress(string taskName, double percentage) { }
    }
}
