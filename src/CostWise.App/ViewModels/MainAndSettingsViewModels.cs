using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;
using CostWise.App.Services.Update;
using CostWise.Core.Entities;
using CostWise.Core.Enums;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using FeedSize = CostWise.Core.Entities.Size;

namespace CostWise.App.ViewModels;

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

public partial class CurrencyRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _kesPerUnit = 1m;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private bool _isBase;
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
    private readonly UpdateService _updateService;

    public ObservableCollection<NamedItemRow> FeedTypes { get; } = new();
    public ObservableCollection<NamedItemRow> SpeciesItems { get; } = new();
    public ObservableCollection<NamedItemRow> Categories { get; } = new();
    public ObservableCollection<NamedItemRow> SubCategories { get; } = new();
    public ObservableCollection<SizeRow> Sizes { get; } = new();
    public ObservableCollection<PricingCostOptionRow> PackingOptions { get; } = new();
    public ObservableCollection<PricingCostOptionRow> DocumentOptions { get; } = new();
    public ObservableCollection<PricingCostOptionRow> AdditiveOptions { get; } = new();
    public ObservableCollection<CurrencyRow> Currencies { get; } = new();
    public int[] DecimalPlaceOptions { get; } = [0, 1, 2, 3, 4];
    public IReadOnlyList<UpdatePolicyOption> UpdatePolicyOptions { get; } =
    [
        new(UpdatePolicy.Prompt, "Ask before downloading (recommended)"),
        new(UpdatePolicy.SilentDownloadApplyOnRestart, "Download quietly; apply on restart"),
        new(UpdatePolicy.Off, "Do not check for updates")
    ];
    public SpecParametersViewModel Nutrients { get; }

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
    [ObservableProperty] private string _newCurrencyCode = string.Empty;
    [ObservableProperty] private string _newCurrencyName = string.Empty;
    [ObservableProperty] private decimal _newCurrencyRate = 1m;
    [ObservableProperty] private NamedItemRow? _selectedFeedType;
    [ObservableProperty] private NamedItemRow? _selectedSpecies;
    [ObservableProperty] private NamedItemRow? _selectedCategory;
    [ObservableProperty] private NamedItemRow? _selectedSubCategory;
    [ObservableProperty] private SizeRow? _selectedSize;
    [ObservableProperty] private PricingCostOptionRow? _selectedPackingOption;
    [ObservableProperty] private PricingCostOptionRow? _selectedDocumentOption;
    [ObservableProperty] private PricingCostOptionRow? _selectedAdditiveOption;
    [ObservableProperty] private CurrencyRow? _selectedCurrency;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _costDecimalPlaces = 2;
    [ObservableProperty] private ImageSource? _victoryLeftLogoImage;
    [ObservableProperty] private ImageSource? _victoryRightLogoImage;
    [ObservableProperty] private ImageSource? _commercialLogoImage;
    [ObservableProperty] private UpdatePolicyOption? _selectedUpdatePolicy;
    [ObservableProperty] private string _appVersionText = string.Empty;
    [ObservableProperty] private bool _isCheckingUpdates;

    public SettingsViewModel(
        IDbContextFactory<CostWiseDbContext> dbFactory,
        AppPreferences preferences,
        UpdateService updateService)
    {
        _dbFactory = dbFactory;
        _preferences = preferences;
        _updateService = updateService;
        Nutrients = new SpecParametersViewModel(dbFactory);
        CostDecimalPlaces = preferences.CostDecimalPlaces;
        AppVersionText = $"Version {UpdateService.CurrentVersion}";
        SelectedUpdatePolicy = UpdatePolicyOptions.FirstOrDefault(o => o.Policy == preferences.UpdatePolicy)
                               ?? UpdatePolicyOptions[0];
        RefreshBrandingPaths();
        _ = LoadAsync();
    }

    private void RefreshBrandingPaths()
    {
        VictoryLeftLogoImage = LoadImage(BrandingLogoStore.GetPath(BrandingLogoStore.VictoryLeft));
        VictoryRightLogoImage = LoadImage(BrandingLogoStore.GetPath(BrandingLogoStore.VictoryRight));
        CommercialLogoImage = LoadImage(BrandingLogoStore.GetPath(BrandingLogoStore.Commercial));
    }

    private static ImageSource? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    [RelayCommand]
    private void UploadBrandingLogo(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return;
        var dialog = new OpenFileDialog
        {
            Filter = "Image files|*.png;*.jpg;*.jpeg|All files|*.*",
            Title = "Select logo"
        };
        if (dialog.ShowDialog() != true) return;
        BrandingLogoStore.SetFromFile(slot, dialog.FileName);
        RefreshBrandingPaths();
        StatusMessage = "Logo updated.";
    }

    [RelayCommand]
    private void ClearBrandingLogo(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return;
        BrandingLogoStore.Clear(slot);
        RefreshBrandingPaths();
        StatusMessage = "Logo cleared.";
    }

    [RelayCommand]
    private void SaveDisplaySettings()
    {
        _preferences.CostDecimalPlaces = CostDecimalPlaces;
        _preferences.Save();
        StatusMessage = $"Display settings saved ({CostDecimalPlaces} decimal place(s) for costs).";
    }

    [RelayCommand]
    private void SaveUpdateSettings()
    {
        if (SelectedUpdatePolicy is null) return;
        _preferences.UpdatePolicy = SelectedUpdatePolicy.Policy;
        if (SelectedUpdatePolicy.Policy != UpdatePolicy.Off)
            _preferences.SkippedUpdateVersion = null;
        _preferences.Save();
        StatusMessage = "Update settings saved.";
    }

    [RelayCommand]
    private async Task CheckForUpdatesNowAsync()
    {
        if (IsCheckingUpdates) return;
        IsCheckingUpdates = true;
        try
        {
            var owner = new WpfWindowOwner(Application.Current?.MainWindow);
            await _updateService.CheckAndHandleAsync(owner, interactivePrompt: true, force: true);
            StatusMessage = "Update check finished.";
        }
        catch (Exception ex)
        {
            AppLog.Error("Manual update check failed", ex);
            StatusMessage = $"Update check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdates = false;
        }
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
        await LoadCurrenciesAsync(db);
    }

    private async Task LoadCurrenciesAsync(CostWiseDbContext db)
    {
        Currencies.Clear();
        foreach (var item in await db.Currencies
                     .OrderBy(x => x.SortOrder)
                     .ThenBy(x => x.Code)
                     .ToListAsync())
        {
            Currencies.Add(new CurrencyRow
            {
                Id = item.Id,
                Code = item.Code,
                Name = item.Name,
                KesPerUnit = item.KesPerUnit,
                IsActive = item.IsActive,
                IsBase = item.IsBase
            });
        }
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

    [RelayCommand]
    private async Task AddCurrencyAsync()
    {
        var code = NewCurrencyCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusMessage = "Currency code is required.";
            return;
        }

        if (NewCurrencyRate <= 0m)
        {
            StatusMessage = "KES per unit must be greater than zero.";
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.Currencies.AnyAsync(c => c.Code.ToLower() == code.ToLower()))
        {
            StatusMessage = $"Currency code '{code}' already exists.";
            return;
        }

        var maxOrder = await db.Currencies.Select(c => (int?)c.SortOrder).MaxAsync() ?? -1;
        db.Currencies.Add(new Currency
        {
            Code = code,
            Name = string.IsNullOrWhiteSpace(NewCurrencyName) ? code : NewCurrencyName.Trim(),
            IsBase = false,
            KesPerUnit = NewCurrencyRate,
            IsActive = true,
            SortOrder = maxOrder + 1
        });

        try
        {
            await db.SaveChangesAsync();
            NewCurrencyCode = string.Empty;
            NewCurrencyName = string.Empty;
            NewCurrencyRate = 1m;
            StatusMessage = "Currency added.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Add failed. Currency code must be unique.";
        }
    }

    [RelayCommand]
    private async Task SaveCurrencyAsync()
    {
        if (Currencies.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.Currencies.ToDictionaryAsync(x => x.Id);
        foreach (var row in Currencies)
        {
            if (!byId.TryGetValue(row.Id, out var entity)) continue;

            if (string.IsNullOrWhiteSpace(row.Code))
            {
                StatusMessage = "Every currency needs a code.";
                return;
            }

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every currency needs a name.";
                return;
            }

            var code = row.Code.Trim().ToUpperInvariant();
            if (!entity.IsBase && row.KesPerUnit <= 0m)
            {
                StatusMessage = $"KES per unit for {code} must be greater than zero.";
                return;
            }

            entity.Code = code;
            entity.Name = row.Name.Trim();
            entity.KesPerUnit = entity.IsBase ? 1m : row.KesPerUnit;
            entity.IsActive = row.IsActive;
        }

        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {Currencies.Count} currency/currencies.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Currency codes must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveCurrencyAsync()
    {
        if (SelectedCurrency is null) return;
        if (SelectedCurrency.IsBase)
        {
            StatusMessage = "Cannot remove the base currency (KES).";
            return;
        }

        if (!ConfirmDelete($"{SelectedCurrency.Code} — {SelectedCurrency.Name}")) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.Currencies.FindAsync(SelectedCurrency.Id);
        if (entity is null) return;

        if (entity.IsBase)
        {
            StatusMessage = "Cannot remove the base currency (KES).";
            return;
        }

        try
        {
            db.Currencies.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Currency removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: currency is in use.";
        }
    }

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
            foreach (var row in rows)
            {
                if (!byId.ContainsKey(row.Id)) continue;
                await SyncLinkedCostSnapshotsAsync(db, kind, row.Id, row.Cost);
            }

            StatusMessage = $"Saved {rows.Count} {label} option(s).";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique within each option type.";
        }
    }

    /// <summary>
    /// Keep PriceBook / CostingScenario snapshotted costs in sync when a linked option's price changes.
    /// </summary>
    private static async Task SyncLinkedCostSnapshotsAsync(
        CostWiseDbContext db,
        PricingCostKind kind,
        int optionId,
        decimal cost)
    {
        switch (kind)
        {
            case PricingCostKind.Packing:
                await db.PriceBooks
                    .Where(b => b.PackingOptionId == optionId)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.PackingCost, cost));
                await db.CostingScenarios
                    .Where(s => s.PackingOptionId == optionId)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.PackingCost, cost));
                break;
            case PricingCostKind.Documents:
                await db.PriceBooks
                    .Where(b => b.ExportDocOptionId == optionId)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.ExportDocCost, cost));
                await db.CostingScenarios
                    .Where(s => s.ExportDocOptionId == optionId)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.ExportDocCost, cost));
                break;
            case PricingCostKind.Additive:
                await db.PriceBooks
                    .Where(b => b.AdditiveOptionId == optionId)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.SpecialAdditiveCost, cost));
                await db.CostingScenarios
                    .Where(s => s.AdditiveOptionId == optionId)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.SpecialAdditiveCost, cost));
                break;
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

public sealed record UpdatePolicyOption(UpdatePolicy Policy, string Label);
