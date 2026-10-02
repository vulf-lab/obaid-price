using System.Text.Json;

namespace CostWise.App.Services;

/// <summary>Price list as issued, without branding logos.</summary>
public sealed record VictoryBriefDocument(
    string BookAName,
    string BookBName,
    IReadOnlyList<PriceListBookRow> BookARows,
    IReadOnlyList<PriceListBookRow> BookBRows,
    IReadOnlyList<RmPriceChangeRow> RmChanges,
    string Commentary,
    IReadOnlyList<string> VisibleColumns,
    decimal RoundBookATo,
    decimal RoundBookBTo,
    decimal BookAMarginPercent,
    decimal BookBMarginPercent,
    decimal? CompareBookAMarginPercent,
    decimal? CompareBookBMarginPercent,
    bool ShowSellCompare,
    decimal RoundDeltaPercentTo);

public static class VictoryBriefArchive
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(VictoryGroupBrief brief)
    {
        var document = new VictoryBriefDocument(
            brief.BookAName,
            brief.BookBName,
            brief.BookARows,
            brief.BookBRows,
            brief.RmChanges,
            brief.Commentary,
            brief.VisibleColumns,
            brief.RoundBookATo,
            brief.RoundBookBTo,
            brief.BookAMarginPercent,
            brief.BookBMarginPercent,
            brief.CompareBookAMarginPercent,
            brief.CompareBookBMarginPercent,
            brief.ShowSellCompare,
            brief.RoundDeltaPercentTo);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static VictoryGroupBrief? Deserialize(string? json, byte[]? logoLeft, byte[]? logoRight)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var document = JsonSerializer.Deserialize<VictoryBriefDocument>(json, JsonOptions);
        if (document is null)
            return null;

        return new VictoryGroupBrief(
            document.BookAName,
            document.BookBName,
            document.BookARows,
            document.BookBRows,
            document.RmChanges,
            document.Commentary,
            document.VisibleColumns,
            logoLeft,
            logoRight,
            document.RoundBookATo,
            document.RoundBookBTo,
            document.BookAMarginPercent,
            document.BookBMarginPercent,
            document.CompareBookAMarginPercent,
            document.CompareBookBMarginPercent,
            document.ShowSellCompare,
            document.RoundDeltaPercentTo);
    }
}
