namespace CostWise.Core.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Formulation> Formulations { get; set; } = new List<Formulation>();
}
