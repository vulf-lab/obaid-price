namespace CostWise.Core.Entities;

public class RawIngredientPriceHistory
{
    public int Id { get; set; }
    public int RawIngredientId { get; set; }
    public decimal PricePerMt { get; set; }
    public decimal ExchangeRateKesPerUsd { get; set; }
    public decimal PricePerMtUsd { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public RawIngredient? RawIngredient { get; set; }
}
