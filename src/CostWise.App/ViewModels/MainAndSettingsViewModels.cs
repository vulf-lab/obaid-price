using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;
using CostWise.Core.Entities;
using CostWise.Core.Enums;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using FeedSize = CostWise.Core.Entities.Size;

namespace CostWise.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly Services.INavigationService _navigation;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPageTitle = "Formulations";

    public MainViewModel(Services.INavigationService navigation)
    {
        _navigation = navigation;
        _navigation.CurrentViewModelChanged += vm =>
        {
            CurrentView = Services.ViewModelToViewConverter.Locator?.Resolve(vm!) ?? vm;
        };
    }

    [RelayCommand]
    private void NavigateFormulations()
    {
        CurrentPageTitle = "Formulations";
        _navigation.NavigateTo<FormulationsViewModel>();
    }

    [RelayCommand]
    private void NavigateProduction()
    {
        CurrentPageTitle = "Active formulations (production)";
        _navigation.NavigateTo<ProductionMatrixViewModel>();
    }

    [RelayCommand]
    private void NavigateRawIngredients()
    {
        CurrentPageTitle = "Raw Ingredients";
        _navigation.NavigateTo<RawIngredientsViewModel>();
    }

    [RelayCommand]
    private void NavigateSpecifications()
    {
        CurrentPageTitle = "Specifications";
        _navigation.NavigateTo<SpecParametersViewModel>();
    }

    [RelayCommand]
    private void NavigatePricing()
    {
        CurrentPageTitle = "Pricing";
        _navigation.NavigateTo<PricingViewModel>();
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        CurrentPageTitle = "Settings";
        _navigation.NavigateTo<SettingsViewModel>();
    }

    public void Initialize() => NavigateFormulations();
}

public partial class NamedItemRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isActive = true;
}

public partial class SizeRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _diameterMm;
    [ObservableProperty] private decimal _conversionCost;
    [ObservableProperty] private bool _isActive = true;
}

public partial class PricingCostOptionRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _cost;
    [ObservableProperty] private bool _isActive = true;
}

public sealed record PricingCostOptionChoice(int? Id, string Label, decimal Cost)
{
    public static PricingCostOptionChoice None { get; } = new(null, "None", 0m);

    public static PricingCostOptionChoice From(PricingCostOption option) =>
        new(option.Id, $"{option.Name} — {option.Cost:0.##}", option.Cost);
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private readonly AppPreferences _preferences;

    public ObservableCollection<NamedItemRow> FeedTypes { get; } = new();
    public ObservableCollection<NamedItemRow> SpeciesItems { get; } = new();
    public ObservableCollection<NamedItemRow> Categories { get; } = new();
    public ObservableCollection<NamedItemRow> SubCategories { get; } = new();
    public ObservableCollection<SizeRow> Sizes { get; } = new();
    public ObservableCollection<PricingCostOptionRow> PackingOptions { get; } = new();
    public ObservableCollection<PricingCostOptionRow> DocumentOptions { get; } = new();
    public ObservableCollection<PricingCostOptionRow> AdditiveOptions { get; } = new();
    public int[] DecimalPlaceOptions { get; } = [0, 1, 2, 3, 4];

    [ObservableProperty] private string _newFeedTypeName = string.Empty;
    [ObservableProperty] private string _newSpeciesName = string.Empty;
    [ObservableProperty] private string _newCategoryName = string.Empty;
    [ObservableProperty] private string _newSubCategoryName = string.Empty;
    [ObservableProperty] private string _newSizeName = string.Empty;
    [ObservableProperty] private decimal _newSizeDiameter;
    [ObservableProperty] private decimal _newSizeConversionCost;
    [ObservableProperty] private string _newPackingName = string.Empty;
    [ObservableProperty] private decimal _newPackingCost;
    [ObservableProperty] private string _newDocumentName = string.Empty;
    [ObservableProperty] private decimal _newDocumentCost;
    [ObservableProperty] private string _newAdditiveName = string.Empty;
    [ObservableProperty] private decimal _newAdditiveCost;
    [ObservableProperty] private NamedItemRow? _selectedFeedType;
    [ObservableProperty] private NamedItemRow? _selectedSpecies;
    [ObservableProperty] private NamedItemRow? _selectedCategory;
    [ObservableProperty] private NamedItemRow? _selectedSubCategory;
    [ObservableProperty] private SizeRow? _selectedSize;
    [ObservableProperty] private PricingCostOptionRow? _selectedPackingOption;
    [ObservableProperty] private PricingCostOptionRow? _selectedDocumentOption;
    [ObservableProperty] private PricingCostOptionRow? _selectedAdditiveOption;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _costDecimalPlaces = 2;

