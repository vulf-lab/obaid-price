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

    public static decimal FromBag(decimal perBag) => perBag * BagsPerMt;

    public static decimal ApplyUnit(decimal perMt, PriceUnit unit) =>
        unit == PriceUnit.PerBag25Kg ? ToBag(perMt) : perMt;

    /// <summary>Round to nearest multiple of <paramref name="increment"/>. If increment &lt;= 0, round to 2 decimals.</summary>
    public static decimal RoundToIncrement(decimal value, decimal increment)
    {
        if (increment <= 0m)
            return Round(value);

        var steps = Math.Round(value / increment, MidpointRounding.AwayFromZero);
        return steps * increment;
    }

    /// <summary>Gross margin on selling price: (sell − cost) / sell × 100. Null when sell is 0.</summary>
    public static decimal? GrossMarginPercent(decimal sellMt, decimal totalCostMt)
    {
        if (sellMt == 0m)
            return null;

        return Round((sellMt - totalCostMt) / sellMt * 100m);
    }

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

        // Margin% = gross margin on sell → Sell = Cost / (1 - m/100)
        var factor = 1m - marginPercent / 100m;
        var sellingPrice = factor > 0m ? total / factor : total;

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
