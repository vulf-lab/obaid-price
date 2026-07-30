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
        Assert.Equal(1177m, result.SellingPrice);
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
        Assert.Equal(1221m, result.SellingPrice);
    }

    [Fact]
    public void ToBag_DividesMtByForty()
    {
        Assert.Equal(40m, CostingCalculator.BagsPerMt);
        Assert.Equal(25m, CostingCalculator.ToBag(1000m));
        Assert.Equal(25m, CostingCalculator.ApplyUnit(1000m, PriceUnit.PerBag25Kg));
        Assert.Equal(1000m, CostingCalculator.ApplyUnit(1000m, PriceUnit.PerMt));
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
