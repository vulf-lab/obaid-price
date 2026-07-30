using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using FeedSize = CostWise.Core.Entities.Size;

namespace CostWise.Import;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var excelPath = args.ElementAtOrDefault(0)
            ?? @"c:\Users\oureh\OneDrive\Documents\Copy of CostwiseF.xlsx";

        if (!File.Exists(excelPath))
        {
            Console.Error.WriteLine($"File not found: {excelPath}");
            return 1;
        }

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise",
            "costwise.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var options = new DbContextOptionsBuilder<CostWiseDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using var db = new CostWiseDbContext(options);
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);

        Console.WriteLine($"Reading {excelPath}");
        var rows = ReadRows(excelPath);
        Console.WriteLine($"Loaded {rows.Count} ingredient lines, {rows.Select(r => r.Code).Distinct().Count()} codes");

        var batchId = $"IMP-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var imported = await ImportAsync(db, rows, batchId);
        Console.WriteLine($"Imported/updated {imported} formulations (batch {batchId}) into {dbPath}");
        return 0;
    }

    private static List<ExcelIngredientRow> ReadRows(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        var header = sheet.Row(1);
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in header.CellsUsed())
            map[cell.GetString().Trim()] = cell.Address.ColumnNumber;

        Require(map, "Category", "Size", "Feed Type", "Code", "Rev", "Sub-Category", "RM/Description", "%");
        var specieCol = map.ContainsKey("Specie") ? map["Specie"] : map["Species"];

        var rows = new List<ExcelIngredientRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var code = CellText(sheet, r, map["Code"]);
            if (string.IsNullOrWhiteSpace(code)) continue;

            rows.Add(new ExcelIngredientRow(
                Category: CellText(sheet, r, map["Category"]).Trim(),
                Size: CellText(sheet, r, map["Size"]).Trim(),
                Species: CellText(sheet, r, specieCol).Trim(),
                FeedType: CellText(sheet, r, map["Feed Type"]).Trim(),
                Code: code.Trim(),
                Revision: NormalizeRev(sheet.Cell(r, map["Rev"]).Value),
                SubCategory: CellText(sheet, r, map["Sub-Category"]).Trim(),
                RawIngredient: CellText(sheet, r, map["RM/Description"]).Trim(),
                Percent: ParsePercent(sheet.Cell(r, map["%"]).Value)));
        }

        return rows;
    }

    private static async Task<int> ImportAsync(CostWiseDbContext db, List<ExcelIngredientRow> rows, string batchId)
    {
        var groups = rows.GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToList();
        var count = 0;
        var now = DateTime.UtcNow;

        foreach (var group in groups)
        {
            var first = group.First();
            var category = await GetOrCreateNamedAsync(db.Categories, db, first.Category, n => new Category { Name = n });
            var subCategory = await GetOrCreateNamedAsync(db.SubCategories, db, first.SubCategory, n => new SubCategory { Name = n });
            var species = await GetOrCreateNamedAsync(db.Species, db, first.Species, n => new Species { Name = n });
            var feedType = await GetOrCreateNamedAsync(db.FeedTypes, db, first.FeedType, n => new FeedType { Name = n });
            var size = await GetOrCreateSizeAsync(db, first.Size);

            var formulation = await db.Formulations
                .Include(f => f.Ingredients)
                .FirstOrDefaultAsync(f => f.Code == first.Code);

            var isNew = formulation is null;
            if (formulation is null)
            {
                formulation = new Formulation
                {
                    Code = first.Code,
                    SystemId = await SystemIdGenerator.NextAsync(db),
                    CreatedAtUtc = now
                };
                db.Formulations.Add(formulation);
            }
            else
            {
                db.FormulationIngredients.RemoveRange(formulation.Ingredients);
            }

            formulation.Name = $"{first.Code} · {first.FeedType} · {first.Size}";
            formulation.FeedTypeId = feedType.Id;
            formulation.SpeciesId = species.Id;
            formulation.SizeId = size.Id;
            formulation.CategoryId = category.Id;
            formulation.SubCategoryId = subCategory.Id;
            formulation.Revision = first.Revision;
            formulation.UpdatedAtUtc = now;
            formulation.ImportedAtUtc = now;
            formulation.ImportBatchId = batchId;

            // Deduplicate RM lines if Excel has duplicates for same code+RM
            var ingredientGroups = group
                .GroupBy(x => x.RawIngredient, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Name = g.First().RawIngredient,
                    Percent = g.Sum(x => x.Percent)
                });

            foreach (var line in ingredientGroups)
            {
                var rm = await GetOrCreateRawAsync(db, line.Name);
                formulation.Ingredients.Add(new FormulationIngredient
                {
                    RawIngredientId = rm.Id,
                    InclusionPercent = line.Percent
                });
            }

            await db.SaveChangesAsync();
            FormulationAudit.Log(
                db,
                formulation,
                FormulationChangeAction.Imported,
                isNew ? $"Imported {formulation.Code}" : $"Re-imported {formulation.Code}",
                details: $"Rev {formulation.Revision}; ingredients {formulation.Ingredients.Count}",
                importBatchId: batchId);
            await db.SaveChangesAsync();

            count++;
            if (count % 25 == 0)
                Console.WriteLine($"  … {count}/{groups.Count}");
        }

        return count;
    }

    private static async Task<T> GetOrCreateNamedAsync<T>(
        DbSet<T> set,
        CostWiseDbContext db,
        string name,
        Func<string, T> factory) where T : class
    {
        name = name.Trim();
        var existing = await set.AsQueryable().FirstOrDefaultAsync(e => EF.Property<string>(e, "Name") == name);
        if (existing is not null) return existing;

        var entity = factory(name);
        set.Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private static async Task<FeedSize> GetOrCreateSizeAsync(CostWiseDbContext db, string sizeName)
    {
        sizeName = sizeName.Trim();
        var existing = await db.Sizes.FirstOrDefaultAsync(s => s.Name == sizeName);
        if (existing is not null) return existing;

        var diameter = ParseDiameterMm(sizeName);
        var size = new FeedSize
        {
            Name = sizeName,
            DiameterMm = diameter,
            ConversionCost = 0m,
            IsActive = true
        };
        db.Sizes.Add(size);
        await db.SaveChangesAsync();
        return size;
    }

    private static async Task<RawIngredient> GetOrCreateRawAsync(CostWiseDbContext db, string name)
    {
        name = name.Trim();
        var existing = await db.RawIngredients.FirstOrDefaultAsync(r => r.Name == name);
        if (existing is not null) return existing;

        var rm = new RawIngredient
        {
            Name = name,
            PricePerMt = 0m,
            IsAvailable = true
        };
        db.RawIngredients.Add(rm);
        await db.SaveChangesAsync();
        return rm;
    }

    private static decimal ParseDiameterMm(string sizeName)
    {
        var m = Regex.Match(sizeName, @"(\d+(\.\d+)?)");
        return m.Success
            ? decimal.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)
            : 0m;
    }

    private static string NormalizeRev(XLCellValue value)
    {
        if (value.IsBlank) return string.Empty;
        if (value.IsNumber)
        {
            var n = value.GetNumber();
            return n == Math.Truncate(n)
                ? ((long)n).ToString(CultureInfo.InvariantCulture)
                : n.ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString().Trim();
    }

    private static decimal ParsePercent(XLCellValue value)
    {
        if (value.IsNumber) return Convert.ToDecimal(value.GetNumber(), CultureInfo.InvariantCulture);
        var text = value.ToString().Trim().TrimEnd('%');
        return decimal.Parse(text, CultureInfo.InvariantCulture);
    }

    private static string CellText(IXLWorksheet sheet, int row, int col) =>
        sheet.Cell(row, col).GetFormattedString().Trim();

    private static void Require(Dictionary<string, int> map, params string[] names)
    {
        foreach (var name in names)
        {
            if (!map.ContainsKey(name))
                throw new InvalidOperationException($"Missing column '{name}'. Found: {string.Join(", ", map.Keys)}");
        }
    }

    private sealed record ExcelIngredientRow(
        string Category,
        string Size,
        string Species,
        string FeedType,
        string Code,
        string Revision,
        string SubCategory,
        string RawIngredient,
        decimal Percent);
}
