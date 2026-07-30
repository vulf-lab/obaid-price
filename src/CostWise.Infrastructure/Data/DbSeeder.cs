using CostWise.Core.Entities;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CostWise.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(CostWiseDbContext db, CancellationToken cancellationToken = default)
    {
        await SeedFeedTypesAsync(db, cancellationToken);
        await SeedSpeciesAsync(db, cancellationToken);
        await SeedCategoriesAsync(db, cancellationToken);
        await SeedSubCategoriesAsync(db, cancellationToken);
        await SeedSizesAsync(db, cancellationToken);
        await SeedSpecParametersAsync(db, cancellationToken);
        await SeedCurrenciesAsync(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCurrenciesAsync(CostWiseDbContext db, CancellationToken ct)
    {
        if (await db.Currencies.AnyAsync(ct)) return;

        db.Currencies.AddRange(
            new Currency
            {
                Code = "KES",
                Name = "Kenyan Shilling",
                IsBase = true,
                KesPerUnit = 1m,
                IsActive = true,
                SortOrder = 0
            },
            new Currency
            {
                Code = "USD",
                Name = "US Dollar",
                IsBase = false,
                KesPerUnit = 130m,
                IsActive = true,
                SortOrder = 1
            });
    }

    private static async Task SeedFeedTypesAsync(CostWiseDbContext db, CancellationToken ct)
    {
        if (await db.FeedTypes.AnyAsync(ct)) return;

        db.FeedTypes.AddRange(
            new FeedType { Name = "Starter" },
            new FeedType { Name = "Pre-grower" },
            new FeedType { Name = "Grower" },
            new FeedType { Name = "Finisher High Pro" },
            new FeedType { Name = "Balanced Finisher" },
            new FeedType { Name = "Broodstock" });
    }

    private static async Task SeedSpeciesAsync(CostWiseDbContext db, CancellationToken ct)
    {
        if (await db.Species.AnyAsync(ct)) return;

        db.Species.AddRange(
            new Species { Name = "Tilapia" },
            new Species { Name = "Catfish" },
            new Species { Name = "Trout" });
    }

    private static async Task SeedCategoriesAsync(CostWiseDbContext db, CancellationToken ct)
    {
        if (await db.Categories.AnyAsync(ct)) return;

        db.Categories.AddRange(
            new Category { Name = "Standard" },
            new Category { Name = "Customer Specific" },
            new Category { Name = "Region Specific" });
    }

    private static async Task SeedSubCategoriesAsync(CostWiseDbContext db, CancellationToken ct)
    {
        if (await db.SubCategories.AnyAsync(ct)) return;

        db.SubCategories.AddRange(
            new SubCategory { Name = "Ultra High Specs" },
            new SubCategory { Name = "Medium Specs" },
            new SubCategory { Name = "Low Specs" });
    }

    private static async Task SeedSizesAsync(CostWiseDbContext db, CancellationToken ct)
    {
        if (await db.Sizes.AnyAsync(ct)) return;

        var diameters = new[] { 0m, 0.5m, 2m, 3m, 4m, 4.5m };
        foreach (var d in diameters)
        {
            db.Sizes.Add(new Size
            {
                Name = $"{d}mm",
                DiameterMm = d,
                ConversionCost = 0m
            });
        }
    }

    private static async Task SeedSpecParametersAsync(CostWiseDbContext db, CancellationToken ct)
    {
        if (await db.SpecParameters.AnyAsync(ct)) return;

        db.SpecParameters.AddRange(
            SpecParameterNormalizer.Canonical.Select(x =>
                new SpecParameter { Name = x.Name, Unit = x.Unit }));
    }
}