    public SettingsViewModel(IDbContextFactory<CostWiseDbContext> dbFactory, AppPreferences preferences)
    {
        _dbFactory = dbFactory;
        _preferences = preferences;
        CostDecimalPlaces = preferences.CostDecimalPlaces;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void SaveDisplaySettings()
    {
        _preferences.CostDecimalPlaces = CostDecimalPlaces;
        _preferences.Save();
        StatusMessage = $"Display settings saved ({CostDecimalPlaces} decimal place(s) for costs).";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        FeedTypes.Clear();
        foreach (var item in await db.FeedTypes.OrderBy(x => x.Name).ToListAsync())
            FeedTypes.Add(new NamedItemRow { Id = item.Id, Name = item.Name, IsActive = item.IsActive });

        SpeciesItems.Clear();
        foreach (var item in await db.Species.OrderBy(x => x.Name).ToListAsync())
            SpeciesItems.Add(new NamedItemRow { Id = item.Id, Name = item.Name, IsActive = item.IsActive });

        Categories.Clear();
        foreach (var item in await db.Categories.OrderBy(x => x.Name).ToListAsync())
            Categories.Add(new NamedItemRow { Id = item.Id, Name = item.Name, IsActive = item.IsActive });

        SubCategories.Clear();
        foreach (var item in await db.SubCategories.OrderBy(x => x.Name).ToListAsync())
            SubCategories.Add(new NamedItemRow { Id = item.Id, Name = item.Name, IsActive = item.IsActive });

        Sizes.Clear();
        foreach (var item in (await db.Sizes.ToListAsync()).OrderBy(x => x.DiameterMm))
        {
            Sizes.Add(new SizeRow
            {
                Id = item.Id,
                Name = item.Name,
                DiameterMm = item.DiameterMm,
                ConversionCost = item.ConversionCost,
                IsActive = item.IsActive
            });
        }

        await LoadPricingCostOptionsAsync(db);
    }

    private async Task LoadPricingCostOptionsAsync(CostWiseDbContext db)
    {
        var all = await db.PricingCostOptions.AsNoTracking().ToListAsync();
        FillPricingCostOptions(PackingOptions, all, PricingCostKind.Packing);
        FillPricingCostOptions(DocumentOptions, all, PricingCostKind.Documents);
        FillPricingCostOptions(AdditiveOptions, all, PricingCostKind.Additive);
    }

    private static void FillPricingCostOptions(
        ObservableCollection<PricingCostOptionRow> target,
        IEnumerable<PricingCostOption> all,
        PricingCostKind kind)
    {
        target.Clear();
        foreach (var item in all.Where(x => x.Kind == kind).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            target.Add(new PricingCostOptionRow
            {
                Id = item.Id,
                Name = item.Name,
                Cost = item.Cost,
                IsActive = item.IsActive
            });
        }
    }

    [RelayCommand]
    private async Task AddFeedTypeAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFeedTypeName)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.FeedTypes.Add(new FeedType { Name = NewFeedTypeName.Trim() });
        await db.SaveChangesAsync();
        NewFeedTypeName = string.Empty;
        StatusMessage = "Feed type added.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveFeedTypeAsync()
    {
        if (FeedTypes.Count == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.FeedTypes.ToDictionaryAsync(x => x.Id);
        foreach (var row in FeedTypes)
        {
            if (!byId.TryGetValue(row.Id, out var entity)) continue;
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every feed type needs a name.";
                return;
            }
            entity.Name = row.Name.Trim();
            entity.IsActive = row.IsActive;
        }
        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {FeedTypes.Count} feed types.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveFeedTypeAsync()
    {
        if (SelectedFeedType is null) return;
        if (!ConfirmDelete(SelectedFeedType.Name)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.FeedTypes.FindAsync(SelectedFeedType.Id);
        if (entity is null) return;
        try
        {
            db.FeedTypes.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Feed type removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: feed type is in use.";
        }
    }

    [RelayCommand]
    private async Task AddSpeciesAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSpeciesName)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Species.Add(new Species { Name = NewSpeciesName.Trim() });
        await db.SaveChangesAsync();
        NewSpeciesName = string.Empty;
        StatusMessage = "Species added.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveSpeciesAsync()
    {
        if (SpeciesItems.Count == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.Species.ToDictionaryAsync(x => x.Id);
        foreach (var row in SpeciesItems)
        {
            if (!byId.TryGetValue(row.Id, out var entity)) continue;
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every species needs a name.";
                return;
            }
            entity.Name = row.Name.Trim();
            entity.IsActive = row.IsActive;
        }
        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {SpeciesItems.Count} species.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveSpeciesAsync()
    {
        if (SelectedSpecies is null) return;
        if (!ConfirmDelete(SelectedSpecies.Name)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Species.FindAsync(SelectedSpecies.Id);
        if (entity is null) return;
        try
        {
            db.Species.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Species removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: species is in use.";
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Categories.Add(new Category { Name = NewCategoryName.Trim() });
        await db.SaveChangesAsync();
        NewCategoryName = string.Empty;
        StatusMessage = "Category added.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveCategoryAsync()
    {
        if (Categories.Count == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.Categories.ToDictionaryAsync(x => x.Id);
        foreach (var row in Categories)
        {
            if (!byId.TryGetValue(row.Id, out var entity)) continue;
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every category needs a name.";
                return;
            }
            entity.Name = row.Name.Trim();
            entity.IsActive = row.IsActive;
        }
        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {Categories.Count} categories.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveCategoryAsync()
    {
        if (SelectedCategory is null) return;
        if (!ConfirmDelete(SelectedCategory.Name)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Categories.FindAsync(SelectedCategory.Id);
        if (entity is null) return;
        try
        {
            db.Categories.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Category removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: category is in use.";
        }
    }

    [RelayCommand]
    private async Task AddSubCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSubCategoryName)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.SubCategories.Add(new SubCategory { Name = NewSubCategoryName.Trim() });
        await db.SaveChangesAsync();
        NewSubCategoryName = string.Empty;
        StatusMessage = "Sub category (version) added.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveSubCategoryAsync()
    {
        if (SubCategories.Count == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.SubCategories.ToDictionaryAsync(x => x.Id);
        foreach (var row in SubCategories)
        {
            if (!byId.TryGetValue(row.Id, out var entity)) continue;
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every version needs a name.";
                return;
            }
            entity.Name = row.Name.Trim();
            entity.IsActive = row.IsActive;
        }
        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {SubCategories.Count} versions.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveSubCategoryAsync()
    {
        if (SelectedSubCategory is null) return;
        if (!ConfirmDelete(SelectedSubCategory.Name)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.SubCategories.FindAsync(SelectedSubCategory.Id);
        if (entity is null) return;
        try
        {
            db.SubCategories.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Sub category removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: sub category is in use.";
        }
    }

    [RelayCommand]
    private async Task AddSizeAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSizeName)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.Sizes.Add(new FeedSize
        {
            Name = NewSizeName.Trim(),
            DiameterMm = NewSizeDiameter,
            ConversionCost = NewSizeConversionCost
        });
        await db.SaveChangesAsync();
        NewSizeName = string.Empty;
        NewSizeDiameter = 0;
        NewSizeConversionCost = 0;
        StatusMessage = "Size added.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveSizeAsync()
    {
        if (Sizes.Count == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.Sizes.ToDictionaryAsync(x => x.Id);
        foreach (var row in Sizes)
        {
            if (!byId.TryGetValue(row.Id, out var entity)) continue;
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every size needs a name.";
                return;
            }
            entity.Name = row.Name.Trim();
            entity.DiameterMm = row.DiameterMm;
            entity.ConversionCost = row.ConversionCost;
            entity.FeedTypeId = null;
            entity.IsActive = row.IsActive;
        }
        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {Sizes.Count} sizes.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveSizeAsync()
    {
        if (SelectedSize is null) return;
        if (!ConfirmDelete(SelectedSize.Name)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Sizes.FindAsync(SelectedSize.Id);
        if (entity is null) return;
        try
        {
            db.Sizes.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Size removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: size is in use.";
        }
    }

    [RelayCommand]
    private async Task AddPackingOptionAsync() =>
        await AddPricingCostOptionAsync(PricingCostKind.Packing, NewPackingName, NewPackingCost, () =>
        {
            NewPackingName = string.Empty;
            NewPackingCost = 0;
        });

    [RelayCommand]
    private async Task SavePackingOptionAsync() =>
        await SavePricingCostOptionsAsync(PackingOptions, PricingCostKind.Packing, "packing");

    [RelayCommand]
    private async Task RemovePackingOptionAsync() =>
        await RemovePricingCostOptionAsync(SelectedPackingOption, "packing option");

    [RelayCommand]
    private async Task AddDocumentOptionAsync() =>
        await AddPricingCostOptionAsync(PricingCostKind.Documents, NewDocumentName, NewDocumentCost, () =>
        {
            NewDocumentName = string.Empty;
            NewDocumentCost = 0;
        });

    [RelayCommand]
    private async Task SaveDocumentOptionAsync() =>
        await SavePricingCostOptionsAsync(DocumentOptions, PricingCostKind.Documents, "document");

    [RelayCommand]
    private async Task RemoveDocumentOptionAsync() =>
        await RemovePricingCostOptionAsync(SelectedDocumentOption, "document option");

    [RelayCommand]
    private async Task AddAdditiveOptionAsync() =>
        await AddPricingCostOptionAsync(PricingCostKind.Additive, NewAdditiveName, NewAdditiveCost, () =>
        {
            NewAdditiveName = string.Empty;
            NewAdditiveCost = 0;
        });

    [RelayCommand]
    private async Task SaveAdditiveOptionAsync() =>
        await SavePricingCostOptionsAsync(AdditiveOptions, PricingCostKind.Additive, "additive");

    [RelayCommand]
    private async Task RemoveAdditiveOptionAsync() =>
        await RemovePricingCostOptionAsync(SelectedAdditiveOption, "additive option");

    private async Task AddPricingCostOptionAsync(
        PricingCostKind kind,
        string name,
        decimal cost,
        Action clearInputs)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.PricingCostOptions.Add(new PricingCostOption
        {
            Name = name.Trim(),
            Cost = cost,
            Kind = kind,
            IsActive = true
        });
        try
        {
            await db.SaveChangesAsync();
            clearInputs();
            StatusMessage = $"{kind} option added.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = $"A {kind.ToString().ToLowerInvariant()} option named '{name.Trim()}' already exists.";
        }
    }

    private async Task SavePricingCostOptionsAsync(
        ObservableCollection<PricingCostOptionRow> rows,
        PricingCostKind kind,
        string label)
    {
        if (rows.Count == 0) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.PricingCostOptions
            .Where(x => x.Kind == kind)
            .ToDictionaryAsync(x => x.Id);
        foreach (var row in rows)
        {
            if (!byId.TryGetValue(row.Id, out var entity)) continue;
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = $"Every {label} option needs a name.";
                return;
            }

            entity.Name = row.Name.Trim();
            entity.Cost = row.Cost;
            entity.IsActive = row.IsActive;
        }

        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {rows.Count} {label} option(s).";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique within each option type.";
        }
    }

    private async Task RemovePricingCostOptionAsync(PricingCostOptionRow? selected, string label)
    {
        if (selected is null) return;
        if (!ConfirmDelete(selected.Name)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.PricingCostOptions.FindAsync(selected.Id);
        if (entity is null) return;
        try
        {
            db.PricingCostOptions.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = $"{label} removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = $"Cannot remove: {label} is in use.";
        }
    }

    private static bool ConfirmDelete(string name) =>
        MessageBox.Show($"Remove '{name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
}
