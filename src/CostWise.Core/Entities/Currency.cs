namespace CostWise.Core.Entities;

public class Currency
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>True only for KES; cannot be deleted or demoted.</summary>
    public bool IsBase { get; set; }
    /// <summary>KES per 1 unit of this currency (1 for base).</summary>
    public decimal KesPerUnit { get; set; } = 1m;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
