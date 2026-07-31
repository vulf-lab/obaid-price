namespace CostWise.App.Services.Update;

public enum UpdatePolicy
{
    /// <summary>Notify the user; download/apply only after confirmation (default).</summary>
    Prompt = 0,

    /// <summary>Download in the background; apply on next restart after user restarts (or after accept).</summary>
    SilentDownloadApplyOnRestart = 1,

    /// <summary>Do not check for updates.</summary>
    Off = 2
}
