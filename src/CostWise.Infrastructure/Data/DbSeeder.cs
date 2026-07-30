using CostWise.Core.Entities;
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
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedFeedTypesAsync(CostWiseDbContext db, CancellationToken ct)
    {
        if (await db.FeedTypes.AnyAsync(ct)) return;

        db.FeedTypes.AddRange(
            new FeedType { Name = "Starter" },
            new FeedType { Name = "Pre-grower" },
            new FeedType { Name = "Grower" },
            new FeedType { Name = "Finisher High Pro" },
            new FeedType { Name = "Finisher Low Pro" },
            new FeedType { Name = "Brood Stock" });
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
            new SpecParameter { Name = "Crude Protein", Unit = "%" },
            new SpecParameter { Name = "Crude Fat", Unit = "%" },
            new SpecParameter { Name = "Crude Fiber", Unit = "%" },
            new SpecParameter { Name = "Ash", Unit = "%" },
            new SpecParameter { Name = "Moisture", Unit = "%" });
    }
}
