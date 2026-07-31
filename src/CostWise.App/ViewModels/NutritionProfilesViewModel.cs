using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Controls;
using CostWise.App.Services;
using CostWise.App.Services.Import;
using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace CostWise.App.ViewModels;

public partial class NutritionMatrixCell : ObservableObject
{
    public int SpecParameterId { get; init; }
    [ObservableProperty] private decimal? _targetValue;
}

public partial class NutritionMatrixRow : ObservableObject
{
    public int FormulationId { get; init; }
    [ObservableProperty] private string _systemId = string.Empty;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _feedTypeName = string.Empty;
    [ObservableProperty] private string _speciesName = string.Empty;
    [ObservableProperty] private string _sizeName = string.Empty;
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _subCategoryName = string.Empty;
    [ObservableProperty] private string _revision = string.Empty;
    [ObservableProperty] private bool _isActive;
    public string ActiveLabel => IsActive ? "Yes" : "No";
    public ObservableCollection<NutritionMatrixCell> Cells { get; } = new();

    [ObservableProperty] private bool _isCompareSelected;

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(ActiveLabel));
}

public partial class NutritionSpecLine : ObservableObject
{
    public int SpecParameterId { get; init; }
    public int DecimalPlaces { get; init; }
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _unit = string.Empty;
    [ObservableProperty] private decimal? _targetValue;

    /// <summary>Formatted Target for grid edit/display (per-nutrient decimals).</summary>
    public string TargetText
    {
        get => TargetValue is null
            ? string.Empty
            : TargetValue.Value.ToString($"F{DecimalPlaces}", System.Globalization.CultureInfo.CurrentCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                TargetValue = null;
                return;
            }

            if (decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.CurrentCulture, out var parsed) ||
                decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed))
            {
                TargetValue = RoundTarget(parsed, DecimalPlaces);
            }
        }
    }

    partial void OnTargetValueChanged(decimal? value) => OnPropertyChanged(nameof(TargetText));

    public static decimal? RoundTarget(decimal? value, int decimals) =>
        value is null ? null : Math.Round(value.Value, decimals, MidpointRounding.AwayFromZero);
}

public partial class NutritionProfilesViewModel : ObservableObject
{
    public const string FilterStateKey = "NutritionProfiles.Matrix";

    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private readonly INavigationService _navigation;
    private readonly CompareSelectionService _compareSelection;
    private readonly Dictionary<string, Func<NutritionMatrixRow, string>> _displayGetters;
    private readonly Dictionary<string, Func<NutritionMatrixRow, IComparable?>> _sortGetters;
    private readonly ColumnFilterController<NutritionMatrixRow> _filterController;
    private Task _loadTask = Task.CompletedTask;
    private string? _pendingProfileCode;
    private bool _suppressFilterPersist;

    public ObservableCollection<NutritionMatrixRow> MatrixRows { get; } = new();
    public ObservableCollection<NutritionMatrixRow> FilteredMatrixRows { get; } = new();
    public ObservableCollection<SpecParameter> SpecParameters { get; } = new();

    public IColumnFilterHost FilterHost => _filterController;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _canCompareProfiles;

