using System.Globalization;
using ClosedXML.Excel;
using CostWise.Core.Services.Import;

namespace CostWise.App.Services.Import;

public static class FormulationImportParser
{
    public static IReadOnlyList<FormulaImportSheetLine> Parse(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
            map[cell.GetString().Trim()] = cell.Address.ColumnNumber;

        Require(map, "Category", "Size", "Feed Type", "Code", "Rev", "Sub-Category", "RM/Description", "%");
        var specieCol = map.ContainsKey("Specie") ? map["Specie"]
            : map.ContainsKey("Species") ? map["Species"]
            : throw new InvalidOperationException("Missing column 'Specie' or 'Species'.");

        var rows = new List<FormulaImportSheetLine>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var code = CellText(sheet, r, map["Code"]);
            if (string.IsNullOrWhiteSpace(code)) continue;

            rows.Add(new FormulaImportSheetLine(
                Category: CellText(sheet, r, map["Category"]),
                Size: CellText(sheet, r, map["Size"]),
                Species: CellText(sheet, r, specieCol),
                FeedType: CellText(sheet, r, map["Feed Type"]),
                Code: code,
                Revision: NormalizeRev(sheet.Cell(r, map["Rev"]).Value),
                SubCategory: CellText(sheet, r, map["Sub-Category"]),
                RawIngredient: CellText(sheet, r, map["RM/Description"]),
                Percent: ParsePercent(sheet.Cell(r, map["%"]).Value),
                ExcelRow: r));
        }

        return rows;
    }

    private static void Require(Dictionary<string, int> map, params string[] names)
    {
        foreach (var name in names)
        {
            if (!map.ContainsKey(name))
                throw new InvalidOperationException(
                    $"Missing column '{name}'. Found: {string.Join(", ", map.Keys)}");
        }
    }

    private static string CellText(IXLWorksheet sheet, int row, int col) =>
        sheet.Cell(row, col).GetFormattedString().Trim();

    private static string NormalizeRev(XLCellValue value)
    {
        if (value.IsBlank) return string.Empty;
        if (value.IsNumber)
        {
            var n = value.GetNumber();
            return n == Math.Truncate(n)
                ? ((long)n).ToString(CultureInfo.InvariantCulture)
                : n.ToString(CultureInfo.InvariantCulture);
        }

        return FormulationImportValidator.NormalizeRev(value.ToString());
    }

    private static decimal ParsePercent(XLCellValue value)
    {
        if (value.IsNumber) return Convert.ToDecimal(value.GetNumber(), CultureInfo.InvariantCulture);
        var text = value.ToString().Trim().TrimEnd('%');
        if (string.IsNullOrEmpty(text)) return 0m;
        return decimal.Parse(text, CultureInfo.InvariantCulture);
    }
}
