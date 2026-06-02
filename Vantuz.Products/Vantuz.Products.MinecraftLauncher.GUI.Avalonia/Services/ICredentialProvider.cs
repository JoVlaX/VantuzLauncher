using System;
using Vantuz.Core;

namespace Vantuz.Products.MinecraftLauncher.GUI.Avalonia.Services;

public delegate void CredentialsSubmittedHandler(object sender, CredentialsSubmittedEventArgs e);
public delegate void CredentialsCancelledHandler(object sender, EventArgs e);
public delegate void StateChangedHandler(object sender, string message);
public delegate void ProgressChangedHandler(object sender, ProgressEventArgs e);

public interface ICredentialProvider
{
    event CredentialsSubmittedHandler? CredentialsSubmitted;
    event CredentialsCancelledHandler? CredentialsCancelled;
    
    Task<Credentials> GetCredentialsAsync(CancellationToken ct);
    void SubmitCredentials(string username, string password);
    void Cancel();
}

public record Credentials(string Username, string Password);

public class CredentialsSubmittedEventArgs : EventArgs
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public record ProgressEventArgs(string OperationId, double Percent);
