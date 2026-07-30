using CostWise.Core.Enums;

namespace CostWise.Core.Services;

public sealed record IngredientCostLine(decimal InclusionPercent, decimal PricePerMt);

public sealed record CostingInput(
    IReadOnlyList<IngredientCostLine> Ingredients,
    decimal ConversionCost,
    decimal PackingCost,
    decimal ExportDocCost,
    decimal SpecialAdditiveCost,
    decimal TransportationCost,
    MarketType Market,
    decimal MarginPercent);

public sealed record CostingResult(
    decimal RmCost,
    decimal ConversionCost,
    decimal PackingCost,
    decimal ExportDocCost,
    decimal SpecialAdditiveCost,
    decimal TransportationCost,
    decimal TotalCost,
    decimal SellingPrice);

public static class CostingCalculator
{
    public const decimal BagWeightKg = 25m;
    public const decimal KgPerMt = 1000m;
    public const decimal BagsPerMt = KgPerMt / BagWeightKg;

    public static decimal CalculateRmCost(IEnumerable<IngredientCostLine> ingredients)
    {
        return ingredients.Sum(i => i.InclusionPercent / 100m * i.PricePerMt);
    }

    public static CostingResult Calculate(CostingInput input)
    {
        var rmCost = CalculateRmCost(input.Ingredients);
        var exportDoc = input.Market == MarketType.Export ? input.ExportDocCost : 0m;
        var specialAdditive = input.SpecialAdditiveCost > 0 ? input.SpecialAdditiveCost : 0m;
        var transportation = input.TransportationCost > 0 ? input.TransportationCost : 0m;

        return BuildResult(
            rmCost,
            input.ConversionCost,
            input.PackingCost,
            exportDoc,
            specialAdditive,
            transportation,
            input.MarginPercent);
    }

    /// <summary>
    /// Price-book costing: export doc is included whenever ExportDocCost &gt; 0 (no market gate).
    /// </summary>
    public static CostingResult CalculateForPriceBook(
        IReadOnlyList<IngredientCostLine> ingredients,
        decimal conversionCost,
        decimal packingCost,
        decimal exportDocCost,
        decimal specialAdditiveCost,
        decimal transportationCost,
        decimal marginPercent)
    {
        var rmCost = CalculateRmCost(ingredients);
        var exportDoc = exportDocCost > 0 ? exportDocCost : 0m;
        var specialAdditive = specialAdditiveCost > 0 ? specialAdditiveCost : 0m;
        var transportation = transportationCost > 0 ? transportationCost : 0m;

        return BuildResult(
            rmCost,
            conversionCost,
            packingCost,
            exportDoc,
            specialAdditive,
            transportation,
            marginPercent);
    }

    public static decimal ToBag(decimal perMt) => perMt / BagsPerMt;

    public static decimal ApplyUnit(decimal perMt, PriceUnit unit) =>
        unit == PriceUnit.PerBag25Kg ? ToBag(perMt) : perMt;

    private static CostingResult BuildResult(
        decimal rmCost,
        decimal conversionCost,
        decimal packingCost,
        decimal exportDoc,
        decimal specialAdditive,
        decimal transportation,
        decimal marginPercent)
    {
        var total = rmCost
                    + conversionCost
                    + packingCost
                    + exportDoc
                    + specialAdditive
                    + transportation;

        var sellingPrice = total * (1m + marginPercent / 100m);

        return new CostingResult(
            Round(rmCost),
            Round(conversionCost),
            Round(packingCost),
            Round(exportDoc),
            Round(specialAdditive),
            Round(transportation),
            Round(total),
            Round(sellingPrice));
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
