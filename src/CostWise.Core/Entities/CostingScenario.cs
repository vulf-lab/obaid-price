using CostWise.Core.Enums;

namespace CostWise.Core.Entities;

public class CostingScenario
{
    public int Id { get; set; }
    public int FormulationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public MarketType Market { get; set; } = MarketType.Domestic;
    public int? PackingOptionId { get; set; }
    public int? ExportDocOptionId { get; set; }
    public int? AdditiveOptionId { get; set; }
    public decimal PackingCost { get; set; }
    public decimal ExportDocCost { get; set; }
    public decimal SpecialAdditiveCost { get; set; }
    public decimal TransportationCost { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal SnapshotRmCost { get; set; }
    public decimal SnapshotConversionCost { get; set; }
    public decimal SnapshotTotalCost { get; set; }
    public decimal SnapshotSellingPrice { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Formulation Formulation { get; set; } = null!;
    public PricingCostOption? PackingOption { get; set; }
    public PricingCostOption? ExportDocOption { get; set; }
    public PricingCostOption? AdditiveOption { get; set; }
}
