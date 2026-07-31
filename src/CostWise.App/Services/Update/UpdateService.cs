using System.Reflection;
using CostWise.App.Views;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace CostWise.App.Services.Update;

public sealed class UpdateService
{
    public const string GitHubRepoUrl = "https://github.com/vulf-lab/obaid-price";
    private static readonly TimeSpan CheckThrottle = TimeSpan.FromDays(1);

    private readonly AppPreferences _preferences;
    private bool _checkInFlight;

    public UpdateService(AppPreferences preferences)
    {
        _preferences = preferences;
    }

    public static string CurrentVersion
    {
        get
        {
            var asm = Assembly.GetExecutingAssembly();
            return asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? asm.GetName().Version?.ToString(3)
                   ?? "0.0.0";
        }
    }

    public bool IsInstalled
    {
        get
        {
            try
            {
                return CreateManager().IsInstalled;
            }
            catch
            {
                return false;
            }
        }
    }

    public async Task TryApplyPendingSilentAsync(WindowOwner owner)
    {
        try
        {
            var mgr = CreateManager();
            if (!mgr.IsInstalled) return;
            var pending = mgr.UpdatePendingRestart;
            if (pending is null) return;

            var version = pending.Version.ToString();
            AppLog.Info($"Applying pending silent update {version}");
            DatabaseBackupService.CreatePreUpdateSnapshot(version);
            mgr.ApplyUpdatesAndRestart(pending);
        }
        catch (NotInstalledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to apply pending silent update", ex);
            owner.ShowInfo("Update", $"Could not apply a downloaded update:\n{ex.Message}");
        }

        await Task.CompletedTask;
    }

    public async Task RunStartupCheckAsync(WindowOwner owner, bool force = false)
    {
        if (_preferences.UpdatePolicy == UpdatePolicy.Off)
            return;

        if (!force
            && _preferences.LastUpdateCheckUtc is { } last
            && DateTime.UtcNow - last < CheckThrottle)
            return;

        await CheckAndHandleAsync(owner, interactivePrompt: true, force);
    }

    public async Task CheckAndHandleAsync(WindowOwner owner, bool interactivePrompt, bool force = false)
    {
        if (_checkInFlight) return;
        _checkInFlight = true;
        try
        {
            UpdateManager mgr;
            try
            {
                mgr = CreateManager();
            }
            catch (Exception ex)
            {
                AppLog.Error("Update manager init failed", ex);
                if (interactivePrompt && force)
                    owner.ShowInfo("Updates", "Could not initialize the update checker.");
                return;
            }

            if (!mgr.IsInstalled)
            {
                AppLog.Info("Update check skipped (not a Velopack install).");
                if (interactivePrompt && force)
                    owner.ShowInfo(
                        "Updates",
                        "Automatic updates are available only from a Velopack install.\n\n" +
                        "This build was started from Visual Studio / dotnet run.");
                return;
            }

            UpdateInfo? info;
            try
            {
                info = await mgr.CheckForUpdatesAsync();
                _preferences.LastUpdateCheckUtc = DateTime.UtcNow;
                _preferences.Save();
            }
            catch (NotInstalledException)
            {
                AppLog.Info("Update check skipped (NotInstalledException).");
                return;
            }
            catch (Exception ex)
            {
                AppLog.Error("Update check failed", ex);
                if (interactivePrompt && force)
                    owner.ShowInfo("Updates", $"Could not check for updates:\n{ex.Message}");
                return;
            }

            if (info is null)
            {
                if (interactivePrompt && force)
                    owner.ShowInfo("Updates", $"You are on the latest version ({CurrentVersion}).");
                return;
            }

            var available = info.TargetFullRelease.Version.ToString();
            if (!string.IsNullOrWhiteSpace(_preferences.SkippedUpdateVersion)
                && string.Equals(_preferences.SkippedUpdateVersion, available, StringComparison.OrdinalIgnoreCase)
                && !force)
            {
                AppLog.Info($"Update {available} skipped by user preference.");
                return;
            }

            if (_preferences.UpdatePolicy == UpdatePolicy.SilentDownloadApplyOnRestart && !force)
            {
                try
                {
                    await DownloadAsync(mgr, info, owner);
                }
                catch
                {
                    return;
                }

                // Leave package pending; next launch applies via TryApplyPendingSilentAsync.
                // Offer optional immediate restart.
                var restart = owner.AskYesNo(
                    "Update ready",
                    $"Version {available} has been downloaded.\n\nRestart now to apply the update?");
                if (restart)
                    ApplyPending(mgr, info);
                return;
            }

            // Prompt (default) or forced manual check
            var notes = info.TargetFullRelease.NotesHTML
                        ?? info.TargetFullRelease.NotesMarkdown
                        ?? string.Empty;
            var plainNotes = StripRoughHtml(notes);
            if (plainNotes.Length > 600)
                plainNotes = plainNotes[..600] + "…";

            var choice = owner.PromptUpdate(CurrentVersion, available, plainNotes);
            switch (choice)
            {
                case UpdatePromptResult.UpdateNow:
                    try
                    {
                        await DownloadAsync(mgr, info, owner);
                        ApplyPending(mgr, info);
                    }
                    catch
                    {
                        // errors already logged/shown
                    }
                    break;
                case UpdatePromptResult.SkipVersion:
                    _preferences.SkippedUpdateVersion = available;
                    _preferences.Save();
                    break;
                case UpdatePromptResult.Later:
                default:
                    break;
            }
        }
        finally
        {
            _checkInFlight = false;
        }
    }

