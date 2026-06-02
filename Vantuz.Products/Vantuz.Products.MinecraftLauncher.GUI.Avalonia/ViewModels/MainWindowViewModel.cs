using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using Vantuz.Products.MinecraftLauncher.GUI.Avalonia.Services;

namespace Vantuz.Products.MinecraftLauncher.GUI.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject, ICredentialProvider
{
    private readonly GUIProgressReporter _reporter;
    private readonly TaskCompletionSource<Credentials> _credentialsTcs;
    
    [ObservableProperty]
    private string _username = string.Empty;
    
    [ObservableProperty]
    private string _password = string.Empty;
    
    [ObservableProperty]
    private string _currentStatus = "Введите логин и пароль";
    
    [ObservableProperty]
    private double _overallProgress = 0;
    
    [ObservableProperty]
    private bool _isLoading = false;
    
    public MainWindowViewModel() : this(new GUIProgressReporter()) { }
    
    public MainWindowViewModel(GUIProgressReporter reporter)
    {
        _reporter = reporter;
        _credentialsTcs = new TaskCompletionSource<Credentials>();
        
        _reporter.ProgressChanged += (sender, e) => OverallProgress = e.Percent;
    }
    
    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            CurrentStatus = "Ошибка: заполните все поля";
            return;
        }
        
        IsLoading = true;
        
        var credentials = new Credentials(Username, Password);
        CredentialsSubmitted?.Invoke(this, new CredentialsSubmittedEventArgs 
        { 
            Username = Username, 
            Password = Password 
        });
        
        _credentialsTcs.TrySetResult(credentials);
        
        CurrentStatus = "Аутентификация...";
    }
    
    public event CredentialsSubmittedHandler? CredentialsSubmitted;
    public event CredentialsCancelledHandler? CredentialsCancelled;
    
    public Task<Credentials> GetCredentialsAsync(CancellationToken ct)
    {
        ct.Register(() => _credentialsTcs.TrySetCanceled());
        return _credentialsTcs.Task;
    }
    
    public void SubmitCredentials(string username, string password)
    {
        Username = username;
        Password = password;
        LoginCommand.ExecuteAsync(null);
    }
    
    public void Cancel()
    {
        _credentialsTcs.TrySetCanceled();
        CredentialsCancelled?.Invoke(this, System.EventArgs.Empty);
    }
}
