using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CostWise.App.Services;

/// <summary>Shared PDF table sizing: compact + centered when few columns.</summary>
public static class PriceListPdfLayout
{
    public static float PageContentWidth => PageSizes.A4.Width - 56f; // 28pt margins each side

    public static IContainer TableHost(IContainer container, float preferredWidth)
    {
        var contentWidth = PageContentWidth;
        if (preferredWidth < contentWidth - 1f)
            return container.AlignCenter().Width(preferredWidth);
        return container;
    }

    public static float PreferredWidth(IEnumerable<float> columnWeights, float pointsPerWeight = 90f)
    {
        var sum = columnWeights.Sum();
        return Math.Min(PageContentWidth, Math.Max(120f, sum * pointsPerWeight));
    }
}
