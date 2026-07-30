using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.Services;

public sealed record ProductionExportColumn(
    string CategoryName,
    string SizeName,
    string Code);

public sealed record ProductionExportRow(
    string IngredientName,
    IReadOnlyList<decimal?> Percents);

public sealed record ProductionExportGroup(
    string Name,
    IReadOnlyList<ProductionExportColumn> Columns,
    IReadOnlyList<ProductionExportRow> Rows,
    IReadOnlyList<decimal> ColumnTotals);

public sealed record ProductionExportSnapshot(
    DateTime GeneratedAtUtc,
    IReadOnlyList<ProductionExportGroup> Groups);

public sealed class ProductionExportService
{
    private readonly IDbContextFactory<CostWiseDbContext> _dbFactory;

    public ProductionExportService(IDbContextFactory<CostWiseDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ProductionExportSnapshot> BuildSnapshotAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var groups = await db.ProductionGroups
            .AsNoTracking()
            .Include(g => g.Formulations)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name)
            .ToListAsync(ct);

        var result = new List<ProductionExportGroup>();

        foreach (var group in groups)
        {
            var memberIds = group.Formulations
                .OrderBy(f => f.SortOrder)
                .Select(f => f.FormulationId)
                .ToList();

            var formulas = await db.Formulations
                .AsNoTracking()
                .Where(f => memberIds.Contains(f.Id))
                .Include(f => f.Category)
                .Include(f => f.Size)
                .Include(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
                .ToListAsync(ct);

            var ordered = formulas
                .OrderBy(f => f.Category.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Size.DiameterMm)
                .ThenBy(f => f.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var columns = ordered
                .Select(f => new ProductionExportColumn(f.Category.Name, f.Size.Name, f.Code))
                .ToList();

            var ingredients = ordered
                .SelectMany(f => f.Ingredients)
                .Where(i => i.RawIngredient is not null)
                .GroupBy(i => i.RawIngredientId)
                .Select(g => g.First().RawIngredient)
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rows = new List<ProductionExportRow>();
            var totals = ordered.Select(_ => 0m).ToList();

            foreach (var rm in ingredients)
            {
                var percents = new List<decimal?>();
                for (var i = 0; i < ordered.Count; i++)
                {
                    var pct = ordered[i].Ingredients
                        .FirstOrDefault(x => x.RawIngredientId == rm.Id)?.InclusionPercent;
                    if (pct is > 0m)
                    {
                        percents.Add(pct);
                        totals[i] += pct.Value;
                    }
                    else
                    {
                        percents.Add(null);
                    }
                }

                rows.Add(new ProductionExportRow(rm.Name, percents));
            }

            result.Add(new ProductionExportGroup(group.Name, columns, rows, totals));
        }

        return new ProductionExportSnapshot(DateTime.UtcNow, result);
    }
}