    private async Task DownloadAsync(UpdateManager mgr, UpdateInfo info, WindowOwner owner)
    {
        try
        {
            owner.ShowBusy("Downloading update…");
            await mgr.DownloadUpdatesAsync(info);
            AppLog.Info($"Downloaded update {info.TargetFullRelease.Version}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Update download failed", ex);
            owner.ShowInfo("Update failed", $"Download failed:\n{ex.Message}");
            throw;
        }
        finally
        {
            owner.HideBusy();
        }
    }

    private void ApplyPending(UpdateManager mgr, UpdateInfo info)
    {
        try
        {
            var version = info.TargetFullRelease.Version.ToString();
            var backup = DatabaseBackupService.CreatePreUpdateSnapshot(version);
            AppLog.Info(backup is null
                ? "No database file to snapshot before update."
                : $"Pre-update DB snapshot: {backup}");
            mgr.ApplyUpdatesAndRestart(info.TargetFullRelease);
        }
        catch (Exception ex)
        {
            AppLog.Error("Apply update failed", ex);
            throw;
        }
    }

    private static UpdateManager CreateManager()
    {
        // Optional local override for testing: set COSTWISE_UPDATE_URL to a folder or HTTP feed.
        var overrideUrl = Environment.GetEnvironmentVariable("COSTWISE_UPDATE_URL");
        if (!string.IsNullOrWhiteSpace(overrideUrl))
            return new UpdateManager(overrideUrl);

        var source = new GithubSource(GitHubRepoUrl, accessToken: null, prerelease: false);
        return new UpdateManager(source);
    }

    private static string StripRoughHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

public enum UpdatePromptResult
{
    Later,
    UpdateNow,
    SkipVersion
}

/// <summary>UI abstraction so UpdateService stays testable and free of Window coupling in unit tests.</summary>
public interface WindowOwner
{
    void ShowInfo(string title, string message);
    bool AskYesNo(string title, string message);
    UpdatePromptResult PromptUpdate(string currentVersion, string newVersion, string releaseNotes);
    void ShowBusy(string message);
    void HideBusy();
}

public sealed class WpfWindowOwner : WindowOwner
{
    private readonly System.Windows.Window? _owner;
    private System.Windows.Window? _busy;

    public WpfWindowOwner(System.Windows.Window? owner) => _owner = owner;

    public void ShowInfo(string title, string message) =>
        System.Windows.MessageBox.Show(_owner, message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

    public bool AskYesNo(string title, string message) =>
        System.Windows.MessageBox.Show(_owner, message, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question)
        == System.Windows.MessageBoxResult.Yes;

    public UpdatePromptResult PromptUpdate(string currentVersion, string newVersion, string releaseNotes)
    {
        var dlg = new UpdateAvailableWindow(currentVersion, newVersion, releaseNotes) { Owner = _owner };
        var ok = dlg.ShowDialog() == true;
        if (!ok) return UpdatePromptResult.Later;
        return dlg.Result;
    }

    public void ShowBusy(string message)
    {
        HideBusy();
        _busy = new System.Windows.Window
        {
            Title = "OBAID Pricing",
            Width = 360,
            Height = 120,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = _owner,
            ResizeMode = System.Windows.ResizeMode.NoResize,
            Content = new System.Windows.Controls.TextBlock
            {
                Text = message,
                Margin = new System.Windows.Thickness(24),
                TextWrapping = System.Windows.TextWrapping.Wrap,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            }
        };
        _busy.Show();
    }

    public void HideBusy()
    {
        if (_busy is null) return;
        try { _busy.Close(); } catch { /* ignore */ }
        _busy = null;
    }
}
