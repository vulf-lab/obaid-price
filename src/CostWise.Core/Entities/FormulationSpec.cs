namespace CostWise.Core.Entities;

public class FormulationSpec
{
    public int Id { get; set; }
    public int FormulationId { get; set; }
    public int SpecParameterId { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }

    public Formulation Formulation { get; set; } = null!;
    public SpecParameter SpecParameter { get; set; } = null!;
}
