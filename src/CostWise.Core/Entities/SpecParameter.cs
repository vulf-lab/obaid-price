namespace CostWise.Core.Entities;

public class SpecParameter
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public ICollection<FormulationSpec> FormulationSpecs { get; set; } = new List<FormulationSpec>();
}
