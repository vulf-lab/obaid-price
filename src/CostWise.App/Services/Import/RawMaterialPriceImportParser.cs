using System.Globalization;
using ClosedXML.Excel;
using CostWise.Core.Services.Import;

namespace CostWise.App.Services.Import;

public static class RawMaterialPriceImportParser
{
    public static IReadOnlyList<RmPriceImportRow> Parse(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var sheet = workbook.Worksheets.First();
        var map = ReadHeaderMap(sheet);
        var nameCol = FindColumn(map, "Name", "RM/Description", "Raw material", "Raw Material");
        var priceCol = FindColumn(map, "Price", "PricePerMt", "KES/MT", "Price / MT", "Price/MT");

        var rows = new List<RmPriceImportRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var name = sheet.Cell(r, nameCol).GetFormattedString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var priceCell = sheet.Cell(r, priceCol);
            decimal? price = null;
            if (!priceCell.IsEmpty())
            {
                if (priceCell.DataType == XLDataType.Number)
                    price = Convert.ToDecimal(priceCell.GetDouble(), CultureInfo.InvariantCulture);
                else if (decimal.TryParse(
                             priceCell.GetFormattedString().Trim().Replace(",", ""),
                             NumberStyles.Any,
                             CultureInfo.InvariantCulture,
                             out var parsed))
                    price = parsed;
            }

            rows.Add(new RmPriceImportRow(name, price, r));
        }

        return rows;
    }

    private static Dictionary<string, int> ReadHeaderMap(IXLWorksheet sheet)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
            map[cell.GetString().Trim()] = cell.Address.ColumnNumber;
        return map;
    }

    private static int FindColumn(Dictionary<string, int> map, params string[] names)
    {
        foreach (var name in names)
        {
            if (map.TryGetValue(name, out var col))
                return col;
        }

        throw new InvalidOperationException(
            $"Missing column (tried: {string.Join(", ", names)}). Found: {string.Join(", ", map.Keys)}");
    }
}
