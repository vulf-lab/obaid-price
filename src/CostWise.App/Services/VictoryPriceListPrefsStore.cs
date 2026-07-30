using System.IO;
using System.Text.Json;

namespace CostWise.App.Services;

public sealed record VictoryPriceListPrefs(
    int? BookAId,
    int? BookBId,
    int[] SelectedRmIds,
    string[] VisibleColumns,
    string Commentary,
    decimal RoundBookATo = 0m,
    decimal RoundBookBTo = 0m,
    DateTime? PreviousPriceAsOfUtc = null,
    int? CompareSnapshotId = null,
    bool CompareSellAuto = true,
    decimal RoundDeltaPercentTo = 0m);

public static class VictoryPriceListPrefsStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "victory-price-list-prefs.json");

    public static VictoryPriceListPrefs? Load()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonSerializer.Deserialize<VictoryPriceListPrefs>(File.ReadAllText(FilePath));
            }
            catch
            {
                return null;
            }
        }
    }

    public static void Save(VictoryPriceListPrefs prefs)
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
                // ignore persistence failures
            }
        }
    }
}