    public NutritionProfilesViewModel(
        IDbContextFactory<CostWiseDbContext> dbFactory,
        INavigationService navigation,
        CompareSelectionService compareSelection)
    {
        _dbFactory = dbFactory;
        _navigation = navigation;
        _compareSelection = compareSelection;
        _compareSelection.Changed += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(SyncCompareFlags);
        };
        _displayGetters = new Dictionary<string, Func<NutritionMatrixRow, string>>(StringComparer.OrdinalIgnoreCase);
        _sortGetters = new Dictionary<string, Func<NutritionMatrixRow, IComparable?>>(StringComparer.OrdinalIgnoreCase);
        SeedReferenceGetters();
        _filterController = new ColumnFilterController<NutritionMatrixRow>(
            () => MatrixRows,
            list =>
            {
                FilteredMatrixRows.Clear();
                foreach (var item in list)
                    FilteredMatrixRows.Add(item);
            },
            _displayGetters,
            _sortGetters);
        _filterController.FiltersChanged += (_, _) =>
        {
            if (_suppressFilterPersist) return;
            ColumnFilterStateStore.Save(FilterStateKey, _filterController.CaptureState());
        };
        _loadTask = LoadAsync();
    }

    private void SyncCompareFlags()
    {
        var atCap = _compareSelection.ProfileCount >= CompareSelectionService.MaxItems;
        foreach (var row in MatrixRows)
        {
            var selected = _compareSelection.IsProfileSelected(row.Code);
            if (selected)
            {
                row.IsCompareSelected = true;
                continue;
            }

            // Force OneWay CheckBox refresh when at cap (clears stale local IsChecked).
            if (atCap || row.IsCompareSelected)
            {
                row.IsCompareSelected = true;
                row.IsCompareSelected = false;
            }
            else
            {
                row.IsCompareSelected = false;
            }
        }

        CanCompareProfiles = _compareSelection.CanCompareProfiles;
        CompareCommand.NotifyCanExecuteChanged();
    }

    private void SeedReferenceGetters()
    {
        _displayGetters["SystemId"] = r => r.SystemId;
        _displayGetters["Code"] = r => r.Code;
        _displayGetters["Name"] = r => r.Name;
        _displayGetters["FeedType"] = r => r.FeedTypeName;
        _displayGetters["Species"] = r => r.SpeciesName;
        _displayGetters["Size"] = r => r.SizeName;
        _displayGetters["Category"] = r => r.CategoryName;
        _displayGetters["Version"] = r => r.SubCategoryName;
        _displayGetters["Rev"] = r => r.Revision;
        _displayGetters["Active"] = r => r.ActiveLabel;

        _sortGetters["SystemId"] = r => r.SystemId;
        _sortGetters["Code"] = r => r.Code;
        _sortGetters["Name"] = r => r.Name;
        _sortGetters["FeedType"] = r => r.FeedTypeName;
        _sortGetters["Species"] = r => r.SpeciesName;
        _sortGetters["Size"] = r => r.SizeName;
        _sortGetters["Category"] = r => r.CategoryName;
        _sortGetters["Version"] = r => r.SubCategoryName;
        _sortGetters["Rev"] = r => r.Revision;
        _sortGetters["Active"] = r => r.ActiveLabel;
    }

    private void RebuildSpecGetters()
    {
        foreach (var key in _displayGetters.Keys.Where(k => k.StartsWith("spec:", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            _displayGetters.Remove(key);
            _sortGetters.Remove(key);
        }

        foreach (var p in SpecParameters)
        {
            var specId = p.Id;
            var name = p.Name;
            var key = $"spec:{specId}";
            _displayGetters[key] = r =>
            {
                var cell = r.Cells.FirstOrDefault(c => c.SpecParameterId == specId);
                return SpecParameterNormalizer.FormatValue(cell?.TargetValue, name);
            };
            _sortGetters[key] = r =>
            {
                var cell = r.Cells.FirstOrDefault(c => c.SpecParameterId == specId);
                return cell?.TargetValue;
            };
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var load = LoadInternalAsync();
        _loadTask = load;
        await load;
        await OpenPendingProfileIfAnyAsync();
    }

    private async Task LoadInternalAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        SpecParameters.Clear();
        var specs = await db.SpecParameters.AsNoTracking().ToListAsync();
        foreach (var p in SpecParameterNormalizer.OrderByCanonical(specs, x => x.Name))
            SpecParameters.Add(p);
        RebuildSpecGetters();
        OnPropertyChanged(nameof(SpecParameters));

        var list = await db.Formulations
            .AsNoTracking()
            .Include(f => f.FeedType)
            .Include(f => f.Species)
            .Include(f => f.Size)
            .Include(f => f.Category)
            .Include(f => f.SubCategory)
            .Include(f => f.Specs)
            .OrderBy(f => f.Code)
            .ToListAsync();

        MatrixRows.Clear();
        foreach (var f in list)
        {
            var row = new NutritionMatrixRow
            {
                FormulationId = f.Id,
                SystemId = f.SystemId,
                Code = f.Code,
                Name = f.Name,
                FeedTypeName = f.FeedType.Name,
                SpeciesName = f.Species.Name,
                SizeName = f.Size.Name,
                CategoryName = f.Category.Name,
                SubCategoryName = f.SubCategory.Name,
                Revision = f.Revision,
                IsActive = f.IsActive
            };
            var byParam = f.Specs.ToDictionary(s => s.SpecParameterId);
            foreach (var p in SpecParameters)
            {
                byParam.TryGetValue(p.Id, out var existing);
                row.Cells.Add(new NutritionMatrixCell
                {
                    SpecParameterId = p.Id,
                    TargetValue = existing?.TargetValue
                });
            }

            MatrixRows.Add(row);
        }

        SyncCompareFlags();
        RestorePersistedFilters();
        StatusMessage = $"Matrix: {MatrixRows.Count} code(s) × {SpecParameters.Count} nutrient(s).";
    }

    private void RestorePersistedFilters()
    {
        _suppressFilterPersist = true;
        try
        {
            _filterController.RestoreState(ColumnFilterStateStore.Load(FilterStateKey));
        }
        finally
        {
            _suppressFilterPersist = false;
        }
    }

    public void QueueOpenProfile(string code)
    {
        _pendingProfileCode = code;
        _ = OpenPendingProfileIfAnyAsync();
    }

    private async Task OpenPendingProfileIfAnyAsync()
    {
        await _loadTask;
        var code = _pendingProfileCode;
        if (string.IsNullOrWhiteSpace(code)) return;
        _pendingProfileCode = null;

        var row = MatrixRows.FirstOrDefault(r =>
            string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            StatusMessage = $"No nutrition profile row for '{code}'.";
            return;
        }

        await OpenProfileAsync(row);
    }

    [RelayCommand]
    private void OpenFormulation(NutritionMatrixRow? row)
    {
        if (row is null) return;
        _navigation.NavigateTo<FormulationsViewModel>(vm => _ = vm.SelectAndOpenByCodeAsync(row.Code));
    }

    [RelayCommand]
    private void ResetAllFilters()
    {
        _filterController.ResetAll();
        ColumnFilterStateStore.Save(FilterStateKey, ColumnFilterState.Empty);
        _compareSelection.ClearProfiles();
        SyncCompareFlags();
        StatusMessage = "Filters and compare selection cleared.";
    }

    [RelayCommand]
    private void ToggleCompare(NutritionMatrixRow? row)
    {
        if (row is null) return;
        var desired = !_compareSelection.IsProfileSelected(row.Code);
        if (!_compareSelection.TrySetProfile(row.Code, desired, out var error))
        {
            // Force OneWay CheckBox binding refresh (local IsChecked may already be true).
            row.IsCompareSelected = true;
            row.IsCompareSelected = false;
            SyncCompareFlags();
            StatusMessage = error ?? "Could not update compare selection.";
            return;
        }

        row.IsCompareSelected = desired;
        SyncCompareFlags();
        StatusMessage = _compareSelection.ProfileCount == 0
            ? "Compare selection cleared."
            : $"Compare: {_compareSelection.ProfileCount} profile(s) selected.";
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCompareProfiles))]
    private void Compare()
    {
        _navigation.NavigateTo<ComparisonViewModel>(vm => vm.ShowProfiles());
    }

    private bool CanExecuteCompareProfiles() => _compareSelection.CanCompareProfiles;

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var ids = MatrixRows.Select(r => r.FormulationId).ToList();
            var entities = await db.Formulations
                .Include(f => f.Specs)
                .Where(f => ids.Contains(f.Id))
                .ToListAsync();
            var byId = entities.ToDictionary(f => f.Id);

            foreach (var row in MatrixRows)
            {
                if (!byId.TryGetValue(row.FormulationId, out var entity)) continue;
                UpsertTargets(entity, row.Cells.Select(c => (c.SpecParameterId, c.TargetValue)));
            }

            await db.SaveChangesAsync();
            StatusMessage = $"Saved matrix for {MatrixRows.Count} formulation(s).";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task OpenProfileAsync(NutritionMatrixRow row)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var specs = await db.FormulationSpecs
                .AsNoTracking()
                .Where(s => s.FormulationId == row.FormulationId)
                .ToListAsync();
            var byParam = specs.ToDictionary(s => s.SpecParameterId);

            var lines = new ObservableCollection<NutritionSpecLine>();
            foreach (var p in SpecParameters)
            {
                byParam.TryGetValue(p.Id, out var existing);
                var decimals = SpecParameterNormalizer.GetDecimals(p.Name);
                lines.Add(new NutritionSpecLine
                {
                    SpecParameterId = p.Id,
                    DecimalPlaces = decimals,
                    Name = p.Name,
                    Unit = p.Unit,
                    TargetValue = NutritionSpecLine.RoundTarget(existing?.TargetValue, decimals)
                });
            }

            var editor = new NutritionProfileEditorViewModel(row.Code, lines, _compareSelection, _navigation);
            var window = new Views.NutritionProfileWindow
            {
                DataContext = editor,
                Owner = Application.Current.MainWindow
            };
            if (window.ShowDialog() != true)
            {
                SyncCompareFlags();
                return;
            }

            await using var saveDb = await _dbFactory.CreateDbContextAsync();
            var entity = await saveDb.Formulations
                .Include(f => f.Specs)
                .FirstOrDefaultAsync(f => f.Id == row.FormulationId);
            if (entity is null) return;

            UpsertTargets(entity, editor.Lines.Select(l =>
                (l.SpecParameterId, NutritionSpecLine.RoundTarget(l.TargetValue, l.DecimalPlaces))));
            await saveDb.SaveChangesAsync();

            foreach (var cell in row.Cells)
            {
                var line = editor.Lines.FirstOrDefault(l => l.SpecParameterId == cell.SpecParameterId);
                if (line is not null)
                    cell.TargetValue = NutritionSpecLine.RoundTarget(line.TargetValue, line.DecimalPlaces);
            }

            _filterController.Apply();
            SyncCompareFlags();
            StatusMessage = $"Saved nutrition profile for {row.Code}.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void UpsertTargets(
        Formulation entity,
        IEnumerable<(int SpecParameterId, decimal? Target)> lines)
    {
        foreach (var (specId, target) in lines)
        {
            var existing = entity.Specs.FirstOrDefault(s => s.SpecParameterId == specId);
            if (existing is null)
            {
                if (target is null)
                    continue;
                entity.Specs.Add(new FormulationSpec
                {
                    SpecParameterId = specId,
                    TargetValue = target
                });
            }
            else
            {
                existing.TargetValue = target;
            }
        }
    }

    [RelayCommand]
    private void DownloadImportSample() =>
        ImportSampleDownload.PromptSave(
            "nutrition-profile-import-sample.xlsx",
            ImportSampleWorkbookFactory.SaveNutritionProfileSample);

    [RelayCommand]
    private async Task ImportNutritionAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            Title = "Import nutrition profiles"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var preview = await NutritionProfileImportService.PreviewAsync(db, dialog.FileName);

            var sections = new List<ImportPreviewSection>
            {
                new("Warnings", "Warning", preview.Issues.Select(i => i.Message)),
                new("Codes to update", "Info", preview.CodesToUpdate.Select(c => c))
            };
            var vm = new ImportPreviewViewModel(
                "Import nutrition profiles",
                preview.SummaryText,
                sections,
                preview.CanApply,
                preview.Issues.Count > 0 ? "Confirm import" : "Confirm import");

            var window = new Views.ImportPreviewWindow
            {
                DataContext = vm,
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
            if (!vm.Confirmed)
            {
                StatusMessage = "Import cancelled.";
                return;
            }

            await using var applyDb = await _dbFactory.CreateDbContextAsync();
            var changed = await NutritionProfileImportService.ApplyAsync(applyDb, dialog.FileName);
            StatusMessage = $"Imported nutrition: {changed} target value(s) updated.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Rebuild matrix DataGrid columns when SpecParameters change.</summary>
    public void BuildMatrixColumns(DataGrid grid)
    {
        grid.Columns.Clear();

        void AddRef(string key, string title, string path, double width, double minWidth, IValueConverter? converter = null)
        {
            var binding = new Binding(path);
            if (converter is not null)
                binding.Converter = converter;

            grid.Columns.Add(new DataGridTextColumn
            {
                Header = CreateFilterHeader(key, title),
                Binding = binding,
                IsReadOnly = true,
                Width = new DataGridLength(width),
                MinWidth = minWidth
            });
        }

        var yesNo = Application.Current.TryFindResource("BoolToYesNo") as IValueConverter;

        grid.Columns.Add(CreateCompareColumn());
        AddRef("SystemId", "System ID", nameof(NutritionMatrixRow.SystemId), 100, 80);
        grid.Columns.Add(CreateCodeLinkColumn());
        AddRef("Name", "Name", nameof(NutritionMatrixRow.Name), 140, 90);
        AddRef("Version", "Version", nameof(NutritionMatrixRow.SubCategoryName), 90, 80);
        AddRef("Category", "Category", nameof(NutritionMatrixRow.CategoryName), 120, 80);
        AddRef("FeedType", "Feed type", nameof(NutritionMatrixRow.FeedTypeName), 120, 90);
        AddRef("Size", "Size", nameof(NutritionMatrixRow.SizeName), 70, 50);
        AddRef("Rev", "Rev", nameof(NutritionMatrixRow.Revision), 60, 40);
        AddRef("Active", "Active", nameof(NutritionMatrixRow.IsActive), 70, 55, yesNo);
        AddRef("Species", "Species", nameof(NutritionMatrixRow.SpeciesName), 90, 70);

        for (var i = 0; i < SpecParameters.Count; i++)
        {
            var p = SpecParameters[i];
            var title = string.IsNullOrWhiteSpace(p.Unit) ? p.Name : $"{p.Name} ({p.Unit})";
            var decimals = SpecParameterNormalizer.GetDecimals(p.Name);
            var format = decimals == 0 ? "0" : $"0.{new string('0', decimals)}";
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = CreateFilterHeader($"spec:{p.Id}", title),
                Binding = new Binding($"Cells[{i}].{nameof(NutritionMatrixCell.TargetValue)}")
                {
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                    StringFormat = format
                },
                Width = new DataGridLength(90),
                MinWidth = 70
            });
        }
    }

    private DataGridTemplateColumn CreateCompareColumn()
    {
        var checkFactory = new FrameworkElementFactory(typeof(CheckBox));
        checkFactory.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            new Binding(nameof(NutritionMatrixRow.IsCompareSelected))
            {
                Mode = BindingMode.OneWay
            });
        checkFactory.SetBinding(System.Windows.Controls.Primitives.ButtonBase.CommandProperty,
            new Binding("DataContext.ToggleCompareCommand")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UserControl), 1)
            });
        checkFactory.SetBinding(System.Windows.Controls.Primitives.ButtonBase.CommandParameterProperty, new Binding());
        checkFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        checkFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        checkFactory.SetValue(FrameworkElement.ToolTipProperty, "Include in comparison (max 10)");

        return new DataGridTemplateColumn
        {
            Header = "Compare",
            CellTemplate = new DataTemplate { VisualTree = checkFactory },
            Width = new DataGridLength(70),
            MinWidth = 60,
            IsReadOnly = false
        };
    }

    private DataGridTemplateColumn CreateCodeLinkColumn()
    {
        var buttonFactory = new FrameworkElementFactory(typeof(Button));
        buttonFactory.SetBinding(Button.ContentProperty, new Binding(nameof(NutritionMatrixRow.Code)));
        buttonFactory.SetBinding(Button.CommandProperty, new Binding("DataContext.OpenFormulationCommand")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(UserControl), 1)
        });
        buttonFactory.SetBinding(Button.CommandParameterProperty, new Binding());
        buttonFactory.SetValue(FrameworkElement.ToolTipProperty, "Open formulation");
        buttonFactory.SetValue(Control.PaddingProperty, new Thickness(4, 0, 4, 0));
        buttonFactory.SetValue(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        buttonFactory.SetValue(Control.BorderThicknessProperty, new Thickness(0));
        buttonFactory.SetValue(Control.CursorProperty, System.Windows.Input.Cursors.Hand);
        if (Application.Current.TryFindResource("AccentBrush") is System.Windows.Media.Brush accent)
            buttonFactory.SetValue(Control.ForegroundProperty, accent);
        buttonFactory.SetValue(Control.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        buttonFactory.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left);

        return new DataGridTemplateColumn
        {
            Header = CreateFilterHeader("Code", "Code"),
            CellTemplate = new DataTemplate { VisualTree = buttonFactory },
            Width = new DataGridLength(100),
            MinWidth = 80,
            IsReadOnly = true
        };
    }

    private ExcelColumnFilterHeader CreateFilterHeader(string key, string title) =>
        new()
        {
            Title = title,
            ColumnKey = key,
            Host = FilterHost,
            AllowValueFilter = true
        };
}

