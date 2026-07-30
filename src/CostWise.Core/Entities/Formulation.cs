namespace CostWise.Core.Entities;

public class Formulation
{
    public int Id { get; set; }
    /// <summary>Immutable system key, e.g. CW-000001.</summary>
    public string SystemId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int FeedTypeId { get; set; }
    public int SpeciesId { get; set; }
    public int SizeId { get; set; }
    public int CategoryId { get; set; }
    public int SubCategoryId { get; set; }
    /// <summary>Revision within a version (1 = earliest, higher = newer).</summary>
    public string Revision { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ImportedAtUtc { get; set; }
    public string? ImportBatchId { get; set; }

    /// <summary>True when this formula is in production (active).</summary>
    public bool IsActive { get; set; }

    public FeedType FeedType { get; set; } = null!;
    public Species Species { get; set; } = null!;
    public Size Size { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public SubCategory SubCategory { get; set; } = null!;
    public ICollection<FormulationIngredient> Ingredients { get; set; } = new List<FormulationIngredient>();
    public ICollection<FormulationSpec> Specs { get; set; } = new List<FormulationSpec>();
    public ICollection<CostingScenario> CostingScenarios { get; set; } = new List<CostingScenario>();
    public ICollection<FormulationChangeLog> ChangeLogs { get; set; } = new List<FormulationChangeLog>();
}
