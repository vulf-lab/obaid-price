using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using CostWise.App.Controls;



namespace CostWise.App.Services;



public sealed record DataGridColumnLayout(string Key, double Width, bool Visible, int DisplayIndex);

/// <summary>Stable layout key plus display title for dynamically built DataGrid columns.</summary>
public sealed class ColumnHeaderKey(string key, string title)
{
    public string Key { get; } = key;
    public string Title { get; } = title;
    public override string ToString() => Title;
}



/// <summary>Persists DataGrid column widths, visibility, and display order.</summary>

public static class DataGridColumnLayoutStore

{

    private static readonly object Gate = new();

    private static Dictionary<string, DataGridColumnLayout[]>? _cache;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };



    /// <summary>Maps current header keys to older saved keys.</summary>

    private static readonly Dictionary<string, string[]> LegacyKeyAliases = new(StringComparer.OrdinalIgnoreCase)

    {

        ["Raw Material"] = ["RM"],

        ["Export Doc"] = ["Export Document"],

    };



    private static string FilePath =>

        Path.Combine(

            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),

            "CostWise",

            "grid-column-layouts.json");



    public static void Restore(DataGrid grid, string key)

    {

        if (grid.Columns.Count == 0) return;



        var layouts = Load().GetValueOrDefault(key);

        if (layouts is null || layouts.Length == 0)

        {

            DataGridColumnWidthStore.Restore(grid, key);

            return;

        }



        var byKey = layouts.ToDictionary(l => l.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var column in grid.Columns)

        {

            var header = ColumnKey(column);

            if (header is null || !TryGetLayout(byKey, header, out var layout))

                continue;



            if (layout.Width > 0)

                column.Width = new DataGridLength(layout.Width);



            if (!string.Equals(header, "Code", StringComparison.OrdinalIgnoreCase))

                column.Visibility = layout.Visible ? Visibility.Visible : Visibility.Collapsed;

        }



        foreach (var layout in layouts.OrderBy(l => l.DisplayIndex))

        {

            var column = grid.Columns.FirstOrDefault(c =>

            {

                var header = ColumnKey(c);

                return header is not null && MatchesLayoutKey(header, layout.Key);

            });

            if (column is null) continue;

            if (layout.DisplayIndex >= 0 && layout.DisplayIndex < grid.Columns.Count)

                column.DisplayIndex = layout.DisplayIndex;

        }

    }



    public static void Save(DataGrid grid, string key)

    {

        if (grid.Columns.Count == 0) return;



        var layouts = grid.Columns

            .Select(c => new DataGridColumnLayout(

                Key: ColumnKey(c) ?? string.Empty,

                Width: c.ActualWidth > 0 ? c.ActualWidth : c.Width.DisplayValue,

                Visible: c.Visibility == Visibility.Visible,

                DisplayIndex: c.DisplayIndex))

            .Where(l => !string.IsNullOrEmpty(l.Key))

            .ToArray();



        lock (Gate)

        {

            var map = Load();

            map[key] = layouts;

            _cache = map;

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(map, JsonOptions));

        }



        DataGridColumnWidthStore.Save(grid, key);

    }



    public static string? ColumnKey(DataGridColumn column) =>
        column.Header switch
        {
            string s => s,
            ExcelColumnFilterHeader h =>
                string.IsNullOrWhiteSpace(h.ColumnKey) ? h.Title : h.ColumnKey,
            ColumnHeaderKey h => h.Key,
            _ => null
        };

    /// <summary>Display title for a column (falls back to <see cref="ColumnKey"/>).</summary>
    public static string? ColumnTitle(DataGridColumn column) =>
        column.Header switch
        {
            string s => s,
            ExcelColumnFilterHeader h => h.Title,
            ColumnHeaderKey h => h.Title,
            _ => ColumnKey(column)
        };



    private static bool TryGetLayout(

        Dictionary<string, DataGridColumnLayout> byKey,

        string header,

        out DataGridColumnLayout layout)

    {

        if (byKey.TryGetValue(header, out layout!))

            return true;



        if (LegacyKeyAliases.TryGetValue(header, out var aliases))

        {

            foreach (var alias in aliases)

            {

                if (byKey.TryGetValue(alias, out layout!))

                    return true;

            }

        }



        layout = null!;

        return false;

    }



    private static bool MatchesLayoutKey(string header, string layoutKey) =>

        string.Equals(header, layoutKey, StringComparison.OrdinalIgnoreCase) ||

        (LegacyKeyAliases.TryGetValue(header, out var aliases) &&

         aliases.Any(a => string.Equals(a, layoutKey, StringComparison.OrdinalIgnoreCase)));



    private static Dictionary<string, DataGridColumnLayout[]> Load()

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

                    _cache = JsonSerializer.Deserialize<Dictionary<string, DataGridColumnLayout[]>>(json)

                             ?? new Dictionary<string, DataGridColumnLayout[]>(StringComparer.OrdinalIgnoreCase);

                    return _cache;

                }

            }

            catch

            {

                // ignore corrupt preferences

            }



            _cache = new Dictionary<string, DataGridColumnLayout[]>(StringComparer.OrdinalIgnoreCase);

            return _cache;

        }

    }

}


