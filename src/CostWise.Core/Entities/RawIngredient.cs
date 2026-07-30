namespace CostWise.Core.Entities;

public class RawIngredient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PricePerMt { get; set; }
    public bool IsAvailable { get; set; } = true;

    public ICollection<FormulationIngredient> FormulationIngredients { get; set; } = new List<FormulationIngredient>();
    public ICollection<RawIngredientPriceHistory> PriceHistory { get; set; } = new List<RawIngredientPriceHistory>();
}
