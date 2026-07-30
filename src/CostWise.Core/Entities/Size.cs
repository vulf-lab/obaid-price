namespace CostWise.Core.Entities;

public class Size
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DiameterMm { get; set; }
    public decimal ConversionCost { get; set; }
    public int? FeedTypeId { get; set; }
    public bool IsActive { get; set; } = true;

    public FeedType? FeedType { get; set; }
    public ICollection<Formulation> Formulations { get; set; } = new List<Formulation>();
}
