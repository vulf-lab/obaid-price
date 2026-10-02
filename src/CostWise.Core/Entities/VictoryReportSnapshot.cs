namespace CostWise.Core.Entities;

public class VictoryReportSnapshot
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Label { get; set; } = string.Empty;
    public int BookAId { get; set; }
    public int BookBId { get; set; }
    public decimal BookAMarginPercent { get; set; }
    public decimal BookBMarginPercent { get; set; }
    public string? Note { get; set; }
    /// <summary>Archived brief without logo bytes. Null for snapshots saved before review support.</summary>
    public string? BriefJson { get; set; }

    public PriceBook? BookA { get; set; }
    public PriceBook? BookB { get; set; }
    public ICollection<VictoryReportSnapshotLine> Lines { get; set; } = new List<VictoryReportSnapshotLine>();
}
