namespace CostWise.Core.Entities;

public class ProductionGroupFormulation
{
    public int Id { get; set; }
    public int ProductionGroupId { get; set; }
    public int FormulationId { get; set; }
    public int SortOrder { get; set; }

    public ProductionGroup ProductionGroup { get; set; } = null!;
    public Formulation Formulation { get; set; } = null!;
}
