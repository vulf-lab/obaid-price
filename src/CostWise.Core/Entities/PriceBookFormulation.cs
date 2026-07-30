namespace CostWise.Core.Entities;

public class PriceBookFormulation
{
    public int Id { get; set; }
    public int PriceBookId { get; set; }
    public int FormulationId { get; set; }
    public int SortOrder { get; set; }
    /// <summary>When set, replaces margin-calculated sell price per MT.</summary>
    public decimal? OverrideSellPriceMt { get; set; }
    /// <summary>When set, replaces margin-calculated sell price per 25 kg bag.</summary>
    public decimal? OverrideSellPriceBag { get; set; }

    public PriceBook PriceBook { get; set; } = null!;
    public Formulation Formulation { get; set; } = null!;
}
