namespace CostWise.Core.Entities;

public class FormulationIngredient
{
    public int Id { get; set; }
    public int FormulationId { get; set; }
    public int RawIngredientId { get; set; }
    public decimal InclusionPercent { get; set; }

    public Formulation Formulation { get; set; } = null!;
    public RawIngredient RawIngredient { get; set; } = null!;
}
