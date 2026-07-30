using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CostWise.App.Services;

public enum AuthLoadState
{
    Ready,
    NeedsSetup,
    Damaged
}

public sealed class AuthAccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private static readonly Regex PinPattern = new(@"^\d{4,8}$", RegexOptions.Compiled);

    public const string DefaultEmail = "orehman.ai@gmail.com";
    private const string DefaultDisplayName = "Orehman";
    private const int MaxFailedAttempts = 5;
    private const int LockoutSeconds = 30;

    private readonly string _filePath;
    private UserAccountDto _account = new();
    private int _failedAttempts;
    private DateTime? _lockoutUntilUtc;

    public AuthLoadState LoadState { get; private set; } = AuthLoadState.NeedsSetup;

    public string Email => string.IsNullOrWhiteSpace(_account.Email) ? DefaultEmail : _account.Email;
    public string DisplayName => string.IsNullOrWhiteSpace(_account.DisplayName) ? DefaultDisplayName : _account.DisplayName;
    public bool RememberMe => _account.RememberMe;
    public DateTime? LastLoginUtc => _account.LastLoginUtc;
    public bool HasPin =>
        !string.IsNullOrEmpty(_account.PinSalt) && !string.IsNullOrEmpty(_account.PinHash);

    public bool IsLockedOut =>
        _lockoutUntilUtc is { } until && until > DateTime.UtcNow;

    public TimeSpan? LockoutRemaining =>
        IsLockedOut ? _lockoutUntilUtc!.Value - DateTime.UtcNow : null;

    public AuthAccountStore(string? filePathOverride = null)
    {
        _filePath = filePathOverride ?? DefaultFilePath();
        Load();
    }

    public static AuthAccountStore CreateAndLoad() => new();

    /// <summary>Intentional wipe → first-run setup (does not seed a known password).</summary>
    public void ResetToSetup()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
        _account = new UserAccountDto { Email = DefaultEmail, DisplayName = DefaultDisplayName };
        LoadState = AuthLoadState.NeedsSetup;
        _failedAttempts = 0;
        _lockoutUntilUtc = null;
    }

    public bool TryCompleteSetup(string password, string confirmPassword, out string error)
    {
        if (LoadState != AuthLoadState.NeedsSetup)
        {
            error = "Account is already set up.";
            return false;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            error = "Password and confirmation do not match.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            error = "Password must be at least 8 characters.";
            return false;
        }

        var (salt, hash) = PasswordHasher.Hash(password);
        _account = new UserAccountDto
        {
            Email = DefaultEmail,
            DisplayName = DefaultDisplayName,
            PasswordSalt = salt,
            PasswordHash = hash,
            RememberMe = false,
            LastLoginUtc = null
        };
        Save();
        LoadState = AuthLoadState.Ready;
        error = string.Empty;
        return true;
    }

    public bool TryAuthenticatePassword(string password, out string error)
    {
        error = string.Empty;
        if (LoadState != AuthLoadState.Ready)
        {
            error = LoadState == AuthLoadState.Damaged
                ? "Account file is damaged. Reset the account from the login screen to continue."
                : "Create a password to finish setup.";
            return false;
        }

        if (!EnsureNotLockedOut(out error))
            return false;

        if (!PasswordHasher.Verify(password, _account.PasswordSalt, _account.PasswordHash))
        {
            RegisterFailure(out error);
            return false;
        }

        ClearFailures();
        return true;
    }

    public bool TryAuthenticatePin(string pin, out string error)
    {
        error = string.Empty;
        if (LoadState != AuthLoadState.Ready || !HasPin)
        {
            error = "PIN is not available.";
            return false;
        }

        if (!EnsureNotLockedOut(out error))
            return false;

        if (!PasswordHasher.Verify(pin, _account.PinSalt!, _account.PinHash!))
        {
            RegisterFailure(out error);
            return false;
        }

        ClearFailures();
        return true;
    }

    public void SetRememberMe(bool value)
    {
        if (LoadState != AuthLoadState.Ready) return;
        _account.RememberMe = value;
        Save();
    }

    public void ClearRememberMe()
    {
        if (LoadState != AuthLoadState.Ready || !_account.RememberMe) return;
        _account.RememberMe = false;
        Save();
    }

    public void TouchLastLogin()
    {
        if (LoadState != AuthLoadState.Ready) return;
        _account.LastLoginUtc = DateTime.UtcNow;
        Save();
    }

    public void UpdateDisplayName(string displayName)
    {
        if (LoadState != AuthLoadState.Ready) return;
        _account.DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? DefaultDisplayName
            : displayName.Trim();
        Save();
    }

    public bool TryChangePassword(string currentPassword, string newPassword, out string error)
    {
        if (!TryAuthenticatePassword(currentPassword, out error))
            return false;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            error = "New password must be at least 8 characters.";
            return false;
        }

        var (salt, hash) = PasswordHasher.Hash(newPassword);
        _account.PasswordSalt = salt;
        _account.PasswordHash = hash;
        Save();
        error = string.Empty;
        return true;
    }

    public bool TrySetPin(string currentPassword, string pin, out string error)
    {
        if (!TryAuthenticatePassword(currentPassword, out error))
            return false;

        if (!PinPattern.IsMatch(pin))
        {
            error = "PIN must be 4–8 digits.";
            return false;
        }

        var (salt, hash) = PasswordHasher.Hash(pin);
        _account.PinSalt = salt;
        _account.PinHash = hash;
        Save();
        error = string.Empty;
        return true;
    }

    public bool TryClearPin(string currentPassword, out string error)
    {
        if (!TryAuthenticatePassword(currentPassword, out error))
            return false;

        _account.PinSalt = null;
        _account.PinHash = null;
        Save();
        error = string.Empty;
        return true;
    }

    private void Load()
    {
        try
        {
            MigrateLegacyPlainJsonIfNeeded();

            if (!File.Exists(_filePath))
            {
                _account = new UserAccountDto { Email = DefaultEmail, DisplayName = DefaultDisplayName };
                LoadState = AuthLoadState.NeedsSetup;
                return;
            }

            var raw = File.ReadAllBytes(_filePath);
            var json = TryDecodeAccountJson(raw);
            if (json is null)
            {
                LoadState = AuthLoadState.Damaged;
                return;
            }

            var dto = JsonSerializer.Deserialize<UserAccountDto>(json);
            if (dto is null ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.PasswordSalt) ||
                string.IsNullOrWhiteSpace(dto.PasswordHash))
            {
                LoadState = AuthLoadState.Damaged;
                return;
            }

            _account = dto;
            LoadState = AuthLoadState.Ready;

            // Migrate legacy plaintext payload to DPAPI.
            if (LooksLikePlainJson(raw))
                Save();
        }
        catch
        {
            LoadState = AuthLoadState.Damaged;
        }
    }

    private void MigrateLegacyPlainJsonIfNeeded()
    {
        if (File.Exists(_filePath)) return;
        var legacy = Path.Combine(
            Path.GetDirectoryName(_filePath)!,
            "user-account.json");
        if (!File.Exists(legacy)) return;

        try
        {
            var text = File.ReadAllText(legacy);
            var dto = JsonSerializer.Deserialize<UserAccountDto>(text);
            if (dto is null ||
                string.IsNullOrWhiteSpace(dto.PasswordSalt) ||
                string.IsNullOrWhiteSpace(dto.PasswordHash))
                return;

            _account = dto;
            Save();
            try { File.Delete(legacy); } catch { /* keep legacy if locked */ }
        }
        catch
        {
            // leave NeedsSetup / Damaged to main Load path
        }
    }

    private void Save()
    {
        var path = _filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(_account, JsonOptions);
        var protectedBytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(json),
            optionalEntropy: null,
            scope: DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, protectedBytes);
    }

    private static string? TryDecodeAccountJson(byte[] raw)
    {
        try
        {
            var unprotected = ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unprotected);
        }
        catch (CryptographicException)
        {
            // Legacy plaintext JSON from earlier builds.
            if (LooksLikePlainJson(raw))
                return Encoding.UTF8.GetString(raw);
            return null;
        }
    }

    private static bool LooksLikePlainJson(byte[] raw)
    {
        if (raw.Length == 0) return false;
        var start = (char)raw[0];
        return start is '{' or '[';
    }

    private bool EnsureNotLockedOut(out string error)
    {
        if (!IsLockedOut)
        {
            error = string.Empty;
            return true;
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(LockoutRemaining!.Value.TotalSeconds));
        error = $"Too many failed attempts. Try again in {seconds}s.";
        return false;
    }

    private void RegisterFailure(out string error)
    {
        _failedAttempts++;
        if (_failedAttempts >= MaxFailedAttempts)
        {
            _lockoutUntilUtc = DateTime.UtcNow.AddSeconds(LockoutSeconds);
            _failedAttempts = 0;
            error = $"Too many failed attempts. Try again in {LockoutSeconds}s.";
            return;
        }

        var left = MaxFailedAttempts - _failedAttempts;
        error = left == 1
            ? "Invalid credentials. 1 attempt remaining before lockout."
            : $"Invalid credentials. {left} attempts remaining before lockout.";
    }

    private void ClearFailures()
    {
        _failedAttempts = 0;
        _lockoutUntilUtc = null;
    }

    private static string DefaultFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "user-account.dat");

    private sealed class UserAccountDto
    {
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? PinSalt { get; set; }
        public string? PinHash { get; set; }
        public bool RememberMe { get; set; }
        public DateTime? LastLoginUtc { get; set; }
    }
}
