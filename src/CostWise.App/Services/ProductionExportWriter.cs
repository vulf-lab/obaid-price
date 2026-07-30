using System.Globalization;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CostWise.App.Services;

public static class ProductionExportWriter
{
    private static readonly XLColor TitleSlate = XLColor.FromHtml("#2C3E50");
    private static readonly XLColor HeaderGray = XLColor.FromHtml("#E8EEF2");
    private static readonly XLColor BorderGray = XLColor.FromHtml("#B0BEC5");
    private static readonly XLColor ZebraGray = XLColor.FromHtml("#F5F7F9");
    private static readonly XLColor[] GroupBandColorsXl =
    [
        XLColor.FromHtml("#1A6B6B"),
        XLColor.FromHtml("#3D5A80"),
        XLColor.FromHtml("#8B6914"),
        XLColor.FromHtml("#4A7C59")
    ];

    private static readonly string[] GroupBandColorsHex =
    [
        "#1A6B6B",
        "#3D5A80",
        "#8B6914",
        "#4A7C59"
    ];

    private const string SlateHex = "#2C3E50";
    private const string HeaderGrayHex = "#E8EEF2";
    private const string BorderGrayHex = "#B0BEC5";
    private const string ZebraGrayHex = "#F5F7F9";

    public static void WriteExcel(ProductionExportSnapshot snapshot, string filePath)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Production");

        if (snapshot.Groups.Count == 0)
        {
            ws.Cell(1, 1).Value = "No production groups.";
            workbook.SaveAs(filePath);
            return;
        }

