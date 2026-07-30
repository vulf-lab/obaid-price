using CostWise.Core.Entities;
using CostWise.Core.Services.Import;
using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CostWise.App.Services.Import;

public static class FormulationImportService
{
    public static async Task<FormulaImportPreview> PreviewAsync(
        CostWiseDbContext db,
        string filePath,
        CancellationToken ct = default)
    {
        var lines = FormulationImportParser.Parse(filePath);
        var groups = FormulationImportValidator.GroupByCode(lines);
        var masters = await LoadMastersAsync(db, ct);
        var existing = await LoadExistingAsync(db, ct);
        return FormulationImportValidator.BuildPreview(groups, existing, masters);
    }

    public static async Task<int> ApplyAsync(
        CostWiseDbContext db,
        FormulaImportPreview preview,
        CancellationToken ct = default)
    {
        var batchId = $"IMP-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var now = DateTime.UtcNow;
        var written = 0;

        var categories = await db.Categories.ToListAsync(ct);
        var subCategories = await db.SubCategories.ToListAsync(ct);
        var feedTypes = await db.FeedTypes.ToListAsync(ct);
        var species = await db.Species.ToListAsync(ct);
        var sizes = await db.Sizes.ToListAsync(ct);
        var raws = await db.RawIngredients.ToListAsync(ct);

        foreach (var item in preview.Results)
        {
            if (item.Disposition is FormulaImportDisposition.Blocked or FormulaImportDisposition.Same)
                continue;
            if (item.SheetGroup is null) continue;

            var group = item.SheetGroup;
            var category = FindNamed(categories, c => c.Name, group.Category) ?? throw Missing("Category", group.Category);
            var subCategory = FindNamed(subCategories, c => c.Name, group.SubCategory) ?? throw Missing("Sub-Category", group.SubCategory);
            var feedType = FindNamed(feedTypes, c => c.Name, group.FeedType) ?? throw Missing("Feed Type", group.FeedType);
            var specie = FindNamed(species, c => c.Name, group.Species) ?? throw Missing("Species", group.Species);
            var size = FindNamed(sizes, c => c.Name, group.Size) ?? throw Missing("Size", group.Size);

            var formulation = await db.Formulations
                .Include(f => f.Ingredients)
                .FirstOrDefaultAsync(f => f.Code == group.Code, ct);

            var isNew = formulation is null;
            if (formulation is null)
            {
                formulation = new Formulation
                {
                    Code = group.Code,
                    SystemId = await SystemIdGenerator.NextAsync(db, ct),
                    CreatedAtUtc = now
                };
                db.Formulations.Add(formulation);
            }
            else
            {
                db.FormulationIngredients.RemoveRange(formulation.Ingredients);
                formulation.Ingredients.Clear();
            }

            formulation.Name = $"{group.Code} · {group.FeedType} · {group.Size}";
            formulation.FeedTypeId = feedType.Id;
            formulation.SpeciesId = specie.Id;
            formulation.SizeId = size.Id;
            formulation.CategoryId = category.Id;
            formulation.SubCategoryId = subCategory.Id;
            formulation.Revision = group.Revision;
            formulation.UpdatedAtUtc = now;
            formulation.ImportedAtUtc = now;
            formulation.ImportBatchId = batchId;

            foreach (var line in group.Ingredients)
            {
                var rm = FindNamed(raws, c => c.Name, line.RawIngredientName)
                         ?? throw Missing("Raw material", line.RawIngredientName);
                formulation.Ingredients.Add(new FormulationIngredient
                {
                    RawIngredientId = rm.Id,
                    InclusionPercent = line.InclusionPercent
                });
            }

            await db.SaveChangesAsync(ct);
            FormulationAudit.Log(
                db,
                formulation,
                FormulationChangeAction.Imported,
                isNew ? $"Imported {formulation.Code}" : $"Re-imported {formulation.Code}",
                details: $"Rev {formulation.Revision}; ingredients {formulation.Ingredients.Count}",
                importBatchId: batchId);
            await db.SaveChangesAsync(ct);
            written++;
        }

        return written;
    }

    private static async Task<MasterNameSets> LoadMastersAsync(CostWiseDbContext db, CancellationToken ct)
    {
        return new MasterNameSets(
            await ToNameSetAsync(db.Categories.Select(x => x.Name), ct),
            await ToNameSetAsync(db.SubCategories.Select(x => x.Name), ct),
            await ToNameSetAsync(db.FeedTypes.Select(x => x.Name), ct),
            await ToNameSetAsync(db.Species.Select(x => x.Name), ct),
            await ToNameSetAsync(db.Sizes.Select(x => x.Name), ct),
            await ToNameSetAsync(db.RawIngredients.Select(x => x.Name), ct));
    }

    private static async Task<Dictionary<string, ExistingFormulaSnapshot>> LoadExistingAsync(
        CostWiseDbContext db,
        CancellationToken ct)
    {
        var list = await db.Formulations
            .AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.SubCategory)
            .Include(f => f.FeedType)
            .Include(f => f.Species)
            .Include(f => f.Size)
            .Include(f => f.Ingredients).ThenInclude(i => i.RawIngredient)
            .ToListAsync(ct);

        return list.ToDictionary(
            f => f.Code,
            f => new ExistingFormulaSnapshot(
                f.Id,
                f.Code,
                f.Category.Name,
                f.SubCategory.Name,
                f.FeedType.Name,
                f.Species.Name,
                f.Size.Name,
                f.Revision,
                f.Ingredients
                    .Select(i => new FormulaImportIngredientLine(
                        i.RawIngredient.Name,
                        i.InclusionPercent))
                    .ToList()),
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<HashSet<string>> ToNameSetAsync(
        IQueryable<string> query,
        CancellationToken ct)
    {
        var names = await query.ToListAsync(ct);
        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }

    private static T? FindNamed<T>(IEnumerable<T> items, Func<T, string> nameSelector, string name)
    {
        foreach (var item in items)
        {
            if (string.Equals(nameSelector(item), name, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return default;
    }

    private static InvalidOperationException Missing(string kind, string name) =>
        new($"{kind} '{name}' was resolved at preview but missing at apply.");
}
