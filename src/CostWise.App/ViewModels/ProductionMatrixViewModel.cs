using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;
using CostWise.App.Views;
using CostWise.Core.Entities;
using CostWise.Core.Services;
using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.ViewModels;

public partial class NamedFilterOption : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public partial class ProductionMatrixColumn : ObservableObject
{
    [ObservableProperty] private int _formulationId;
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _sizeName = string.Empty;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private decimal _totalPercent;
    [ObservableProperty] private bool _isValid = true;
}

public partial class ProductionMatrixCell : ObservableObject
{
    public ProductionMatrixCell(int formulationId, decimal? percent)
    {
        FormulationId = formulationId;
        Percent = percent;
    }

    public int FormulationId { get; }
    public decimal? Percent { get; }

    public string PercentText =>
        Percent is null or 0m
            ? string.Empty
            : Percent.Value.ToString("0.##", CultureInfo.CurrentCulture);
}

public partial class ProductionMatrixRow : ObservableObject
{
    public int RawIngredientId { get; init; }
    public string IngredientName { get; init; } = string.Empty;
    public ObservableCollection<ProductionMatrixCell> Cells { get; } = new();
}

public partial class ProductionMatrixViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private readonly ProductionExportService _exportService;
    private HashSet<int> _memberIds = new();

    public ObservableCollection<ProductionGroup> Groups { get; } = new();
    public ObservableCollection<ProductionMatrixColumn> Columns { get; } = new();
    public ObservableCollection<ProductionMatrixRow> Rows { get; } = new();

    public event Action? MatrixStructureChanged;

    [ObservableProperty] private ProductionGroup? _selectedGroup;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _totalsSummary = string.Empty;
    [ObservableProperty] private string _selectionCountText = string.Empty;

    public ProductionMatrixViewModel(
        IDbContextFactory<CostWiseDbContext> dbFactory,
        ProductionExportService exportService)
    {
        _dbFactory = dbFactory;
        _exportService = exportService;
        _ = LoadAsync();
    }

    partial void OnSelectedGroupChanged(ProductionGroup? value)
    {
        HasSelection = value is not null;
        if (value is null)
        {
            EditName = string.Empty;
            _memberIds = new HashSet<int>();
            SelectionCountText = string.Empty;
            ClearMatrix();
            return;
        }

        EditName = value.Name;
        _ = LoadGroupDetailAsync(value.Id);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await FormulationActiveSync.SyncFromProductionGroupsAsync(db);
        await db.SaveChangesAsync();

        var selectedId = SelectedGroup?.Id;
        Groups.Clear();
        foreach (var g in await db.ProductionGroups
                     .OrderBy(g => g.SortOrder)
                     .ThenBy(g => g.Name)
                     .ToListAsync())
            Groups.Add(g);

        SelectedGroup = selectedId is int id
            ? Groups.FirstOrDefault(g => g.Id == id) ?? Groups.FirstOrDefault()
            : Groups.FirstOrDefault();

        StatusMessage = $"{Groups.Count} production group(s).";
    }

    [RelayCommand]
    private async Task NewGroupAsync()
    {
        var name = $"Production group {Groups.Count + 1}";
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.ProductionGroups.Select(g => g.Name).ToListAsync();
        var baseName = name;
        var n = 1;
        while (existing.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
        {
            n++;
            name = $"{baseName} ({n})";
        }

        var maxOrder = await db.ProductionGroups.Select(g => (int?)g.SortOrder).MaxAsync() ?? -1;
        var group = new ProductionGroup
        {
            Name = name,
            SortOrder = maxOrder + 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.ProductionGroups.Add(group);
        await db.SaveChangesAsync();
        StatusMessage = $"Created '{group.Name}'.";
        await LoadAsync();
        SelectedGroup = Groups.FirstOrDefault(g => g.Id == group.Id);
    }

    [RelayCommand]
    private async Task SaveGroupNameAsync()
    {
        if (SelectedGroup is null) return;

        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Group name is required.";
            EditName = SelectedGroup.Name;
            return;
        }

        var name = EditName.Trim();
        if (string.Equals(name, SelectedGroup.Name, StringComparison.Ordinal))
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.ProductionGroups.AnyAsync(g => g.Id != SelectedGroup.Id && g.Name.ToLower() == name.ToLower()))
        {
            StatusMessage = $"A group named '{name}' already exists.";
            EditName = SelectedGroup.Name;
            return;
        }

        var entity = await db.ProductionGroups.FindAsync(SelectedGroup.Id);
        if (entity is null)
        {
            StatusMessage = "Group not found.";
            return;
        }

        entity.Name = name;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        StatusMessage = $"Renamed group to '{entity.Name}'.";
        await LoadAsync();
        SelectedGroup = Groups.FirstOrDefault(g => g.Id == entity.Id);
    }

    [RelayCommand]
    private async Task OpenFormulaPickerAsync()
    {
        if (SelectedGroup is null)
        {
            StatusMessage = "Select or create a group first.";
            return;
        }

        // Persist name first if edited
        if (!string.IsNullOrWhiteSpace(EditName) &&
            !string.Equals(EditName.Trim(), SelectedGroup.Name, StringComparison.Ordinal))
        {
            await SaveGroupNameAsync();
            if (SelectedGroup is null) return;
        }

        List<FormulaPickerRow> rows;
        HashSet<int> memberIds;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            memberIds = (await db.ProductionGroupFormulations
                    .Where(x => x.ProductionGroupId == SelectedGroup.Id)
                    .Select(x => x.FormulationId)
                    .ToListAsync())
                .ToHashSet();

            var formulations = (await db.Formulations
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .Include(f => f.SubCategory)
                    .Include(f => f.Size)
                    .Include(f => f.FeedType)
                    .OrderBy(f => f.Category.Name)
                    .ThenBy(f => f.Code)
                    .ToListAsync())
                .OrderBy(f => f.Category.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Size.DiameterMm)
                .ThenBy(f => f.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            rows = formulations.Select(f => new FormulaPickerRow
            {
                FormulationId = f.Id,
                CategoryId = f.CategoryId,
                SubCategoryId = f.SubCategoryId,
                Code = f.Code,
                CategoryName = f.Category.Name,
                SubCategoryName = f.SubCategory.Name,
                SizeName = f.Size.Name,
                FeedTypeName = f.FeedType.Name,
                IsSelected = memberIds.Contains(f.Id)
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load formulations:\n{ex.Message}", "Select formulas",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = $"Failed to load formulas: {ex.Message}";
            return;
        }

        var pickerVm = new FormulaPickerViewModel(SelectedGroup.Name, rows);
        var window = new FormulaPickerWindow
        {
            DataContext = pickerVm,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current.MainWindow
        };
        window.ShowDialog();

        if (!pickerVm.Confirmed)
        {
            StatusMessage = "Formula selection cancelled.";
            return;
        }

        var selectedIds = pickerVm.GetSelectedIds();
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var entity = await db.ProductionGroups
                .Include(g => g.Formulations)
                .FirstOrDefaultAsync(g => g.Id == SelectedGroup.Id);
            if (entity is null)
            {
                StatusMessage = "Group not found.";
                return;
            }

            db.ProductionGroupFormulations.RemoveRange(entity.Formulations);
            var order = 0;
            foreach (var fid in selectedIds)
            {
                db.ProductionGroupFormulations.Add(new ProductionGroupFormulation
                {
                    ProductionGroupId = entity.Id,
                    FormulationId = fid,
                    SortOrder = order++
                });
            }

            entity.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await FormulationActiveSync.SyncFromProductionGroupsAsync(db);
            await db.SaveChangesAsync();
        }

        StatusMessage = $"Group '{SelectedGroup.Name}' now has {selectedIds.Count} formula(s).";
        await LoadGroupDetailAsync(SelectedGroup.Id);
    }

    [RelayCommand]
    private async Task ReorderGroupsAsync(IList<int>? orderedIds)
    {
        if (orderedIds is null || orderedIds.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var groups = await db.ProductionGroups.ToListAsync();
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var entity = groups.FirstOrDefault(g => g.Id == orderedIds[i]);
            if (entity is null) continue;
            entity.SortOrder = i;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var selectedId = SelectedGroup?.Id;
        Groups.Clear();
        foreach (var id in orderedIds)
        {
            var g = groups.FirstOrDefault(x => x.Id == id);
            if (g is not null)
                Groups.Add(g);
        }

        SelectedGroup = selectedId is int sid
            ? Groups.FirstOrDefault(g => g.Id == sid)
            : SelectedGroup;

        StatusMessage = "Group order saved.";
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        try
        {
            var snapshot = await _exportService.BuildSnapshotAsync();
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel workbook (*.xlsx)|*.xlsx",
                FileName = $"CostWise-Production-{DateTime.Now:yyyyMMdd}.xlsx"
            };
            if (dialog.ShowDialog() != true) return;

            ProductionExportWriter.WriteExcel(snapshot, dialog.FileName);
            StatusMessage = $"Exported Excel ({snapshot.Groups.Count} group(s)): {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Excel export failed: {ex.Message}";
            MessageBox.Show(ex.Message, "Export Excel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        try
        {
            var snapshot = await _exportService.BuildSnapshotAsync();
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF document (*.pdf)|*.pdf",
                FileName = $"CostWise-Production-{DateTime.Now:yyyyMMdd}.pdf"
            };
            if (dialog.ShowDialog() != true) return;

            ProductionExportWriter.WritePdf(snapshot, dialog.FileName);
            StatusMessage = $"Exported PDF ({snapshot.Groups.Count} group(s)): {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"PDF export failed: {ex.Message}";
            MessageBox.Show(ex.Message, "Export PDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteGroupAsync()
    {
        if (SelectedGroup is null)
        {
            StatusMessage = "Select a group to delete.";
            return;
        }

        var confirm = MessageBox.Show(
            $"Delete production group '{SelectedGroup.Name}'?",
            "Delete group",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.ProductionGroups.FindAsync(SelectedGroup.Id);
        if (entity is not null)
        {
            db.ProductionGroups.Remove(entity);
            await db.SaveChangesAsync();
            await FormulationActiveSync.SyncFromProductionGroupsAsync(db);
            await db.SaveChangesAsync();
        }

        StatusMessage = "Group deleted.";
        SelectedGroup = null;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedGroup is null)
            await LoadAsync();
        else
            await LoadGroupDetailAsync(SelectedGroup.Id);
    }

    private async Task LoadGroupDetailAsync(int groupId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var group = await db.ProductionGroups
                .AsNoTracking()
                .Include(g => g.Formulations)
                .FirstOrDefaultAsync(g => g.Id == groupId);
            if (group is null) return;

            EditName = group.Name;
            _memberIds = group.Formulations.Select(f => f.FormulationId).ToHashSet();
            SelectionCountText = _memberIds.Count == 0
                ? "No formulas in group — click Select formulas"
                : $"{_memberIds.Count} formula(s) in group";

            var memberFormulas = await db.Formulations
                .AsNoTracking()
                .Where(f => _memberIds.Contains(f.Id))
                .Include(f => f.Category)
                .Include(f => f.Size)
                .Include(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
                .ToListAsync();

            var ordered = memberFormulas
                .OrderBy(f => f.Category.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Size.DiameterMm)
                .ThenBy(f => f.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            BuildMatrix(ordered);
            StatusMessage =
                $"{group.Name}: {ordered.Count} formula column(s), {Rows.Count} ingredient row(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load group: {ex.Message}";
        }
    }

    private void BuildMatrix(List<Formulation> formulas)
    {
        ClearMatrix();

        foreach (var f in formulas)
        {
            Columns.Add(new ProductionMatrixColumn
            {
                FormulationId = f.Id,
                CategoryName = f.Category.Name,
                SizeName = f.Size.Name,
                Code = f.Code
            });
        }

        var ingredients = formulas
            .SelectMany(f => f.Ingredients)
            .Where(i => i.RawIngredient is not null)
            .GroupBy(i => i.RawIngredientId)
            .Select(g => g.First().RawIngredient)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var rm in ingredients)
        {
            var row = new ProductionMatrixRow
            {
                RawIngredientId = rm.Id,
                IngredientName = rm.Name
            };
            foreach (var f in formulas)
            {
                var pct = f.Ingredients.FirstOrDefault(i => i.RawIngredientId == rm.Id)?.InclusionPercent;
                row.Cells.Add(new ProductionMatrixCell(f.Id, pct is > 0m ? pct : null));
            }
            Rows.Add(row);
        }

        RecalcTotals();
        MatrixStructureChanged?.Invoke();
    }

    private void ClearMatrix()
    {
        Columns.Clear();
        Rows.Clear();
        TotalsSummary = string.Empty;
        MatrixStructureChanged?.Invoke();
    }

    private void RecalcTotals()
    {
        foreach (var col in Columns)
        {
            var total = Rows
                .Select(r => r.Cells.FirstOrDefault(c => c.FormulationId == col.FormulationId)?.Percent ?? 0m)
                .Sum();
            col.TotalPercent = total;
            col.IsValid = Math.Abs(total - 100m) <= FormulationRules.InclusionTolerance;
        }

        TotalsSummary = string.Join("  |  ",
            Columns.Select(c =>
                $"{c.Code}: {c.TotalPercent:0.##}%{(c.IsValid ? "" : " !")}"));
    }
}
