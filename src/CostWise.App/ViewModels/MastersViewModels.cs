using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Controls;
using CostWise.App.Services;
using CostWise.App.Services.Import;
using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;

namespace CostWise.App.ViewModels;

public partial class RawIngredientRow : ObservableObject
{
    private readonly Func<decimal> _getRate;
    private readonly Func<int> _getUsdPlaces;

    public RawIngredientRow(Func<decimal> getRate, Func<int> getUsdPlaces)
    {
        _getRate = getRate;
        _getUsdPlaces = getUsdPlaces;
    }

    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _pricePerMt;
    [ObservableProperty] private bool _isAvailable = true;

    public decimal PricePerMtUsd
    {
        get
        {
            var rate = _getRate();
            return rate > 0m
                ? Math.Round(PricePerMt / rate, _getUsdPlaces(), MidpointRounding.AwayFromZero)
                : 0m;
        }
    }

    public string AvailableLabel => IsAvailable ? "Yes" : "No";

    partial void OnPricePerMtChanged(decimal value) => OnPropertyChanged(nameof(PricePerMtUsd));
    partial void OnIsAvailableChanged(bool value) => OnPropertyChanged(nameof(AvailableLabel));

    public void NotifyFormatsChanged()
    {
        OnPropertyChanged(nameof(PricePerMt));
        OnPropertyChanged(nameof(PricePerMtUsd));
    }
}

