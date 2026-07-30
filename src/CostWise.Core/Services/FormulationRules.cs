using CostWise.Core.Entities;

namespace CostWise.Core.Services;

public static class FormulationRules
{
    public const decimal InclusionTolerance = 0.01m;

    public static decimal TotalInclusionPercent(IEnumerable<FormulationIngredient> ingredients) =>
        ingredients.Sum(i => i.InclusionPercent);

    public static bool InclusionsSumTo100(IEnumerable<FormulationIngredient> ingredients)
    {
        var total = TotalInclusionPercent(ingredients);
        return Math.Abs(total - 100m) <= InclusionTolerance;
    }

    public static bool IsProducible(Formulation formulation)
    {
        if (formulation.Ingredients is null || formulation.Ingredients.Count == 0)
            return true;

        return formulation.Ingredients.All(i =>
            i.RawIngredient is null || i.RawIngredient.IsAvailable);
    }

    public static bool IsUnfitToProduce(Formulation formulation) => !IsProducible(formulation);
}
