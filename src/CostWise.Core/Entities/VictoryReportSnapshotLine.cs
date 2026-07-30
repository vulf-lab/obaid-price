namespace CostWise.Core.Entities;

public class VictoryReportSnapshotLine
{
    public int Id { get; set; }
    public int SnapshotId { get; set; }
    public int PriceBookId { get; set; }
    public int FormulationId { get; set; }
    public decimal? SellMt { get; set; }
    public decimal? SellBag { get; set; }
    public string CurrencyCode { get; set; } = "KES";

    public VictoryReportSnapshot? Snapshot { get; set; }
    public PriceBook? PriceBook { get; set; }
    public Formulation? Formulation { get; set; }
}
