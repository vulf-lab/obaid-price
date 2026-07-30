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
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public PricingCostOption? PackingOption { get; set; }
    public PricingCostOption? ExportDocOption { get; set; }
    public PricingCostOption? AdditiveOption { get; set; }
}
