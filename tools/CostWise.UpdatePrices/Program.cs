using CostWise.Infrastructure;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.UpdatePrices;

/// <summary>
/// Imports RM prices provided as KES/kg into PricePerMt (×1000).
/// Dry-run by default; pass --apply to write.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            Console.WriteLine("Usage: CostWise.UpdatePrices [--apply] --db <path> | --force-local");
            Console.WriteLine(ToolDatabaseArgs.HelpText);
            return 0;
        }

        if (!ToolDatabaseArgs.TryResolve(args, out var dbPath, out var dbError))
        {
            Console.Error.WriteLine(dbError);
            return dbError.Contains("Refusing", StringComparison.Ordinal) ? 2 : 0;
        }

        var apply = args.Any(a => string.Equals(a, "--apply", StringComparison.OrdinalIgnoreCase));

        Console.WriteLine(apply ? "Mode: APPLY" : "Mode: DRY-RUN");
        Console.WriteLine($"Database: {dbPath}");

        var sheet = GetSheetPrices();
        var options = new DbContextOptionsBuilder<CostWiseDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using var db = new CostWiseDbContext(options);
        await db.Database.MigrateAsync();

        var ingredients = await db.RawIngredients.ToListAsync();
        var byName = ingredients.ToDictionary(r => r.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        var markedUnavailable = 0;
        var skippedNaUnmatched = 0;
        var unmatched = new List<string>();
        var matchedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, priceKg) in sheet)
        {
            if (priceKg is null)
            {
                if (!byName.TryGetValue(name, out var naEntity))
                {
                    skippedNaUnmatched++;
                    Console.WriteLine($"  N/A miss   {name} (not in DB)");
                    continue;
                }

                matchedNames.Add(naEntity.Name);
                if (naEntity.IsAvailable)
                {
                    Console.WriteLine($"  UNAVAIL    {naEntity.Name} (n/a price → Available=false)");
                    if (apply)
                        naEntity.IsAvailable = false;
                    markedUnavailable++;
                }
                else
                {
                    Console.WriteLine($"  N/A ok     {naEntity.Name} (already unavailable)");
                }

                continue;
            }

            if (!byName.TryGetValue(name, out var entity))
            {
                unmatched.Add(name);
                Console.WriteLine($"  UNMATCHED  {name}  ({priceKg} KES/kg)");
                continue;
            }

            var priceMt = Math.Round(priceKg.Value * 1000m, 2);
            Console.WriteLine($"  UPDATE     {entity.Name}: {entity.PricePerMt:N2} → {priceMt:N2} KES/MT  (from {priceKg} KES/kg)");
            matchedNames.Add(entity.Name);
            if (apply && entity.PricePerMt != priceMt)
            {
                var rate = ReadExchangeRateKesPerUsd();
                var usd = rate > 0m ? Math.Round(priceMt / rate, 2) : 0m;
                entity.PricePerMt = priceMt;
                db.RawIngredientPriceHistories.Add(new CostWise.Core.Entities.RawIngredientPriceHistory
                {
                    RawIngredientId = entity.Id,
                    PricePerMt = priceMt,
                    ExchangeRateKesPerUsd = rate,
                    PricePerMtUsd = usd,
                    ChangedAtUtc = DateTime.UtcNow
                });
            }
            else if (apply)
            {
                entity.PricePerMt = priceMt;
            }

            updated++;
        }

        var stillZero = ingredients
            .Where(r => !matchedNames.Contains(r.Name) && r.PricePerMt == 0m)
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToList();

        if (apply)
            await db.SaveChangesAsync();

        Console.WriteLine();
        Console.WriteLine(
            $"Summary: prices_updated={updated} marked_unavailable={markedUnavailable} " +
            $"na_not_in_db={skippedNaUnmatched} unmatched_priced={unmatched.Count}");
        if (unmatched.Count > 0)
        {
            Console.WriteLine("Unmatched sheet names (priced):");
            foreach (var n in unmatched.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"  - {n}");
        }

        if (stillZero.Count > 0)
        {
            Console.WriteLine($"DB RMs still at 0 Price/MT and not on sheet ({stillZero.Count}):");
            foreach (var n in stillZero)
                Console.WriteLine($"  - {n}");
        }

        if (!apply)
            Console.WriteLine("\nDry-run complete. Re-run with --apply to write changes.");
        else
            Console.WriteLine("\nApplied.");

        return 0;
    }

    private static decimal ReadExchangeRateKesPerUsd()
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CostWise",
                "app-preferences.json");
            if (!File.Exists(path)) return 130m;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("ExchangeRateKesPerUsd", out var prop) &&
                prop.TryGetDecimal(out var rate) && rate > 0m)
                return rate;
        }
        catch
        {
            // ignore
        }

        return 130m;
    }

    /// <summary>Sheet prices in KES/kg. null = n/a.</summary>
    private static IReadOnlyList<(string Name, decimal? PriceKg)> GetSheetPrices() =>
    [
        ("SOYA BEAN MEAL (Samakgo)", 97.0m),
        ("MAIZE GRAIN (Samakgro)", 50.5m),
        ("POULTRY MEAL 65 (Samakgro)", 137.9m),
        ("RICE BRAN (Samakgro)", 18.2m),
        ("FISH MEAL 35 LP (Samakgro)", 92.9m),
        ("WHEAT BRAN (Samakgro)", 24.4m),
        ("MBM 50 (Samakgro)", 96.0m),
        ("L-LYSINE SULFATE 70", null),
        ("SOYA OIL", 241.3m),
        ("PX AQUA 0,5% ADM", 323.3m),
        ("SALCURB", 555.5m),
        ("CHOLINE 60", 163.6m),
        ("DL METHIONINE (PURE)", 727.2m),
        ("THREONINE PURE", 505.0m),
        ("NUFO (Antioxidant)", 404.0m),
        ("MCP", 222.2m),
        ("BLOOD MEAL (Samakgro)", 153.5m),
        ("LIMESTONE", 13.1m),
        ("RICE POLISH (Samakgro)", 31.3m),
        ("SORGHUM GRAIN (Samakgro)", 58.6m),
        ("WHEAT POLLARD (Samakgro)", 37.4m),
        ("MBM 50 low fat (Samakgro)", null),
        ("L-LYSINE-HCL (PURE)", 222.2m),
        ("SALT", 31.8m),
        ("SOYA BEAN MEAL EXP (Samakgo)", null),
        ("COPRAH MEAL (Samakgro)", 48.5m),
        ("VIT C 35% protected", 707.7m),
        ("MORINGA LEAVES (Samakgro)", null),
        ("SESBANIA LEAVES", null),
        ("MBM 40 NEW (Samakgro)", null),
        ("FISH MEAL 50 (FULU FISH - Africa)", 111.1m),
        ("SHRIMP MEAL (OCHONGA - Samakgro)", 111.1m),
        ("CROTON SEED MEAL", null),
        ("INSECT MEAL (BSF)", null),
        ("FISH MEAL BY PRODUCT 55 (Brazil)", null),
        ("RICE PROTEIN (SAMAKGRO)", null),
        ("VIT C 97.5% ENROBEE", 707.7m),
        ("MYCOTOXIN BINDER ADISSEO", 555.5m),
        ("MYCOCURB DRY", 454.5m),
        ("CANOLA MEAL (Samakgro)", null),
        ("FEATHER MEAL (Samakgro)", 149.9m),
        ("MBM 55 (Samakgro)", 97.0m),
    ];
}
