using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CostWise.App.Services;

public static class VictoryGroupPriceListWriter
{
    public static void WritePdf(VictoryGroupBrief brief, string filePath)
    {
        CreateDocument(brief).GeneratePdf(filePath);
    }

    /// <summary>Renders each PDF page as a PNG byte array for in-app preview.</summary>
    public static IReadOnlyList<byte[]> RenderPreviewImages(VictoryGroupBrief brief, int dpi = 144)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return CreateDocument(brief).GenerateImages(new ImageGenerationSettings
        {
            ImageFormat = ImageFormat.Png,
            RasterDpi = dpi
        }).ToList();
    }

    private static Document CreateDocument(VictoryGroupBrief brief)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken3));

                page.Header().Row(row =>
                {
                    row.ConstantItem(72).Height(40).Element(e =>
                    {
                        if (brief.LogoLeft is { Length: > 0 } bytes)
                            e.Width(72).Height(36).Image(bytes).FitArea();
                    });
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignCenter().Text("Victory Group — Pricing brief")
                            .SemiBold().FontSize(12).FontColor(Colors.BlueGrey.Darken4);
                        col.Item().AlignCenter().Text(DateTime.Now.ToString("dd MMM yyyy", CultureInfo.InvariantCulture))
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                    row.ConstantItem(72).Height(40).Element(e =>
                    {
                        if (brief.LogoRight is { Length: > 0 } bytes)
                            e.Width(72).Height(36).Image(bytes).FitArea();
                    });
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(6);

                    if (brief.RmChanges.Count > 0)
                    {
                        col.Item().Text("Raw material price changes").SemiBold().FontSize(9);
                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                                c.RelativeColumn(1);
                            });
                            t.Header(h =>
                            {
                                HeaderCell(h.Cell(), "Raw material");
                                HeaderCell(h.Cell(), "Previous");
                                HeaderCell(h.Cell(), "Current");
                                HeaderCell(h.Cell(), "Δ %");
                            });
                            foreach (var r in brief.RmChanges)
                            {
                                t.Cell().Element(CellBody).Text(r.Name);
                                t.Cell().Element(CellBody).AlignRight().Text(PriceListNumberFormat.FormatMoney(r.PreviousKes, "KES"));
                                t.Cell().Element(CellBody).AlignRight().Text(PriceListNumberFormat.FormatMoney(r.CurrentKes, "KES"));
                                WriteDeltaPercentCell(t.Cell().Element(CellBody).AlignRight(), r.DeltaPercent, brief.RoundDeltaPercentTo);
                            }
                        });
                    }

                    WriteBookTable(
                        col, brief.BookAName, brief.BookARows, brief.VisibleColumns, brief.RoundBookATo,
                        brief.ShowSellCompare, brief.BookAMarginPercent, brief.CompareBookAMarginPercent,
                        brief.RoundDeltaPercentTo);
                    WriteBookTable(
                        col, brief.BookBName, brief.BookBRows, brief.VisibleColumns, brief.RoundBookBTo,
                        brief.ShowSellCompare, brief.BookBMarginPercent, brief.CompareBookBMarginPercent,
                        brief.RoundDeltaPercentTo);

                    if (!string.IsNullOrWhiteSpace(brief.Commentary))
                    {
                        col.Item().Text("Commentary").SemiBold().FontSize(9);
                        col.Item().Text(brief.Commentary).FontSize(8);
                    }
                });
            });
        });
    }

    private static void WriteBookTable(
        ColumnDescriptor col,
        string title,
        IReadOnlyList<PriceListBookRow> rows,
        IReadOnlyList<string> visible,
        decimal roundSellTo,
        bool showCompare,
        decimal currentMargin,
        decimal? compareMargin,
        decimal roundDeltaTo)
    {
        col.Item().Text(string.IsNullOrWhiteSpace(title) ? "Price book" : title).SemiBold().FontSize(9);
        if (rows.Count == 0)
        {
            col.Item().Text("No formulas.").Italic().FontColor(Colors.Grey.Medium);
            return;
        }

        var cols = visible.Where(c => PriceListBriefBuilder.AllExportColumns.Contains(c)).ToList();
        if (cols.Count == 0)
            cols = PriceListBriefBuilder.DefaultVisibleColumns.ToList();

        if (showCompare && cols.Contains("Sell / MT"))
        {
            var sellIdx = cols.IndexOf("Sell / MT");
            cols.Insert(sellIdx + 1, "Last Price");
            cols.Insert(sellIdx + 2, "Change %");
        }

        var preferred = PriceListPdfLayout.PreferredWidth(cols.Select(_ => 1f), pointsPerWeight: 52f);
        PriceListPdfLayout.TableHost(col.Item(), preferred).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                foreach (var _ in cols)
                    c.RelativeColumn();
            });
            t.Header(h =>
            {
                foreach (var name in cols)
                    HeaderCell(h.Cell(), HeaderLabel(name, showCompare, currentMargin, compareMargin));
            });
            foreach (var r in rows)
            {
                foreach (var name in cols)
                {
                    var cell = t.Cell().Element(CellBody).AlignCenter();
                    if (name == "Change %")
                        WriteDeltaPercentCell(cell, r.SellChangePercent, roundDeltaTo);
                    else
                        cell.Text(CellValue(r, name, roundSellTo)).FontSize(6.5f);
                }
            }
        });
    }

    private static string HeaderLabel(
        string name, bool showCompare, decimal currentMargin, decimal? compareMargin) => name switch
    {
        "Sell / MT" when showCompare => $"Selling Price {FormatMargin(currentMargin)}",
        "Last Price" => compareMargin is decimal m
            ? $"Last Price {FormatMargin(m)}"
            : "Last Price",
        _ => name
    };

    private static string FormatMargin(decimal margin) =>
        $"{margin.ToString("0.##", CultureInfo.InvariantCulture)}%";

    private static void WriteDeltaPercentCell(IContainer cell, decimal? delta, decimal roundTo)
    {
        if (delta is null)
        {
            cell.Text("—").FontSize(6.5f);
            return;
        }

        var value = PriceListNumberFormat.ApplyRound(delta.Value, roundTo);
        var text = FormatDeltaPercent(value);
        var color = value > 0m ? Colors.Red.Medium
            : value < 0m ? Colors.Green.Medium
            : Colors.Grey.Darken3;
        cell.Text(text).FontSize(6.5f).FontColor(color);
    }

    /// <summary>▲ 2.1% (up/red), ▼ 4.2% (down/green), 0.0% (flat).</summary>
    public static string FormatDeltaPercent(decimal value)
    {
        var abs = Math.Abs(value).ToString("0.0", CultureInfo.InvariantCulture);
        if (value > 0m) return $"▲ {abs}%";
        if (value < 0m) return $"▼ {abs}%";
        return "0.0%";
    }

    private static string CellValue(PriceListBookRow r, string col, decimal roundSellTo) => col switch
    {
        "Code" => r.Code,
        "Production" => r.IsInProduction ? "In" : "Out",
        "Category" => r.CategoryName,
        "Feed type" => r.FeedTypeName,
        "Version" => r.SubCategoryName,
        "Size" => r.SizeName,
        "Raw Material" => PriceListNumberFormat.FormatMoney(r.RmCost, r.CurrencyCode),
        "Conversion" => PriceListNumberFormat.FormatMoney(r.ConversionCost, r.CurrencyCode),
        "Packing cost" => PriceListNumberFormat.FormatMoney(r.PackingCost, r.CurrencyCode),
        "Additive" => PriceListNumberFormat.FormatMoney(r.AdditiveCost, r.CurrencyCode),
        "Export Doc" => PriceListNumberFormat.FormatMoney(r.ExportDocCost, r.CurrencyCode),
        "Total cost" => PriceListNumberFormat.FormatMoney(r.TotalCost, r.CurrencyCode),
        "Sell / MT" => PriceListNumberFormat.FormatMoney(
            PriceListNumberFormat.ApplyRound(r.SellMt, roundSellTo), r.CurrencyCode),
        "Last Price" => PriceListNumberFormat.FormatMoney(
            PriceListNumberFormat.ApplyRound(r.LastSellMt, roundSellTo), r.CurrencyCode),
        "Manual MT" => PriceListNumberFormat.FormatMoney(
            PriceListNumberFormat.ApplyRound(r.OverrideSellMt, roundSellTo), r.CurrencyCode),
        "Sell / bag" => PriceListNumberFormat.FormatMoney(
            PriceListNumberFormat.ApplyRound(r.SellBag, roundSellTo), r.CurrencyCode),
        "Manual bag" => PriceListNumberFormat.FormatMoney(
            PriceListNumberFormat.ApplyRound(r.OverrideSellBag, roundSellTo), r.CurrencyCode),
        _ => ""
    };

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background(Colors.Grey.Lighten3).Padding(2).AlignCenter()
            .Text(text).SemiBold().FontSize(6.5f);

    private static IContainer CellBody(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(1.5f);
}
