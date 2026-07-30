using System.IO;
using System.Text.Json;

namespace CostWise.App.Services;

public sealed record CommercialPriceListPrefs(
    string[] VisibleColumns,
    decimal RoundSaleTo = 0m,
    decimal RoundCpTo = 0m,
    decimal RoundFatTo = 0m,
    int SchemaVersion = 1);

public static class CommercialPriceListPrefsStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "commercial-price-list-prefs.json");

    public static CommercialPriceListPrefs? Load()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonSerializer.Deserialize<CommercialPriceListPrefs>(File.ReadAllText(FilePath));
            }
            catch
            {
                return null;
            }
        }
    }

    public static void Save(CommercialPriceListPrefs prefs)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(prefs, JsonOptions));
            }
            catch
            {
                // ignore
            }
        }
    }
}
