using CostWise.Core.Services;

namespace CostWise.Core.Services.Import;

public sealed record FormulaImportSheetLine(
    string Category,
    string Size,
    string Species,
    string FeedType,
    string Code,
    string Revision,
    string SubCategory,
    string RawIngredient,
    decimal Percent,
    int ExcelRow);

public sealed record FormulaImportIngredientLine(string RawIngredientName, decimal InclusionPercent);

public sealed record FormulaImportGroup(
    string Code,
    string Category,
    string Size,
    string Species,
    string FeedType,
    string Revision,
    string SubCategory,
    IReadOnlyList<FormulaImportIngredientLine> Ingredients);

public enum FormulaImportDisposition
{
    New,
    Same,
    Different,
    Blocked
}

public sealed record FormulaImportCodeResult(
    string Code,
    FormulaImportDisposition Disposition,
    string Summary,
    IReadOnlyList<string> BlockerReasons,
    IReadOnlyList<string> DiffLines,
    FormulaImportGroup? SheetGroup,
    int? ExistingFormulationId);

public sealed record FormulaImportPreview(IReadOnlyList<FormulaImportCodeResult> Results)
{
    public IReadOnlyList<FormulaImportCodeResult> Blocked =>
        Results.Where(r => r.Disposition == FormulaImportDisposition.Blocked).ToList();

    public IReadOnlyList<FormulaImportCodeResult> Different =>
        Results.Where(r => r.Disposition == FormulaImportDisposition.Different).ToList();

    public IReadOnlyList<FormulaImportCodeResult> Applyable =>
        Results.Where(r => r.Disposition is FormulaImportDisposition.New
            or FormulaImportDisposition.Different
            or FormulaImportDisposition.Same).ToList();

    /// <summary>Codes that will write on confirm (skip exact Same no-ops).</summary>
    public IReadOnlyList<FormulaImportCodeResult> WillWrite =>
        Results.Where(r => r.Disposition is FormulaImportDisposition.New
            or FormulaImportDisposition.Different).ToList();

    public bool HasBlockers => Blocked.Count > 0;
    public bool HasDifferentCodeWarnings => Different.Count > 0;

    public string SummaryText =>
        $"{Results.Count} code(s): {WillWrite.Count(r => r.Disposition == FormulaImportDisposition.New)} new, " +
        $"{Different.Count} different (overwrite), " +
        $"{Results.Count(r => r.Disposition == FormulaImportDisposition.Same)} identical, " +
        $"{Blocked.Count} blocked.";
}

public sealed record ExistingFormulaSnapshot(
    int Id,
    string Code,
    string CategoryName,
    string SubCategoryName,
    string FeedTypeName,
    string SpeciesName,
    string SizeName,
    string Revision,
    IReadOnlyList<FormulaImportIngredientLine> Ingredients);

public sealed record MasterNameSets(
    IReadOnlySet<string> Categories,
    IReadOnlySet<string> SubCategories,
    IReadOnlySet<string> FeedTypes,
    IReadOnlySet<string> Species,
    IReadOnlySet<string> Sizes,
    IReadOnlySet<string> RawIngredients);

