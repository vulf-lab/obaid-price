using System.Diagnostics;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CostWise.App.Services;

public static class ComparisonPrintService
{
    public static void WriteProfileComparisonPdf(
        string filePath,
        IReadOnlyList<string> codes,
        IReadOnlyList<(string Nutrient, string Unit, IReadOnlyList<string> Values)> rows)
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "PDF engine failed to initialize (QuestPDF native library). " +
                "Try rebuilding with win-x64, or reinstall the app dependencies.",
                ex);
        }

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Text("Nutrition profile comparison").SemiBold().FontSize(14).FontColor(Colors.BlueGrey.Darken3);
                    col.Item().PaddingTop(2).Text($"Codes: {string.Join(", ", codes)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    var codeCount = Math.Max(codes.Count, 1);
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.2f);
                        columns.ConstantColumn(42);
                        for (var i = 0; i < codeCount; i++)
                            columns.RelativeColumn(1);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Nutrient").SemiBold();
                        header.Cell().Element(HeaderCell).Text("Unit").SemiBold();
                        foreach (var code in codes)
                            header.Cell().Element(HeaderCell).AlignCenter().Text(code).SemiBold().FontSize(7);
                    });

                    var zebra = false;
                    foreach (var row in rows)
                    {
                        var bg = zebra ? Colors.Grey.Lighten4 : Colors.White;
                        zebra = !zebra;
                        table.Cell().Element(c => BodyCell(c, bg)).Text(row.Nutrient);
                        table.Cell().Element(c => BodyCell(c, bg)).Text(row.Unit);
                        for (var i = 0; i < codes.Count; i++)
                        {
                            var text = i < row.Values.Count ? row.Values[i] : string.Empty;
                            table.Cell().Element(c => BodyCell(c, bg)).AlignCenter().Text(text);
                        }
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf(filePath);
    }

    public static void OpenFile(string path) =>
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });

    private static IContainer HeaderCell(IContainer container) =>
        container
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten1)
            .Background(Colors.Grey.Lighten3)
            .Padding(3);

    private static IContainer BodyCell(IContainer container, string background) =>
        container
            .Background(background)
            .BorderBottom(0.25f)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(2)
            .PaddingHorizontal(3);
}
