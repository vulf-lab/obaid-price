using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Controls;
using CostWise.App.Services;
using CostWise.Core.Services;
using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace CostWise.App.ViewModels;

public enum ComparisonMode
{
    Profiles,
    Formulations
}

public partial class ComparisonChip : ObservableObject
{
    public string Code { get; init; } = string.Empty;
    public int? FormulationId { get; init; }
}

public partial class ComparisonNutrientRow : ObservableObject
{
    public string Nutrient { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public ObservableCollection<string> Values { get; } = new();
    public ObservableCollection<bool> Differs { get; } = new();
    /// <summary>Per-column highlight: null none; 0 dark / 1 medium / 2 light green (RM cost row).</summary>
    public ObservableCollection<int?> HighlightRanks { get; } = new();
}

public partial class ComparisonIngredientRow : ObservableObject
{
    public string RawMaterial { get; init; } = string.Empty;
    public ObservableCollection<string> Values { get; } = new();
    public ObservableCollection<bool> Differs { get; } = new();
    /// <summary>Per-column highlight: null none; 0 dark / 1 medium / 2 light green (RM cost row).</summary>
    public ObservableCollection<int?> HighlightRanks { get; } = new();
}

public partial class ComparisonViewModel : ObservableObject
{
    private static readonly SolidColorBrush CostRankDark = CreateFreezeBrush(0x1B, 0x5E, 0x20);
    private static readonly SolidColorBrush CostRankMedium = CreateFreezeBrush(0x43, 0xA0, 0x47);
    private static readonly SolidColorBrush CostRankLight = CreateFreezeBrush(0xA5, 0xD6, 0xA7);

    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private readonly CompareSelectionService _selection;
    private readonly AppPreferences _preferences;
    private readonly ColumnFilterController<ComparisonNutrientRow> _nutrientFilter;

    public ObservableCollection<ComparisonChip> Chips { get; } = new();
    public ObservableCollection<string> ColumnHeaders { get; } = new();
    public ObservableCollection<ComparisonNutrientRow> NutrientRows { get; } = new();
    public ObservableCollection<ComparisonNutrientRow> FilteredNutrientRows { get; } = new();
    public ObservableCollection<ComparisonIngredientRow> IngredientRows { get; } = new();

    public IColumnFilterHost FilterHost => _nutrientFilter;

    [ObservableProperty] private ComparisonMode _mode = ComparisonMode.Profiles;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isProfilesMode = true;
    [ObservableProperty] private bool _isFormulationsMode;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _canPrintProfiles;
    [ObservableProperty] private ComparisonNutrientRow? _selectedNutrientRow;
    [ObservableProperty] private bool _canSortColumns;

