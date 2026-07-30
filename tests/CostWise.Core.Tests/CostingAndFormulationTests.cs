using CostWise.Core.Entities;
using CostWise.Core.Enums;
using CostWise.Core.Services;

namespace CostWise.Core.Tests;

public class CostingCalculatorTests
{
    [Fact]
    public void CalculateRmCost_WeightsByInclusionPercent()
    {
        var lines = new[]
        {
            new IngredientCostLine(60m, 1000m),
            new IngredientCostLine(40m, 500m)
        };

        var rm = CostingCalculator.CalculateRmCost(lines);

        Assert.Equal(800m, rm);
    }

    [Fact]
    public void Calculate_Domestic_ExcludesExportDocCost()
    {
        var input = new CostingInput(
            Ingredients: new[] { new IngredientCostLine(100m, 1000m) },
            ConversionCost: 50m,
            PackingCost: 20m,
            ExportDocCost: 30m,
            SpecialAdditiveCost: 0m,
            TransportationCost: 0m,
            Market: MarketType.Domestic,
            MarginPercent: 10m);

        var result = CostingCalculator.Calculate(input);

        Assert.Equal(1000m, result.RmCost);
        Assert.Equal(0m, result.ExportDocCost);
        Assert.Equal(1070m, result.TotalCost);
        // Gross margin 10% on sell → 1070 / 0.9
        Assert.Equal(1188.89m, result.SellingPrice);
    }

    [Fact]
    public void Calculate_Export_IncludesExportDocAndOptionalCosts()
    {
        var input = new CostingInput(
            Ingredients: new[] { new IngredientCostLine(100m, 1000m) },
            ConversionCost: 50m,
            PackingCost: 20m,
            ExportDocCost: 30m,
            SpecialAdditiveCost: 15m,
            TransportationCost: 25m,
            Market: MarketType.Export,
            MarginPercent: 0m);

        var result = CostingCalculator.Calculate(input);

        Assert.Equal(30m, result.ExportDocCost);
        Assert.Equal(15m, result.SpecialAdditiveCost);
        Assert.Equal(25m, result.TransportationCost);
        Assert.Equal(1140m, result.TotalCost);
        Assert.Equal(1140m, result.SellingPrice);
    }

    [Fact]
    public void CalculateForPriceBook_IncludesExportDocWhenGreaterThanZero()
    {
        var result = CostingCalculator.CalculateForPriceBook(
            ingredients: new[] { new IngredientCostLine(100m, 1000m) },
            conversionCost: 50m,
            packingCost: 20m,
            exportDocCost: 30m,
            specialAdditiveCost: 0m,
            transportationCost: 10m,
            marginPercent: 10m);

        Assert.Equal(30m, result.ExportDocCost);
        Assert.Equal(1110m, result.TotalCost);
        // Gross margin 10% on sell → 1110 / 0.9
        Assert.Equal(1233.33m, result.SellingPrice);
    }

    [Fact]
    public void ToBag_DividesMtByForty()
    {
        Assert.Equal(40m, CostingCalculator.BagsPerMt);
        Assert.Equal(25m, CostingCalculator.ToBag(1000m));
        Assert.Equal(25m, CostingCalculator.ApplyUnit(1000m, PriceUnit.PerBag25Kg));
        Assert.Equal(1000m, CostingCalculator.ApplyUnit(1000m, PriceUnit.PerMt));
    }

    [Fact]
    public void RoundToIncrement_ZeroKeepsTwoDecimals()
    {
        Assert.Equal(1221.56m, CostingCalculator.RoundToIncrement(1221.555m, 0m));
    }

    [Fact]
    public void RoundToIncrement_RoundsToNearestMultiple()
    {
        Assert.Equal(1200m, CostingCalculator.RoundToIncrement(1221m, 100m));
        Assert.Equal(1250m, CostingCalculator.RoundToIncrement(1225m, 50m));
        Assert.Equal(30m, CostingCalculator.RoundToIncrement(27.6m, 5m));
        Assert.Equal(25m, CostingCalculator.RoundToIncrement(27.4m, 5m));
    }

    [Fact]
    public void FromBag_MultipliesByForty()
    {
        Assert.Equal(100000m, CostingCalculator.FromBag(2500m));
    }

    [Fact]
    public void GrossMarginPercent_OnSellingPrice()
    {
        Assert.Equal(14.6m, CostingCalculator.GrossMarginPercent(100000m, 85402m));
        Assert.Null(CostingCalculator.GrossMarginPercent(0m, 100m));
    }

    [Fact]
    public void Calculate_MarginPercent_MatchesGrossMarginOnSell()
    {
        var result = CostingCalculator.CalculateForPriceBook(
            ingredients: new[] { new IngredientCostLine(100m, 85402m) },
            conversionCost: 0m,
            packingCost: 0m,
            exportDocCost: 0m,
            specialAdditiveCost: 0m,
            transportationCost: 0m,
            marginPercent: 14.5m);

        Assert.Equal(85402m, result.TotalCost);
        var gm = CostingCalculator.GrossMarginPercent(result.SellingPrice, result.TotalCost);
        Assert.Equal(14.5m, gm);
    }
}

public class FormulationRulesTests
{
    [Fact]
    public void InclusionsSumTo100_AllowsSmallTolerance()
    {
        var ingredients = new[]
        {
            new FormulationIngredient { InclusionPercent = 50.005m },
            new FormulationIngredient { InclusionPercent = 49.995m }
        };

        Assert.True(FormulationRules.InclusionsSumTo100(ingredients));
    }

    [Fact]
    public void IsUnfitToProduce_WhenAnyIngredientUnavailable()
    {
        var formulation = new Formulation
        {
            Ingredients =
            {
                new FormulationIngredient
                {
                    RawIngredient = new RawIngredient { Name = "Fish Meal", IsAvailable = true }
                },
                new FormulationIngredient
                {
                    RawIngredient = new RawIngredient { Name = "Soy", IsAvailable = false }
                }
            }
        };

        Assert.True(FormulationRules.IsUnfitToProduce(formulation));
        Assert.False(FormulationRules.IsProducible(formulation));
    }

    [Fact]
    public void IsProducible_WhenAllIngredientsAvailable()
    {
        var formulation = new Formulation
        {
            Ingredients =
            {
                new FormulationIngredient
                {
                    RawIngredient = new RawIngredient { Name = "Fish Meal", IsAvailable = true }
                }
            }
        };

        Assert.True(FormulationRules.IsProducible(formulation));
    }
}
