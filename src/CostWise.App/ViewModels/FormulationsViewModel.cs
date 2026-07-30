using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;
using CostWise.Core.Entities;
using CostWise.Core.Services;
using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using FeedSize = CostWise.Core.Entities.Size;

namespace CostWise.App.ViewModels;

public enum ProducibilityFilter
{
    All,
    Producible,
    Unfit
}

public enum ActiveFilter
{
    All,
    Active,
    Inactive
}

public partial class FormulationListItem : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _systemId = string.Empty;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _feedTypeName = string.Empty;
    [ObservableProperty] private string _speciesName = string.Empty;
    [ObservableProperty] private string _sizeName = string.Empty;
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _subCategoryName = string.Empty;
    [ObservableProperty] private string _revision = string.Empty;
    [ObservableProperty] private bool _isUnfit;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _unavailableMaterials = string.Empty;
    [ObservableProperty] private decimal _rmCost;
    [ObservableProperty] private DateTime _updatedAtUtc;
}

public partial class IngredientLineRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private int _rawIngredientId;
    [ObservableProperty] private string _rawIngredientName = string.Empty;
    [ObservableProperty] private decimal _inclusionPercent;
    [ObservableProperty] private decimal _pricePerMt;
    [ObservableProperty] private bool _isAvailable = true;

    public decimal LineCost => InclusionPercent / 100m * PricePerMt;

    partial void OnInclusionPercentChanged(decimal value) => OnPropertyChanged(nameof(LineCost));
    partial void OnPricePerMtChanged(decimal value) => OnPropertyChanged(nameof(LineCost));
}

public partial class SpecLineRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private int _specParameterId;
    [ObservableProperty] private string _specParameterName = string.Empty;
    [ObservableProperty] private string _unit = string.Empty;
    [ObservableProperty] private decimal? _targetValue;
    [ObservableProperty] private decimal? _minValue;
    [ObservableProperty] private decimal? _maxValue;
}

public partial class ChangeLogRow : ObservableObject
{
    [ObservableProperty] private DateTime _changedAtUtc;
    [ObservableProperty] private string _action = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
}

