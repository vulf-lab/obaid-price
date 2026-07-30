using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Controls;

namespace CostWise.App.ViewModels;

public partial class FormulaPickerRow : ObservableObject
{
    public int FormulationId { get; init; }
    public int CategoryId { get; init; }
    public int SubCategoryId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string SubCategoryName { get; init; } = string.Empty;
    public string Revision { get; init; } = string.Empty;
    public string SizeName { get; init; } = string.Empty;
    public string FeedTypeName { get; init; } = string.Empty;
    public bool IsInProduction { get; init; }
    public string InProductionLabel => IsInProduction ? "Yes" : "No";

    [ObservableProperty] private bool _isSelected;
}

public partial class FormulaPickerViewModel : ObservableObject
{
    private readonly List<FormulaPickerRow> _all = new();

    public ObservableCollection<FormulaPickerRow> FilteredRows { get; } = new();
    public IColumnFilterHost FilterHost { get; }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _countText = string.Empty;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private string _windowTitle = "Select formulas";
    [ObservableProperty] private string _selectionColumnHeader = "In group";

    public bool Confirmed { get; private set; }

    public FormulaPickerViewModel(string groupName, IReadOnlyList<FormulaPickerRow> rows, string selectionColumnHeader = "In group")
    {
        WindowTitle = $"Select formulas — {groupName}";
        SelectionColumnHeader = selectionColumnHeader;
        _all.AddRange(rows);

        FilterHost = new ColumnFilterController<FormulaPickerRow>(
            GetSearchFiltered,
            list =>
            {
                FilteredRows.Clear();
                foreach (var item in list)
                    FilteredRows.Add(item);
                UpdateCount();
            },
            new Dictionary<string, Func<FormulaPickerRow, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Code"] = r => r.Code,
                ["Category"] = r => r.CategoryName,
                ["Version"] = r => r.SubCategoryName,
                ["Rev"] = r => r.Revision,
                ["Size"] = r => r.SizeName,
                ["FeedType"] = r => r.FeedTypeName,
                ["InProduction"] = r => r.InProductionLabel
            },
            new Dictionary<string, Func<FormulaPickerRow, IComparable?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Code"] = r => r.Code,
                ["Category"] = r => r.CategoryName,
                ["Version"] = r => r.SubCategoryName,
                ["Rev"] = r => r.Revision,
                ["Size"] = r => r.SizeName,
                ["FeedType"] = r => r.FeedTypeName,
                ["InProduction"] = r => r.InProductionLabel
            });

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

        ((ColumnFilterController<FormulaPickerRow>)FilterHost).Apply();
    }

    partial void OnSearchTextChanged(string value) =>
        ((ColumnFilterController<FormulaPickerRow>)FilterHost).Apply();

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
    private void ResetAllFilters() => FilterHost.ResetAll();

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

    private IEnumerable<FormulaPickerRow> GetSearchFiltered()
    {
        var search = SearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(search))
            return _all;

        return _all.Where(row =>
            row.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            row.CategoryName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            row.SubCategoryName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            row.Revision.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            row.SizeName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            row.FeedTypeName.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateCount()
    {
        var selected = _all.Count(r => r.IsSelected);
        CountText = $"Showing {FilteredRows.Count} of {_all.Count}  ·  {selected} selected";
    }
}
