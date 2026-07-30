using System.Text.RegularExpressions;
using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.Infrastructure.Services;

public static class FeedTypeNormalizer
{
    private static readonly string[] CanonicalNames =
    [
        "Starter",
        "Pre-grower",
        "Grower",
        "Finisher High Pro",
        "Balanced Finisher",
        "Broodstock"
    ];

    private static readonly Dictionary<string, string> AliasToCanonical = new(StringComparer.OrdinalIgnoreCase)
    {
        ["starter"] = "Starter",
        ["starter 1.5"] = "Starter",
        ["starter1.5"] = "Starter",
        ["pre grower"] = "Pre-grower",
        ["pre-grower"] = "Pre-grower",
        ["pregrower"] = "Pre-grower",
        ["grower"] = "Grower",
        ["finisher high pro"] = "Finisher High Pro",
        ["finisher - high pro"] = "Finisher High Pro",
        ["finisherhighpro"] = "Finisher High Pro",
        ["finisher low pro"] = "Balanced Finisher",
        ["finisher - low pro"] = "Balanced Finisher",
        ["finisherlowpro"] = "Balanced Finisher",
        ["balanced finisher"] = "Balanced Finisher",
        ["balancedfinisher"] = "Balanced Finisher",
        ["broodstock"] = "Broodstock",
        ["brood stock"] = "Broodstock",
        ["brooder"] = "Broodstock"
    };

    public static async Task NormalizeAsync(CostWiseDbContext db, CancellationToken ct = default)
    {
        var existing = await db.FeedTypes.ToListAsync(ct);
        var claimedIds = new HashSet<int>();
        var byCanonical = new Dictionary<string, FeedType>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in CanonicalNames)
        {
            var keeper = existing.FirstOrDefault(ft =>
                !claimedIds.Contains(ft.Id) &&
                string.Equals(ft.Name, name, StringComparison.OrdinalIgnoreCase));

            keeper ??= existing.FirstOrDefault(ft =>
                !claimedIds.Contains(ft.Id) &&
                ResolveCanonical(ft.Name) == name);

            if (keeper is null)
            {
                keeper = new FeedType { Name = name, IsActive = true };
                db.FeedTypes.Add(keeper);
                existing.Add(keeper);
            }
            else
            {
                keeper.Name = name;
                keeper.IsActive = true;
                claimedIds.Add(keeper.Id);
            }

            byCanonical[name] = keeper;
        }

        await db.SaveChangesAsync(ct);

        // Refresh ids after inserts
        existing = await db.FeedTypes.ToListAsync(ct);
        byCanonical = CanonicalNames.ToDictionary(
            n => n,
            n => existing.First(ft => string.Equals(ft.Name, n, StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);
        var canonicalIds = byCanonical.Values.Select(ft => ft.Id).ToHashSet();

        var remap = new Dictionary<int, int>();
        foreach (var ft in existing.Where(ft => !canonicalIds.Contains(ft.Id)))
        {
            var canonicalName = ResolveCanonical(ft.Name);
            if (canonicalName is null || !byCanonical.TryGetValue(canonicalName, out var target))
                continue;
            remap[ft.Id] = target.Id;
        }

        if (remap.Count > 0)
        {
            foreach (var f in await db.Formulations.Where(f => remap.Keys.Contains(f.FeedTypeId)).ToListAsync(ct))
                f.FeedTypeId = remap[f.FeedTypeId];

            foreach (var s in await db.Sizes
                         .Where(s => s.FeedTypeId != null && remap.Keys.Contains(s.FeedTypeId.Value))
                         .ToListAsync(ct))
                s.FeedTypeId = remap[s.FeedTypeId!.Value];
        }

        var orphans = existing.Where(ft => !canonicalIds.Contains(ft.Id)).ToList();
        if (orphans.Count > 0)
            db.FeedTypes.RemoveRange(orphans);

        await db.SaveChangesAsync(ct);
    }

    private static string? ResolveCanonical(string rawName)
    {
        var key = NormalizeKey(rawName);
        if (AliasToCanonical.TryGetValue(key, out var canonical))
            return canonical;

        var compact = Regex.Replace(key, @"[^a-z0-9]", "");
        foreach (var (alias, name) in AliasToCanonical)
        {
            var aliasCompact = Regex.Replace(alias, @"[^a-z0-9]", "");
            if (string.Equals(compact, aliasCompact, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        return null;
    }

    private static string NormalizeKey(string name)
    {
        var s = name.Trim().ToLowerInvariant().Replace('-', ' ');
        return Regex.Replace(s, @"\s+", " ");
    }
}
