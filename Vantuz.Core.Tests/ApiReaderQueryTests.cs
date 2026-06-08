namespace Vantuz.Core.Tests;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Plugins.Net;
using Xunit;

/// <summary>
/// Tests for ApiReaderQuery — ARM005 CQRS Query for reading API data.
/// Per INVARIANT_THEORY §1.2: falsifiable claims about request/response handling.
/// </summary>
public class ApiReaderQueryTests
{
    /// <summary>
    /// E_doc: ExecuteAsync throws InvalidOperationException when URL is missing.
    /// F_doc: ExecuteAsync returns null or succeeds without URL.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MissingUrl_ThrowsInvalidOperationException()
    {
        var reporter = new ListReporter();
        var context = new QueryContext(new Dictionary<string, object>(), CancellationToken.None, reporter);
        var stepConfig = JsonDocument.Parse(@"{ ""payloadKey"": ""test"" }").RootElement;

        var query = new ApiReaderQuery();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ExecuteAsync(context, stepConfig));
        Assert.Contains("URL is missing", ex.Message);
    }

    /// <summary>
    /// E_doc: ExecuteAsync throws InvalidOperationException when payloadKey is missing.
    /// F_doc: ExecuteAsync returns a result without payloadKey.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MissingPayloadKey_ThrowsInvalidOperationException()
    {
        var reporter = new ListReporter();
        var context = new QueryContext(new Dictionary<string, object>(), CancellationToken.None, reporter);
        var stepConfig = JsonDocument.Parse(@"{ ""url"": ""http://localhost"" }").RootElement;

        var query = new ApiReaderQuery();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.ExecuteAsync(context, stepConfig));
        Assert.Contains("payloadKey is missing", ex.Message);
    }

    /// <summary>
    /// E_doc: DisposeAsync completes without error.
    /// F_doc: DisposeAsync throws or hangs.
    /// </summary>
    [Fact]
    public async Task DisposeAsync_CompletesSuccessfully()
    {
        var query = new ApiReaderQuery();
        await query.DisposeAsync();
        Assert.True(true);
    }

    private class ListReporter : IStatusReporter
    {
        public System.Collections.Generic.List<string> Logs { get; } = new();
        public void ReportState(string message) => Logs.Add(message);
        public void ReportProgress(string taskName, double percentage) => Logs.Add($"[{taskName}] {percentage:F1}%");
    }
}
