using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.Infrastructure.Services;

public static class SpecParameterNormalizer
{
    /// <summary>Locked nutrient sequence and display decimals (from nutrition master sheet).</summary>
    public static readonly (string Name, string Unit, int Decimals)[] Canonical =
    [
        ("Moisture", "%", 1),
        ("Fat", "%", 1),
        ("Ash", "%", 1),
        ("Protein", "%", 1),
        ("Digestible Protein Tilapia", "%", 1),
        ("Fiber", "%", 1),
        ("Starch", "%", 1),
        ("DE Tilapia Fish", "kcal/kg", 0),
        ("DP:DE", "mg/kcal", 0),
        ("Calcium", "g/kg", 1),
        ("Av Phosphorus Aqua", "g/kg", 1),
        ("Calcium/ Phosphorus Aqua", ".", 1),
        ("Dig Lys Aqua", "g/kg", 1),
        ("Dig Met Aqua", "g/kg", 1),
        ("Dig Thr Aqua", "g/kg", 1),
        ("Vitamine A", "UI/kg", 0),
        ("Vitamine C", "mg/kg", 0),
        ("Vitamine D3", "UI/kg", 0),
        ("Vitamine E", "UI/kg", 0),
        ("MET SID / LYS SID Aqua", ".", 2),
        ("THR SID / LYS SID Aqua", ".", 2)
    ];

    private static readonly Dictionary<string, int> IndexByName =
        Canonical
            .Select((x, i) => (x.Name, i))
            .ToDictionary(x => x.Name, x => x.i, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> DecimalsByName =
        Canonical.ToDictionary(x => x.Name, x => x.Decimals, StringComparer.OrdinalIgnoreCase);

    public static int CanonicalIndex(string name) =>
        IndexByName.TryGetValue(name, out var i) ? i : int.MaxValue;

    public static int GetDecimals(string name) =>
        DecimalsByName.TryGetValue(name, out var d) ? d : 2;

    public static string FormatValue(decimal? value, string nutrientName)
    {
        if (value is null) return string.Empty;
        var decimals = GetDecimals(nutrientName);
        return value.Value.ToString($"F{decimals}");
    }

    public static IOrderedEnumerable<T> OrderByCanonical<T>(IEnumerable<T> items, Func<T, string> nameSelector) =>
        items.OrderBy(x => CanonicalIndex(nameSelector(x))).ThenBy(x => nameSelector(x), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ensures all canonical nutrients exist. Inserts missing names only — never wipes
    /// existing SpecParameters or FormulationSpecs.
    /// </summary>
    public static async Task NormalizeAsync(CostWiseDbContext db, CancellationToken ct = default)
    {
        await EnsureCanonicalAsync(db, ct);
    }

    public static async Task EnsureCanonicalAsync(CostWiseDbContext db, CancellationToken ct = default)
    {
        var existing = await db.SpecParameters.ToListAsync(ct);
        var byName = existing.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var added = false;

        foreach (var (name, unit, _) in Canonical)
        {
            if (byName.TryGetValue(name, out var row))
            {
                if (!string.Equals(row.Unit, unit, StringComparison.Ordinal))
                    row.Unit = unit;
                continue;
            }

            var entity = new SpecParameter { Name = name, Unit = unit };
            db.SpecParameters.Add(entity);
            byName[name] = entity;
            added = true;
        }

        if (added || db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }
}
