using CostWise.Core.Entities;
using CostWise.Core.Enums;
using CostWise.Core.Services;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.Services;

public sealed record PriceListBookRow(
    int FormulationId,
    string Code,
    string CategoryName,
    string FeedTypeName,
    string SubCategoryName,
    string SizeName,
    bool IsInProduction,
    decimal RmCost,
    decimal ConversionCost,
    decimal PackingCost,
    decimal AdditiveCost,
    decimal ExportDocCost,
    decimal TotalCost,
    decimal? SellMt,
    decimal? SellBag,
    decimal? OverrideSellMt,
    decimal? OverrideSellBag,
    decimal? ProteinTarget,
    decimal? FatTarget,
    string PriceBookName,
    string CurrencyCode,
    decimal? LastSellMt = null,
    decimal? SellChangePercent = null);

public sealed record RmPriceChangeRow(
    string Name,
    decimal? PreviousKes,
    decimal CurrentKes,
    decimal? DeltaPercent);

public sealed record VictoryGroupBrief(
    string BookAName,
    string BookBName,
    IReadOnlyList<PriceListBookRow> BookARows,
    IReadOnlyList<PriceListBookRow> BookBRows,
    IReadOnlyList<RmPriceChangeRow> RmChanges,
    string Commentary,
    IReadOnlyList<string> VisibleColumns,
    byte[]? LogoLeft,
    byte[]? LogoRight,
    decimal RoundBookATo = 0m,
    decimal RoundBookBTo = 0m,
    decimal BookAMarginPercent = 0m,
    decimal BookBMarginPercent = 0m,
    decimal? CompareBookAMarginPercent = null,
    decimal? CompareBookBMarginPercent = null,
    bool ShowSellCompare = false,
    decimal RoundDeltaPercentTo = 0m);

public sealed record CommercialBrief(
    string ListName,
    DateTime EffectiveDate,
    string CurrencyCode,
    PriceUnit SellUnit,
    IReadOnlyList<PriceListBookRow> Rows,
    byte[]? Logo,
    IReadOnlyList<string> VisibleColumns,
    decimal RoundSaleTo,
    decimal RoundCpTo,
    decimal RoundFatTo,
    bool TransportIncluded = false);

public static class PriceListBriefBuilder
{
    public static readonly string[] AllExportColumns =
    [
        "Code", "Production", "Category", "Feed type", "Version", "Size",
        "Raw Material", "Conversion", "Packing cost", "Additive", "Export Doc",
        "Total cost", "Sell / MT", "Manual MT", "Sell / bag", "Manual bag"
    ];

    public static IReadOnlyList<string> DefaultVisibleColumns { get; } =
        AllExportColumns.Where(c => c is not ("Manual MT" or "Manual bag")).ToList();