    public ComparisonViewModel(
        IDbContextFactory<CostWiseDbContext> dbFactory,
        CompareSelectionService selection,
        AppPreferences preferences)
    {
        _dbFactory = dbFactory;
        _selection = selection;
        _preferences = preferences;
        _nutrientFilter = new ColumnFilterController<ComparisonNutrientRow>(
            () => NutrientRows,
            list =>
            {
                FilteredNutrientRows.Clear();
                foreach (var item in list)
                    FilteredNutrientRows.Add(item);
                RefreshCanPrint();
            },
            new Dictionary<string, Func<ComparisonNutrientRow, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nutrient"] = r => r.Nutrient,
                ["Unit"] = r => r.Unit
            },
            new Dictionary<string, Func<ComparisonNutrientRow, IComparable?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nutrient"] = r => r.Nutrient,
                ["Unit"] = r => r.Unit
            });
        _selection.Changed += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RefreshChips();
                _ = LoadAsync();
            });
        };
        RefreshChips();
        _ = LoadAsync();
    }

    public void ShowProfiles()
    {
        Mode = ComparisonMode.Profiles;
        RefreshChips();
        _ = LoadAsync();
    }

    public void ShowFormulations()
    {
        Mode = ComparisonMode.Formulations;
        RefreshChips();
        _ = LoadAsync();
    }

    partial void OnModeChanged(ComparisonMode value)
    {
        IsProfilesMode = value == ComparisonMode.Profiles;
        IsFormulationsMode = value == ComparisonMode.Formulations;
        RefreshCanPrint();
    }

    private void RefreshCanPrint()
    {
        CanPrintProfiles = IsProfilesMode && FilteredNutrientRows.Count > 0 && ColumnHeaders.Count > 0;
        RefreshCanSortColumns();
    }

    private void RefreshCanSortColumns() =>
        CanSortColumns = SelectedNutrientRow is not null && ColumnHeaders.Count >= 2;

    partial void OnSelectedNutrientRowChanged(ComparisonNutrientRow? value)
    {
        RefreshCanSortColumns();
        SortColumnsAscendingCommand.NotifyCanExecuteChanged();
        SortColumnsDescendingCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanSortColumnsChanged(bool value)
    {
        SortColumnsAscendingCommand.NotifyCanExecuteChanged();
        SortColumnsDescendingCommand.NotifyCanExecuteChanged();
    }

    private void RefreshChips()
    {
        Chips.Clear();
        if (Mode == ComparisonMode.Profiles)
        {
            foreach (var code in _selection.ProfileCodes)
                Chips.Add(new ComparisonChip { Code = code });
        }
        else
        {
            foreach (var f in _selection.Formulations)
                Chips.Add(new ComparisonChip { Code = f.Code, FormulationId = f.Id });
        }

        HasSelection = Chips.Count > 0;
        ColumnHeaders.Clear();
        foreach (var c in Chips)
            ColumnHeaders.Add(c.Code);
        OnPropertyChanged(nameof(ColumnHeaders));
        RefreshCanPrint();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            NutrientRows.Clear();
            IngredientRows.Clear();
            FilteredNutrientRows.Clear();
            SelectedNutrientRow = null;

            if (Mode == ComparisonMode.Profiles)
            {
                if (_selection.ProfileCodes.Count == 0)
                {
                    StatusMessage = "Select 2–10 profiles on Nutrition Profiles, then click Compare.";
                    RefreshCanPrint();
                    return;
                }
                await LoadProfileTargetsAsync(_selection.ProfileCodes.ToList());
                var profileCost = await LoadRmCostSummaryByCodesAsync(_selection.ProfileCodes.ToList());
                if (profileCost is not null)
                    InsertNutrientCostRow(profileCost);
                _nutrientFilter.Apply();
                StatusMessage = $"Comparing {_selection.ProfileCodes.Count} profile(s).";
                OnPropertyChanged(nameof(ColumnHeaders));
                RefreshCanPrint();
                return;
            }

            if (_selection.Formulations.Count == 0)
            {
                StatusMessage = "Select 2–10 formulations, then click Compare.";
                RefreshCanPrint();
                return;
            }

            var codes = _selection.Formulations.Select(f => f.Code).ToList();
            var ids = _selection.Formulations.Select(f => f.Id).ToList();
            var costSummary = await LoadFormulationIngredientsAsync(ids);
            await LoadProfileTargetsAsync(codes);
            if (costSummary is not null)
                InsertNutrientCostRow(costSummary);
            _nutrientFilter.Apply();
            StatusMessage = $"Comparing {_selection.Formulations.Count} formulation(s).";
            OnPropertyChanged(nameof(ColumnHeaders));
            RefreshCanPrint();
        }
        catch (Exception ex)
        {
            AppLog.Error("Comparison load failed", ex);
            StatusMessage = "Could not load comparison data. See log for details.";
            MessageBox.Show(AppLog.UserFacing(ex), "Comparison", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadProfileTargetsAsync(IReadOnlyList<string> codes)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var specs = await db.SpecParameters.AsNoTracking().ToListAsync();
        var ordered = SpecParameterNormalizer.OrderByCanonical(specs, x => x.Name).ToList();

        var formulations = await db.Formulations
            .AsNoTracking()
            .Include(f => f.Specs)
            .Where(f => codes.Contains(f.Code))
            .ToListAsync();
        var byCode = formulations
            .GroupBy(f => f.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var p in ordered)
        {
            var row = new ComparisonNutrientRow
            {
                Nutrient = p.Name,
                Unit = p.Unit
            };
            decimal? baseline = null;
            for (var i = 0; i < codes.Count; i++)
            {
                byCode.TryGetValue(codes[i], out var f);
                var target = f?.Specs.FirstOrDefault(s => s.SpecParameterId == p.Id)?.TargetValue;
                var text = SpecParameterNormalizer.FormatValue(target, p.Name);
                row.Values.Add(text);
                if (i == 0)
                    baseline = target;
                row.Differs.Add(i > 0 && !Nullable.Equals(baseline, target));
                row.HighlightRanks.Add(null);
            }

            NutrientRows.Add(row);
        }
    }

    private async Task<RmCostSummary?> LoadRmCostSummaryByCodesAsync(IReadOnlyList<string> codes)
    {
        if (codes.Count == 0) return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var formulations = await db.Formulations
            .AsNoTracking()
            .Include(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
            .Where(f => codes.Contains(f.Code))
            .ToListAsync();
        var byCode = formulations
            .GroupBy(f => f.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var costs = new decimal?[codes.Count];
        for (var i = 0; i < codes.Count; i++)
        {
            if (!byCode.TryGetValue(codes[i], out var f)) continue;
            costs[i] = CostingCalculator.CalculateRmCost(
                f.Ingredients.Select(x => new IngredientCostLine(x.InclusionPercent, x.RawIngredient.PricePerMt)));
        }

        var ranks = RankLowestThree(costs);
        var costTexts = costs.Select(c => c is null ? string.Empty : _preferences.FormatMoney(c.Value)).ToList();
        return new RmCostSummary(costTexts, ranks);
    }

    private async Task<RmCostSummary?> LoadFormulationIngredientsAsync(IReadOnlyList<int> ids)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var formulations = await db.Formulations
            .AsNoTracking()
            .Include(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
            .Where(f => ids.Contains(f.Id))
            .ToListAsync();
        var byId = formulations.ToDictionary(f => f.Id);

        var costs = new decimal?[ids.Count];
        for (var i = 0; i < ids.Count; i++)
        {
            if (!byId.TryGetValue(ids[i], out var f)) continue;
            costs[i] = CostingCalculator.CalculateRmCost(
                f.Ingredients.Select(x => new IngredientCostLine(x.InclusionPercent, x.RawIngredient.PricePerMt)));
        }

        var ranks = RankLowestThree(costs);
        var costTexts = costs.Select(c => c is null ? string.Empty : _preferences.FormatMoney(c.Value)).ToList();
        var summary = new RmCostSummary(costTexts, ranks);

        var rmNames = formulations
            .SelectMany(f => f.Ingredients)
            .Select(i => i.RawIngredient.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in rmNames)
        {
            var row = new ComparisonIngredientRow { RawMaterial = name };
            decimal? baseline = null;
            for (var i = 0; i < ids.Count; i++)
            {
                byId.TryGetValue(ids[i], out var f);
                var pct = f?.Ingredients
                    .FirstOrDefault(x => string.Equals(x.RawIngredient.Name, name, StringComparison.OrdinalIgnoreCase))
                    ?.InclusionPercent;
                row.Values.Add(pct is null ? string.Empty : pct.Value.ToString("0.##"));
                if (i == 0)
                    baseline = pct;
                row.Differs.Add(i > 0 && !Nullable.Equals(baseline, pct));
                row.HighlightRanks.Add(null);
            }

            IngredientRows.Add(row);
        }

        return summary;
    }

    private void InsertNutrientCostRow(RmCostSummary summary)
    {
        var row = new ComparisonNutrientRow
        {
            Nutrient = "Raw material cost",
            Unit = string.Empty
        };
        for (var i = 0; i < summary.Texts.Count; i++)
        {
            row.Values.Add(summary.Texts[i]);
            row.Differs.Add(false);
            row.HighlightRanks.Add(summary.Ranks[i]);
        }
        NutrientRows.Insert(0, row);
    }

    private static IReadOnlyList<int?> RankLowestThree(IReadOnlyList<decimal?> costs)
    {
        var ranks = new int?[costs.Count];
        var ordered = costs
            .Select((c, i) => (Cost: c, Index: i))
            .Where(x => x.Cost is not null)
            .OrderBy(x => x.Cost)
            .ThenBy(x => x.Index)
            .Take(3)
            .ToList();
        for (var rank = 0; rank < ordered.Count; rank++)
            ranks[ordered[rank].Index] = rank;
        return ranks;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        if (Mode == ComparisonMode.Profiles)
            _selection.ClearProfiles();
        else
            _selection.ClearFormulations();
        RefreshChips();
        NutrientRows.Clear();
        IngredientRows.Clear();
        FilteredNutrientRows.Clear();
        SelectedNutrientRow = null;
        StatusMessage = "Selection cleared.";
        RefreshCanPrint();
    }

    [RelayCommand]
    private void RemoveChip(ComparisonChip? chip)
    {
        if (chip is null) return;
        if (Mode == ComparisonMode.Profiles)
            _selection.RemoveProfile(chip.Code);
        else if (chip.FormulationId is int id)
            _selection.RemoveFormulation(id);
        RefreshChips();
        _ = LoadAsync();
    }

    [RelayCommand]
    private void SetProfilesMode()
    {
        Mode = ComparisonMode.Profiles;
        RefreshChips();
        _ = LoadAsync();
    }

    [RelayCommand]
    private void SetFormulationsMode()
    {
        Mode = ComparisonMode.Formulations;
        RefreshChips();
        _ = LoadAsync();
    }

    [RelayCommand]
    private void ResetNutrientFilters() => _nutrientFilter.ResetAll();

    [RelayCommand(CanExecute = nameof(CanExecuteSortColumns))]
    private void SortColumnsAscending() => SortColumnsBySelectedRow(ascending: true);

    [RelayCommand(CanExecute = nameof(CanExecuteSortColumns))]
    private void SortColumnsDescending() => SortColumnsBySelectedRow(ascending: false);

    private bool CanExecuteSortColumns() => CanSortColumns;

    private void SortColumnsBySelectedRow(bool ascending)
    {
        if (SelectedNutrientRow is null || ColumnHeaders.Count < 2) return;

        var values = SelectedNutrientRow.Values;
        var order = Enumerable.Range(0, ColumnHeaders.Count)
            .Select(i => (
                Index: i,
                Value: TryParseCompareValue(i < values.Count ? values[i] : null)))
            .ToList();

        var sorted = ascending
            ? order
                .OrderBy(x => x.Value is null)
                .ThenBy(x => x.Value)
                .ThenBy(x => x.Index)
                .ToList()
            : order
                .OrderBy(x => x.Value is null)
                .ThenByDescending(x => x.Value)
                .ThenBy(x => x.Index)
                .ToList();

        var map = sorted.Select(x => x.Index).ToList();
        if (map.SequenceEqual(Enumerable.Range(0, map.Count)))
        {
            StatusMessage = ascending
                ? $"Already ascending by {SelectedNutrientRow.Nutrient}."
                : $"Already descending by {SelectedNutrientRow.Nutrient}.";
            return;
        }

        var chipByCode = Chips.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var newHeaders = map.Select(i => ColumnHeaders[i]).ToList();
        ColumnHeaders.Clear();
        foreach (var h in newHeaders)
            ColumnHeaders.Add(h);

        Chips.Clear();
        foreach (var h in ColumnHeaders)
        {
            if (chipByCode.TryGetValue(h, out var chip))
                Chips.Add(chip);
            else
                Chips.Add(new ComparisonChip { Code = h });
        }

        // Persist column order in the shared selection bag for next launch.
        if (Mode == ComparisonMode.Profiles)
        {
            _selection.ReorderProfiles(ColumnHeaders.ToList());
        }
        else
        {
            _selection.ReorderFormulations(
                Chips
                    .Where(c => c.FormulationId is int)
                    .Select(c => new FormulationCompareRef(c.FormulationId!.Value, c.Code))
                    .ToList());
        }

        foreach (var row in NutrientRows)
        {
            Permute(row.Values, map);
            Permute(row.HighlightRanks, map);
            RecomputeDiffers(row.Values, row.Differs);
        }

        foreach (var row in IngredientRows)
        {
            Permute(row.Values, map);
            Permute(row.HighlightRanks, map);
            RecomputeDiffers(row.Values, row.Differs);
        }

        var nutrientName = SelectedNutrientRow.Nutrient;

        _nutrientFilter.Apply();
        OnPropertyChanged(nameof(ColumnHeaders));

        SelectedNutrientRow = NutrientRows.FirstOrDefault(r =>
                                string.Equals(r.Nutrient, nutrientName, StringComparison.OrdinalIgnoreCase))
                            ?? FilteredNutrientRows.FirstOrDefault(r =>
                                string.Equals(r.Nutrient, nutrientName, StringComparison.OrdinalIgnoreCase));

        StatusMessage = ascending
            ? $"Columns sorted ascending by {nutrientName}."
            : $"Columns sorted descending by {nutrientName}.";
    }

    private static decimal? TryParseCompareValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var cleaned = text.Replace(",", "").Replace(" ", "").Trim();
        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.CurrentCulture, out var v) ||
            decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out v))
            return v;
        return null;
    }

    private static void Permute<T>(ObservableCollection<T> list, IReadOnlyList<int> map)
    {
        var old = list.ToList();
        list.Clear();
        foreach (var i in map)
            list.Add(i >= 0 && i < old.Count ? old[i] : default!);
    }

    private static void RecomputeDiffers(ObservableCollection<string> values, ObservableCollection<bool> differs)
    {
        differs.Clear();
        var baseline = values.Count > 0 ? values[0] : null;
        for (var i = 0; i < values.Count; i++)
            differs.Add(i > 0 && !string.Equals(baseline, values[i], StringComparison.Ordinal));
    }

    [RelayCommand(CanExecute = nameof(CanExecutePrintProfiles))]
    private void PrintProfiles()
    {
        if (!IsProfilesMode || FilteredNutrientRows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = "nutrition-profile-comparison.pdf",
            Title = "Print nutrition profile comparison"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            var codes = ColumnHeaders.ToList();
            var rows = FilteredNutrientRows
                .Select(r => (
                    r.Nutrient,
                    r.Unit,
                    (IReadOnlyList<string>)r.Values.ToList()))
                .ToList();
            ComparisonPrintService.WriteProfileComparisonPdf(dialog.FileName, codes, rows);
            ComparisonPrintService.OpenFile(dialog.FileName);
            StatusMessage = $"Printed to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Print failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanExecutePrintProfiles() => CanPrintProfiles;

    partial void OnCanPrintProfilesChanged(bool value) => PrintProfilesCommand.NotifyCanExecuteChanged();

    public void RebuildNutrientGrid(DataGrid grid, bool withFilters = false)
    {
        const double labelWidth = 180;
        const double unitWidth = 70;
        const double valueWidth = 100;
        const double valueMinWidth = 80;

        grid.Columns.Clear();
        if (withFilters)
            grid.ColumnHeaderHeight = 56;

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = withFilters
                ? CreateFilterHeader("Nutrient", "Nutrient")
                : "Nutrient",
            Binding = new Binding(nameof(ComparisonNutrientRow.Nutrient)),
            IsReadOnly = true,
            Width = new DataGridLength(labelWidth),
            MinWidth = 120
        });
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = withFilters
                ? CreateFilterHeader("Unit", "Unit")
                : "Unit",
            Binding = new Binding(nameof(ComparisonNutrientRow.Unit)),
            IsReadOnly = true,
            Width = new DataGridLength(unitWidth),
            MinWidth = 50
        });

        for (var i = 0; i < ColumnHeaders.Count; i++)
        {
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = ColumnHeaders[i],
                CellTemplate = CreateValueCellTemplate(i, includeDifferAccent: true),
                Width = new DataGridLength(valueWidth),
                MinWidth = valueMinWidth
            });
        }
    }

    private ExcelColumnFilterHeader CreateFilterHeader(string key, string title) =>
        new()
        {
            Title = title,
            ColumnKey = key,
            Host = FilterHost,
            AllowValueFilter = true
        };

    public void RebuildIngredientGrid(DataGrid grid)
    {
        const double labelWidth = 180;
        const double unitWidth = 70;
        const double valueWidth = 100;
        const double valueMinWidth = 80;

        grid.Columns.Clear();
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Raw material",
            Binding = new Binding(nameof(ComparisonIngredientRow.RawMaterial)),
            IsReadOnly = true,
            Width = new DataGridLength(labelWidth + unitWidth),
            MinWidth = 160
        });

        for (var i = 0; i < ColumnHeaders.Count; i++)
        {
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = ColumnHeaders[i],
                CellTemplate = CreateValueCellTemplate(i, includeDifferAccent: true),
                Width = new DataGridLength(valueWidth),
                MinWidth = valueMinWidth
            });
        }
    }

    private static DataTemplate CreateValueCellTemplate(int colIndex, bool includeDifferAccent)
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(4, 2, 4, 2));

        var borderStyle = new Style(typeof(Border));
        borderStyle.Setters.Add(new Setter(Border.BackgroundProperty, Brushes.Transparent));
        AddRankTrigger(borderStyle, colIndex, 0, CostRankDark);
        AddRankTrigger(borderStyle, colIndex, 1, CostRankMedium);
        AddRankTrigger(borderStyle, colIndex, 2, CostRankLight);
        borderFactory.SetValue(FrameworkElement.StyleProperty, borderStyle);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new Binding($"Values[{colIndex}]"));
        textFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var textStyle = new Style(typeof(TextBlock));
        textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Black));
        textStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Normal));

        if (includeDifferAccent)
        {
            var differTrigger = new DataTrigger
            {
                Binding = new Binding($"Differs[{colIndex}]"),
                Value = true
            };
            differTrigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            if (Application.Current.TryFindResource("AccentBrush") is Brush accent)
                differTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, accent));
            textStyle.Triggers.Add(differTrigger);
        }

        AddRankTextTrigger(textStyle, colIndex, 0, Brushes.White, FontWeights.SemiBold);
        AddRankTextTrigger(textStyle, colIndex, 1, Brushes.White, FontWeights.SemiBold);
        AddRankTextTrigger(textStyle, colIndex, 2, Brushes.Black, FontWeights.SemiBold);

        textFactory.SetValue(FrameworkElement.StyleProperty, textStyle);
        borderFactory.AppendChild(textFactory);

        return new DataTemplate { VisualTree = borderFactory };
    }

    private static void AddRankTrigger(Style borderStyle, int colIndex, int rank, Brush background)
    {
        var trigger = new DataTrigger
        {
            Binding = new Binding($"HighlightRanks[{colIndex}]"),
            Value = rank
        };
        trigger.Setters.Add(new Setter(Border.BackgroundProperty, background));
        borderStyle.Triggers.Add(trigger);
    }

    private static void AddRankTextTrigger(Style textStyle, int colIndex, int rank, Brush foreground, FontWeight weight)
    {
        var trigger = new DataTrigger
        {
            Binding = new Binding($"HighlightRanks[{colIndex}]"),
            Value = rank
        };
        trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, foreground));
        trigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, weight));
        textStyle.Triggers.Add(trigger);
    }

    private static SolidColorBrush CreateFreezeBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private sealed record RmCostSummary(IReadOnlyList<string> Texts, IReadOnlyList<int?> Ranks);
}
