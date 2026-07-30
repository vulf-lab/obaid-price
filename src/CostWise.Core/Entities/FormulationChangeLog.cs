namespace CostWise.Core.Entities;

public enum FormulationChangeAction
{
    Created = 0,
    Imported = 1,
    Updated = 2,
    Deleted = 3
}

public class FormulationChangeLog
{
    public int Id { get; set; }
    public int? FormulationId { get; set; }
    public string SystemId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public FormulationChangeAction Action { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ImportBatchId { get; set; }
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;

    public Formulation? Formulation { get; set; }
}