    public static async Task<IReadOnlyList<PriceListBookRow>> BuildBookRowsAsync(
        CostWiseDbContext db,
        int priceBookId,
        CancellationToken ct = default)
    {
        var book = await db.PriceBooks
            .Include(b => b.DisplayCurrency)
            .FirstOrDefaultAsync(b => b.Id == priceBookId, ct);
        if (book is null)
            return [];

        var rate = Math.Max(book.DisplayCurrency?.KesPerUnit ?? 1m, 0.000001m);
        var currencyCode = book.DisplayCurrency?.Code ?? "KES";

        var members = await db.PriceBookFormulations
            .AsNoTracking()
            .Include(x => x.Formulation).ThenInclude(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
            .Include(x => x.Formulation).ThenInclude(f => f.Category)
            .Include(x => x.Formulation).ThenInclude(f => f.FeedType)
            .Include(x => x.Formulation).ThenInclude(f => f.SubCategory)
            .Include(x => x.Formulation).ThenInclude(f => f.Size)
            .Include(x => x.Formulation).ThenInclude(f => f.Specs).ThenInclude(s => s.SpecParameter)
            .Where(x => x.PriceBookId == priceBookId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        var rows = new List<PriceListBookRow>();
        foreach (var m in members)
        {
            var f = m.Formulation;
            var ingredients = f.Ingredients
                .Select(i => new IngredientCostLine(i.InclusionPercent, i.RawIngredient.PricePerMt))
                .ToList();
            var result = CostingCalculator.CalculateForPriceBook(
                ingredients,
                f.Size.ConversionCost,
                book.PackingCost,
                book.ExportDocCost,
                book.SpecialAdditiveCost,
                book.TransportationCost,
                book.MarginPercent);

            var calcDisplayMt = CostingCalculator.RoundToIncrement(result.SellingPrice / rate, book.RoundMtTo);
            var calcDisplayBag = CostingCalculator.RoundToIncrement(
                CostingCalculator.ToBag(result.SellingPrice) / rate, book.RoundBagTo);

            var sellMt = m.OverrideSellPriceMt is decimal ovMt
                ? ovMt / rate
                : calcDisplayMt;
            var sellBag = m.OverrideSellPriceBag is decimal ovBag
                ? ovBag / rate
                : calcDisplayBag;

            decimal? protein = f.Specs
                .FirstOrDefault(s => string.Equals(s.SpecParameter.Name, "Protein", StringComparison.OrdinalIgnoreCase))
                ?.TargetValue;
            decimal? fat = f.Specs
                .FirstOrDefault(s => string.Equals(s.SpecParameter.Name, "Fat", StringComparison.OrdinalIgnoreCase))
                ?.TargetValue;

            rows.Add(new PriceListBookRow(
                f.Id,
                f.Code,
                f.Category.Name,
                f.FeedType.Name,
                f.SubCategory.Name,
                f.Size.Name,
                f.IsActive,
                result.RmCost / rate,
                result.ConversionCost / rate,
                result.PackingCost / rate,
                result.SpecialAdditiveCost / rate,
                result.ExportDocCost / rate,
                result.TotalCost / rate,
                sellMt,
                sellBag,
                m.OverrideSellPriceMt is null ? null : m.OverrideSellPriceMt / rate,
                m.OverrideSellPriceBag is null ? null : m.OverrideSellPriceBag / rate,
                protein,
                fat,
                book.Name,
                currencyCode));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<PriceListBookRow>> BuildBookRowsInCurrencyAsync(
        CostWiseDbContext db,
        int priceBookId,
        decimal kesPerUnit,
        string currencyCode,
        CancellationToken ct = default)
    {
        var book = await db.PriceBooks.FirstOrDefaultAsync(b => b.Id == priceBookId, ct);
        if (book is null)
            return [];

        var rate = Math.Max(kesPerUnit, 0.000001m);
        var members = await db.PriceBookFormulations
            .AsNoTracking()
            .Include(x => x.Formulation).ThenInclude(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
            .Include(x => x.Formulation).ThenInclude(f => f.Category)
            .Include(x => x.Formulation).ThenInclude(f => f.FeedType)
            .Include(x => x.Formulation).ThenInclude(f => f.SubCategory)
            .Include(x => x.Formulation).ThenInclude(f => f.Size)
            .Include(x => x.Formulation).ThenInclude(f => f.Specs).ThenInclude(s => s.SpecParameter)
            .Where(x => x.PriceBookId == priceBookId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

        var rows = new List<PriceListBookRow>();
        foreach (var m in members)
        {
            var f = m.Formulation;
            var ingredients = f.Ingredients
                .Select(i => new IngredientCostLine(i.InclusionPercent, i.RawIngredient.PricePerMt))
                .ToList();
            var result = CostingCalculator.CalculateForPriceBook(
                ingredients,
                f.Size.ConversionCost,
                book.PackingCost,
                book.ExportDocCost,
                book.SpecialAdditiveCost,
                book.TransportationCost,
                book.MarginPercent);

            var calcDisplayMt = CostingCalculator.RoundToIncrement(result.SellingPrice / rate, book.RoundMtTo);
            var calcDisplayBag = CostingCalculator.RoundToIncrement(
                CostingCalculator.ToBag(result.SellingPrice) / rate, book.RoundBagTo);

            var sellMt = m.OverrideSellPriceMt is decimal ovMt ? ovMt / rate : calcDisplayMt;
            var sellBag = m.OverrideSellPriceBag is decimal ovBag ? ovBag / rate : calcDisplayBag;

            decimal? protein = f.Specs
                .FirstOrDefault(s => string.Equals(s.SpecParameter.Name, "Protein", StringComparison.OrdinalIgnoreCase))
                ?.TargetValue;
            decimal? fat = f.Specs
                .FirstOrDefault(s => string.Equals(s.SpecParameter.Name, "Fat", StringComparison.OrdinalIgnoreCase))
                ?.TargetValue;

            rows.Add(new PriceListBookRow(
                f.Id, f.Code, f.Category.Name, f.FeedType.Name, f.SubCategory.Name, f.Size.Name, f.IsActive,
                result.RmCost / rate, result.ConversionCost / rate, result.PackingCost / rate,
                result.SpecialAdditiveCost / rate, result.ExportDocCost / rate, result.TotalCost / rate,
                sellMt, sellBag,
                m.OverrideSellPriceMt is null ? null : m.OverrideSellPriceMt / rate,
                m.OverrideSellPriceBag is null ? null : m.OverrideSellPriceBag / rate,
                protein, fat, book.Name, currencyCode));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<RmPriceChangeRow>> BuildRmChangesAsync(
        CostWiseDbContext db,
        IEnumerable<int> rawIngredientIds,
        DateTime? previousAsOfUtc = null,
        CancellationToken ct = default)
    {
        var ids = rawIngredientIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var ingredients = await db.RawIngredients
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var history = await db.RawIngredientPriceHistories
            .AsNoTracking()
            .Where(h => ids.Contains(h.RawIngredientId))
            .OrderByDescending(h => h.ChangedAtUtc)
            .ToListAsync(ct);

        var rows = new List<RmPriceChangeRow>();
        foreach (var rm in ingredients)
        {
            var entries = history.Where(h => h.RawIngredientId == rm.Id).ToList();
            decimal? previous = ResolvePreviousPrice(rm.PricePerMt, entries, previousAsOfUtc);

            decimal? delta = previous is > 0m
                ? Math.Round((rm.PricePerMt - previous.Value) / previous.Value * 100m, 2)
                : null;

            rows.Add(new RmPriceChangeRow(rm.Name, previous, rm.PricePerMt, delta));
        }

        return rows;
    }

    /// <summary>
    /// When <paramref name="previousAsOfUtc"/> is set, use the latest history price on or before that instant.
    /// Otherwise use the prior change vs current (second-most-recent when current matches latest).
    /// </summary>
    public static decimal? ResolvePreviousPrice(
        decimal currentPrice,
        IReadOnlyList<RawIngredientPriceHistory> entriesNewestFirst,
        DateTime? previousAsOfUtc)
    {
        if (entriesNewestFirst.Count == 0)
            return null;

        if (previousAsOfUtc is DateTime asOf)
        {
            var onOrBefore = entriesNewestFirst
                .Where(h => h.ChangedAtUtc <= asOf)
                .OrderByDescending(h => h.ChangedAtUtc)
                .FirstOrDefault();
            return onOrBefore?.PricePerMt;
        }

        decimal? previous = entriesNewestFirst.Count >= 2 ? entriesNewestFirst[1].PricePerMt
            : entriesNewestFirst.Count == 1 && entriesNewestFirst[0].PricePerMt != currentPrice
                ? entriesNewestFirst[0].PricePerMt
                : null;

        if (entriesNewestFirst.Count >= 1 && entriesNewestFirst[0].PricePerMt == currentPrice && entriesNewestFirst.Count >= 2)
            previous = entriesNewestFirst[1].PricePerMt;
        else if (entriesNewestFirst.Count >= 1 && entriesNewestFirst[0].PricePerMt != currentPrice)
            previous = entriesNewestFirst[0].PricePerMt;

        return previous;
    }

    public static async Task<IReadOnlyList<DateTime>> GetDistinctHistoryDatesUtcAsync(
        CostWiseDbContext db,
        CancellationToken ct = default)
    {
        var stamps = await db.RawIngredientPriceHistories
            .AsNoTracking()
            .Select(h => h.ChangedAtUtc)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync(ct);

        // Collapse to calendar days (UTC date).
        return stamps
            .Select(d => d.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();
    }
}
