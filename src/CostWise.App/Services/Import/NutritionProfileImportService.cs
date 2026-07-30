using System.Globalization;
using ClosedXML.Excel;
using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.Services.Import;

public sealed record NutritionImportIssue(string Severity, string Message);

public sealed record NutritionImportPreview(
    IReadOnlyList<NutritionImportIssue> Issues,
    IReadOnlyList<string> CodesToUpdate,
    IReadOnlyList<string> CodesMissingInDb,
    int NutrientColumnsMapped,
    int CellUpdates)
{
    public string SummaryText =>
        $"{CodesToUpdate.Count} code(s) will update ({CellUpdates} target cell(s)); " +
        $"{CodesMissingInDb.Count} code(s) in file not in DB; {NutrientColumnsMapped} nutrient column(s) mapped.";

    public bool CanApply => CodesToUpdate.Count > 0;
}

public static class NutritionProfileImportService
{
    /// <summary>Excel header (trimmed, case-insensitive) → SpecParameter.Name</summary>
    public static readonly IReadOnlyDictionary<string, string> HeaderToSpecName =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MOISTURE"] = "Moisture",
            ["PROTEIN"] = "Protein",
            ["DP TILAPIA"] = "Digestible Protein Tilapia",
            ["FAT"] = "Fat",
            ["FIBER"] = "Fiber",
            ["DE TILAPIA"] = "DE Tilapia Fish",
            ["CALCIUM"] = "Calcium",
            ["ASH"] = "Ash",
            ["STARCH"] = "Starch",
            ["DP:DE"] = "DP:DE",
            ["AV PHOSPHORUS"] = "Av Phosphorus Aqua",
            ["Ca:P"] = "Calcium/ Phosphorus Aqua",
            ["AV LYSINE"] = "Dig Lys Aqua",
            ["AV METHIONINE"] = "Dig Met Aqua",
            ["AV THREONINE"] = "Dig Thr Aqua",
            ["VITAMINE A"] = "Vitamine A",
            ["VITAMINE C"] = "Vitamine C",
            ["VITAMINE D3"] = "Vitamine D3",
            ["VITAMINE E"] = "Vitamine E",
            ["MET SID / LYS SID Aqua"] = "MET SID / LYS SID Aqua",
            ["THR SID / LYS SID Aqua"] = "THR SID / LYS SID Aqua"
        };

    public static async Task<NutritionImportPreview> PreviewAsync(
        CostWiseDbContext db,
        string filePath,
        CancellationToken ct = default)
    {
        var sheet = ParseSheet(filePath);
        var formulations = await db.Formulations.AsNoTracking()
            .Select(f => new { f.Id, f.Code })
            .ToListAsync(ct);
        var byCode = formulations.ToDictionary(f => f.Code, f => f.Id, StringComparer.OrdinalIgnoreCase);
        var specs = await db.SpecParameters.AsNoTracking().ToListAsync(ct);
        var specByName = specs.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        var issues = new List<NutritionImportIssue>();
        var toUpdate = new List<string>();
        var missing = new List<string>();
        var cellUpdates = 0;
        var mappedCols = sheet.MappedColumns.Count;

        foreach (var header in sheet.UnmappedHeaders)
            issues.Add(new NutritionImportIssue("Warning", $"Unmapped column '{header}' will be ignored."));

        foreach (var row in sheet.Rows)
        {
            if (!byCode.ContainsKey(row.Code))
            {
                missing.Add(row.Code);
                issues.Add(new NutritionImportIssue("Warning", $"Code '{row.Code}' is not in the system — skipped."));
                continue;
            }

            var updatesForCode = 0;
            foreach (var (specName, value) in row.Targets)
            {
                if (!specByName.ContainsKey(specName))
                {
                    issues.Add(new NutritionImportIssue("Warning",
                        $"Nutrient '{specName}' missing from Settings — skipped for {row.Code}."));
                    continue;
                }

                updatesForCode++;
            }

            if (updatesForCode > 0)
            {
                toUpdate.Add(row.Code);
                cellUpdates += updatesForCode;
            }
        }

        return new NutritionImportPreview(issues, toUpdate, missing, mappedCols, cellUpdates);
    }

    public static async Task<int> ApplyAsync(
        CostWiseDbContext db,
        string filePath,
        CancellationToken ct = default)
    {
        var sheet = ParseSheet(filePath);
        var formulations = await db.Formulations
            .Include(f => f.Specs)
            .ToListAsync(ct);
        var byCode = formulations.ToDictionary(f => f.Code, StringComparer.OrdinalIgnoreCase);
        var specs = await db.SpecParameters.ToListAsync(ct);
        var specByName = specs.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        var changed = 0;
        foreach (var row in sheet.Rows)
        {
            if (!byCode.TryGetValue(row.Code, out var formulation))
                continue;

            foreach (var (specName, value) in row.Targets)
            {
                if (!specByName.TryGetValue(specName, out var parameter))
                    continue;

                var existing = formulation.Specs.FirstOrDefault(s => s.SpecParameterId == parameter.Id);
                if (existing is null)
                {
                    formulation.Specs.Add(new FormulationSpec
                    {
                        SpecParameterId = parameter.Id,
                        TargetValue = value
                    });
                    changed++;
                }
                else if (existing.TargetValue != value)
                {
                    existing.TargetValue = value;
                    changed++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return changed;
    }

    private static ParsedSheet ParseSheet(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var ws = workbook.Worksheets.First();
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in ws.Row(1).CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(text))
                headerMap[text] = cell.Address.ColumnNumber;
        }

        if (!headerMap.TryGetValue("CODE", out var codeCol))
            throw new InvalidOperationException("Missing CODE column.");

        var mapped = new List<(string SpecName, int Col)>();
        var unmapped = new List<string>();
        foreach (var (header, col) in headerMap)
        {
            if (string.Equals(header, "CODE", StringComparison.OrdinalIgnoreCase))
                continue;
            var key = header.Trim();
            if (HeaderToSpecName.TryGetValue(key, out var specName))
                mapped.Add((specName, col));
            else
                unmapped.Add(header);
        }

        var rows = new List<ParsedRow>();
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= last; r++)
        {
            var code = ws.Cell(r, codeCol).GetFormattedString().Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;

            var targets = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var (specName, col) in mapped)
            {
                var cell = ws.Cell(r, col);
                if (cell.IsEmpty()) continue;
                if (TryParseDecimal(cell, out var value))
                    targets[specName] = value;
            }

            rows.Add(new ParsedRow(code, targets));
        }

        return new ParsedSheet(rows, mapped, unmapped);
    }

    private static bool TryParseDecimal(IXLCell cell, out decimal value)
    {
        if (cell.DataType == XLDataType.Number)
        {
            value = Convert.ToDecimal(cell.GetDouble(), CultureInfo.InvariantCulture);
            return true;
        }

        var text = cell.GetFormattedString().Trim().Replace(",", "");
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private sealed record ParsedRow(string Code, Dictionary<string, decimal> Targets);

    private sealed record ParsedSheet(
        IReadOnlyList<ParsedRow> Rows,
        IReadOnlyList<(string SpecName, int Col)> MappedColumns,
        IReadOnlyList<string> UnmappedHeaders);
}
