using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Controls;
using CostWise.App.Services;
using CostWise.App.Views;
using CostWise.Core.Entities;
using CostWise.Core.Enums;
using CostWise.Core.Services;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace CostWise.App.ViewModels;
public partial class ActivePriceRow : ObservableObject
{
    public int FormulationId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string FeedTypeName { get; init; } = string.Empty;
    public string SubCategoryName { get; init; } = string.Empty;
    public bool IsInProduction { get; init; }
    public string ProductionLabel => IsInProduction ? "In Production" : "Not in Production";
    /// <summary>Total cost per MT.</summary>
    public decimal TotalCostMt { get; set; }
    [ObservableProperty] private decimal _rmCost;
    [ObservableProperty] private decimal _conversionCost;
    [ObservableProperty] private decimal _packingCost;
    [ObservableProperty] private decimal _additiveCost;
    [ObservableProperty] private decimal _exportDocCost;
    [ObservableProperty] private decimal _totalCost;
    [ObservableProperty] private decimal _calculatedSellMt;
    [ObservableProperty] private decimal _calculatedSellBag;
    [ObservableProperty] private decimal? _sellMt;
    [ObservableProperty] private decimal? _sellBag;
    [ObservableProperty] private string _overrideSellMtText = string.Empty;
    [ObservableProperty] private string _overrideSellBagText = string.Empty;
    [ObservableProperty] private decimal? _grossMarginPercent;
}
public sealed record PriceUnitOption(PriceUnit Value, string Label);
public partial class ActivePricingViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private List<Formulation> _bookFormulations = new();
    private Dictionary<int, (decimal? Mt, decimal? Bag)> _overrides = new();
    private bool _suppressOptionSync;
    private bool _suppressOverrideSync;
    private bool _suppressMarginClear;
    private bool _suppressCurrencySync;
    private List<Currency> _currencyLookup = new();
    public ObservableCollection<PriceBook> Books { get; } = new();
    public ObservableCollection<ActivePriceRow> Rows { get; } = new();
    public ObservableCollection<ActivePriceRow> FilteredRows { get; } = new();
    public ObservableCollection<Currency> Currencies { get; } = new();
    public IColumnFilterHost FilterHost { get; }
    public bool CanReorderRows => !FilterHost.HasActiveFilters;
    public ObservableCollection<PricingCostOptionChoice> PackingChoices { get; } = new();
    public ObservableCollection<PricingCostOptionChoice> DocumentChoices { get; } = new();
    public ObservableCollection<PricingCostOptionChoice> AdditiveChoices { get; } = new();
    [ObservableProperty] private PriceBook? _selectedBook;
    [ObservableProperty] private Currency? _selectedDisplayCurrency;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private PricingCostOptionChoice? _selectedPackingChoice;
    [ObservableProperty] private PricingCostOptionChoice? _selectedDocumentChoice;
    [ObservableProperty] private PricingCostOptionChoice? _selectedAdditiveChoice;
    [ObservableProperty] private decimal _editPackingCost;
    [ObservableProperty] private decimal _editTransportationCost;
    [ObservableProperty] private decimal _editSpecialAdditiveCost;
    [ObservableProperty] private decimal _editExportDocCost;
    [ObservableProperty] private decimal _editMarginPercent = 10m;
    [ObservableProperty] private decimal _editRoundMtTo;
    [ObservableProperty] private decimal _editRoundBagTo;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _hasFormulas;
    [ObservableProperty] private bool _hasPacking;
    [ObservableProperty] private string _selectionCountText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public string CostsHeaderText =>
        $"Formulas in book — costs per MT ({SelectedDisplayCurrency?.Code ?? "KES"})";

    private decimal KesPerUnitRate =>
        Math.Max(SelectedDisplayCurrency?.KesPerUnit ?? 1m, 0.000001m);

    private decimal ConvertToDisplay(decimal kes) => kes / KesPerUnitRate;
    private decimal ConvertToKes(decimal display) => display * KesPerUnitRate;

    public ActivePricingViewModel(IDbContextFactory<CostWiseDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        FilterHost = new ColumnFilterController<ActivePriceRow>(
            () => Rows,
            list =>
            {
                FilteredRows.Clear();
                foreach (var item in list)
                    FilteredRows.Add(item);
            },
            new Dictionary<string, Func<ActivePriceRow, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Code"] = r => r.Code,
                ["Production"] = r => r.ProductionLabel,
                ["Category"] = r => r.CategoryName,
                ["FeedType"] = r => r.FeedTypeName,
                ["Version"] = r => r.SubCategoryName,
                ["Raw Material"] = r => r.RmCost.ToString("0.##"),
                ["Conversion"] = r => r.ConversionCost.ToString("0.##"),
                ["Packing cost"] = r => r.PackingCost.ToString("0.##"),
                ["Additive"] = r => r.AdditiveCost.ToString("0.##"),
                ["Export Doc"] = r => r.ExportDocCost.ToString("0.##"),
                ["Total cost"] = r => r.TotalCost.ToString("0.##"),
                ["Sell / MT"] = r => r.SellMt?.ToString("0.##") ?? string.Empty,
                ["Manual MT"] = r => r.OverrideSellMtText,
                ["Sell / bag"] = r => r.SellBag?.ToString("0.##") ?? string.Empty,
                ["Manual bag"] = r => r.OverrideSellBagText,
                ["Gross margin %"] = r => r.GrossMarginPercent?.ToString("0.##") ?? string.Empty
            },
            new Dictionary<string, Func<ActivePriceRow, IComparable?>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Code"] = r => r.Code,
                ["Production"] = r => r.ProductionLabel,
                ["Category"] = r => r.CategoryName,
                ["FeedType"] = r => r.FeedTypeName,
                ["Version"] = r => r.SubCategoryName,
                ["Raw Material"] = r => r.RmCost,
                ["Conversion"] = r => r.ConversionCost,
                ["Packing cost"] = r => r.PackingCost,
                ["Additive"] = r => r.AdditiveCost,
                ["Export Doc"] = r => r.ExportDocCost,
                ["Total cost"] = r => r.TotalCost,
                ["Sell / MT"] = r => r.SellMt,
                ["Manual MT"] = r => ParseSortDecimal(r.OverrideSellMtText),
                ["Sell / bag"] = r => r.SellBag,
                ["Manual bag"] = r => ParseSortDecimal(r.OverrideSellBagText),
                ["Gross margin %"] = r => r.GrossMarginPercent
            });
        FilterHost.FiltersChanged += (_, _) => OnPropertyChanged(nameof(CanReorderRows));
        _ = LoadAsync();
    }

    private static decimal? ParseSortDecimal(string? text) =>
        decimal.TryParse(text?.Trim(), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.CurrentCulture, out var value)
            ? value
            : null;
    partial void OnSelectedPackingChoiceChanged(PricingCostOptionChoice? value)
    {
        var packing = value?.Id is not null;
        if (_suppressOptionSync)
        {
            HasPacking = packing;
            return;
        }
        if (value is null) return;
        var wasPacking = HasPacking;
        HasPacking = packing;
        if (EditPackingCost != ConvertToDisplay(value.Cost))
            EditPackingCost = ConvertToDisplay(value.Cost);
        else if (wasPacking != HasPacking)
            RecalculateRows();
    }
    partial void OnSelectedDocumentChoiceChanged(PricingCostOptionChoice? value)
    {
        if (_suppressOptionSync || value is null) return;
        EditExportDocCost = ConvertToDisplay(value.Cost);
    }
    partial void OnSelectedAdditiveChoiceChanged(PricingCostOptionChoice? value)
    {
        if (_suppressOptionSync || value is null) return;
        EditSpecialAdditiveCost = ConvertToDisplay(value.Cost);
    }
    partial void OnSelectedDisplayCurrencyChanged(Currency? value)
    {
        if (_suppressCurrencySync) return;

        if (SelectedBook is not null)
            SelectedBook.DisplayCurrencyId = value?.Id;

        if (SelectedBook is not null && HasSelection)
        {
            ApplyBookCostsForDisplay();
            RecalculateRows();
        }

        OnPropertyChanged(nameof(CostsHeaderText));
    }
    partial void OnSelectedBookChanged(PriceBook? value)
    {
        HasSelection = value is not null;
        if (value is null)
        {
            EditName = string.Empty;
            ApplyOptionSelection(null, null, null, 0, 0, 0);
            EditTransportationCost = 0;
            _suppressMarginClear = true;
            EditMarginPercent = 10m;
            _suppressMarginClear = false;
            EditRoundMtTo = 0;
            EditRoundBagTo = 0;
            _suppressCurrencySync = true;
            SelectedDisplayCurrency = null;
            _suppressCurrencySync = false;
            _bookFormulations = new();
            _overrides = new();
            Rows.Clear();
            HasFormulas = false;
            HasPacking = false;
            SelectionCountText = string.Empty;
            ((ColumnFilterController<ActivePriceRow>)FilterHost).Apply();
            return;
        }

        EditName = value.Name;
        _suppressMarginClear = true;
        EditMarginPercent = value.MarginPercent;
        _suppressMarginClear = false;
        EditRoundMtTo = value.RoundMtTo;
        EditRoundBagTo = value.RoundBagTo;
        _suppressCurrencySync = true;
        SelectedDisplayCurrency = ResolveDisplayCurrency(value.DisplayCurrencyId);
        _suppressCurrencySync = false;
        ApplyBookCostsForDisplay();
        _ = LoadBookFormulationsAsync(value.Id);
    }

    private void ApplyBookCostsForDisplay()
    {
        if (SelectedBook is null) return;

        ApplyOptionSelection(
            SelectedBook.PackingOptionId,
            SelectedBook.ExportDocOptionId,
            SelectedBook.AdditiveOptionId,
            SelectedBook.PackingCost,
            SelectedBook.ExportDocCost,
            SelectedBook.SpecialAdditiveCost);
        EditTransportationCost = ConvertToDisplay(SelectedBook.TransportationCost);
    }

    private Currency? ResolveDisplayCurrency(int? currencyId)
    {
        if (currencyId is int id)
        {
            var match = _currencyLookup.FirstOrDefault(c => c.Id == id);
            if (match is not null)
                return match;
        }

        return _currencyLookup.FirstOrDefault(c => c.IsBase)
               ?? _currencyLookup.FirstOrDefault(c => string.Equals(c.Code, "KES", StringComparison.OrdinalIgnoreCase));
    }

    partial void OnEditPackingCostChanged(decimal value) => RecalculateRows();
    partial void OnEditTransportationCostChanged(decimal value) => RecalculateRows();
    partial void OnEditSpecialAdditiveCostChanged(decimal value) => RecalculateRows();
    partial void OnEditExportDocCostChanged(decimal value) => RecalculateRows();
    partial void OnEditMarginPercentChanged(decimal value)
    {
        if (_suppressMarginClear)
        {
            RecalculateRows();
            return;
        }

        _ = ClearManualOverridesForMarginChangeAsync();
    }
    partial void OnEditRoundMtToChanged(decimal value) => RecalculateRows();
    partial void OnEditRoundBagToChanged(decimal value) => RecalculateRows();

    private async Task ClearManualOverridesForMarginChangeAsync()
    {
        foreach (var key in _overrides.Keys.ToList())
            _overrides[key] = (null, null);

        if (SelectedBook is not null)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                var members = await db.PriceBookFormulations
                    .Where(x => x.PriceBookId == SelectedBook.Id)
                    .ToListAsync();
                foreach (var member in members)
                {
                    member.OverrideSellPriceMt = null;
                    member.OverrideSellPriceBag = null;
                }

                var book = await db.PriceBooks.FindAsync(SelectedBook.Id);
                if (book is not null)
                {
                    book.MarginPercent = EditMarginPercent;
                    book.UpdatedAtUtc = DateTime.UtcNow;
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to clear manuals after margin change: {ex.Message}";
                RecalculateRows();
                return;
            }
        }

        // Do not re-import Manual MT/bag text from existing grid rows.
        _suppressOverrideSync = true;
        RecalculateRows();
        StatusMessage = "Margin changed — manual prices cleared; sells recalculated from margin.";
    }
    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await LoadOptionChoicesAsync(db);
        await LoadCurrenciesAsync(db);
        var selectedId = SelectedBook?.Id;
        Books.Clear();
        foreach (var book in await db.PriceBooks
                     .OrderBy(b => b.SortOrder)
                     .ThenBy(b => b.Name)
                     .ToListAsync())
            Books.Add(book);
        SelectedBook = selectedId is int id
            ? Books.FirstOrDefault(b => b.Id == id) ?? Books.FirstOrDefault()
            : Books.FirstOrDefault();
        StatusMessage = $"{Books.Count} price book(s).";
    }

    private async Task LoadCurrenciesAsync(CostWiseDbContext db)
    {
        _currencyLookup = await db.Currencies
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .ToListAsync();

        Currencies.Clear();
        foreach (var currency in _currencyLookup.Where(c => c.IsActive))
            Currencies.Add(currency);

        if (SelectedBook is not null)
        {
            _suppressCurrencySync = true;
            SelectedDisplayCurrency = ResolveDisplayCurrency(SelectedBook.DisplayCurrencyId);
            _suppressCurrencySync = false;
        }
    }

    private async Task LoadBookFormulationsAsync(int bookId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var members = await db.PriceBookFormulations
                .AsNoTracking()
                .Where(x => x.PriceBookId == bookId)
                .OrderBy(x => x.SortOrder)
                .Select(x => new
                {
                    x.FormulationId,
                    x.OverrideSellPriceMt,
                    x.OverrideSellPriceBag
                })
                .ToListAsync();
            _overrides = members.ToDictionary(
                x => x.FormulationId,
                x => (x.OverrideSellPriceMt, x.OverrideSellPriceBag));
            NormalizeLinkedOverrides();
            var memberIds = members.Select(x => x.FormulationId).ToList();
            if (memberIds.Count == 0)
            {
                _bookFormulations = new();
                RecalculateRows();
                SelectionCountText = "No formulas in book - click Select formulas";
                StatusMessage = HasPacking
                    ? $"{SelectedBook?.Name}: 0 formula(s)."
                    : "Packing required to save book.";
                return;
            }
            var formulas = await db.Formulations
                .AsNoTracking()
                .Where(f => memberIds.Contains(f.Id))
                .Include(f => f.Size)
                .Include(f => f.Category)
                .Include(f => f.SubCategory)
                .Include(f => f.FeedType)
                .Include(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
                .ToListAsync();
            var byId = formulas.ToDictionary(f => f.Id);
            _bookFormulations = memberIds
                .Where(id => byId.ContainsKey(id))
                .Select(id => byId[id])
                .ToList();
            RecalculateRows();
            SelectionCountText = $"{_bookFormulations.Count} formula(s) in book";
            StatusMessage = HasPacking
                ? $"{SelectedBook?.Name}: {_bookFormulations.Count} formula(s). Drag rows to reorder."
                : $"{SelectedBook?.Name}: {_bookFormulations.Count} formula(s). Packing required to save book.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load book formulas: {ex.Message}";
        }
    }
    private async Task LoadOptionChoicesAsync(CostWiseDbContext db)
    {
        var options = await db.PricingCostOptions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();
        FillChoices(PackingChoices, options, PricingCostKind.Packing, includeNone: false);
        FillChoices(DocumentChoices, options, PricingCostKind.Documents, includeNone: true);
        FillChoices(AdditiveChoices, options, PricingCostKind.Additive, includeNone: true);
    }

    private static void FillChoices(
        ObservableCollection<PricingCostOptionChoice> target,
        IEnumerable<PricingCostOption> options,
        PricingCostKind kind,
        bool includeNone)
    {
        target.Clear();
        if (includeNone)
            target.Add(PricingCostOptionChoice.None);
        foreach (var option in options.Where(x => x.Kind == kind))
            target.Add(PricingCostOptionChoice.From(option));
    }

    private static PricingCostOptionChoice? DefaultPackingChoice(
        ObservableCollection<PricingCostOptionChoice> packingChoices)
    {
        var samakgro = packingChoices.FirstOrDefault(c =>
            c.Id is not null &&
            c.Label.Contains("Samakgro", StringComparison.OrdinalIgnoreCase));
        return samakgro ?? packingChoices.FirstOrDefault(c => c.Id is not null);
    }

    private void ApplyOptionSelection(
        int? packingId,
        int? documentId,
        int? additiveId,
        decimal packingCost,
        decimal documentCost,
        decimal additiveCost)
    {
        _suppressOptionSync = true;
        SelectedPackingChoice = FindPackingChoice(packingId);
        SelectedDocumentChoice = FindChoice(DocumentChoices, documentId);
        SelectedAdditiveChoice = FindChoice(AdditiveChoices, additiveId);
        EditPackingCost = ConvertToDisplay(SelectedPackingChoice?.Cost
                                             ?? (packingId is not null ? packingCost : 0m));
        EditExportDocCost = ConvertToDisplay(
            SelectedDocumentChoice?.Id is not null
                ? SelectedDocumentChoice.Cost
                : documentId is not null ? documentCost : 0m);
        EditSpecialAdditiveCost = ConvertToDisplay(
            SelectedAdditiveChoice?.Id is not null
                ? SelectedAdditiveChoice.Cost
                : additiveId is not null ? additiveCost : 0m);
        HasPacking = SelectedPackingChoice?.Id is not null;
        _suppressOptionSync = false;
    }

    private PricingCostOptionChoice FindPackingChoice(int? optionId)
    {
        if (optionId is int id)
        {
            var match = PackingChoices.FirstOrDefault(c => c.Id == id);
            if (match is not null)
                return match;
        }

        return DefaultPackingChoice(PackingChoices) ?? PricingCostOptionChoice.None;
    }

    private static PricingCostOptionChoice FindChoice(
        ObservableCollection<PricingCostOptionChoice> choices,
        int? optionId)
    {
        if (optionId is int id)
        {
            var match = choices.FirstOrDefault(c => c.Id == id);
            if (match is not null)
                return match;
        }

        return choices.FirstOrDefault() ?? PricingCostOptionChoice.None;
    }
    [RelayCommand]
    private async Task NewBookAsync()
    {
        var name = $"Price book {Books.Count + 1}";
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.PriceBooks.Select(b => b.Name).ToListAsync();
        var baseName = name;
        var n = 1;
        while (existing.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
        {
            n++;
            name = $"{baseName} ({n})";
        }
        var defaultPacking = DefaultPackingChoice(PackingChoices);
        var kesCurrencyId = _currencyLookup.FirstOrDefault(c => c.IsBase)?.Id
                            ?? _currencyLookup.FirstOrDefault(c =>
                                string.Equals(c.Code, "KES", StringComparison.OrdinalIgnoreCase))?.Id
                            ?? await db.Currencies.Where(c => c.IsBase).Select(c => (int?)c.Id).FirstOrDefaultAsync()
                            ?? await db.Currencies.Where(c => c.Code == "KES").Select(c => (int?)c.Id).FirstOrDefaultAsync();
        var maxOrder = await db.PriceBooks.Select(b => (int?)b.SortOrder).MaxAsync() ?? -1;
        var book = new PriceBook
        {
            Name = name,
            MarginPercent = 10m,
            PriceUnit = PriceUnit.PerMt,
            SortOrder = maxOrder + 1,
            PackingOptionId = defaultPacking?.Id,
            PackingCost = defaultPacking?.Cost ?? 0m,
            DisplayCurrencyId = kesCurrencyId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.PriceBooks.Add(book);
        await db.SaveChangesAsync();
        StatusMessage = $"Created '{book.Name}' (no formulas yet).";
        await LoadAsync();
        SelectedBook = Books.FirstOrDefault(b => b.Id == book.Id);
    }
    [RelayCommand]
    private async Task OpenFormulaPickerAsync()
    {
        if (SelectedBook is null)
        {
            StatusMessage = "Select or create a price book first.";
            return;
        }
        List<FormulaPickerRow> rows;
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var memberIds = (await db.PriceBookFormulations
                    .Where(x => x.PriceBookId == SelectedBook.Id)
                    .Select(x => x.FormulationId)
                    .ToListAsync())
                .ToHashSet();
            var formulations = (await db.Formulations
                    .AsNoTracking()
                    .Include(f => f.Category)
                    .Include(f => f.SubCategory)
                    .Include(f => f.Size)
                    .Include(f => f.FeedType)
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
                Revision = f.Revision,
                SizeName = f.Size.Name,
                FeedTypeName = f.FeedType.Name,
                IsInProduction = f.IsActive,
                IsSelected = memberIds.Contains(f.Id)
            }).ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not load formulations:\n{ex.Message}", "Select formulas",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var pickerVm = new FormulaPickerViewModel(SelectedBook.Name, rows, "In book");
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
            var existing = await db.PriceBookFormulations
                .Where(x => x.PriceBookId == SelectedBook.Id)
                .ToListAsync();
            var previousOverrides = existing.ToDictionary(
                x => x.FormulationId,
                x => (x.OverrideSellPriceMt, x.OverrideSellPriceBag));
            db.PriceBookFormulations.RemoveRange(existing);
            var order = 0;
            foreach (var fid in selectedIds)
            {
                previousOverrides.TryGetValue(fid, out var ov);
                db.PriceBookFormulations.Add(new PriceBookFormulation
                {
                    PriceBookId = SelectedBook.Id,
                    FormulationId = fid,
                    SortOrder = order++,
                    OverrideSellPriceMt = ov.OverrideSellPriceMt,
                    OverrideSellPriceBag = ov.OverrideSellPriceBag
                });
            }
            var book = await db.PriceBooks.FindAsync(SelectedBook.Id);
            if (book is not null)
                book.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        StatusMessage = $"Book '{SelectedBook.Name}' now has {selectedIds.Count} formula(s).";
        await LoadBookFormulationsAsync(SelectedBook.Id);
    }
    [RelayCommand]
    private async Task ReorderBookFormulasAsync(IList<int>? orderedIds)
    {
        if (SelectedBook is null || orderedIds is null || orderedIds.Count == 0)
            return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var members = await db.PriceBookFormulations
            .Where(x => x.PriceBookId == SelectedBook.Id)
            .ToListAsync();
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var entity = members.FirstOrDefault(m => m.FormulationId == orderedIds[i]);
            if (entity is null) continue;
            entity.SortOrder = i;
        }
        var book = await db.PriceBooks.FindAsync(SelectedBook.Id);
        if (book is not null)
            book.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var byId = _bookFormulations.ToDictionary(f => f.Id);
        _bookFormulations = orderedIds
            .Where(id => byId.ContainsKey(id))
            .Select(id => byId[id])
            .ToList();
        RecalculateRows();
        StatusMessage = "Formula order saved.";
    }
    [RelayCommand]
    private async Task SaveBookAsync()
    {
        if (SelectedBook is null)
        {
            StatusMessage = "Select or create a price book.";
            return;
        }
        if (string.IsNullOrWhiteSpace(EditName))
        {
            StatusMessage = "Book name is required.";
            return;
        }
        if (!HasPacking)
        {
            StatusMessage = "Packing is required before saving a price book.";
            return;
        }
        SyncOverridesFromRows();
        var name = EditName.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync();
        var duplicate = await db.PriceBooks.AnyAsync(b =>
            b.Id != SelectedBook.Id && b.Name.ToLower() == name.ToLower());
        if (duplicate)
        {
            StatusMessage = $"A price book named '{name}' already exists.";
            return;
        }
        var entity = await db.PriceBooks.FindAsync(SelectedBook.Id);
        if (entity is null)
        {
            StatusMessage = "Price book not found.";
            return;
        }
        entity.Name = name;
        entity.PackingOptionId = SelectedPackingChoice?.Id;
        entity.ExportDocOptionId = SelectedDocumentChoice?.Id;
        entity.AdditiveOptionId = SelectedAdditiveChoice?.Id;
        entity.PackingCost = ConvertToKes(EditPackingCost);
        entity.TransportationCost = ConvertToKes(EditTransportationCost);
        entity.SpecialAdditiveCost = ConvertToKes(EditSpecialAdditiveCost);
        entity.ExportDocCost = ConvertToKes(EditExportDocCost);
        entity.DisplayCurrencyId = SelectedDisplayCurrency?.Id;
        entity.MarginPercent = EditMarginPercent;
        entity.RoundMtTo = EditRoundMtTo;
        entity.RoundBagTo = EditRoundBagTo;
        entity.PriceUnit = PriceUnit.PerMt;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        var members = await db.PriceBookFormulations
            .Where(x => x.PriceBookId == entity.Id)
            .ToListAsync();
        foreach (var member in members)
        {
            if (_overrides.TryGetValue(member.FormulationId, out var ov))
            {
                member.OverrideSellPriceMt = ov.Mt;
                member.OverrideSellPriceBag = ov.Bag;
            }
            else
            {
                member.OverrideSellPriceMt = null;
                member.OverrideSellPriceBag = null;
            }
        }
        await db.SaveChangesAsync();
        StatusMessage = $"Saved '{entity.Name}'.";
        await LoadAsync();
        SelectedBook = Books.FirstOrDefault(b => b.Id == entity.Id);
    }
    [RelayCommand]
    private async Task DeleteBookAsync()
    {
        if (SelectedBook is null)
        {
            StatusMessage = "Select a price book to delete.";
            return;
        }
        var confirm = MessageBox.Show(
            $"Delete price book '{SelectedBook.Name}'?",
            "Delete price book",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entity = await db.PriceBooks.FindAsync(SelectedBook.Id);
        if (entity is not null)
        {
            db.PriceBooks.Remove(entity);
            await db.SaveChangesAsync();
        }
        StatusMessage = "Price book deleted.";
        SelectedBook = null;
        await LoadAsync();
    }
    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();
    /// <summary>
    /// Persist a manual sell override. <paramref name="fromMt"/> true = Manual MT edited;
    /// false = Manual bag edited. Clears both when the edited field is blank.
    /// </summary>
    public async Task PersistManualOverrideAsync(ActivePriceRow row, bool fromMt)
    {
        if (SelectedBook is null)
            return;
        ApplyLinkedOverride(row, fromMt);
        await using var db = await _dbFactory.CreateDbContextAsync();
        var member = await db.PriceBookFormulations
            .FirstOrDefaultAsync(x => x.PriceBookId == SelectedBook.Id && x.FormulationId == row.FormulationId);
        if (member is null)
            return;
        _overrides.TryGetValue(row.FormulationId, out var ov);
        member.OverrideSellPriceMt = ov.Mt;
        member.OverrideSellPriceBag = ov.Bag;
        var book = await db.PriceBooks.FindAsync(SelectedBook.Id);
        if (book is not null)
            book.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        ApplyFinalPrices(row);
        StatusMessage = ov.Mt is null
            ? $"Cleared override for {row.Code}."
            : $"Override saved for {row.Code}.";
    }
    private void NormalizeLinkedOverrides()
    {
        foreach (var id in _overrides.Keys.ToList())
        {
            var (mt, bag) = _overrides[id];
            if (mt is not null && bag is null)
                ApplyLinkedFromMt(id, mt.Value);
            else if (bag is not null && mt is null)
                ApplyLinkedFromBag(id, bag.Value);
        }
    }
    private void SyncOverridesFromRows()
    {
        foreach (var row in Rows)
            SyncOverrideFromRowLinked(row);
    }
    /// <summary>When syncing without a known edit source, keep linked pairs or link from whichever side is set.</summary>
    private void SyncOverrideFromRowLinked(ActivePriceRow row)
    {
        var displayMt = ParseOptionalDecimal(row.OverrideSellMtText);
        var displayBag = ParseOptionalDecimal(row.OverrideSellBagText);
        if (displayMt is null && displayBag is null)
        {
            _overrides[row.FormulationId] = (null, null);
            return;
        }

        if (displayMt is not null && displayBag is null)
        {
            ApplyLinkedFromMt(row.FormulationId, ConvertToKes(displayMt.Value));
            return;
        }

        if (displayBag is not null && displayMt is null)
        {
            ApplyLinkedFromBag(row.FormulationId, ConvertToKes(displayBag.Value));
            return;
        }

        _overrides[row.FormulationId] = (ConvertToKes(displayMt!.Value), ConvertToKes(displayBag!.Value));
    }
    private void ApplyLinkedOverride(ActivePriceRow row, bool fromMt)
    {
        if (fromMt)
        {
            var displayMt = ParseOptionalDecimal(row.OverrideSellMtText);
            if (displayMt is null)
            {
                _overrides[row.FormulationId] = (null, null);
                row.OverrideSellMtText = string.Empty;
                row.OverrideSellBagText = string.Empty;
                return;
            }

            ApplyLinkedFromMt(row.FormulationId, ConvertToKes(displayMt.Value));
        }
        else
        {
            var displayBag = ParseOptionalDecimal(row.OverrideSellBagText);
            if (displayBag is null)
            {
                _overrides[row.FormulationId] = (null, null);
                row.OverrideSellMtText = string.Empty;
                row.OverrideSellBagText = string.Empty;
                return;
            }

            ApplyLinkedFromBag(row.FormulationId, ConvertToKes(displayBag.Value));
        }
        _overrides.TryGetValue(row.FormulationId, out var ov);
        _suppressOverrideSync = true;
        row.OverrideSellMtText = FormatOverride(ov.Mt);
        row.OverrideSellBagText = FormatOverride(ov.Bag);
        _suppressOverrideSync = false;
    }
    private void ApplyLinkedFromMt(int formulationId, decimal mtKes)
    {
        // Typed MT stays as-is; derived bag rounds in display currency.
        var displayBag = CostingCalculator.RoundToIncrement(
            CostingCalculator.ToBag(ConvertToDisplay(mtKes)), EditRoundBagTo);
        _overrides[formulationId] = (mtKes, ConvertToKes(displayBag));
    }

    private void ApplyLinkedFromBag(int formulationId, decimal bagKes)
    {
        // Typed bag stays as-is; derived MT rounds in display currency.
        var displayMt = CostingCalculator.RoundToIncrement(
            CostingCalculator.FromBag(ConvertToDisplay(bagKes)), EditRoundMtTo);
        _overrides[formulationId] = (ConvertToKes(displayMt), bagKes);
    }

    private void ApplyFinalPrices(ActivePriceRow row)
    {
        _overrides.TryGetValue(row.FormulationId, out var ov);
        var kesSellMt = ov.Mt ?? row.CalculatedSellMt;
        var kesSellBag = ov.Bag ?? row.CalculatedSellBag;
        row.SellMt = ConvertToDisplay(kesSellMt);
        row.SellBag = ConvertToDisplay(kesSellBag);
        row.OverrideSellMtText = FormatOverride(ov.Mt);
        row.OverrideSellBagText = FormatOverride(ov.Bag);
        row.GrossMarginPercent = CostingCalculator.GrossMarginPercent(kesSellMt, row.TotalCostMt);
    }

    private static decimal? ParseOptionalDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var value)
            ? value
            : null;
    }

    private bool IsUsdDisplay =>
        string.Equals(SelectedDisplayCurrency?.Code, "USD", StringComparison.OrdinalIgnoreCase);

    private string FormatOverride(decimal? kesValue)
    {
        if (kesValue is null) return string.Empty;
        var places = IsUsdDisplay
            ? AppPreferences.Current?.UsdDecimalPlaces ?? 2
            : AppPreferences.Current?.CostDecimalPlaces ?? 2;
        return ConvertToDisplay(kesValue.Value).ToString($"N{places}", CultureInfo.CurrentCulture);
    }

    private void RecalculateRows()
    {
        // Keep typed overrides if rows already exist.
        if (!_suppressOverrideSync && Rows.Count > 0)
            SyncOverridesFromRows();
        Rows.Clear();
        if (!HasSelection)
        {
            HasFormulas = false;
            ((ColumnFilterController<ActivePriceRow>)FilterHost).Apply();
            return;
        }
        _suppressOverrideSync = true;
        foreach (var f in _bookFormulations)
        {
            var ingredients = f.Ingredients
                .Select(i => new IngredientCostLine(i.InclusionPercent, i.RawIngredient.PricePerMt))
                .ToList();
            var result = CostingCalculator.CalculateForPriceBook(
                ingredients,
                f.Size.ConversionCost,
                ConvertToKes(EditPackingCost),
                ConvertToKes(EditExportDocCost),
                ConvertToKes(EditSpecialAdditiveCost),
                ConvertToKes(EditTransportationCost),
                EditMarginPercent);
            // Round in display currency so Round MT/bag match what the user sees.
            var calcDisplayMt = CostingCalculator.RoundToIncrement(
                ConvertToDisplay(result.SellingPrice), EditRoundMtTo);
            var calcDisplayBag = CostingCalculator.RoundToIncrement(
                ConvertToDisplay(CostingCalculator.ToBag(result.SellingPrice)), EditRoundBagTo);
            var calcMt = ConvertToKes(calcDisplayMt);
            var calcBag = ConvertToKes(calcDisplayBag);
            _overrides.TryGetValue(f.Id, out var ov);
            var kesSellMt = ov.Mt ?? calcMt;
            var kesSellBag = ov.Bag ?? calcBag;
            Rows.Add(new ActivePriceRow
            {
                FormulationId = f.Id,
                Code = f.Code,
                CategoryName = f.Category.Name,
                FeedTypeName = f.FeedType.Name,
                SubCategoryName = f.SubCategory.Name,
                IsInProduction = f.IsActive,
                TotalCostMt = result.TotalCost,
                RmCost = ConvertToDisplay(result.RmCost),
                ConversionCost = ConvertToDisplay(result.ConversionCost),
                PackingCost = ConvertToDisplay(result.PackingCost),
                AdditiveCost = ConvertToDisplay(result.SpecialAdditiveCost),
                ExportDocCost = ConvertToDisplay(result.ExportDocCost),
                TotalCost = ConvertToDisplay(result.TotalCost),
                CalculatedSellMt = calcMt,
                CalculatedSellBag = calcBag,
                SellMt = ov.Mt is not null ? ConvertToDisplay(ov.Mt.Value) : calcDisplayMt,
                SellBag = ov.Bag is not null ? ConvertToDisplay(ov.Bag.Value) : calcDisplayBag,
                OverrideSellMtText = FormatOverride(ov.Mt),
                OverrideSellBagText = FormatOverride(ov.Bag),
                GrossMarginPercent = CostingCalculator.GrossMarginPercent(kesSellMt, result.TotalCost)
            });
        }
        _suppressOverrideSync = false;
        HasFormulas = Rows.Count > 0;
        if (HasFormulas && !HasPacking)
            StatusMessage = "Packing required to save book.";
        ((ColumnFilterController<ActivePriceRow>)FilterHost).Apply();
    }

    [RelayCommand]
    private void ResetAllFilters() => FilterHost.ResetAll();

    [RelayCommand]
    private async Task ReorderBooksAsync(IList<int>? orderedIds)
    {
        if (orderedIds is null || orderedIds.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var books = await db.PriceBooks.ToListAsync();
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var entity = books.FirstOrDefault(b => b.Id == orderedIds[i]);
            if (entity is null) continue;
            entity.SortOrder = i;
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var selectedId = SelectedBook?.Id;
        Books.Clear();
        foreach (var id in orderedIds)
        {
            var book = books.FirstOrDefault(x => x.Id == id);
            if (book is not null)
                Books.Add(book);
        }

        SelectedBook = selectedId is int sid
            ? Books.FirstOrDefault(b => b.Id == sid) ?? Books.FirstOrDefault()
            : Books.FirstOrDefault();
        StatusMessage = "Price book order saved.";
    }
}
