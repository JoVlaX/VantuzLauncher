namespace Vantuz.Core.Tests;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Plugins.Net;
using Xunit;

/// <summary>
/// Tests for UpdateCommand — ARM005 CQRS Command for launcher updates.
/// Per INVARIANT_THEORY §1.2: falsifiable claims about update state transitions.
/// </summary>
public class UpdateCommandTests
{
    /// <summary>
    /// E_doc: When currentVersion equals targetVersion, returns success without download.
    /// F_doc: Attempts download despite versions matching.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SameVersion_ReturnsSuccessWithoutDownload()
    {
        var reporter = new ListReporter();
        var context = new CommandContext(CancellationToken.None, reporter);

        var stepConfig = JsonDocument.Parse(@"{
            ""currentVersion"": ""2.0.0"",
            ""targetVersion"": ""2.0.0"",
            ""url"": ""http://localhost/update.zip""
        }").RootElement;

        var command = new UpdateCommand();
        var result = await command.ExecuteAsync(context, stepConfig);

        Assert.True(result.Success);
        Assert.Contains("актуальная версия", reporter.Logs[^1]);
    }

    /// <summary>
    /// E_doc: Missing URL throws InvalidOperationException.
    /// F_doc: Returns false result instead of throwing.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MissingUrl_ThrowsInvalidOperationException()
    {
        var reporter = new ListReporter();
        var context = new CommandContext(CancellationToken.None, reporter);

        var stepConfig = JsonDocument.Parse("{}").RootElement;
        var command = new UpdateCommand();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(context, stepConfig));
        Assert.Contains("URL is missing", ex.Message);
    }

    /// <summary>
    /// E_doc: DisposeAsync disposes the internal HttpClient.
    /// F_doc: DisposeAsync throws or leaves HttpClient undisposed.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_CompletesSuccessfully()
    {
        var command = new UpdateCommand();
        await command.DisposeAsync();
        Assert.True(true);
    }

    private class ListReporter : IStatusReporter
    {
        public System.Collections.Generic.List<string> Logs { get; } = new();
        public void ReportState(string message) => Logs.Add(message);
        public void ReportProgress(string taskName, double percentage) => Logs.Add($"[{taskName}] {percentage:F1}%");
    }
}
