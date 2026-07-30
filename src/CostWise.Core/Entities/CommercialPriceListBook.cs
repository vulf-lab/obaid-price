namespace CostWise.Core.Entities;

public class CommercialPriceListBook
{
    public int Id { get; set; }
    public int CommercialPriceListId { get; set; }
    public int PriceBookId { get; set; }
    public int SortOrder { get; set; }

    public CommercialPriceList CommercialPriceList { get; set; } = null!;
    public PriceBook PriceBook { get; set; } = null!;
}
