using System.IO;
using System.Text.Json;
using CostWise.App.Controls;

namespace CostWise.App.Services;

/// <summary>Persists Excel-style column filter/sort state per screen.</summary>
public static class ColumnFilterStateStore
{
    private static readonly object Gate = new();
    private static Dictionary<string, ColumnFilterStateDto>? _cache;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "column-filters.json");

    public static ColumnFilterState? Load(string key)
    {
        lock (Gate)
        {
            var map = EnsureCache();
            if (!map.TryGetValue(key, out var dto) || dto is null)
                return null;
            return FromDto(dto);
        }
    }

    public static void Save(string key, ColumnFilterState state)
    {
        lock (Gate)
        {
            var map = EnsureCache();
            if (!state.HasContent)
                map.Remove(key);
            else
                map[key] = ToDto(state);
            Persist(map);
        }
    }

    private static Dictionary<string, ColumnFilterStateDto> EnsureCache()
    {
        if (_cache is not null) return _cache;
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, ColumnFilterStateDto>>(
                    File.ReadAllText(FilePath), JsonOptions);
                _cache = loaded ?? new Dictionary<string, ColumnFilterStateDto>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _cache = new Dictionary<string, ColumnFilterStateDto>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _cache = new Dictionary<string, ColumnFilterStateDto>(StringComparer.OrdinalIgnoreCase);
        }

        return _cache;
    }

    private static void Persist(Dictionary<string, ColumnFilterStateDto> map)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(map, JsonOptions));
        }
        catch
        {
            // ignore disk errors
        }
    }

    private static ColumnFilterState FromDto(ColumnFilterStateDto dto)
    {
        var selected = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (dto.SelectedByColumn is not null)
        {
            foreach (var (k, v) in dto.SelectedByColumn)
                selected[k] = v ?? [];
        }

        return new ColumnFilterState(selected, dto.SortKey, dto.SortAscending);
    }

    private static ColumnFilterStateDto ToDto(ColumnFilterState state) => new()
    {
        SelectedByColumn = state.SelectedByColumn.ToDictionary(
            kv => kv.Key,
            kv => kv.Value,
            StringComparer.OrdinalIgnoreCase),
        SortKey = state.SortKey,
        SortAscending = state.SortAscending
    };

    private sealed class ColumnFilterStateDto
    {
        public Dictionary<string, string[]>? SelectedByColumn { get; set; }
        public string? SortKey { get; set; }
        public bool SortAscending { get; set; } = true;
    }
}
