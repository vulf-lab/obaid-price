namespace CostWise.Core.Services.Import;

public sealed record RmPriceImportRow(string Name, decimal? PricePerMt, int ExcelRow);

public enum RmImportIssueKind
{
    MissingFromFile,
    MissingPrice,
    NewIngredient,
    ReactivatingPrice,
    WillUpdate
}

public sealed record RmImportIssue(
    RmImportIssueKind Kind,
    string Message,
    string Name,
    decimal? SheetPrice = null,
    decimal? CurrentPrice = null,
    bool? CurrentAvailable = null);

public sealed record RmImportPreview(
    IReadOnlyList<RmImportIssue> Issues,
    IReadOnlyList<RmPriceImportRow> RowsToUpdate,
    IReadOnlyList<RmPriceImportRow> RowsToCreate,
    int MatchedUnchangedCount)
{
    public bool HasWarnings => Issues.Any(i =>
        i.Kind is RmImportIssueKind.MissingFromFile
            or RmImportIssueKind.MissingPrice
            or RmImportIssueKind.NewIngredient
            or RmImportIssueKind.ReactivatingPrice);

    public int ActionCount => RowsToUpdate.Count + RowsToCreate.Count;

    public string SummaryText =>
        $"{ActionCount} change(s): {RowsToUpdate.Count} update(s), {RowsToCreate.Count} new; " +
        $"{MatchedUnchangedCount} unchanged; {Issues.Count} note(s).";
}

public sealed record ExistingRawIngredientSnapshot(
    int Id,
    string Name,
    decimal PricePerMt,
    bool IsAvailable);

public static class RawMaterialPriceImportValidator
{
    public static RmImportPreview BuildPreview(
        IReadOnlyList<RmPriceImportRow> sheetRows,
        IReadOnlyList<ExistingRawIngredientSnapshot> existing)
    {
        var byName = existing.ToDictionary(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase);
        var sheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var issues = new List<RmImportIssue>();
        var toUpdate = new List<RmPriceImportRow>();
        var toCreate = new List<RmPriceImportRow>();
        var unchanged = 0;

        foreach (var row in sheetRows)
        {
            var name = row.Name.Trim();
            if (string.IsNullOrEmpty(name)) continue;

            sheetNames.Add(name);

            if (row.PricePerMt is null)
            {
                issues.Add(new RmImportIssue(
                    RmImportIssueKind.MissingPrice,
                    $"Row {row.ExcelRow}: '{name}' has a missing/invalid price — will be skipped.",
                    name));
                continue;
            }

            var price = row.PricePerMt.Value;
            if (!byName.TryGetValue(name, out var current))
            {
                issues.Add(new RmImportIssue(
                    RmImportIssueKind.NewIngredient,
                    $"New raw ingredient '{name}' @ {price:0.##} KES/MT — will be created.",
                    name,
                    SheetPrice: price));
                toCreate.Add(row with { Name = name, PricePerMt = price });
                continue;
            }

            var wasZeroOrUnavailable = current.PricePerMt == 0m || !current.IsAvailable;
            if (wasZeroOrUnavailable && price > 0m)
            {
                issues.Add(new RmImportIssue(
                    RmImportIssueKind.ReactivatingPrice,
                    $"'{current.Name}': was {(current.IsAvailable ? "available" : "unavailable")} @ {current.PricePerMt:0.##} → {price:0.##} KES/MT.",
                    current.Name,
                    SheetPrice: price,
                    CurrentPrice: current.PricePerMt,
                    CurrentAvailable: current.IsAvailable));
                toUpdate.Add(row with { Name = current.Name, PricePerMt = price });
                continue;
            }

            if (current.PricePerMt != price)
            {
                issues.Add(new RmImportIssue(
                    RmImportIssueKind.WillUpdate,
                    $"'{current.Name}': {current.PricePerMt:0.##} → {price:0.##} KES/MT.",
                    current.Name,
                    SheetPrice: price,
                    CurrentPrice: current.PricePerMt,
                    CurrentAvailable: current.IsAvailable));
                toUpdate.Add(row with { Name = current.Name, PricePerMt = price });
            }
            else
            {
                unchanged++;
            }
        }

        foreach (var entity in existing.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (sheetNames.Contains(entity.Name)) continue;
            issues.Add(new RmImportIssue(
                RmImportIssueKind.MissingFromFile,
                $"System RM '{entity.Name}' is not in the file — price will be left unchanged.",
                entity.Name,
                CurrentPrice: entity.PricePerMt,
                CurrentAvailable: entity.IsAvailable));
        }

        return new RmImportPreview(issues, toUpdate, toCreate, unchanged);
    }
}
