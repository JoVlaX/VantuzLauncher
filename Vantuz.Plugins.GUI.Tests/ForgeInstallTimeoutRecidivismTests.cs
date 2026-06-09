using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;
using Vantuz.Plugins.Game;
using Xunit;

namespace Vantuz.Plugins.GUI.Tests;

/// <summary>
/// Recidivism prevention: verifies that GameInstallerCommand fails fast with a clear timeout
/// message when the Forge/network installer stalls, instead of hanging forever at 0%.
/// Per INVARIANT_THEORY.md В§1.2 and В§17.
/// </summary>
/// F_doc: {ForgeInstallTimeoutRecidivismTests returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ForgeInstallTimeoutRecidivismTests behavior
public class ForgeInstallTimeoutRecidivismTests
{
    private class NullReporter : IStatusReporter
    {
        /// F_doc: {ReportProgress returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportProgress behavior
        public void ReportProgress(string taskName, double percentage) { }
        /// F_doc: {ReportState returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies ReportState behavior
        public void ReportState(string message) { }
    }

    private class MockSlowProvider : IGameProvider
    {
        public string ProviderName => "SlowForge";
/// F_doc: {CheckVersionAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies CheckVersionAsync behavior

        public Task<VersionCheckResult> CheckVersionAsync(string version, string installDir, CancellationToken ct)
            => Task.FromResult(new VersionCheckResult(false));

        public async Task<InstallResult> InstallVersionAsync(
            string version,
            string installDir,
            IStatusReporter reporter,
            CancellationToken ct,
            TimeSpan? timeout = null)
        {
            // Simulate a stalled network installer that NEVER completes on its own.
            // The only way this returns is if ct is cancelled by the timeout mechanism.
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new InstallResult(true);
        }
/// F_doc: {BuildLaunchParametersAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies BuildLaunchParametersAsync behavior

        public Task<LaunchParameters> BuildLaunchParametersAsync(
            string version,
            string installDir,
            LaunchOptions options,
            CancellationToken ct)
            => throw new NotImplementedException();
/// F_doc: {DisposeAsync returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies DisposeAsync behavior

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GameInstallerCommand_WithSlowProviderAndShortTimeout_ReturnsTimeoutError()
    {
        var context = new CommandContext(CancellationToken.None, new NullReporter());
        context.Set("GameProvider.SlowForge", new MockSlowProvider());

        var installDir = Path.Combine(Path.GetTempPath(), "test").Replace("\\", "\\\\");
        var stepConfig = JsonDocument.Parse($"{{\n            \"provider\": \"SlowForge\",\n            \"version\": \"1.20.1-forge-47.3.0\",\n            \"installDir\": \"{installDir}\",\n            \"operationTimeout\": \"00:00:02\"\n        }}").RootElement;

        var command = new GameInstallerCommand();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await command.ExecuteAsync(context, stepConfig);
        sw.Stop();

        Assert.False(result.Success);
        Assert.Contains("timed out", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        // Upper bound must tolerate loaded CI environments where timer thread scheduling can lag.
        // Per INVARIANT_THEORY В§1.2: the claim is "timeout fires", not "fires at exactly 2 s".
        Assert.True(sw.Elapsed.TotalSeconds < 30,
            $"Expected timeout within a reasonable window, but elapsed {sw.Elapsed.TotalSeconds:F1} s. The command did not respect the timeout.");
    }
}