public partial class RawIngredientsViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private readonly AppPreferences _preferences;

    public ObservableCollection<RawIngredientRow> Items { get; } = new();
    public ObservableCollection<RawIngredientRow> FilteredItems { get; } = new();
    public ObservableCollection<RawIngredientHistoryRow> HistoryItems { get; } = new();
    public IColumnFilterHost FilterHost { get; }

    [ObservableProperty] private RawIngredientRow? _selectedItem;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private decimal _newPrice;
    [ObservableProperty] private decimal _exchangeRateKesPerUsd = 130m;
    [ObservableProperty] private int _kesDecimalPlaces = 2;
    [ObservableProperty] private int _usdDecimalPlaces = 2;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isHistoryVisible;
    [ObservableProperty] private string _historyTitle = "Price history";


    public string KesPriceFormat => $"N{KesDecimalPlaces}";
    public string UsdPriceFormat => $"N{UsdDecimalPlaces}";

    public RawIngredientsViewModel(IDbContextFactory<CostWiseDbContext> dbFactory, AppPreferences preferences)
    {
        _dbFactory = dbFactory;
        _preferences = preferences;
        _kesDecimalPlaces = _preferences.KesDecimalPlaces;
        _usdDecimalPlaces = _preferences.UsdDecimalPlaces;
        _preferences.Changed += (_, _) =>
        {
            if (KesDecimalPlaces != _preferences.KesDecimalPlaces)
                KesDecimalPlaces = _preferences.KesDecimalPlaces;
            if (UsdDecimalPlaces != _preferences.UsdDecimalPlaces)
                UsdDecimalPlaces = _preferences.UsdDecimalPlaces;
            RefreshFormats();
        };
        FilterHost = new ColumnFilterController<RawIngredientRow>(
            () => Items,
            list =>
            {
                FilteredItems.Clear();
                foreach (var item in list)
                    FilteredItems.Add(item);
            },
            new Dictionary<string, Func<RawIngredientRow, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = r => r.Name,
                ["Available"] = r => r.AvailableLabel,
                ["PricePerMtKes"] = r => r.PricePerMt.ToString(KesPriceFormat, CultureInfo.CurrentCulture),
                ["PricePerMtUsd"] = r => r.PricePerMtUsd.ToString(UsdPriceFormat, CultureInfo.CurrentCulture)
            },
            new Dictionary<string, Func<RawIngredientRow, IComparable?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = r => r.Name,
                ["Available"] = r => r.AvailableLabel,
                ["PricePerMtKes"] = r => r.PricePerMt,
                ["PricePerMtUsd"] = r => r.PricePerMtUsd
            });
        _ = LoadAsync();
    }

    partial void OnKesDecimalPlacesChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 4);
        if (clamped != value)
        {
            KesDecimalPlaces = clamped;
            return;
        }
        if (_preferences.KesDecimalPlaces != clamped)
        {
            _preferences.KesDecimalPlaces = clamped;
            _preferences.Save();
        }
        OnPropertyChanged(nameof(KesPriceFormat));
        RefreshFormats();
    }

    partial void OnUsdDecimalPlacesChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 4);
        if (clamped != value)
        {
            UsdDecimalPlaces = clamped;
            return;
        }
        if (_preferences.UsdDecimalPlaces != clamped)
        {
            _preferences.UsdDecimalPlaces = clamped;
            _preferences.Save();
        }
        OnPropertyChanged(nameof(UsdPriceFormat));
        RefreshFormats();
    }

    partial void OnExchangeRateKesPerUsdChanged(decimal value)
    {
        if (value <= 0m) return;
        _ = PersistUsdRateAsync(value);
        RefreshFormats();
    }

    private async Task PersistUsdRateAsync(decimal rate)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var usd = await db.Currencies.FirstOrDefaultAsync(c => c.Code == "USD");
            if (usd is not null)
            {
                usd.KesPerUnit = rate;
                await db.SaveChangesAsync();
            }
            _preferences.ExchangeRateKesPerUsd = rate;
            _preferences.Save();
        }
        catch
        {
            _preferences.ExchangeRateKesPerUsd = rate;
            _preferences.Save();
        }
    }

    private void RefreshFormats()
    {
        foreach (var row in Items)
            row.NotifyFormatsChanged();
        ((ColumnFilterController<RawIngredientRow>)FilterHost).Apply();
    }

    private decimal CurrentRate() => ExchangeRateKesPerUsd > 0m ? ExchangeRateKesPerUsd : 130m;

    private RawIngredientRow CreateRow(int id, string name, decimal price, bool available) =>
        new(() => CurrentRate(), () => UsdDecimalPlaces)
        {
            Id = id,
            Name = name,
            PricePerMt = price,
            IsAvailable = available
        };

    [RelayCommand]
    private void DecrementKesDecimals() => KesDecimalPlaces = Math.Max(0, KesDecimalPlaces - 1);

    [RelayCommand]
    private void IncrementKesDecimals() => KesDecimalPlaces = Math.Min(4, KesDecimalPlaces + 1);

    [RelayCommand]
    private void DecrementUsdDecimals() => UsdDecimalPlaces = Math.Max(0, UsdDecimalPlaces - 1);

    [RelayCommand]
    private void IncrementUsdDecimals() => UsdDecimalPlaces = Math.Min(4, UsdDecimalPlaces + 1);

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var usd = await db.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.Code == "USD");
        ExchangeRateKesPerUsd = usd is { KesPerUnit: > 0 }
            ? usd.KesPerUnit
            : (_preferences.ExchangeRateKesPerUsd > 0 ? _preferences.ExchangeRateKesPerUsd : 130m);

        Items.Clear();
        foreach (var item in await db.RawIngredients.OrderBy(x => x.Name).ToListAsync())
            Items.Add(CreateRow(item.Id, item.Name, item.PricePerMt, item.IsAvailable));
        ((ColumnFilterController<RawIngredientRow>)FilterHost).Apply();
    }

    private static decimal RoundKes(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;
        var price = RoundKes(NewPrice);
        var rate = CurrentRate();
        var usd = rate > 0m ? Math.Round(price / rate, UsdDecimalPlaces, MidpointRounding.AwayFromZero) : 0m;
        var available = price > 0m;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = new RawIngredient
        {
            Name = NewName.Trim(),
            PricePerMt = price,
            IsAvailable = available
        };
        db.RawIngredients.Add(entity);
        await db.SaveChangesAsync();

        db.RawIngredientPriceHistories.Add(new RawIngredientPriceHistory
        {
            RawIngredientId = entity.Id,
            PricePerMt = price,
            ExchangeRateKesPerUsd = rate,
            PricePerMtUsd = usd,
            ChangedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        NewName = string.Empty;
        NewPrice = 0;
        StatusMessage = available
            ? "Raw ingredient added."
            : "Raw ingredient added as unavailable (price is 0).";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Items.Count == 0) return;

        var invalid = Items
            .Where(r => r.IsAvailable && RoundKes(r.PricePerMt) == 0m)
            .ToList();
        if (invalid.Count > 0)
        {
            foreach (var row in invalid)
                row.IsAvailable = false;
            StatusMessage =
                "Cannot mark as available when price is 0: " +
                string.Join(", ", invalid.Select(r => r.Name));
            ((ColumnFilterController<RawIngredientRow>)FilterHost).Apply();
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.RawIngredients.ToDictionaryAsync(x => x.Id);
        var madeUnavailable = 0;
        var rate = CurrentRate();
        var history = new List<RawIngredientPriceHistory>();

        foreach (var row in Items)
        {
            if (!byId.TryGetValue(row.Id, out var entity))
                continue;

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every raw ingredient needs a name.";
                return;
            }

            var rounded = RoundKes(row.PricePerMt);
            row.PricePerMt = rounded;

            var wasAvailable = entity.IsAvailable;
            var priceChanged = entity.PricePerMt != rounded;
            entity.Name = row.Name.Trim();
            entity.PricePerMt = rounded;
            entity.IsAvailable = row.IsAvailable;
            if (wasAvailable && !row.IsAvailable)
                madeUnavailable++;

            if (priceChanged)
            {
                var usd = rate > 0m ? Math.Round(rounded / rate, UsdDecimalPlaces, MidpointRounding.AwayFromZero) : 0m;
                history.Add(new RawIngredientPriceHistory
                {
                    RawIngredientId = entity.Id,
                    PricePerMt = rounded,
                    ExchangeRateKesPerUsd = rate,
                    PricePerMtUsd = usd,
                    ChangedAtUtc = DateTime.UtcNow
                });
            }
        }

        try
        {
            if (history.Count > 0)
                db.RawIngredientPriceHistories.AddRange(history);
            await db.SaveChangesAsync();
            StatusMessage = madeUnavailable > 0
                ? $"Saved {Items.Count} raw ingredients. {madeUnavailable} marked unavailable — affected formulations are unfit."
                : $"Saved {Items.Count} raw ingredients.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (SelectedItem is null) return;
        if (MessageBox.Show($"Remove '{SelectedItem.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.RawIngredients.FindAsync(SelectedItem.Id);
        if (entity is null) return;
        try
        {
            db.RawIngredients.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: ingredient is used in formulations.";
        }
    }

    partial void OnSelectedItemChanged(RawIngredientRow? value)
    {
        if (IsHistoryVisible)
            _ = LoadHistoryAsync();
    }

    [RelayCommand]
    private async Task ShowHistoryAsync()
    {
        if (SelectedItem is null)
        {
            StatusMessage = "Select a raw material to view price history.";
            return;
        }

        IsHistoryVisible = true;
        await LoadHistoryAsync();
    }

    [RelayCommand]
    private void DownloadImportSample() =>
        ImportSampleDownload.PromptSave(
            "raw-material-price-import-sample.xlsx",
            ImportSampleWorkbookFactory.SaveRawMaterialSample);

    [RelayCommand]
    private async Task ImportPricesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel workbook (*.xlsx)|*.xlsx",
            Title = "Import raw material prices"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var preview = await RawMaterialPriceImportService.PreviewAsync(db, dialog.FileName);
            if (!ImportPreviewDialog.ShowRmPreview(Application.Current.MainWindow, preview))
            {
                StatusMessage = "Import cancelled.";
                return;
            }

            await using var applyDb = await _dbFactory.CreateDbContextAsync();
            var changed = await RawMaterialPriceImportService.ApplyAsync(
                applyDb, preview, CurrentRate());
            StatusMessage = $"Imported prices: {changed} change(s).";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Import failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void HideHistory()
    {
        IsHistoryVisible = false;
        HistoryItems.Clear();
    }

    private async Task LoadHistoryAsync()
    {
        HistoryItems.Clear();
        if (SelectedItem is null)
        {
            HistoryTitle = "Price history";
            return;
        }

        HistoryTitle = $"Price history — {SelectedItem.Name}";
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.RawIngredientPriceHistories
            .AsNoTracking()
            .Where(h => h.RawIngredientId == SelectedItem.Id)
            .OrderByDescending(h => h.ChangedAtUtc)
            .ToListAsync();

        foreach (var h in rows)
        {
            HistoryItems.Add(new RawIngredientHistoryRow(
                h.ChangedAtUtc.ToLocalTime(),
                h.PricePerMt,
                h.PricePerMtUsd,
                h.ExchangeRateKesPerUsd));
        }

        StatusMessage = rows.Count == 0
            ? $"No price history for '{SelectedItem.Name}'."
            : $"Showing {rows.Count} history row(s) for '{SelectedItem.Name}'.";
    }
}

public sealed class RawIngredientHistoryRow
{
    public RawIngredientHistoryRow(DateTime changedAtLocal, decimal priceKes, decimal priceUsd, decimal rate)
    {
        ChangedAtLocal = changedAtLocal;
        PricePerMtKes = priceKes;
        PricePerMtUsd = priceUsd;
        ExchangeRateKesPerUsd = rate;
    }

    public DateTime ChangedAtLocal { get; }
    public decimal PricePerMtKes { get; }
    public decimal PricePerMtUsd { get; }
    public decimal ExchangeRateKesPerUsd { get; }
}


public partial class SpecParameterRow : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _unit = string.Empty;
}

public partial class SpecParametersViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;

    public ObservableCollection<SpecParameterRow> Items { get; } = new();
    public ObservableCollection<SpecParameterRow> FilteredItems { get; } = new();
    public IColumnFilterHost FilterHost { get; }

    [ObservableProperty] private SpecParameterRow? _selectedItem;
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newUnit = "%";
    [ObservableProperty] private string _statusMessage = string.Empty;

    public SpecParametersViewModel(IDbContextFactory<CostWiseDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        FilterHost = new ColumnFilterController<SpecParameterRow>(
            () => Items,
            list =>
            {
                FilteredItems.Clear();
                foreach (var item in list)
                    FilteredItems.Add(item);
            },
            new Dictionary<string, Func<SpecParameterRow, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = r => r.Name,
                ["Unit"] = r => r.Unit
            });
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        Items.Clear();
        foreach (var item in await db.SpecParameters.OrderBy(x => x.Name).ToListAsync())
            Items.Add(new SpecParameterRow { Id = item.Id, Name = item.Name, Unit = item.Unit });
        ((ColumnFilterController<SpecParameterRow>)FilterHost).Apply();
    }

    [RelayCommand]
    private void ResetAllFilters() => FilterHost.ResetAll();

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.SpecParameters.Add(new SpecParameter { Name = NewName.Trim(), Unit = string.IsNullOrWhiteSpace(NewUnit) ? "%" : NewUnit.Trim() });
        await db.SaveChangesAsync();
        NewName = string.Empty;
        StatusMessage = "Nutrient added.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Items.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var byId = await db.SpecParameters.ToDictionaryAsync(x => x.Id);

        foreach (var row in Items)
        {
            if (!byId.TryGetValue(row.Id, out var entity))
                continue;

            if (string.IsNullOrWhiteSpace(row.Name))
            {
                StatusMessage = "Every nutrient needs a name.";
                return;
            }

            entity.Name = row.Name.Trim();
            entity.Unit = string.IsNullOrWhiteSpace(row.Unit) ? "%" : row.Unit.Trim();
        }

        try
        {
            await db.SaveChangesAsync();
            StatusMessage = $"Saved {Items.Count} nutrients.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Save failed. Names must be unique.";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync()
    {
        if (SelectedItem is null) return;
        if (MessageBox.Show($"Remove '{SelectedItem.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.SpecParameters.FindAsync(SelectedItem.Id);
        if (entity is null) return;
        try
        {
            db.SpecParameters.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Removed.";
            await LoadAsync();
        }
        catch
        {
            StatusMessage = "Cannot remove: nutrient is used in formulations.";
        }
    }
}
