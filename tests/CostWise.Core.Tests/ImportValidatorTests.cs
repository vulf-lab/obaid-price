using CostWise.Core.Services.Import;

namespace CostWise.Core.Tests;

public class ImportValidatorTests
{
    [Fact]
    public void Rm_MissingFromFile_New_MissingPrice_Reactivating()
    {
        var existing = new[]
        {
            new ExistingRawIngredientSnapshot(1, "Corn", 1000m, true),
            new ExistingRawIngredientSnapshot(2, "Soy", 0m, false),
            new ExistingRawIngredientSnapshot(3, "Fishmeal", 5000m, true)
        };
        var sheet = new[]
        {
            new RmPriceImportRow("Corn", 1100m, 2),
            new RmPriceImportRow("Soy", 2000m, 3),
            new RmPriceImportRow("Wheat", 900m, 4),
            new RmPriceImportRow("Broken", null, 5)
        };

        var preview = RawMaterialPriceImportValidator.BuildPreview(sheet, existing);

        Assert.Contains(preview.Issues, i => i.Kind == RmImportIssueKind.MissingFromFile && i.Name == "Fishmeal");
        Assert.Contains(preview.Issues, i => i.Kind == RmImportIssueKind.MissingPrice && i.Name == "Broken");
        Assert.Contains(preview.Issues, i => i.Kind == RmImportIssueKind.NewIngredient && i.Name == "Wheat");
        Assert.Contains(preview.Issues, i => i.Kind == RmImportIssueKind.ReactivatingPrice && i.Name == "Soy");
        Assert.Contains(preview.Issues, i => i.Kind == RmImportIssueKind.WillUpdate && i.Name == "Corn");
        Assert.Equal(2, preview.RowsToUpdate.Count);
        Assert.Single(preview.RowsToCreate);
        Assert.True(preview.HasWarnings);
    }

    [Fact]
    public void Formula_Blocks_When_Not_100_Or_Missing_Rm_Or_Master()
    {
        var masters = new MasterNameSets(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cat" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Commercial" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Grower" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Tilapia" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "3mm" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Corn" });

        var badPercent = new FormulaImportGroup(
            "A1", "Cat", "3mm", "Tilapia", "Grower", "1", "Commercial",
            [new FormulaImportIngredientLine("Corn", 50m)]);
        var missingRm = new FormulaImportGroup(
            "A2", "Cat", "3mm", "Tilapia", "Grower", "1", "Commercial",
            [new FormulaImportIngredientLine("Unknown", 100m)]);
        var missingCat = new FormulaImportGroup(
            "A3", "Nope", "3mm", "Tilapia", "Grower", "1", "Commercial",
            [new FormulaImportIngredientLine("Corn", 100m)]);

        var existing = new Dictionary<string, ExistingFormulaSnapshot>(StringComparer.OrdinalIgnoreCase);
        var preview = FormulationImportValidator.BuildPreview(
            [badPercent, missingRm, missingCat], existing, masters);

        Assert.All(preview.Results, r => Assert.Equal(FormulaImportDisposition.Blocked, r.Disposition));
        Assert.Contains(preview.Blocked[0].BlockerReasons, b => b.Contains("100%"));
        Assert.Contains(preview.Blocked[1].BlockerReasons, b => b.Contains("Unknown"));
        Assert.Contains(preview.Blocked[2].BlockerReasons, b => b.Contains("Category"));
    }

    [Fact]
    public void Formula_Same_Vs_Different_Code_Compare()
    {
        var masters = new MasterNameSets(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Cat" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Commercial" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Grower" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Tilapia" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "3mm" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Corn", "Soy" });

        var existing = new Dictionary<string, ExistingFormulaSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            ["X1"] = new ExistingFormulaSnapshot(
                10, "X1", "Cat", "Commercial", "Grower", "Tilapia", "3mm", "1",
                [
                    new FormulaImportIngredientLine("Corn", 60m),
                    new FormulaImportIngredientLine("Soy", 40m)
                ])
        };

        var same = new FormulaImportGroup(
            "X1", "Cat", "3mm", "Tilapia", "Grower", "1", "Commercial",
            [
                new FormulaImportIngredientLine("Corn", 60m),
                new FormulaImportIngredientLine("Soy", 40m)
            ]);
        var different = new FormulaImportGroup(
            "X1", "Cat", "3mm", "Tilapia", "Grower", "2", "Commercial",
            [
                new FormulaImportIngredientLine("Corn", 70m),
                new FormulaImportIngredientLine("Soy", 30m)
            ]);
        var brandNew = new FormulaImportGroup(
            "Y1", "Cat", "3mm", "Tilapia", "Grower", "1", "Commercial",
            [new FormulaImportIngredientLine("Corn", 100m)]);

        Assert.Equal(FormulaImportDisposition.Same,
            FormulationImportValidator.Evaluate(same, existing, masters).Disposition);
        var diffResult = FormulationImportValidator.Evaluate(different, existing, masters);
        Assert.Equal(FormulaImportDisposition.Different, diffResult.Disposition);
        Assert.NotEmpty(diffResult.DiffLines);
        Assert.Equal(FormulaImportDisposition.New,
            FormulationImportValidator.Evaluate(brandNew, existing, masters).Disposition);
    }

    [Fact]
    public void Formula_GroupByCode_Sums_Duplicate_Rms()
    {
        var lines = new[]
        {
            new FormulaImportSheetLine("Cat", "3mm", "Tilapia", "Grower", "Z1", "1", "Commercial", "Corn", 40m, 2),
            new FormulaImportSheetLine("Cat", "3mm", "Tilapia", "Grower", "Z1", "1", "Commercial", "Corn", 10m, 3),
            new FormulaImportSheetLine("Cat", "3mm", "Tilapia", "Grower", "Z1", "1", "Commercial", "Soy", 50m, 4)
        };
        var groups = FormulationImportValidator.GroupByCode(lines);
        Assert.Single(groups);
        Assert.Equal(2, groups[0].Ingredients.Count);
        Assert.Equal(50m, groups[0].Ingredients.First(i => i.RawIngredientName == "Corn").InclusionPercent);
    }
}
