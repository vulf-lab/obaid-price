using CostWise.Core.Enums;

namespace CostWise.Core.Entities;

public class PriceBook
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? PackingOptionId { get; set; }
    public int? ExportDocOptionId { get; set; }
    public int? AdditiveOptionId { get; set; }
    public decimal PackingCost { get; set; }
    public decimal TransportationCost { get; set; }
    public decimal SpecialAdditiveCost { get; set; }
    public decimal ExportDocCost { get; set; }
    public decimal MarginPercent { get; set; } = 10m;
    public PriceUnit PriceUnit { get; set; } = PriceUnit.PerMt;
    /// <summary>Round sell/MT to nearest multiple (0 = 2-decimal only).</summary>
    public decimal RoundMtTo { get; set; }
    /// <summary>Round sell/bag to nearest multiple (0 = 2-decimal only).</summary>
    public decimal RoundBagTo { get; set; }
    public int SortOrder { get; set; }
    public int? DisplayCurrencyId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public PricingCostOption? PackingOption { get; set; }
    public PricingCostOption? ExportDocOption { get; set; }
    public PricingCostOption? AdditiveOption { get; set; }
    public Currency? DisplayCurrency { get; set; }
    public ICollection<PriceBookFormulation> Formulations { get; set; } = new List<PriceBookFormulation>();
}
