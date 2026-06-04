namespace Vantuz.Plugins.GUI.MinecraftLauncher;

/// <summary>
/// Credentials container for authentication.
/// Per INVARIANT_THEORY.md В§2.2 CQRS: immutable data transfer object.
/// </summary>
public sealed class Credentials
{
    public string Username { get; }
    public string Password { get; }
    public bool RememberMe { get; }
    public int RamMb { get; }

    public Credentials(string username, string password, bool rememberMe, int ramMb)
    {
        Username = username ?? throw new ArgumentNullException(nameof(username));
        Password = password ?? throw new ArgumentNullException(nameof(password));
        RememberMe = rememberMe;
        RamMb = ramMb;
    }
}

/// <summary>
/// Interface for collecting user credentials through GUI.
/// Per COMPOSITUM_SPECIFICATION.md В§4.2: Plugin-provided capability, not Core concern.
/// </summary>
public interface ICredentialProvider
{
    /// <summary>
    /// Asynchronously collect credentials from user.
    /// Blocks until user submits or cancels.
    /// </summary>
    Task<Credentials> CollectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signal that credentials have been collected and auth is in progress.
    /// Transitions UI from "input mode" to "progress mode".
    /// </summary>
    void ShowProgress();

    /// <summary>
    /// Update status message during authentication flow.
    /// </summary>
    void UpdateStatus(string message);

    /// <summary>
    /// Event raised when credentials are submitted by user.
    /// </summary>
    event EventHandler<CredentialsSubmittedEventArgs>? CredentialsSubmitted;

    /// <summary>
    /// Event raised when user cancels credential collection.
    /// </summary>
    event EventHandler? CredentialsCancelled;
}

public sealed class CredentialsSubmittedEventArgs : EventArgs
{
    public Credentials Credentials { get; }
    public CredentialsSubmittedEventArgs(Credentials credentials) => Credentials = credentials;
}
