namespace CostWise.App.Services;

public sealed class AuthSession
{
    private readonly AuthAccountStore _store;

    public AuthSession(AuthAccountStore store) => _store = store;

    public bool IsAuthenticated { get; private set; }

    public event Action? SignedOut;

    public void MarkAuthenticated(bool rememberMe)
    {
        _store.SetRememberMe(rememberMe);
        _store.TouchLastLogin();
        IsAuthenticated = true;
    }

    /// <summary>Unlock via remembered session (auto or after PIN) without changing RememberMe.</summary>
    public void UnlockRemembered()
    {
        _store.TouchLastLogin();
        IsAuthenticated = true;
    }

    public void SignOut()
    {
        _store.ClearRememberMe();
        IsAuthenticated = false;
        SignedOut?.Invoke();
    }
}
