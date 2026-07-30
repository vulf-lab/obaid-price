using CostWise.Core.Entities;
using CostWise.Core.Services.Import;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.Services.Import;

public static class RawMaterialPriceImportService
{
    public static async Task<RmImportPreview> PreviewAsync(
        CostWiseDbContext db,
        string filePath,
        CancellationToken ct = default)
    {
        var sheetRows = RawMaterialPriceImportParser.Parse(filePath);
        var existing = await db.RawIngredients.AsNoTracking()
            .Select(r => new ExistingRawIngredientSnapshot(r.Id, r.Name, r.PricePerMt, r.IsAvailable))
            .ToListAsync(ct);
        return RawMaterialPriceImportValidator.BuildPreview(sheetRows, existing);
    }

    public static async Task<int> ApplyAsync(
        CostWiseDbContext db,
        RmImportPreview preview,
        decimal exchangeRateKesPerUsd,
        CancellationToken ct = default)
    {
        var rate = exchangeRateKesPerUsd > 0m ? exchangeRateKesPerUsd : 130m;
        var now = DateTime.UtcNow;
        var changed = 0;

        var byName = await db.RawIngredients.ToDictionaryAsync(
            x => x.Name,
            StringComparer.OrdinalIgnoreCase,
            ct);

        foreach (var row in preview.RowsToUpdate)
        {
            if (row.PricePerMt is null) continue;
            if (!byName.TryGetValue(row.Name, out var entity)) continue;

            var price = row.PricePerMt.Value;
            var priceChanged = entity.PricePerMt != price;
            entity.PricePerMt = price;
            if (price > 0m)
                entity.IsAvailable = true;

            if (priceChanged)
            {
                db.RawIngredientPriceHistories.Add(History(entity.Id, price, rate, now));
                changed++;
            }
        }

        foreach (var row in preview.RowsToCreate)
        {
            if (row.PricePerMt is null) continue;
            var price = row.PricePerMt.Value;
            var entity = new RawIngredient
            {
                Name = row.Name.Trim(),
                PricePerMt = price,
                IsAvailable = price > 0m
            };
            db.RawIngredients.Add(entity);
            await db.SaveChangesAsync(ct);
            db.RawIngredientPriceHistories.Add(History(entity.Id, price, rate, now));
            changed++;
        }

        await db.SaveChangesAsync(ct);
        return changed;
    }

    private static RawIngredientPriceHistory History(int id, decimal price, decimal rate, DateTime now) =>
        new()
        {
            RawIngredientId = id,
            PricePerMt = price,
            ExchangeRateKesPerUsd = rate,
            PricePerMtUsd = rate > 0m
                ? Math.Round(price / rate, 4, MidpointRounding.AwayFromZero)
                : 0m,
            ChangedAtUtc = now
        };
}