public static class FormulationImportValidator
{
    public static IReadOnlyList<FormulaImportGroup> GroupByCode(IEnumerable<FormulaImportSheetLine> lines)
    {
        return lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Code))
            .GroupBy(l => l.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var ingredients = g
                    .GroupBy(x => x.RawIngredient.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(ig => !string.IsNullOrEmpty(ig.Key))
                    .Select(ig => new FormulaImportIngredientLine(
                        ig.First().RawIngredient.Trim(),
                        ig.Sum(x => x.Percent)))
                    .OrderBy(x => x.RawIngredientName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new FormulaImportGroup(
                    first.Code.Trim(),
                    first.Category.Trim(),
                    first.Size.Trim(),
                    first.Species.Trim(),
                    first.FeedType.Trim(),
                    NormalizeRev(first.Revision),
                    first.SubCategory.Trim(),
                    ingredients);
            })
            .OrderBy(g => g.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static FormulaImportPreview BuildPreview(
        IReadOnlyList<FormulaImportGroup> groups,
        IReadOnlyDictionary<string, ExistingFormulaSnapshot> existingByCode,
        MasterNameSets masters)
    {
        var results = new List<FormulaImportCodeResult>();
        foreach (var group in groups)
            results.Add(Evaluate(group, existingByCode, masters));
        return new FormulaImportPreview(results);
    }

    public static FormulaImportCodeResult Evaluate(
        FormulaImportGroup group,
        IReadOnlyDictionary<string, ExistingFormulaSnapshot> existingByCode,
        MasterNameSets masters)
    {
        var blockers = new List<string>();

        if (string.IsNullOrWhiteSpace(group.Category) || !ContainsName(masters.Categories, group.Category))
            blockers.Add($"Category '{group.Category}' is missing from Settings.");
        if (string.IsNullOrWhiteSpace(group.SubCategory) || !ContainsName(masters.SubCategories, group.SubCategory))
            blockers.Add($"Version (Sub-Category) '{group.SubCategory}' is missing from Settings.");
        if (string.IsNullOrWhiteSpace(group.FeedType) || !ContainsName(masters.FeedTypes, group.FeedType))
            blockers.Add($"Feed Type '{group.FeedType}' is missing from Settings.");
        if (string.IsNullOrWhiteSpace(group.Species) || !ContainsName(masters.Species, group.Species))
            blockers.Add($"Species '{group.Species}' is missing from Settings.");
        if (string.IsNullOrWhiteSpace(group.Size) || !ContainsName(masters.Sizes, group.Size))
            blockers.Add($"Size '{group.Size}' is missing from Settings.");

        foreach (var line in group.Ingredients)
        {
            if (!ContainsName(masters.RawIngredients, line.RawIngredientName))
                blockers.Add($"Raw material '{line.RawIngredientName}' is not in the system.");
        }

        var total = group.Ingredients.Sum(i => i.InclusionPercent);
        if (Math.Abs(total - 100m) > FormulationRules.InclusionTolerance)
            blockers.Add($"Inclusions sum to {total:0.##}% (must be 100% ± {FormulationRules.InclusionTolerance}).");

        if (blockers.Count > 0)
        {
            return new FormulaImportCodeResult(
                group.Code,
                FormulaImportDisposition.Blocked,
                $"Blocked: {blockers[0]}",
                blockers,
                [],
                group,
                null);
        }

        if (!existingByCode.TryGetValue(group.Code, out var existing))
        {
            return new FormulaImportCodeResult(
                group.Code,
                FormulaImportDisposition.New,
                $"New formula {group.Code} ({group.FeedType} · {group.Size}).",
                [],
                [],
                group,
                null);
        }

        var diffs = BuildDiffs(existing, group);
        if (diffs.Count == 0)
        {
            return new FormulaImportCodeResult(
                group.Code,
                FormulaImportDisposition.Same,
                $"Identical to existing {group.Code} — no write needed.",
                [],
                [],
                group,
                existing.Id);
        }

        return new FormulaImportCodeResult(
            group.Code,
            FormulaImportDisposition.Different,
            $"Code {group.Code} exists but differs — confirm to overwrite.",
            [],
            diffs,
            group,
            existing.Id);
    }

    public static IReadOnlyList<string> BuildDiffs(ExistingFormulaSnapshot existing, FormulaImportGroup sheet)
    {
        var diffs = new List<string>();
        CompareField(diffs, "Category", existing.CategoryName, sheet.Category);
        CompareField(diffs, "Version", existing.SubCategoryName, sheet.SubCategory);
        CompareField(diffs, "Feed type", existing.FeedTypeName, sheet.FeedType);
        CompareField(diffs, "Species", existing.SpeciesName, sheet.Species);
        CompareField(diffs, "Size", existing.SizeName, sheet.Size);
        CompareField(diffs, "Rev", NormalizeRev(existing.Revision), NormalizeRev(sheet.Revision));

        var existingIng = existing.Ingredients
            .GroupBy(i => i.RawIngredientName.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.InclusionPercent), StringComparer.OrdinalIgnoreCase);
        var sheetIng = sheet.Ingredients
            .ToDictionary(i => i.RawIngredientName, i => i.InclusionPercent, StringComparer.OrdinalIgnoreCase);

        foreach (var key in existingIng.Keys.Union(sheetIng.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            existingIng.TryGetValue(key, out var a);
            sheetIng.TryGetValue(key, out var b);
            if (Math.Abs(a - b) > FormulationRules.InclusionTolerance)
                diffs.Add($"RM '{key}': {a:0.##}% → {b:0.##}%");
        }

        return diffs;
    }

    private static void CompareField(List<string> diffs, string label, string existing, string sheet)
    {
        if (!string.Equals(existing.Trim(), sheet.Trim(), StringComparison.OrdinalIgnoreCase))
            diffs.Add($"{label}: '{existing}' → '{sheet}'");
    }

    private static bool ContainsName(IReadOnlySet<string> set, string name) =>
        set.Contains(name) || set.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeRev(string? revision)
    {
        if (string.IsNullOrWhiteSpace(revision)) return string.Empty;
        var t = revision.Trim();
        if (decimal.TryParse(t, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var n) &&
            n == Math.Truncate(n))
            return ((long)n).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return t;
    }
}
