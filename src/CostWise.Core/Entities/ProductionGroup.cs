namespace CostWise.Core.Entities;

public class ProductionGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ProductionGroupFormulation> Formulations { get; set; } = new List<ProductionGroupFormulation>();
}
