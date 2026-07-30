using CostWise.Core.Enums;

namespace CostWise.Core.Entities;

public class PricingCostOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public PricingCostKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
}
