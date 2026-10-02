namespace CostWise.Core.Entities;

public class VictoryReportSnapshotLine
{
    public int Id { get; set; }
    public int SnapshotId { get; set; }
    public int? PriceBookId { get; set; }
    public int? FormulationId { get; set; }
    /// <summary>A or B — which side of the brief this sell was saved on.</summary>
    public string BookRole { get; set; } = "A";
    public string FormulationCode { get; set; } = string.Empty;
    public string FeedTypeName { get; set; } = string.Empty;
    public string SizeName { get; set; } = string.Empty;
    public decimal? SellMt { get; set; }
    public decimal? SellBag { get; set; }
    public string CurrencyCode { get; set; } = "KES";

    public VictoryReportSnapshot? Snapshot { get; set; }
    public PriceBook? PriceBook { get; set; }
    public Formulation? Formulation { get; set; }
}
