using System.IO;
using CostWise.Infrastructure;

namespace CostWise.App.Services.Update;

public static class DatabaseBackupService
{
    public const int MaxBackups = 5;

    public static string BackupDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "backups");

    public static string? CreatePreUpdateSnapshot(string? targetVersion)
    {
        var dbPath = DependencyInjection.GetDefaultDatabasePath();
        if (!File.Exists(dbPath))
            return null;

        Directory.CreateDirectory(BackupDirectory);
        var safeVersion = string.IsNullOrWhiteSpace(targetVersion)
            ? "unknown"
            : string.Concat(targetVersion.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' ? c : '_'));
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var dest = Path.Combine(BackupDirectory, $"pre-update-{safeVersion}-{stamp}.db");
        File.Copy(dbPath, dest, overwrite: false);
        PruneOldBackups();
        return dest;
    }

    public static string? FindLatestBackup()
    {
        if (!Directory.Exists(BackupDirectory))
            return null;

        return Directory.EnumerateFiles(BackupDirectory, "pre-update-*.db")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static bool TryRestoreLatestBackup()
    {
        var backup = FindLatestBackup();
        if (backup is null || !File.Exists(backup))
            return false;

        var dbPath = DependencyInjection.GetDefaultDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        File.Copy(backup, dbPath, overwrite: true);
        return true;
    }

    private static void PruneOldBackups()
    {
        try
        {
            var files = Directory.EnumerateFiles(BackupDirectory, "pre-update-*.db")
                .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                .Skip(MaxBackups)
                .ToList();
            foreach (var file in files)
                File.Delete(file);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
