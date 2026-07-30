using System.IO;
using System.Text.Json;
using System.Windows.Controls;

namespace CostWise.App.Services;

/// <summary>Persists DataGrid column pixel widths so user resizes survive Save/Refresh/navigation.</summary>
public static class DataGridColumnWidthStore
{
    private static readonly object Gate = new();
    private static Dictionary<string, double[]>? _cache;

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "grid-column-widths.json");

    public static void Restore(DataGrid grid, string key)
    {
        if (grid.Columns.Count == 0) return;

        var widths = Load()
            .GetValueOrDefault(key);
        if (widths is null || widths.Length != grid.Columns.Count)
            return;

        for (var i = 0; i < grid.Columns.Count; i++)
        {
            if (widths[i] > 0)
                grid.Columns[i].Width = new DataGridLength(widths[i]);
        }
    }

    public static void Save(DataGrid grid, string key)
    {
        if (grid.Columns.Count == 0) return;

        var widths = grid.Columns
            .Select(c => c.ActualWidth > 0 ? c.ActualWidth : c.Width.DisplayValue)
            .ToArray();

        lock (Gate)
        {
            var map = Load();
            map[key] = widths;
            _cache = map;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(map));
        }
    }

    private static Dictionary<string, double[]> Load()
    {
        lock (Gate)
        {
            if (_cache is not null)
                return _cache;

            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _cache = JsonSerializer.Deserialize<Dictionary<string, double[]>>(json)
                             ?? new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
                    return _cache;
                }
            }
            catch
            {
                // ignore corrupt preferences
            }

            _cache = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            return _cache;
        }
    }
}
