using CostWise.Core.Enums;

namespace CostWise.Core.Entities;

public class CommercialPriceList
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? CurrencyId { get; set; }
    public PriceUnit SellUnit { get; set; } = PriceUnit.PerMt;
    public DateTime EffectiveDate { get; set; } = DateTime.Today;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Currency? Currency { get; set; }
    public ICollection<CommercialPriceListBook> Books { get; set; } = new List<CommercialPriceListBook>();
}
