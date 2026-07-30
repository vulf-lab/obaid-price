using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;

namespace CostWise.App.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly AuthAccountStore _store;
    private readonly AuthSession _session;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _lastLoginText = "—";
    [ObservableProperty] private bool _hasPin;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ProfileViewModel(AuthAccountStore store, AuthSession session)
    {
        _store = store;
        _session = session;
        RefreshFromStore();
    }

    public void RefreshFromStore()
    {
        Email = _store.Email;
        DisplayName = _store.DisplayName;
        HasPin = _store.HasPin;
        LastLoginText = _store.LastLoginUtc is { } utc
            ? utc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "—";
    }

    [RelayCommand]
    private void SaveDisplayName()
    {
        _store.UpdateDisplayName(DisplayName);
        DisplayName = _store.DisplayName;
        StatusMessage = "Display name saved.";
    }

    public bool TryChangePassword(string current, string next, string confirm, out string message)
    {
        if (!string.Equals(next, confirm, StringComparison.Ordinal))
        {
            message = "New password and confirmation do not match.";
            return false;
        }

        if (!_store.TryChangePassword(current, next, out var error))
        {
            message = error;
            return false;
        }

        message = "Password updated.";
        return true;
    }

    public bool TrySavePin(string currentPassword, string pin, string confirm, out string message)
    {
        if (!string.Equals(pin, confirm, StringComparison.Ordinal))
        {
            message = "PIN and confirmation do not match.";
            return false;
        }

        if (!_store.TrySetPin(currentPassword, pin, out var error))
        {
            message = error;
            return false;
        }

        HasPin = true;
        message = "PIN saved. With Remember me, the app will ask for this PIN on launch.";
        return true;
    }

    public bool TryClearPin(string currentPassword, out string message)
    {
        if (!_store.TryClearPin(currentPassword, out var error))
        {
            message = error;
            return false;
        }

        HasPin = false;
        message = "PIN cleared.";
        return true;
    }

    [RelayCommand]
    private void SignOut() => _session.SignOut();
}
