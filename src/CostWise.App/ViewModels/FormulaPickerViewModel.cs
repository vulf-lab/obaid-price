using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CostWise.App.ViewModels;

public partial class FormulaPickerRow : ObservableObject
{
    public int FormulationId { get; init; }
    public int CategoryId { get; init; }
    public int SubCategoryId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string SubCategoryName { get; init; } = string.Empty;
    public string SizeName { get; init; } = string.Empty;
    public string FeedTypeName { get; init; } = string.Empty;

    [ObservableProperty] private bool _isSelected;
}

public partial class FormulaPickerViewModel : ObservableObject
{
    private readonly List<FormulaPickerRow> _all = new();

    public ObservableCollection<FormulaPickerRow> FilteredRows { get; } = new();
    public ObservableCollection<NamedFilterOption> CategoryFilters { get; } = new();
    public ObservableCollection<NamedFilterOption> VersionFilters { get; } = new();

    [ObservableProperty] private NamedFilterOption? _selectedCategoryFilter;
    [ObservableProperty] private NamedFilterOption? _selectedVersionFilter;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _countText = string.Empty;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private string _windowTitle = "Select formulas";

    public bool Confirmed { get; private set; }

    public FormulaPickerViewModel(string groupName, IReadOnlyList<FormulaPickerRow> rows)
    {
        WindowTitle = $"Select formulas — {groupName}";
        _all.AddRange(rows);

        CategoryFilters.Add(new NamedFilterOption { Id = 0, Name = "All categories" });
        foreach (var c in _all.Select(r => (r.CategoryId, r.CategoryName)).Distinct().OrderBy(x => x.CategoryName))
            CategoryFilters.Add(new NamedFilterOption { Id = c.CategoryId, Name = c.CategoryName });
        SelectedCategoryFilter = CategoryFilters[0];

        VersionFilters.Add(new NamedFilterOption { Id = 0, Name = "All versions" });
        foreach (var v in _all.Select(r => (r.SubCategoryId, r.SubCategoryName)).Distinct().OrderBy(x => x.SubCategoryName))
            VersionFilters.Add(new NamedFilterOption { Id = v.SubCategoryId, Name = v.SubCategoryName });
        SelectedVersionFilter = VersionFilters[0];

        if (_all.Count == 0)
            ErrorText = "No formulations found in the database.";
        else
            ErrorText = null;

        foreach (var row in _all)
        {
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FormulaPickerRow.IsSelected))
                    UpdateCount();
            };
        }

        ApplyFilters();
    }

    partial void OnSelectedCategoryFilterChanged(NamedFilterOption? value) => ApplyFilters();
    partial void OnSelectedVersionFilterChanged(NamedFilterOption? value) => ApplyFilters();
    partial void OnSearchTextChanged(string value) => ApplyFilters();

    public IReadOnlyList<int> GetSelectedIds() =>
        _all.Where(r => r.IsSelected).Select(r => r.FormulationId).ToList();

    [RelayCommand]
    private void SelectAllFiltered()
    {
        foreach (var row in FilteredRows)
            row.IsSelected = true;
        UpdateCount();
    }

    [RelayCommand]
    private void ClearFiltered()
    {
        foreach (var row in FilteredRows)
            row.IsSelected = false;
        UpdateCount();
    }

    [RelayCommand]
    private void Ok(Window? window)
    {
        Confirmed = true;
        window?.Close();
    }

    [RelayCommand]
    private void Cancel(Window? window)
    {
        Confirmed = false;
        window?.Close();
    }

    private void ApplyFilters()
    {
        FilteredRows.Clear();
        var categoryId = SelectedCategoryFilter?.Id ?? 0;
        var versionId = SelectedVersionFilter?.Id ?? 0;
        var search = SearchText?.Trim() ?? string.Empty;

        foreach (var row in _all)
        {
            if (categoryId > 0 && row.CategoryId != categoryId) continue;
            if (versionId > 0 && row.SubCategoryId != versionId) continue;
            if (!string.IsNullOrEmpty(search))
            {
                var hit =
                    row.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    row.CategoryName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    row.SubCategoryName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    row.SizeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    row.FeedTypeName.Contains(search, StringComparison.OrdinalIgnoreCase);
                if (!hit) continue;
            }

            FilteredRows.Add(row);
        }

        UpdateCount();
    }

    private void UpdateCount()
    {
        var selected = _all.Count(r => r.IsSelected);
        CountText = $"Showing {FilteredRows.Count} of {_all.Count}  ·  {selected} selected";
    }
}