        WriteCombinedExcelSheet(ws, BuildCombinedMatrix(snapshot));
        workbook.SaveAs(filePath);
    }

    public static void WritePdf(ProductionExportSnapshot snapshot, string filePath)
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "PDF engine failed to initialize (QuestPDF native library). Excel export still works. " +
                "Try rebuilding with win-x64, or reinstall the app dependencies.",
                ex);
        }

        Document.Create(container =>
        {
            if (snapshot.Groups.Count == 0)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.Content().Text("No production groups.");
                });
                return;
            }

            var matrix = BuildCombinedMatrix(snapshot);
            var pageWidth = Math.Max(
                PageSizes.A4.Landscape().Width,
                120f + matrix.FlatColumns.Count * 42f);

            container.Page(page =>
            {
                page.Size(pageWidth, PageSizes.A4.Landscape().Height);
                page.Margin(18);
                page.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken3));

                page.Header().Element(c => DrawPdfTitle(c, matrix.Title));
                page.Content().PaddingTop(6).Element(c => DrawCombinedPdfTable(c, matrix));
            });
        }).GeneratePdf(filePath);
    }

    private sealed record FlatColumn(
        ProductionExportGroup Group,
        ProductionExportColumn Column,
        int ColumnIndex,
        int GroupBand);

    private sealed record CombinedMatrix(
        string Title,
        IReadOnlyList<FlatColumn> FlatColumns,
        IReadOnlyList<string> Ingredients,
        IReadOnlyDictionary<ProductionExportGroup, IReadOnlyDictionary<string, ProductionExportRow>> RowLookup,
        IReadOnlyList<(string Name, int StartIndex, int Count, int Band)> GroupSpans);

    private static CombinedMatrix BuildCombinedMatrix(ProductionExportSnapshot snapshot)
    {
        var flat = new List<FlatColumn>();
        var groupBandByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextBand = 0;
        foreach (var group in snapshot.Groups)
        {
            if (!groupBandByName.TryGetValue(group.Name, out var band))
            {
                band = nextBand++ % GroupBandColorsXl.Length;
                groupBandByName[group.Name] = band;
            }

            for (var i = 0; i < group.Columns.Count; i++)
                flat.Add(new FlatColumn(group, group.Columns[i], i, band));
        }

        var spans = new List<(string Name, int StartIndex, int Count, int Band)>();
        var iCol = 0;
        while (iCol < flat.Count)
        {
            var name = flat[iCol].Group.Name;
            var band = flat[iCol].GroupBand;
            var start = iCol;
            while (iCol + 1 < flat.Count &&
                   string.Equals(flat[iCol + 1].Group.Name, name, StringComparison.Ordinal))
                iCol++;
            spans.Add((name, start, iCol - start + 1, band));
            iCol++;
        }

        var ingredients = snapshot.Groups
            .SelectMany(g => g.Rows.Select(r => r.IngredientName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rowLookup = snapshot.Groups.ToDictionary(
            g => g,
            g => (IReadOnlyDictionary<string, ProductionExportRow>)g.Rows
                .ToDictionary(r => r.IngredientName, r => r, StringComparer.OrdinalIgnoreCase));

        return new CombinedMatrix(
            $"Updated Formulation  ·  {snapshot.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC",
            flat,
            ingredients,
            rowLookup,
            spans);
    }

    private static decimal? LookupPercent(
        CombinedMatrix matrix,
        FlatColumn flat,
        string ingredientName)
    {
        if (!matrix.RowLookup[flat.Group].TryGetValue(ingredientName, out var row))
            return null;
        if (flat.ColumnIndex >= row.Percents.Count)
            return null;
        return row.Percents[flat.ColumnIndex];
    }

    private static void WriteCombinedExcelSheet(IXLWorksheet ws, CombinedMatrix matrix)
    {
        var flat = matrix.FlatColumns;
        var lastCol = Math.Max(1, flat.Count + 1);
        const int titleRow = 1;
        const int groupHeaderRow = 2;
        const int categoryRow = 3;
        const int sizeRow = 4;
        const int codeRow = 5;
        const int dataStartRow = 6;

        ws.Range(titleRow, 1, titleRow, lastCol).Merge();
        ws.Cell(titleRow, 1).Value = matrix.Title;
        ws.Cell(titleRow, 1).Style.Font.SetBold();
        ws.Cell(titleRow, 1).Style.Font.SetFontColor(XLColor.White);
        ws.Cell(titleRow, 1).Style.Font.SetFontSize(14);
        ws.Cell(titleRow, 1).Style.Fill.SetBackgroundColor(TitleSlate);
        ws.Cell(titleRow, 1).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Cell(titleRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
        ws.Row(titleRow).Height = 24;

        ws.Range(groupHeaderRow, 1, codeRow, 1).Merge();
        ws.Cell(groupHeaderRow, 1).Value = "Ingredient";
        ws.Cell(groupHeaderRow, 1).Style.Font.SetBold();
        ws.Cell(groupHeaderRow, 1).Style.Font.SetFontColor(XLColor.White);
        ws.Cell(groupHeaderRow, 1).Style.Fill.SetBackgroundColor(TitleSlate);
        ws.Cell(groupHeaderRow, 1).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Cell(groupHeaderRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        for (var c = 0; c < flat.Count; c++)
        {
            var col = flat[c].Column;
            var excelCol = c + 2;
            ws.Cell(categoryRow, excelCol).Value = col.CategoryName;
            ws.Cell(sizeRow, excelCol).Value = col.SizeName;
            ws.Cell(codeRow, excelCol).Value = col.Code;
            ws.Column(excelCol).Width = 11.5;
        }

        foreach (var (name, start, count, band) in matrix.GroupSpans)
        {
            var startCol = start + 2;
            var endCol = start + count + 1;
            if (count > 1)
                ws.Range(groupHeaderRow, startCol, groupHeaderRow, endCol).Merge();

            var groupCell = ws.Cell(groupHeaderRow, startCol);
            groupCell.Value = name;
            groupCell.Style.Font.SetBold();
            groupCell.Style.Font.SetFontColor(XLColor.White);
            groupCell.Style.Fill.SetBackgroundColor(GroupBandColorsXl[band]);
            groupCell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            groupCell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        }

        if (flat.Count > 0)
        {
            var metaRange = ws.Range(categoryRow, 2, codeRow, lastCol);
            metaRange.Style.Fill.SetBackgroundColor(HeaderGray);
            metaRange.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            metaRange.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            metaRange.Style.Alignment.SetWrapText(true);
            ws.Range(categoryRow, 2, categoryRow, lastCol).Style.Font.SetBold();
            ws.Range(codeRow, 2, codeRow, lastCol).Style.Font.SetBold();
        }

        for (var r = 0; r < matrix.Ingredients.Count; r++)
        {
            var name = matrix.Ingredients[r];
            var excelRow = dataStartRow + r;
            var zebra = r % 2 == 1;

            ws.Cell(excelRow, 1).Value = name;
            if (zebra)
                ws.Cell(excelRow, 1).Style.Fill.SetBackgroundColor(ZebraGray);

            for (var c = 0; c < flat.Count; c++)
            {
                var cell = ws.Cell(excelRow, c + 2);
                cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                cell.Style.NumberFormat.Format = "0.##";
                if (zebra)
                    cell.Style.Fill.SetBackgroundColor(ZebraGray);

                if (LookupPercent(matrix, flat[c], name) is decimal pct)
                    cell.Value = pct;
            }
        }

        var totalRow = dataStartRow + matrix.Ingredients.Count;
        var totalRange = ws.Range(totalRow, 1, totalRow, lastCol);
        totalRange.Style.Font.SetBold();
        totalRange.Style.Font.SetFontColor(XLColor.White);
        totalRange.Style.Fill.SetBackgroundColor(TitleSlate);
        totalRange.Style.Border.SetTopBorder(XLBorderStyleValues.Medium);
        totalRange.Style.Border.SetTopBorderColor(TitleSlate);

        ws.Cell(totalRow, 1).Value = "Total %";
        for (var c = 0; c < flat.Count; c++)
        {
            var flatCol = flat[c];
            var cell = ws.Cell(totalRow, c + 2);
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            cell.Style.NumberFormat.Format = "0.##";
            if (flatCol.ColumnIndex < flatCol.Group.ColumnTotals.Count)
                cell.Value = flatCol.Group.ColumnTotals[flatCol.ColumnIndex];
        }

        var used = ws.Range(titleRow, 1, totalRow, lastCol);
        used.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        used.Style.Border.SetOutsideBorderColor(BorderGray);
        used.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        used.Style.Border.SetInsideBorderColor(BorderGray);

        for (var c = 0; c < flat.Count; c++)
        {
            if (c == 0 || !string.Equals(flat[c].Group.Name, flat[c - 1].Group.Name, StringComparison.Ordinal))
            {
                var excelCol = c + 2;
                ws.Range(groupHeaderRow, excelCol, totalRow, excelCol).Style.Border
                    .SetLeftBorder(XLBorderStyleValues.Medium);
                ws.Range(groupHeaderRow, excelCol, totalRow, excelCol).Style.Border
                    .SetLeftBorderColor(TitleSlate);
            }
        }

        ws.Column(1).Width = 28;
        ws.SheetView.FreezeRows(codeRow);
        ws.SheetView.FreezeColumns(1);

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.Left = 0.4;
        ws.PageSetup.Margins.Right = 0.4;
        ws.PageSetup.Margins.Top = 0.4;
        ws.PageSetup.Margins.Bottom = 0.4;
    }

    private static void DrawPdfTitle(IContainer container, string title)
    {
        container
            .Background(SlateHex)
            .PaddingVertical(8)
            .PaddingHorizontal(10)
            .Text(title)
            .SemiBold()
            .FontSize(12)
            .FontColor(Colors.White);
    }

    private static void DrawCombinedPdfTable(IContainer container, CombinedMatrix matrix)
    {
        var flat = matrix.FlatColumns;
        var colCount = Math.Max(1, flat.Count) + 1;

        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2.4f);
                for (var i = 1; i < colCount; i++)
                    cols.RelativeColumn();
            });

            table.Header(header =>
            {
                // Ingredient spans the 4 header rows.
                header.Cell().RowSpan(4u).Element(c =>
                    SlateCorner(c).AlignCenter().AlignMiddle().Text("Ingredient").SemiBold().FontColor(Colors.White).FontSize(8));

                if (flat.Count == 0)
                {
                    header.Cell().RowSpan(4u).Element(MetaHeader).AlignCenter().Text("—");
                    return;
                }

                foreach (var (name, _, count, band) in matrix.GroupSpans)
                {
                    header.Cell().ColumnSpan((uint)count).Element(c =>
                        GroupHeader(c, band).AlignCenter().AlignMiddle()
                            .Text(name).SemiBold().FontColor(Colors.White).FontSize(8));
                }

                foreach (var flatCol in flat)
                    header.Cell().Element(MetaHeader).AlignCenter().Text(flatCol.Column.CategoryName).SemiBold().FontSize(6.5f);

                foreach (var flatCol in flat)
                    header.Cell().Element(MetaHeader).AlignCenter().Text(flatCol.Column.SizeName).FontSize(6.5f);

                foreach (var flatCol in flat)
                    header.Cell().Element(MetaHeader).AlignCenter().Text(flatCol.Column.Code).SemiBold().FontSize(6.5f);
            });

            for (var r = 0; r < matrix.Ingredients.Count; r++)
            {
                var name = matrix.Ingredients[r];
                var zebra = r % 2 == 1;
                headerBodyCell(table, zebra).Text(name).FontSize(7);

                foreach (var flatCol in flat)
                {
                    var pct = LookupPercent(matrix, flatCol, name);
                    headerBodyCell(table, zebra).AlignCenter()
                        .Text(pct is null ? "" : pct.Value.ToString("0.##", CultureInfo.InvariantCulture));
                }
            }

            table.Cell().Element(TotalCell).Text("Total %").SemiBold().FontColor(Colors.White);
            foreach (var flatCol in flat)
            {
                var total = flatCol.ColumnIndex < flatCol.Group.ColumnTotals.Count
                    ? flatCol.Group.ColumnTotals[flatCol.ColumnIndex]
                    : 0m;
                table.Cell().Element(TotalCell).AlignCenter()
                    .Text(total.ToString("0.##", CultureInfo.InvariantCulture)).SemiBold().FontColor(Colors.White);
            }
        });

        static IContainer headerBodyCell(TableDescriptor table, bool zebra) =>
            table.Cell().Element(c => BodyCell(c, zebra));
    }

    private static IContainer SlateCorner(IContainer c) =>
        c.Border(0.5f).BorderColor(BorderGrayHex).Background(SlateHex).Padding(3);

    private static IContainer GroupHeader(IContainer c, int band) =>
        c.Border(0.5f).BorderColor(BorderGrayHex)
            .Background(GroupBandColorsHex[band % GroupBandColorsHex.Length])
            .Padding(3);

    private static IContainer MetaHeader(IContainer c) =>
        c.Border(0.5f).BorderColor(BorderGrayHex).Background(HeaderGrayHex).Padding(2);

    private static IContainer BodyCell(IContainer c, bool zebra) =>
        c.Border(0.5f).BorderColor(BorderGrayHex)
            .Background(zebra ? ZebraGrayHex : Colors.White)
            .Padding(2);

    private static IContainer TotalCell(IContainer c) =>
        c.Border(0.5f).BorderColor(BorderGrayHex).Background(SlateHex).Padding(3);
}
