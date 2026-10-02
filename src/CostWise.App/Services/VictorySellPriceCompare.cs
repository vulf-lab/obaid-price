using CostWise.Core.Entities;

namespace CostWise.App.Services;

/// <summary>
/// Fills Last Price / Change % from a saved Victory snapshot.
/// Prefers the current price-book id, then the book side recorded on the snapshot.
/// Within that set, matches feed type and size. Formula code is used only when
/// the snapshot line has no product names.
/// </summary>
public static class VictorySellPriceCompare
{
    public const string RoleA = "A";
    public const string RoleB = "B";

    public static IReadOnlyList<PriceListBookRow> Apply(
        IReadOnlyList<PriceListBookRow> rows,
        int currentBookId,
        int snapshotBookId,
        string bookRole,
        IEnumerable<VictoryReportSnapshotLine> lines)
    {
        var all = lines as IReadOnlyList<VictoryReportSnapshotLine> ?? lines.ToList();
        var forBook = all.Where(l => l.PriceBookId == currentBookId).ToList();
        if (forBook.Count == 0)
        {
            forBook = all.Where(l =>
                    string.Equals(l.BookRole, bookRole, StringComparison.OrdinalIgnoreCase)
                    || l.PriceBookId == snapshotBookId)
                .ToList();
        }

        return rows.Select(r =>
        {
            var line = FindLine(forBook, r);
            var last = line?.SellMt;
            decimal? change = last is > 0m && r.SellMt is decimal sell
                ? Math.Round((sell - last.Value) / last.Value * 100m, 1)
                : null;
            return r with { LastSellMt = last, SellChangePercent = change };
        }).ToList();
    }

    public static bool AnyLastPrice(IReadOnlyList<PriceListBookRow> rowsA, IReadOnlyList<PriceListBookRow> rowsB) =>
        rowsA.Any(r => r.LastSellMt is not null) || rowsB.Any(r => r.LastSellMt is not null);

    private static VictoryReportSnapshotLine? FindLine(
        IReadOnlyList<VictoryReportSnapshotLine> lines,
        PriceListBookRow row)
    {
        var product = lines.Where(l => HasProduct(l)
            && string.Equals(l.FeedTypeName, row.FeedTypeName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(l.SizeName, row.SizeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (product.Count > 0)
        {
            return product.FirstOrDefault(l =>
                       !string.IsNullOrWhiteSpace(l.FormulationCode)
                       && string.Equals(l.FormulationCode, row.Code, StringComparison.OrdinalIgnoreCase))
                   ?? product[0];
        }

        return lines.FirstOrDefault(l =>
            !HasProduct(l)
            && !string.IsNullOrWhiteSpace(l.FormulationCode)
            && string.Equals(l.FormulationCode, row.Code, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasProduct(VictoryReportSnapshotLine line) =>
        !string.IsNullOrWhiteSpace(line.FeedTypeName) && !string.IsNullOrWhiteSpace(line.SizeName);
}
