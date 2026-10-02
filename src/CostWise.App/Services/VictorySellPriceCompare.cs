using CostWise.Core.Entities;

namespace CostWise.App.Services;

/// <summary>
/// Fills Last Price / Change % from a saved Victory snapshot.
/// Prefers the current price-book id, then the book side recorded on the snapshot.
/// Within that set, matches formulation id, then formula code.
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

        var byId = new Dictionary<int, VictoryReportSnapshotLine>();
        var byCode = new Dictionary<string, VictoryReportSnapshotLine>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in forBook)
        {
            if (line.FormulationId is int id && !byId.ContainsKey(id))
                byId[id] = line;
            if (!string.IsNullOrWhiteSpace(line.FormulationCode) && !byCode.ContainsKey(line.FormulationCode))
                byCode[line.FormulationCode] = line;
        }

        return rows.Select(r =>
        {
            if (!byId.TryGetValue(r.FormulationId, out var line))
                byCode.TryGetValue(r.Code, out line);

            var last = line?.SellMt;
            decimal? change = last is > 0m && r.SellMt is decimal sell
                ? Math.Round((sell - last.Value) / last.Value * 100m, 1)
                : null;
            return r with { LastSellMt = last, SellChangePercent = change };
        }).ToList();
    }

    public static bool AnyLastPrice(IReadOnlyList<PriceListBookRow> rowsA, IReadOnlyList<PriceListBookRow> rowsB) =>
        rowsA.Any(r => r.LastSellMt is not null) || rowsB.Any(r => r.LastSellMt is not null);
}
