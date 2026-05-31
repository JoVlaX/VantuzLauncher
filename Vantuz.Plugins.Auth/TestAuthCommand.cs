using System.Text.Json;
using Vantuz.Core;

namespace Vantuz.Plugins.Auth;

/// <summary>
/// ARM010 CQRS Command: Test authentication plugin.
/// Per INVARIANT_THEORY.md:76-78 (SRP) - dedicated test auth separate from production.
/// Per INVARIANT_THEORY.md:498 (Explicitness) - explicitly named TestAuthCommand.
/// Per INVARIANT_THEORY.md:17 (Determinism) - returns deterministic mock credentials.
/// Per INVARIANT_THEORY.md:31 (Measurability) - verifiable without network.
/// </summary>
public class TestAuthCommand : ICommandPlugin
{
    /// <summary>
    /// Explicit plugin name per Axiom of Explicitness (498).
    /// Format: {Domain}.{ClassName} for exact matching.
    /// </summary>
    public string Name => "Auth.TestAuthCommand";

    /// <summary>
    /// Executes deterministic test authentication.
    /// No external network calls - satisfies Nomadic Invariant.
    /// </summary>
    public Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        // Deterministic test credentials per Axiom of Determinism (17)
        // Same result every run - no variance from network or external state
        var testToken = "TEST_TOKEN_VANTUZ_2026";
        var testUuid = "00000000-0000-0000-0000-000000000001";
        var testUsername = context.Get<string>("username") ?? "TestPlayer";
        
        // Store auth results in payload for downstream plugins (Game, OS)
        context.Set("auth_token", testToken);
        context.Set("auth_uuid", testUuid);
        context.Set("auth_username", testUsername);
        context.Set("auth_success", true);
        context.Set("auth_timestamp", DateTime.UtcNow.ToString("o"));

        // Report for test visibility (Measurability per 31)
        context.Reporter.ReportState("[TEST] Authentication successful (mock)");
        context.Reporter.ReportProgress("Auth.TestAuthCommand", 100.0);

        return Task.FromResult(new CommandResult(true));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
