using CostWise.Core.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CostWise.App.Services;

public static class CommercialPriceListWriter
{
    public static readonly string[] AllColumns = ["Category", "Feed type", "Size", "CP", "Fat", "Sale"];

    public static void WritePdf(CommercialBrief brief, string filePath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var unitLabel = brief.SellUnit == PriceUnit.PerBag25Kg ? "Per bag" : "Per MT";
        var currencyLabel = PriceListNumberFormat.CurrencyLabel(brief.CurrencyCode);
        var saleHeader = $"Sale ({currencyLabel} {unitLabel})";
        var cols = brief.VisibleColumns
            .Where(c => AllColumns.Contains(c, StringComparer.Ordinal))
            .ToList();
        if (cols.Count == 0)
            cols = AllColumns.ToList();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken3));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(brief.ListName).SemiBold().FontSize(14).FontColor(Colors.BlueGrey.Darken4);
                        col.Item().Text($"Effective {brief.EffectiveDate:dd MMM yyyy}  ·  {brief.CurrencyCode}  ·  {unitLabel}")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                        col.Item().Text(brief.TransportIncluded
                                ? "Delivery Terms: Delivered"
                                : "Delivery Terms: Ex-Factory")
                            .FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                    row.ConstantItem(72).Height(40).Element(e =>
                    {
                        if (brief.Logo is { Length: > 0 } bytes)
                            e.AlignRight().Image(bytes);
                    });
                });

                var preferred = PriceListPdfLayout.PreferredWidth(cols.Select(ColumnWidth));
                PriceListPdfLayout.TableHost(page.Content().PaddingTop(12), preferred).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        foreach (var name in cols)
                            c.RelativeColumn(ColumnWidth(name));
                    });

                    t.Header(h =>
                    {
                        foreach (var name in cols)
                        {
                            var title = name == "Sale" ? saleHeader : name;
                            Header(h.Cell(), title);
                        }
                    });

                    foreach (var r in brief.Rows)
                    {
                        var sell = brief.SellUnit == PriceUnit.PerBag25Kg ? r.SellBag : r.SellMt;
                        foreach (var name in cols)
                        {
                            var cell = t.Cell().Element(CellBody);
                            switch (name)
                            {
                                case "Category":
                                    cell.Text(r.CategoryName);
                                    break;
                                case "Feed type":
                                    cell.Text(r.FeedTypeName);
                                    break;
                                case "Size":
                                    cell.Text(r.SizeName);
                                    break;
                                case "CP":
                                    cell.AlignCenter().Text(PriceListNumberFormat.FormatNutrient(
                                        PriceListNumberFormat.ApplyRound(r.ProteinTarget, brief.RoundCpTo)));
                                    break;
                                case "Fat":
                                    cell.AlignCenter().Text(PriceListNumberFormat.FormatNutrient(
                                        PriceListNumberFormat.ApplyRound(r.FatTarget, brief.RoundFatTo)));
                                    break;
                                case "Sale":
                                    var roundedSell = sell is null
                                        ? null
                                        : (decimal?)PriceListNumberFormat.ApplyRound(sell.Value, brief.RoundSaleTo);
                                    cell.AlignRight()
                                        .Text(PriceListNumberFormat.FormatMoney(roundedSell, brief.CurrencyCode));
                                    break;
                            }
                        }
                    }
                });
            });
        }).GeneratePdf(filePath);
    }

    private static float ColumnWidth(string name) => name switch
    {
        "Category" => 2.0f,
        "Feed type" => 2.2f,
        "Size" => 1.0f,
        "CP" or "Fat" => 0.9f,
        "Sale" => 1.3f,
        _ => 1f
    };

    private static void Header(IContainer cell, string text) =>
        cell.Background(Colors.Grey.Lighten3).Padding(4).Text(text).SemiBold().FontSize(8);

    private static IContainer CellBody(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
}
