using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CostWise.App.Services;
using CostWise.Core.Entities;
using CostWise.Core.Enums;
using CostWise.Core.Services;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.ViewModels;

public partial class CostingViewModel : ObservableObject
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;
    private readonly AppPreferences _preferences;
    private bool _suppressOptionSync;

    public ObservableCollection<Formulation> Formulations { get; } = new();
    public ObservableCollection<CostingScenario> SavedScenarios { get; } = new();
    public ObservableCollection<PricingCostOptionChoice> PackingChoices { get; } = new();
    public ObservableCollection<PricingCostOptionChoice> DocumentChoices { get; } = new();
    public ObservableCollection<PricingCostOptionChoice> AdditiveChoices { get; } = new();
    public Array Markets => Enum.GetValues(typeof(MarketType));
    public IReadOnlyList<PriceUnitOption> PriceUnitOptions { get; } =
    [
        new(PriceUnit.PerMt, "Per MT"),
        new(PriceUnit.PerBag25Kg, "Per bag (25 kg)")
    ];

    [ObservableProperty] private Formulation? _selectedFormulation;
    [ObservableProperty] private string _scenarioName = "Quote";
    [ObservableProperty] private MarketType _market = MarketType.Domestic;
    [ObservableProperty] private PricingCostOptionChoice? _selectedPackingChoice;
    [ObservableProperty] private PricingCostOptionChoice? _selectedDocumentChoice;
    [ObservableProperty] private PricingCostOptionChoice? _selectedAdditiveChoice;
    [ObservableProperty] private decimal _packingCost;
    [ObservableProperty] private decimal _exportDocCost;
    [ObservableProperty] private decimal _specialAdditiveCost;
    [ObservableProperty] private decimal _transportationCost;
    [ObservableProperty] private decimal _marginPercent = 10m;
    [ObservableProperty] private PriceUnit _priceUnit = PriceUnit.PerMt;
    [ObservableProperty] private PriceUnitOption? _selectedPriceUnitOption;
    [ObservableProperty] private decimal _rmCost;
    [ObservableProperty] private decimal _conversionCost;
    [ObservableProperty] private decimal _appliedExportDoc;
    [ObservableProperty] private decimal _totalCost;
    [ObservableProperty] private decimal _sellingPrice;
    [ObservableProperty] private decimal _displayRmCost;
    [ObservableProperty] private decimal _displayConversionCost;
    [ObservableProperty] private decimal _displayPackingCost;
    [ObservableProperty] private decimal _displayAppliedExportDoc;
    [ObservableProperty] private decimal _displayTotalCost;
    [ObservableProperty] private decimal _displaySellingPrice;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isExport;
    [ObservableProperty] private string _liveCostingTitle = "Live costing / MT";

    public CostingViewModel(IDbContextFactory<CostWiseDbContext> dbFactory, AppPreferences preferences)
    {
        _dbFactory = dbFactory;
        _preferences = preferences;
        SelectedPriceUnitOption = PriceUnitOptions[0];
        _preferences.Changed += (_, _) => RefreshMoneyDisplay();
        _ = LoadAsync();
    }

    private void RefreshMoneyDisplay()
    {
        Recalculate();
        OnPropertyChanged(nameof(DisplayRmCost));
        OnPropertyChanged(nameof(DisplayConversionCost));
        OnPropertyChanged(nameof(DisplayPackingCost));
        OnPropertyChanged(nameof(DisplayAppliedExportDoc));
        OnPropertyChanged(nameof(DisplayTotalCost));
        OnPropertyChanged(nameof(DisplaySellingPrice));
        var scenarios = SavedScenarios.ToList();
        SavedScenarios.Clear();
        foreach (var s in scenarios)
            SavedScenarios.Add(s);
    }

    partial void OnSelectedFormulationChanged(Formulation? value) => Recalculate();
    partial void OnMarketChanged(MarketType value)
    {
        IsExport = value == MarketType.Export;
        Recalculate();
    }
    partial void OnSelectedPackingChoiceChanged(PricingCostOptionChoice? value)
    {
        if (_suppressOptionSync || value is null) return;
        PackingCost = value.Cost;
    }
    partial void OnSelectedDocumentChoiceChanged(PricingCostOptionChoice? value)
    {
        if (_suppressOptionSync || value is null) return;
        ExportDocCost = value.Cost;
    }
    partial void OnSelectedAdditiveChoiceChanged(PricingCostOptionChoice? value)
    {
        if (_suppressOptionSync || value is null) return;
        SpecialAdditiveCost = value.Cost;
    }
    partial void OnPackingCostChanged(decimal value) => Recalculate();
    partial void OnExportDocCostChanged(decimal value) => Recalculate();
    partial void OnSpecialAdditiveCostChanged(decimal value) => Recalculate();
    partial void OnTransportationCostChanged(decimal value) => Recalculate();
    partial void OnMarginPercentChanged(decimal value) => Recalculate();
    partial void OnSelectedPriceUnitOptionChanged(PriceUnitOption? value)
    {
        if (value is null) return;
        PriceUnit = value.Value;
    }
    partial void OnPriceUnitChanged(PriceUnit value)
    {
        LiveCostingTitle = value == PriceUnit.PerBag25Kg
            ? "Live costing / bag (25 kg)"
            : "Live costing / MT";
        Recalculate();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        await LoadOptionChoicesAsync(db);

        Formulations.Clear();
        foreach (var f in await db.Formulations
                     .Include(x => x.Size)
                     .Include(x => x.Ingredients).ThenInclude(i => i.RawIngredient)
                     .OrderBy(x => x.Code)
                     .ToListAsync())
            Formulations.Add(f);

        SelectedFormulation = Formulations.FirstOrDefault();
        EnsureDefaultChoices();
        await LoadScenariosAsync();
        Recalculate();
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

    private void EnsureDefaultChoices()
    {
        _suppressOptionSync = true;
        SelectedPackingChoice ??= PackingChoices.FirstOrDefault();
        SelectedDocumentChoice ??= DocumentChoices.FirstOrDefault();
        SelectedAdditiveChoice ??= AdditiveChoices.FirstOrDefault();
        _suppressOptionSync = false;
    }

    private async Task LoadScenariosAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        SavedScenarios.Clear();
        foreach (var s in await db.CostingScenarios
                     .Include(x => x.Formulation)
                     .OrderByDescending(x => x.CreatedAtUtc)
                     .Take(50)
                     .ToListAsync())
            SavedScenarios.Add(s);
    }

    private void Recalculate()
    {
        if (SelectedFormulation is null)
        {
            RmCost = ConversionCost = AppliedExportDoc = TotalCost = SellingPrice = 0;
            DisplayRmCost = DisplayConversionCost = DisplayPackingCost = DisplayAppliedExportDoc =
                DisplayTotalCost = DisplaySellingPrice = 0;
            return;
        }

        var input = new CostingInput(
            SelectedFormulation.Ingredients
                .Select(i => new IngredientCostLine(i.InclusionPercent, i.RawIngredient.PricePerMt))
                .ToList(),
            SelectedFormulation.Size.ConversionCost,
            PackingCost,
            ExportDocCost,
            SpecialAdditiveCost,
            TransportationCost,
            Market,
            MarginPercent);

        var result = CostingCalculator.Calculate(input);
        RmCost = result.RmCost;
        ConversionCost = result.ConversionCost;
        AppliedExportDoc = result.ExportDocCost;
        TotalCost = result.TotalCost;
        SellingPrice = result.SellingPrice;

        DisplayRmCost = CostingCalculator.ApplyUnit(RmCost, PriceUnit);
        DisplayConversionCost = CostingCalculator.ApplyUnit(ConversionCost, PriceUnit);
        DisplayPackingCost = CostingCalculator.ApplyUnit(PackingCost, PriceUnit);
        DisplayAppliedExportDoc = CostingCalculator.ApplyUnit(AppliedExportDoc, PriceUnit);
        DisplayTotalCost = CostingCalculator.ApplyUnit(TotalCost, PriceUnit);
        DisplaySellingPrice = CostingCalculator.ApplyUnit(SellingPrice, PriceUnit);
    }

    [RelayCommand]
    private async Task SaveScenarioAsync()
    {
        if (SelectedFormulation is null)
        {
            StatusMessage = "Select a formulation.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ScenarioName))
        {
            StatusMessage = "Scenario name is required.";
            return;
        }

        Recalculate();

        await using var db = await _dbFactory.CreateDbContextAsync();
        db.CostingScenarios.Add(new CostingScenario
        {
            FormulationId = SelectedFormulation.Id,
            Name = ScenarioName.Trim(),
            Market = Market,
            PackingOptionId = SelectedPackingChoice?.Id,
            ExportDocOptionId = SelectedDocumentChoice?.Id,
            AdditiveOptionId = SelectedAdditiveChoice?.Id,
            PackingCost = PackingCost,
            ExportDocCost = ExportDocCost,
            SpecialAdditiveCost = SpecialAdditiveCost,
            TransportationCost = TransportationCost,
            MarginPercent = MarginPercent,
            SnapshotRmCost = RmCost,
            SnapshotConversionCost = ConversionCost,
            SnapshotTotalCost = TotalCost,
            SnapshotSellingPrice = SellingPrice,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        StatusMessage = "Costing scenario saved with price snapshots (per MT).";
        await LoadScenariosAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();
}