public partial class FormulationsViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private readonly AppPreferences _preferences;

    public ObservableCollection<FormulationListItem> Items { get; } = new();
    public ObservableCollection<FormulationListItem> FilteredItems { get; } = new();
    public ObservableCollection<Category> CategoryFilters { get; } = new();
    public ObservableCollection<SubCategory> SubCategoryFilters { get; } = new();
    public ObservableCollection<FeedType> FeedTypeFilters { get; } = new();
    public ObservableCollection<string> RevisionFilters { get; } = new();
    public ObservableCollection<ChangeLogRow> ChangeLogs { get; } = new();

    [ObservableProperty] private FormulationListItem? _selectedItem;
    [ObservableProperty] private ProducibilityFilter _producibilityFilter = ProducibilityFilter.All;
    [ObservableProperty] private ActiveFilter _activeFilter = ActiveFilter.All;
    [ObservableProperty] private Category? _selectedCategoryFilter;
    [ObservableProperty] private SubCategory? _selectedSubCategoryFilter;
    [ObservableProperty] private FeedType? _selectedFeedTypeFilter;
    [ObservableProperty] private string? _selectedRevisionFilter;
    [ObservableProperty] private bool _isDetailOpen;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _editSystemId = string.Empty;
    [ObservableProperty] private string _editCode = string.Empty;
    [ObservableProperty] private FeedType? _editFeedType;
    [ObservableProperty] private Species? _editSpecies;
    [ObservableProperty] private FeedSize? _editSize;
    [ObservableProperty] private Category? _editCategory;
    [ObservableProperty] private SubCategory? _editSubCategory;
    [ObservableProperty] private string _editRevision = string.Empty;
    [ObservableProperty] private bool _editIsActive;
    [ObservableProperty] private DateTime _editCreatedAtUtc;
    [ObservableProperty] private DateTime _editUpdatedAtUtc;
    [ObservableProperty] private DateTime? _editImportedAtUtc;
    [ObservableProperty] private decimal _totalInclusion;
    [ObservableProperty] private decimal _liveRmCost;
    [ObservableProperty] private bool _inclusionsValid;

    public ObservableCollection<FeedType> FeedTypes { get; } = new();
    public ObservableCollection<Species> SpeciesOptions { get; } = new();
    public ObservableCollection<FeedSize> Sizes { get; } = new();
    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<SubCategory> SubCategories { get; } = new();
    public ObservableCollection<RawIngredient> AvailableRawIngredients { get; } = new();
    public ObservableCollection<SpecParameter> SpecParameters { get; } = new();
    public ObservableCollection<IngredientLineRow> IngredientLines { get; } = new();
    public ObservableCollection<SpecLineRow> SpecLines { get; } = new();

    [ObservableProperty] private RawIngredient? _selectedRawToAdd;
    [ObservableProperty] private SpecParameter? _selectedSpecToAdd;

    public Array ProducibilityFilters => Enum.GetValues(typeof(ProducibilityFilter));
    public Array ActiveFilters => Enum.GetValues(typeof(ActiveFilter));
    public bool IsCodeEditable => IsEditMode && EditId == 0;
    public bool IsViewMode => IsDetailOpen && !IsEditMode;

    public FormulationsViewModel(IDbContextFactory<CostWiseDbContext> dbFactory, AppPreferences preferences)
    {
        _dbFactory = dbFactory;
        _preferences = preferences;
        _preferences.Changed += (_, _) => RefreshMoneyDisplay();
        IngredientLines.CollectionChanged += (_, _) => RecalcTotals();
        _ = LoadAsync();
    }

    partial void OnProducibilityFilterChanged(ProducibilityFilter value) => ApplyFilters();
    partial void OnActiveFilterChanged(ActiveFilter value) => ApplyFilters();
    partial void OnSelectedCategoryFilterChanged(Category? value) => ApplyFilters();
    partial void OnSelectedSubCategoryFilterChanged(SubCategory? value) => ApplyFilters();
    partial void OnSelectedFeedTypeFilterChanged(FeedType? value) => ApplyFilters();
    partial void OnSelectedRevisionFilterChanged(string? value) => ApplyFilters();
    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCodeEditable));
        OnPropertyChanged(nameof(IsViewMode));
    }
    partial void OnIsDetailOpenChanged(bool value) => OnPropertyChanged(nameof(IsViewMode));
    partial void OnEditIdChanged(int value) => OnPropertyChanged(nameof(IsCodeEditable));

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await FormulationActiveSync.SyncFromProductionGroupsAsync(db);
        await db.SaveChangesAsync();

        CategoryFilters.Clear();
        CategoryFilters.Add(new Category { Id = 0, Name = "All categories" });
        foreach (var c in await db.Categories.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync())
            CategoryFilters.Add(c);
        SelectedCategoryFilter = CategoryFilters.First();

        SubCategoryFilters.Clear();
        SubCategoryFilters.Add(new SubCategory { Id = 0, Name = "All versions" });
        foreach (var s in await db.SubCategories.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync())
            SubCategoryFilters.Add(s);
        SelectedSubCategoryFilter = SubCategoryFilters.First();

        FeedTypeFilters.Clear();
        FeedTypeFilters.Add(new FeedType { Id = 0, Name = "All feed types" });
        foreach (var ft in await db.FeedTypes.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync())
            FeedTypeFilters.Add(ft);
        SelectedFeedTypeFilter = FeedTypeFilters.First();

        var formulations = await db.Formulations
            .Include(f => f.FeedType)
            .Include(f => f.Species)
            .Include(f => f.Size)
            .Include(f => f.Category)
            .Include(f => f.SubCategory)
            .Include(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
            .OrderBy(f => f.Code)
            .ToListAsync();

        Items.Clear();
        foreach (var f in formulations)
        {
            var unavailable = f.Ingredients
                .Where(i => i.RawIngredient is not null && !i.RawIngredient.IsAvailable)
                .Select(i => i.RawIngredient.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();
            var unfit = unavailable.Count > 0;
            var rm = CostingCalculator.CalculateRmCost(
                f.Ingredients.Select(i => new IngredientCostLine(i.InclusionPercent, i.RawIngredient.PricePerMt)));
            Items.Add(new FormulationListItem
            {
                Id = f.Id,
                SystemId = f.SystemId,
                Code = f.Code,
                FeedTypeName = f.FeedType.Name,
                SpeciesName = f.Species.Name,
                SizeName = f.Size.Name,
                CategoryName = f.Category.Name,
                SubCategoryName = f.SubCategory.Name,
                Revision = f.Revision,
                IsUnfit = unfit,
                IsActive = f.IsActive,
                UnavailableMaterials = unfit
                    ? "Unavailable:\n" + string.Join("\n", unavailable.Select(n => "• " + n))
                    : string.Empty,
                RmCost = rm,
                UpdatedAtUtc = f.UpdatedAtUtc
            });
        }

        RevisionFilters.Clear();
        RevisionFilters.Add("All revs");
        foreach (var rev in Items.Select(i => i.Revision).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
            RevisionFilters.Add(rev);
        SelectedRevisionFilter = RevisionFilters.First();

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        FilteredItems.Clear();
        foreach (var item in Items)
        {
            if (ProducibilityFilter == ProducibilityFilter.Producible && item.IsUnfit) continue;
            if (ProducibilityFilter == ProducibilityFilter.Unfit && !item.IsUnfit) continue;
            if (ActiveFilter == ActiveFilter.Active && !item.IsActive) continue;
            if (ActiveFilter == ActiveFilter.Inactive && item.IsActive) continue;
            if (SelectedCategoryFilter is { Id: > 0 } && item.CategoryName != SelectedCategoryFilter.Name) continue;
            if (SelectedSubCategoryFilter is { Id: > 0 } && item.SubCategoryName != SelectedSubCategoryFilter.Name) continue;
            if (SelectedFeedTypeFilter is { Id: > 0 } && item.FeedTypeName != SelectedFeedTypeFilter.Name) continue;
            if (!string.IsNullOrEmpty(SelectedRevisionFilter)
                && SelectedRevisionFilter != "All revs"
                && !string.Equals(item.Revision, SelectedRevisionFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            FilteredItems.Add(item);
        }
    }

    [RelayCommand]
    private async Task NewAsync()
    {
        try
        {
            await LoadLookupAsync();
            EditId = 0;
            EditSystemId = "(assigned on save)";
            EditCode = string.Empty;
            EditFeedType = FeedTypes.FirstOrDefault();
            EditSpecies = SpeciesOptions.FirstOrDefault();
            EditSize = Sizes.FirstOrDefault();
            EditCategory = Categories.FirstOrDefault();
            EditSubCategory = SubCategories.FirstOrDefault();
            EditRevision = "1";
            EditIsActive = false;
            EditCreatedAtUtc = DateTime.UtcNow;
            EditUpdatedAtUtc = DateTime.UtcNow;
            EditImportedAtUtc = null;
            IngredientLines.Clear();
            SpecLines.Clear();
            ChangeLogs.Clear();
            RecalcTotals();
            StatusMessage = string.Empty;
            IsDetailOpen = true;
            IsEditMode = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not create formulation:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task OpenAsync(FormulationListItem? item)
    {
        try
        {
            if (item is not null)
                SelectedItem = item;
            if (SelectedItem is null) return;

            await LoadFormulationDetailAsync(SelectedItem.Id);
            StatusMessage = string.Empty;
            IsDetailOpen = true;
            IsEditMode = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open formulation:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task BeginEditAsync()
    {
        if (!IsDetailOpen) return;
        if (EditId == 0)
        {
            IsEditMode = true;
            return;
        }

        try
        {
            await LoadLookupAsync();
            await LoadFormulationDetailAsync(EditId);
            IsEditMode = true;
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not enter edit mode:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (!IsDetailOpen) return;

        if (IsEditMode)
        {
            if (EditId == 0)
            {
                CloseDetail();
                return;
            }

            try
            {
                await LoadFormulationDetailAsync(EditId);
                IsEditMode = false;
                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not cancel edit:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return;
        }

        CloseDetail();
    }

    [RelayCommand]
    private void CloseDetail()
    {
        IsEditMode = false;
        IsDetailOpen = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void AddIngredientLine()
    {
        if (!IsEditMode || SelectedRawToAdd is null) return;
        if (IngredientLines.Any(x => x.RawIngredientId == SelectedRawToAdd.Id))
        {
            StatusMessage = "Ingredient already in formula.";
            return;
        }

        var row = new IngredientLineRow
        {
            RawIngredientId = SelectedRawToAdd.Id,
            RawIngredientName = SelectedRawToAdd.Name,
            InclusionPercent = 0,
            PricePerMt = SelectedRawToAdd.PricePerMt,
            IsAvailable = SelectedRawToAdd.IsAvailable
        };
        row.PropertyChanged += OnIngredientPropertyChanged;
        IngredientLines.Add(row);
        RecalcTotals();
    }

    [RelayCommand]
    private void RemoveIngredientLine(IngredientLineRow? row)
    {
        if (!IsEditMode || row is null) return;
        IngredientLines.Remove(row);
        RecalcTotals();
    }

    [RelayCommand]
    private void AddSpecLine()
    {
        if (!IsEditMode || SelectedSpecToAdd is null) return;
        if (SpecLines.Any(x => x.SpecParameterId == SelectedSpecToAdd.Id))
        {
            StatusMessage = "Spec already in formula.";
            return;
        }

        SpecLines.Add(new SpecLineRow
        {
            SpecParameterId = SelectedSpecToAdd.Id,
            SpecParameterName = SelectedSpecToAdd.Name,
            Unit = SelectedSpecToAdd.Unit
        });
    }

    [RelayCommand]
    private void RemoveSpecLine(SpecLineRow? row)
    {
        if (!IsEditMode || row is null) return;
        SpecLines.Remove(row);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!IsEditMode) return;

        if (string.IsNullOrWhiteSpace(EditCode))
        {
            StatusMessage = "Code is required.";
            return;
        }

        if (EditFeedType is null || EditSpecies is null || EditSize is null || EditCategory is null || EditSubCategory is null)
        {
            StatusMessage = "Feed type, species, size, category, and version are required.";
            return;
        }

        RecalcTotals();
        if (!InclusionsValid)
        {
            StatusMessage = $"Inclusions must total 100% (currently {TotalInclusion:0.####}%).";
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        Formulation entity;
        var isNew = EditId == 0;
        if (isNew)
        {
            entity = new Formulation
            {
                SystemId = await SystemIdGenerator.NextAsync(db),
                CreatedAtUtc = now
            };
            db.Formulations.Add(entity);
        }
        else
        {
            entity = await db.Formulations
                .Include(x => x.Ingredients)
                .Include(x => x.Specs)
                .FirstAsync(x => x.Id == EditId);
            db.FormulationIngredients.RemoveRange(entity.Ingredients);
            db.FormulationSpecs.RemoveRange(entity.Specs);
        }

        entity.Code = EditCode.Trim();
        entity.Name = EditCode.Trim();
        entity.FeedTypeId = EditFeedType.Id;
        entity.SpeciesId = EditSpecies.Id;
        entity.SizeId = EditSize.Id;
        entity.CategoryId = EditCategory.Id;
        entity.SubCategoryId = EditSubCategory.Id;
        entity.Revision = string.IsNullOrWhiteSpace(EditRevision) ? string.Empty : EditRevision.Trim();
        // IsActive is owned by Production group membership — do not change here
        entity.UpdatedAtUtc = now;
        entity.Ingredients = IngredientLines.Select(l => new FormulationIngredient
        {
            RawIngredientId = l.RawIngredientId,
            InclusionPercent = l.InclusionPercent
        }).ToList();
        entity.Specs = SpecLines.Select(l => new FormulationSpec
        {
            SpecParameterId = l.SpecParameterId,
            TargetValue = l.TargetValue,
            MinValue = l.MinValue,
            MaxValue = l.MaxValue
        }).ToList();

        try
        {
            await db.SaveChangesAsync();
            var details = $"Rev {entity.Revision}; ingredients {entity.Ingredients.Count}; specs {entity.Specs.Count}; active={entity.IsActive}";
            FormulationAudit.Log(
                db,
                entity,
                isNew ? FormulationChangeAction.Created : FormulationChangeAction.Updated,
                isNew ? $"Created {entity.Code}" : $"Updated {entity.Code}",
                details: details);
            await db.SaveChangesAsync();

            EditId = entity.Id;
            await LoadAsync();
            await LoadFormulationDetailAsync(EditId);
            IsEditMode = false;
            StatusMessage = "Formulation saved.";
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Code must be unique.";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedItem is null) return;
        if (MessageBox.Show($"Delete formulation '{SelectedItem.Code}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Formulations.FindAsync(SelectedItem.Id);
        if (entity is null) return;

        FormulationAudit.Log(db, entity, FormulationChangeAction.Deleted, $"Deleted {entity.Code}");
        db.Formulations.Remove(entity);
        await db.SaveChangesAsync();
        CloseDetail();
        StatusMessage = "Formulation deleted.";
        await LoadAsync();
    }

    private async Task LoadFormulationDetailAsync(int formulationId)
    {
        await LoadLookupAsync();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var f = await db.Formulations
            .Include(x => x.Ingredients).ThenInclude(i => i.RawIngredient)
            .Include(x => x.Specs).ThenInclude(s => s.SpecParameter)
            .FirstAsync(x => x.Id == formulationId);

        EditId = f.Id;
        EditSystemId = f.SystemId;
        EditCode = f.Code;
        EditFeedType = FeedTypes.FirstOrDefault(x => x.Id == f.FeedTypeId);
        EditSpecies = SpeciesOptions.FirstOrDefault(x => x.Id == f.SpeciesId);
        EditSize = Sizes.FirstOrDefault(x => x.Id == f.SizeId);
        EditCategory = Categories.FirstOrDefault(x => x.Id == f.CategoryId);
        EditSubCategory = SubCategories.FirstOrDefault(x => x.Id == f.SubCategoryId);
        EditRevision = f.Revision;
        EditIsActive = f.IsActive;
        EditCreatedAtUtc = f.CreatedAtUtc;
        EditUpdatedAtUtc = f.UpdatedAtUtc;
        EditImportedAtUtc = f.ImportedAtUtc;

        IngredientLines.Clear();
        foreach (var line in f.Ingredients)
        {
            if (line.RawIngredient is null) continue;
            var row = new IngredientLineRow
            {
                Id = line.Id,
                RawIngredientId = line.RawIngredientId,
                RawIngredientName = line.RawIngredient.Name,
                InclusionPercent = line.InclusionPercent,
                PricePerMt = line.RawIngredient.PricePerMt,
                IsAvailable = line.RawIngredient.IsAvailable
            };
            row.PropertyChanged += OnIngredientPropertyChanged;
            IngredientLines.Add(row);
        }

        SpecLines.Clear();
        foreach (var line in f.Specs)
        {
            if (line.SpecParameter is null) continue;
            SpecLines.Add(new SpecLineRow
            {
                Id = line.Id,
                SpecParameterId = line.SpecParameterId,
                SpecParameterName = line.SpecParameter.Name,
                Unit = line.SpecParameter.Unit,
                TargetValue = line.TargetValue,
                MinValue = line.MinValue,
                MaxValue = line.MaxValue
            });
        }

        ChangeLogs.Clear();
        foreach (var log in await db.FormulationChangeLogs
                     .Where(l => l.FormulationId == formulationId || l.SystemId == f.SystemId)
                     .OrderByDescending(l => l.ChangedAtUtc)
                     .Take(30)
                     .ToListAsync())
        {
            ChangeLogs.Add(new ChangeLogRow
            {
                ChangedAtUtc = log.ChangedAtUtc,
                Action = log.Action.ToString(),
                Summary = log.Summary
            });
        }

        RecalcTotals();
    }

    private void OnIngredientPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IngredientLineRow.InclusionPercent) or nameof(IngredientLineRow.PricePerMt))
            RecalcTotals();
    }

    private async Task LoadLookupAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        FeedTypes.Clear();
        foreach (var x in await db.FeedTypes.Where(f => f.IsActive).OrderBy(f => f.Name).ToListAsync())
            FeedTypes.Add(x);
        SpeciesOptions.Clear();
        foreach (var x in await db.Species.Where(f => f.IsActive).OrderBy(f => f.Name).ToListAsync())
            SpeciesOptions.Add(x);
        Sizes.Clear();
        foreach (var x in (await db.Sizes.Where(f => f.IsActive).ToListAsync()).OrderBy(f => f.DiameterMm))
            Sizes.Add(x);
        Categories.Clear();
        foreach (var x in await db.Categories.Where(f => f.IsActive).OrderBy(f => f.Name).ToListAsync())
            Categories.Add(x);
        SubCategories.Clear();
        foreach (var x in await db.SubCategories.Where(f => f.IsActive).OrderBy(f => f.Name).ToListAsync())
            SubCategories.Add(x);
        AvailableRawIngredients.Clear();
        foreach (var x in await db.RawIngredients.OrderBy(f => f.Name).ToListAsync())
            AvailableRawIngredients.Add(x);
        SpecParameters.Clear();
        foreach (var x in await db.SpecParameters.OrderBy(f => f.Name).ToListAsync())
            SpecParameters.Add(x);
    }

    private void RecalcTotals()
    {
        TotalInclusion = IngredientLines.Sum(x => x.InclusionPercent);
        LiveRmCost = IngredientLines.Sum(x => x.LineCost);
        InclusionsValid = Math.Abs(TotalInclusion - 100m) <= FormulationRules.InclusionTolerance;
    }

    private void RefreshMoneyDisplay()
    {
        // Recompute list RmCost at current display precision and refresh bindings
        foreach (var item in Items)
        {
            var v = item.RmCost;
            item.RmCost = v + 0.0000000001m;
            item.RmCost = v;
        }

        if (IsDetailOpen)
        {
            RecalcTotals();
            foreach (var line in IngredientLines)
            {
                var p = line.PricePerMt;
                line.PricePerMt = p + 0.0000000001m;
                line.PricePerMt = p;
            }
        }
    }
}
