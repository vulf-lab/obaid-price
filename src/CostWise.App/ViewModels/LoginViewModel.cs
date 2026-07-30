using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;

namespace CostWise.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthAccountStore _store;
    private readonly AuthSession _session;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private bool _rememberMe;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isPinMode;
    [ObservableProperty] private bool _isSetupMode;
    [ObservableProperty] private bool _isDamagedMode;
    [ObservableProperty] private bool _isPasswordMode = true;

    public LoginViewModel(AuthAccountStore store, AuthSession session)
    {
        _store = store;
        _session = session;
        ApplyLoadState();
    }

    public bool Succeeded { get; private set; }

    private void ApplyLoadState()
    {
        Email = _store.Email;
        RememberMe = _store.RememberMe;
        IsSetupMode = _store.LoadState == AuthLoadState.NeedsSetup;
        IsDamagedMode = _store.LoadState == AuthLoadState.Damaged;
        IsPinMode = !IsSetupMode && !IsDamagedMode && _store.RememberMe && _store.HasPin;
        IsPasswordMode = !IsSetupMode && !IsDamagedMode && !IsPinMode;
        ErrorMessage = IsDamagedMode
            ? "Account file is damaged or unreadable. Reset to create a new local password (data in the database is unchanged)."
            : string.Empty;
    }

    public bool TryCompleteSetup(string password, string confirm)
    {
        ErrorMessage = string.Empty;
        Succeeded = false;

        if (!_store.TryCompleteSetup(password, confirm, out var error))
        {
            ErrorMessage = error;
            return false;
        }

        _session.MarkAuthenticated(rememberMe: false);
        Succeeded = true;
        return true;
    }

    public bool TryPasswordLogin(string password)
    {
        ErrorMessage = string.Empty;
        Succeeded = false;

        if (!_store.TryAuthenticatePassword(password, out var error))
        {
            ErrorMessage = string.IsNullOrWhiteSpace(error) ? "Invalid email or password." : error;
            return false;
        }

        _session.MarkAuthenticated(RememberMe);
        Succeeded = true;
        return true;
    }

    public bool TryPinUnlock(string pin)
    {
        ErrorMessage = string.Empty;
        Succeeded = false;

        if (!_store.TryAuthenticatePin(pin, out var error))
        {
            ErrorMessage = error;
            return false;
        }

        _session.UnlockRemembered();
        Succeeded = true;
        return true;
    }

    [RelayCommand]
    private void UsePasswordInstead()
    {
        IsPinMode = false;
        IsPasswordMode = true;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void ResetDamagedAccount()
    {
        _store.ResetToSetup();
        ApplyLoadState();
        ErrorMessage = "Account reset. Create a new password to continue.";
    }
}
