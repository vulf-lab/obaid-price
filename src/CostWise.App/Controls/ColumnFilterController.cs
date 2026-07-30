using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CostWise.App.Controls;

public sealed record ColumnFilterState(
    IReadOnlyDictionary<string, string[]> SelectedByColumn,
    string? SortKey,
    bool SortAscending)
{
    public static ColumnFilterState Empty { get; } =
        new(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase), null, true);

    public bool HasContent =>
        SelectedByColumn.Count > 0 || !string.IsNullOrEmpty(SortKey);
}

public interface IColumnFilterHost
{
    event EventHandler? FiltersChanged;
    bool HasActiveFilters { get; }
    IReadOnlyList<FilterValueOption> GetOptions(string columnKey);
    bool IsFilterActive(string columnKey);
    void ApplyValueFilter(string columnKey, IEnumerable<string> selectedValues);
    void ClearColumnFilter(string columnKey);
    void Sort(string columnKey, bool ascending);
    void ResetAll();
    ColumnFilterState CaptureState();
    void RestoreState(ColumnFilterState? state);
}

public sealed partial class FilterValueOption : ObservableObject
{
    [ObservableProperty] private bool _isSelected = true;
    public string Value { get; init; } = string.Empty;
}

/// <summary>Excel-style filter/sort over an in-memory source list into a filtered collection.</summary>
public sealed class ColumnFilterController<T> : IColumnFilterHost, INotifyPropertyChanged
{
    private readonly Func<IEnumerable<T>> _getSource;
    private readonly Action<IList<T>> _setFiltered;
    private readonly Dictionary<string, Func<T, string>> _displayGetters;
    private readonly Dictionary<string, Func<T, IComparable?>> _sortGetters;
    private readonly Dictionary<string, HashSet<string>> _selected = new(StringComparer.OrdinalIgnoreCase);
    private string? _sortKey;
    private bool _sortAscending = true;

    public ColumnFilterController(
        Func<IEnumerable<T>> getSource,
        Action<IList<T>> setFiltered,
        Dictionary<string, Func<T, string>> displayGetters,
        Dictionary<string, Func<T, IComparable?>>? sortGetters = null)
    {
        _getSource = getSource;
        _setFiltered = setFiltered;
        _displayGetters = displayGetters;
        _sortGetters = sortGetters ?? displayGetters.ToDictionary(
            kv => kv.Key,
            kv => (Func<T, IComparable?>)(row => kv.Value(row)),
            StringComparer.OrdinalIgnoreCase);
    }

    public event EventHandler? FiltersChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasActiveFilters =>
        _selected.Count > 0 || !string.IsNullOrEmpty(_sortKey);

    public IReadOnlyList<FilterValueOption> GetOptions(string columnKey)
    {
        if (!_displayGetters.TryGetValue(columnKey, out var getter))
            return Array.Empty<FilterValueOption>();

        var values = _getSource()
            .Select(getter)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _selected.TryGetValue(columnKey, out var selected);
        return values.Select(v => new FilterValueOption
        {
            Value = v,
            IsSelected = selected is null || selected.Contains(v)
        }).ToList();
    }

    public bool IsFilterActive(string columnKey) =>
        _selected.ContainsKey(columnKey);

    public void ApplyValueFilter(string columnKey, IEnumerable<string> selectedValues)
    {
        if (!_displayGetters.ContainsKey(columnKey)) return;

        var set = new HashSet<string>(selectedValues, StringComparer.OrdinalIgnoreCase);
        var all = _getSource()
            .Select(_displayGetters[columnKey])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (set.Count == 0 || (set.Count >= all.Count && all.All(set.Contains)))
            _selected.Remove(columnKey);
        else
            _selected[columnKey] = set;

        Apply();
    }

    public void ClearColumnFilter(string columnKey)
    {
        _selected.Remove(columnKey);
        Apply();
    }

    public void Sort(string columnKey, bool ascending)
    {
        _sortKey = columnKey;
        _sortAscending = ascending;
        Apply();
    }

    public void ResetAll()
    {
        _selected.Clear();
        _sortKey = null;
        Apply();
    }

    public ColumnFilterState CaptureState()
    {
        var selected = _selected.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        return new ColumnFilterState(selected, _sortKey, _sortAscending);
    }

    public void RestoreState(ColumnFilterState? state)
    {
        _selected.Clear();
        _sortKey = null;
        _sortAscending = true;

        if (state is not null)
        {
            foreach (var (key, values) in state.SelectedByColumn)
            {
                if (!_displayGetters.ContainsKey(key)) continue;
                if (values.Length == 0) continue;
                _selected[key] = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(state.SortKey) && _sortGetters.ContainsKey(state.SortKey))
            {
                _sortKey = state.SortKey;
                _sortAscending = state.SortAscending;
            }
        }

        Apply();
    }

    public void Apply()
    {
        IEnumerable<T> query = _getSource();
        foreach (var (key, allowed) in _selected)
        {
            if (!_displayGetters.TryGetValue(key, out var getter)) continue;
            query = query.Where(row => allowed.Contains(getter(row)));
        }

        if (_sortKey is not null && _sortGetters.TryGetValue(_sortKey, out var sortGetter))
        {
            query = _sortAscending
                ? query.OrderBy(r => sortGetter(r) ?? "")
                : query.OrderByDescending(r => sortGetter(r) ?? "");
        }

        _setFiltered(query.ToList());
        OnPropertyChanged(nameof(HasActiveFilters));
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
