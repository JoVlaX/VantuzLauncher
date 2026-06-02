using System.Text.Json;
using Vantuz.Core;
using Vantuz.Host;

namespace Vantuz.Products.MinecraftLauncher.GUI;

/// <summary>
/// Pipeline step for collecting user credentials through GUI.
/// Per INVARIANT_THEORY.md §2.2 CQRS: Command (UI interaction) separate from Query (auth validation).
/// Per COMPOSITUM_SPECIFICATION.md §4.1: Plugin scope, not Core.
/// </summary>
public class CredentialCollectionStep : ICommandPlugin
{
    public string Name => "GUI.CredentialCollection";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, JsonElement stepConfig)
    {
        var cts = new CancellationTokenSource();
        if (context.Get<CancellationToken>("cancellation_token") is CancellationToken parentToken)
        {
            parentToken.Register(() => cts.Cancel());
        }

        try
        {
            // Get credential provider from GUI plugin
            var credentialProvider = context.Get<ICredentialProvider>("gui.credential_provider");
            if (credentialProvider == null)
            {
                return new CommandResult(false, "Credential provider not available. Ensure GUI.MinecraftLauncher step executed first.");
            }

            // Collect credentials from user (blocks until user submits)
            var credentials = await credentialProvider.CollectAsync(cts.Token);

            // Store in context for downstream plugins (Auth, etc.)
            // Per INVARIANT_THEORY.md §2.1: keys must match consumer expectations (YggdrasilPlugin expects "username"/"password")
            context.Set("auth.credentials", credentials);
            context.Set("username", credentials.Username);  // YggdrasilPlugin.cs:22 expects "username"
            context.Set("password", credentials.Password);  // YggdrasilPlugin.cs:23 expects "password"
            context.Set("launch.ram_mb", credentials.RamMb);

            // Transition UI to progress mode
            credentialProvider.ShowProgress();
            credentialProvider.UpdateStatus("Authenticating...");

            return new CommandResult(true);
        }
        catch (OperationCanceledException)
        {
            return new CommandResult(false, "Credential collection cancelled by user.");
        }
        catch (Exception ex)
        {
            return new CommandResult(false, $"Credential collection failed: {ex.Message}");
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
