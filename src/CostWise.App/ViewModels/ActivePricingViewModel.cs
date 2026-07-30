using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.Core.Entities;
using CostWise.Core.Enums;
using CostWise.Core.Services;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.ViewModels;

public partial class ActivePriceRow : ObservableObject
{
    [ObservableProperty] private int _formulationId;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _subCategoryName = string.Empty;
    [ObservableProperty] private decimal _rmCost;
    [ObservableProperty] private decimal _conversionCost;
    [ObservableProperty] private decimal _totalCost;
    [ObservableProperty] private decimal _sellingPrice;
}

public sealed record PriceUnitOption(PriceUnit Value, string Label);

public partial class ActivePricingViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private List<Formulation> _activeFormulations = new();
    private bool _suppressOptionSync;

    public ObservableCollection<PriceBook> Books { get; } = new();
    public ObservableCollection<ActivePriceRow> Rows { get; } = new();
    public ObservableCollection<PricingCostOptionChoice> PackingChoices { get; } = new();
    public ObservableCollection<PricingCostOptionChoice> DocumentChoices { get; } = new();
    public ObservableCollection<PricingCostOptionChoice> AdditiveChoices { get; } = new();
    public IReadOnlyList<PriceUnitOption> PriceUnitOptions { get; } =
    [
        new(PriceUnit.PerMt, "Per MT"),
        new(PriceUnit.PerBag25Kg, "Per bag (25 kg)")
    ];

    [ObservableProperty] private PriceBook? _selectedBook;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private PricingCostOptionChoice? _selectedPackingChoice;
    [ObservableProperty] private PricingCostOptionChoice? _selectedDocumentChoice;
    [ObservableProperty] private PricingCostOptionChoice? _selectedAdditiveChoice;
    [ObservableProperty] private decimal _editPackingCost;
    [ObservableProperty] private decimal _editTransportationCost;
    [ObservableProperty] private decimal _editSpecialAdditiveCost;
    [ObservableProperty] private decimal _editExportDocCost;
    [ObservableProperty] private decimal _editMarginPercent = 10m;
    [ObservableProperty] private PriceUnit _editPriceUnit = PriceUnit.PerMt;
    [ObservableProperty] private PriceUnitOption? _selectedPriceUnitOption;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _unitLabel = "MT";

    public ActivePricingViewModel(IDbContextFactory<CostWiseDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
        SelectedPriceUnitOption = PriceUnitOptions[0];
        _ = LoadAsync();
    }

    partial void OnSelectedPriceUnitOptionChanged(PriceUnitOption? value)
    {
        if (value is null) return;
        EditPriceUnit = value.Value;
    }

    partial void OnSelectedPackingChoiceChanged(PricingCostOptionChoice? value)
    {
        if (_suppressOptionSync || value is null) return;
        EditPackingCost = value.Cost;
    }

    partial void OnSelectedDocumentChoiceChanged(PricingCostOptionChoice? value)
    {
        if (_suppressOptionSync || value is null) return;
        EditExportDocCost = value.Cost;
    }

    partial void OnSelectedAdditiveChoiceChanged(PricingCostOptionChoice? value)
    {
        if (_suppressOptionSync || value is null) return;
        EditSpecialAdditiveCost = value.Cost;
    }

    partial void OnSelectedBookChanged(PriceBook? value)
    {
        HasSelection = value is not null;
        if (value is null)
        {
            EditName = string.Empty;
            ApplyOptionSelection(null, null, null, 0, 0, 0);
            EditTransportationCost = 0;
            EditMarginPercent = 10m;
            EditPriceUnit = PriceUnit.PerMt;
            SelectedPriceUnitOption = PriceUnitOptions[0];
            Rows.Clear();
            return;
        }

        EditName = value.Name;
        ApplyOptionSelection(
            value.PackingOptionId,
            value.ExportDocOptionId,
            value.AdditiveOptionId,
            value.PackingCost,
            value.ExportDocCost,
            value.SpecialAdditiveCost);
        EditTransportationCost = value.TransportationCost;
        EditMarginPercent = value.MarginPercent;
        EditPriceUnit = value.PriceUnit;
        SelectedPriceUnitOption = PriceUnitOptions.First(o => o.Value == value.PriceUnit);
        RecalculateRows();
    }

    partial void OnEditPackingCostChanged(decimal value) => RecalculateRows();
    partial void OnEditTransportationCostChanged(decimal value) => RecalculateRows();
    partial void OnEditSpecialAdditiveCostChanged(decimal value) => RecalculateRows();
    partial void OnEditExportDocCostChanged(decimal value) => RecalculateRows();
    partial void OnEditMarginPercentChanged(decimal value) => RecalculateRows();
    partial void OnEditPriceUnitChanged(PriceUnit value)
    {
        UnitLabel = value == PriceUnit.PerBag25Kg ? "bag (25 kg)" : "MT";
        RecalculateRows();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        _activeFormulations = await db.Formulations
            .AsNoTracking()
            .Where(f => f.IsActive)
            .Include(f => f.Size)
            .Include(f => f.Category)
            .Include(f => f.SubCategory)
            .Include(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
            .OrderBy(f => f.Code)
            .ToListAsync();

        await LoadOptionChoicesAsync(db);

        var selectedId = SelectedBook?.Id;
        Books.Clear();
        foreach (var book in await db.PriceBooks.OrderBy(b => b.Name).ToListAsync())
            Books.Add(book);

        SelectedBook = selectedId is int id
            ? Books.FirstOrDefault(b => b.Id == id) ?? Books.FirstOrDefault()
            : Books.FirstOrDefault();

        StatusMessage = $"{Books.Count} price book(s); {_activeFormulations.Count} active formulation(s).";
    }

    private async Task LoadOptionChoicesAsync(CostWiseDbContext db)
    {
        var options = await db.PricingCostOptions
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        FillChoices(PackingChoices, options, PricingCostKind.Packing);
        FillChoices(DocumentChoices, options, PricingCostKind.Documents);
        FillChoices(AdditiveChoices, options, PricingCostKind.Additive);
    }

    private static void FillChoices(
        ObservableCollection<PricingCostOptionChoice> target,
        IEnumerable<PricingCostOption> options,
        PricingCostKind kind)
    {
        target.Clear();
        target.Add(PricingCostOptionChoice.None);
        foreach (var option in options.Where(x => x.Kind == kind))
            target.Add(PricingCostOptionChoice.From(option));
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
        SelectedPackingChoice = FindChoice(PackingChoices, packingId);
        SelectedDocumentChoice = FindChoice(DocumentChoices, documentId);
        SelectedAdditiveChoice = FindChoice(AdditiveChoices, additiveId);
        EditPackingCost = packingCost;
        EditExportDocCost = documentCost;
        EditSpecialAdditiveCost = additiveCost;
        _suppressOptionSync = false;
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

        var book = new PriceBook
        {
            Name = name,
            MarginPercent = 10m,
            PriceUnit = PriceUnit.PerMt,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.PriceBooks.Add(book);
        await db.SaveChangesAsync();
        StatusMessage = $"Created '{book.Name}'.";
        await LoadAsync();
        SelectedBook = Books.FirstOrDefault(b => b.Id == book.Id);
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
        entity.PackingCost = EditPackingCost;
        entity.TransportationCost = EditTransportationCost;
        entity.SpecialAdditiveCost = EditSpecialAdditiveCost;
        entity.ExportDocCost = EditExportDocCost;
        entity.MarginPercent = EditMarginPercent;
        entity.PriceUnit = EditPriceUnit;
        entity.UpdatedAtUtc = DateTime.UtcNow;
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

    private void RecalculateRows()
    {
        Rows.Clear();
        if (!HasSelection)
            return;

        foreach (var f in _activeFormulations)
        {
            var ingredients = f.Ingredients
                .Select(i => new IngredientCostLine(i.InclusionPercent, i.RawIngredient.PricePerMt))
                .ToList();
            var result = CostingCalculator.CalculateForPriceBook(
                ingredients,
                f.Size.ConversionCost,
                EditPackingCost,
                EditExportDocCost,
                EditSpecialAdditiveCost,
                EditTransportationCost,
                EditMarginPercent);

            Rows.Add(new ActivePriceRow
            {
                FormulationId = f.Id,
                Code = f.Code,
                CategoryName = f.Category.Name,
                SubCategoryName = f.SubCategory.Name,
                RmCost = CostingCalculator.ApplyUnit(result.RmCost, EditPriceUnit),
                ConversionCost = CostingCalculator.ApplyUnit(result.ConversionCost, EditPriceUnit),
                TotalCost = CostingCalculator.ApplyUnit(result.TotalCost, EditPriceUnit),
                SellingPrice = CostingCalculator.ApplyUnit(result.SellingPrice, EditPriceUnit)
            });
        }
    }
}
