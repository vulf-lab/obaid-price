using System.IO;

namespace CostWise.App.Services;

/// <summary>Stores price-list branding logos under LocalAppData (not hard-coded in PDFs).</summary>
public static class BrandingLogoStore
{
    public const string VictoryLeft = "victory-left";
    public const string VictoryRight = "victory-right";
    public const string Commercial = "commercial";

    public static readonly string[] AllSlots = [VictoryLeft, VictoryRight, Commercial];

    public static string BrandingFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "branding");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string? GetPath(string slot)
    {
        var folder = BrandingFolder();
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            var path = Path.Combine(folder, slot + ext);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static byte[]? GetBytes(string slot)
    {
        var path = GetPath(slot);
        return path is null ? null : File.ReadAllBytes(path);
    }

    public static bool HasLogo(string slot) => GetPath(slot) is not null;

    public static void SetFromFile(string slot, string sourcePath)
    {
        if (!AllSlots.Contains(slot, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentOutOfRangeException(nameof(slot));

        Clear(slot);
        var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg"))
            ext = ".png";

        var dest = Path.Combine(BrandingFolder(), slot + ext);
        File.Copy(sourcePath, dest, overwrite: true);
    }

    public static void Clear(string slot)
    {
        var folder = BrandingFolder();
        foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
        {
            var path = Path.Combine(folder, slot + ext);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
