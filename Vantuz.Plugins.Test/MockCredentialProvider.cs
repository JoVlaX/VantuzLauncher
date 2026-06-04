using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Core;
using Vantuz.Host;

namespace Vantuz.Plugins.Test;

/// <summary>
/// Mock credential provider for headless testing.
/// Per INVARIANT_THEORY.md §1.2 Measurability: Automated verification without human interaction.
/// Per runtime-verification-limitations.md: Enables agentic self-testing.
/// </summary>
public class MockCredentialProvider : ICommandPlugin
{
    public string Name => "Test.MockCredentialProvider";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        var config = stepConfig.Deserialize<MockConfig>();
        var credentials = new Credentials(
            config?.Username ?? "test_user",
            config?.Password ?? "test_pass",
            config?.RememberMe ?? false,
            config?.RamMb ?? 4096
        );

        // Register in context for downstream plugins
        context.Set("gui.credential_provider", new CredentialProviderImpl(credentials));
        context.Set("username", credentials.Username);
        context.Set("password", credentials.Password);
        context.Set("auth.credentials", credentials);

        context.Reporter.ReportState($"[MOCK] Credentials auto-submitted for user: {credentials.Username}");

        // Simulate async delay like real GUI
        await Task.Delay(100);

        return new CommandResult(true);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private record MockConfig(string? Username, string? Password, bool? RememberMe, int? RamMb);

    private class CredentialProviderImpl : ICredentialProvider
    {
        private readonly Credentials _credentials;

        public CredentialProviderImpl(Credentials credentials) => _credentials = credentials;

        public Task<Credentials> CollectAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_credentials);

        public void ShowProgress() { }
        public void UpdateStatus(string message) { }

        #pragma warning disable CS0067 // Events unused in test stub
        public event EventHandler<Vantuz.Core.CredentialsSubmittedEventArgs>? CredentialsSubmitted;
        public event EventHandler? CredentialsCancelled;
        #pragma warning restore CS0067
    }
}