public partial class NutritionProfileEditorViewModel : ObservableObject
{
    private readonly ColumnFilterController<NutritionSpecLine> _filterController;
    private readonly CompareSelectionService _compareSelection;
    private readonly INavigationService _navigation;

    public string Code { get; }
    public string Title => $"Nutrition profile — {Code}";
    public ObservableCollection<NutritionSpecLine> Lines { get; }
    public ObservableCollection<NutritionSpecLine> FilteredLines { get; } = new();
    public IColumnFilterHost FilterHost => _filterController;

    [ObservableProperty] private bool _isInCompare;
    [ObservableProperty] private string _compareToggleLabel = "Add to compare";
    [ObservableProperty] private bool _canCompareNow;

    public NutritionProfileEditorViewModel(
        string code,
        ObservableCollection<NutritionSpecLine> lines,
        CompareSelectionService compareSelection,
        INavigationService navigation)
    {
        Code = code;
        Lines = lines;
        _compareSelection = compareSelection;
        _navigation = navigation;
        RefreshCompareUi();
        _filterController = new ColumnFilterController<NutritionSpecLine>(
            () => Lines,
            list =>
            {
                FilteredLines.Clear();
                foreach (var item in list)
                    FilteredLines.Add(item);
            },
            new Dictionary<string, Func<NutritionSpecLine, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nutrient"] = r => r.Name,
                ["Unit"] = r => r.Unit,
                ["Value"] = r => r.TargetText
            },
            new Dictionary<string, Func<NutritionSpecLine, IComparable?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Nutrient"] = r => r.Name,
                ["Unit"] = r => r.Unit,
                ["Value"] = r => r.TargetValue
            });
        _filterController.Apply();
    }

    private void RefreshCompareUi()
    {
        IsInCompare = _compareSelection.IsProfileSelected(Code);
        CompareToggleLabel = IsInCompare ? "Remove from compare" : "Add to compare";
        var count = _compareSelection.ProfileCount;
        CanCompareNow = IsInCompare
            ? count >= 2
            : count + 1 >= 2;
        CompareNowCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ToggleCompareSelection()
    {
        var next = !IsInCompare;
        if (!_compareSelection.TrySetProfile(Code, next, out var error))
        {
            MessageBox.Show(error ?? "Could not update compare selection.", "Compare",
                MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshCompareUi();
            return;
        }

        RefreshCompareUi();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteCompareNow))]
    private void CompareNow(Window? window)
    {
        if (!_compareSelection.IsProfileSelected(Code))
        {
            if (!_compareSelection.TrySetProfile(Code, true, out var error))
            {
                MessageBox.Show(error ?? "Could not add to compare.", "Compare",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshCompareUi();
                return;
            }
        }

        if (!_compareSelection.CanCompareProfiles)
        {
            MessageBox.Show("Select at least 2 profiles to compare.", "Compare",
                MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshCompareUi();
            return;
        }

        if (window is not null)
            window.DialogResult = false;
        _navigation.NavigateTo<ComparisonViewModel>(vm => vm.ShowProfiles());
    }

    private bool CanExecuteCompareNow() => CanCompareNow;

    partial void OnCanCompareNowChanged(bool value) => CompareNowCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void ResetAllFilters() => FilterHost.ResetAll();

    [RelayCommand]
    private void Save(Window? window)
    {
        if (window is not null)
            window.DialogResult = true;
    }

    [RelayCommand]
    private void Cancel(Window? window)
    {
        if (window is not null)
            window.DialogResult = false;
    }
}
