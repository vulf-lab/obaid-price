using ClosedXML.Excel;
using CostWise.Core.Entities;
using CostWise.Infrastructure;
using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CostWise.ApplySheet;

internal static class Program
{
    private const string ActionRemove = "Remove";
    private const string ActionTrial = "Change Sub Category to Trial";

    private static async Task<int> Main(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help" or "/?"))
        {
            Console.WriteLine("Usage: CostWise.ApplySheet <excel.xlsx> [--apply] --db <path> | --force-local");
            Console.WriteLine(ToolDatabaseArgs.HelpText);
            return 0;
        }

        if (!ToolDatabaseArgs.TryResolve(args, out var dbPath, out var dbError))
        {
            Console.Error.WriteLine(dbError);
            return dbError.Contains("Refusing", StringComparison.Ordinal) ? 2 : 0;
        }

        var apply = args.Any(a => string.Equals(a, "--apply", StringComparison.OrdinalIgnoreCase));
        var excelPath = args.FirstOrDefault(a =>
            !a.StartsWith("--", StringComparison.Ordinal) &&
            !a.StartsWith("-", StringComparison.Ordinal) &&
            !IsSamePath(a, dbPath));

        if (excelPath is null || !File.Exists(excelPath))
        {
            Console.Error.WriteLine(excelPath is null
                ? "Usage: CostWise.ApplySheet <excel.xlsx> [--apply] --db <path> | --force-local"
                : $"File not found: {excelPath}");
            return 1;
        }

        Console.WriteLine($"Reading {excelPath}");
        Console.WriteLine(apply ? "Mode: APPLY (database will be modified)" : "Mode: DRY-RUN (no database writes)");
        Console.WriteLine($"Database: {dbPath}");

        List<SheetActionRow> rows;
        try
        {
            rows = ReadRows(excelPath);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Cannot open Excel (is it open in another app?): {ex.Message}");
            return 1;
        }

        var removes = rows.Where(r => r.Action == SheetAction.Remove).ToList();
        var trials = rows.Where(r => r.Action == SheetAction.ChangeToTrial).ToList();
        var skips = rows.Where(r => r.Action == SheetAction.Skip).ToList();

        Console.WriteLine($"Rows: {rows.Count}  Remove: {removes.Count}  Trial: {trials.Count}  Skip(blank): {skips.Count}");

        var options = new DbContextOptionsBuilder<CostWiseDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        await using var db = new CostWiseDbContext(options);
        await db.Database.MigrateAsync();

        var formulations = await db.Formulations
            .Include(f => f.SubCategory)
            .ToListAsync();
        var byCode = formulations.ToDictionary(f => f.Code, StringComparer.OrdinalIgnoreCase);

        var missingRemove = new List<string>();
        var missingTrial = new List<string>();
        var deleteTargets = new List<Formulation>();
        var trialTargets = new List<(Formulation Formulation, string CurrentVersion)>();

        foreach (var row in removes)
        {
            if (!byCode.TryGetValue(row.Code, out var f))
            {
                missingRemove.Add(row.Code);
                continue;
            }
            deleteTargets.Add(f);
        }

        foreach (var row in trials)
        {
            if (!byCode.TryGetValue(row.Code, out var f))
            {
                missingTrial.Add(row.Code);
                continue;
            }
            trialTargets.Add((f, f.SubCategory?.Name ?? "(none)"));
        }

        Console.WriteLine();
        Console.WriteLine($"Would delete ({deleteTargets.Count}):");
        foreach (var f in deleteTargets.OrderBy(f => f.Code, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"  DELETE  {f.SystemId}  {f.Code}");

        if (missingRemove.Count > 0)
        {
            Console.WriteLine($"Missing codes for Remove ({missingRemove.Count}):");
            foreach (var code in missingRemove.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"  MISSING {code}");
        }

        Console.WriteLine($"Would set SubCategory → Trial ({trialTargets.Count}):");
        foreach (var (f, current) in trialTargets.OrderBy(t => t.Formulation.Code, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"  TRIAL   {f.SystemId}  {f.Code}  ({current} → Trial)");

        if (missingTrial.Count > 0)
        {
            Console.WriteLine($"Missing codes for Trial ({missingTrial.Count}):");
            foreach (var code in missingTrial.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
                Console.WriteLine($"  MISSING {code}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Summary: delete={deleteTargets.Count} trial={trialTargets.Count} skip={skips.Count} " +
            $"missing={missingRemove.Count + missingTrial.Count}");

        if (!apply)
        {
            Console.WriteLine();
            Console.WriteLine("Dry-run complete. Re-run with --apply to write changes.");
            return 0;
        }

        var deleted = 0;
        var updated = 0;
        var now = DateTime.UtcNow;

        foreach (var f in deleteTargets)
        {
            FormulationAudit.Log(db, f, FormulationChangeAction.Deleted, $"Deleted {f.Code} (sheet Remove)");
            db.Formulations.Remove(f);
            deleted++;
        }

        var trialSub = await db.SubCategories.FirstOrDefaultAsync(s => s.Name == "Trial");
        if (trialSub is null)
        {
            trialSub = new SubCategory { Name = "Trial", IsActive = true };
            db.SubCategories.Add(trialSub);
            await db.SaveChangesAsync();
        }

        foreach (var (f, current) in trialTargets)
        {
            if (string.Equals(current, "Trial", StringComparison.OrdinalIgnoreCase))
                continue;

            f.SubCategoryId = trialSub.Id;
            f.UpdatedAtUtc = now;
            FormulationAudit.Log(
                db,
                f,
                FormulationChangeAction.Updated,
                $"SubCategory → Trial",
                details: $"{current} → Trial (sheet action)");
            updated++;
        }

        await db.SaveChangesAsync();

        Console.WriteLine();
        Console.WriteLine($"Applied: deleted={deleted} updated={updated}");
        return 0;
    }

    private static List<SheetActionRow> ReadRows(string path)
    {
        // Copy if locked by Excel
        var readPath = path;
        string? tempCopy = null;
        try
        {
            using var probe = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
        catch (IOException)
        {
            tempCopy = Path.Combine(Path.GetTempPath(), $"costwise2_apply_{Guid.NewGuid():N}.xlsx");
            File.Copy(path, tempCopy, overwrite: true);
            readPath = tempCopy;
            Console.WriteLine($"(Using temp copy: {tempCopy})");
        }

        try
        {
            using var workbook = new XLWorkbook(readPath);
            var sheet = workbook.Worksheets.First();
            var header = sheet.Row(1);
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in header.CellsUsed())
                map[cell.GetString().Trim()] = cell.Address.ColumnNumber;

            Require(map, "Code", "Remove");

            var rows = new List<SheetActionRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 2; r <= lastRow; r++)
            {
                var code = sheet.Cell(r, map["Code"]).GetFormattedString().Trim();
                if (string.IsNullOrWhiteSpace(code)) continue;

                var actionRaw = sheet.Cell(r, map["Remove"]).GetFormattedString().Trim();
                var action = ParseAction(actionRaw);
                rows.Add(new SheetActionRow(code, action, actionRaw));
            }

            return rows;
        }
        finally
        {
            if (tempCopy is not null)
            {
                try { File.Delete(tempCopy); } catch { /* ignore */ }
            }
        }
    }

    private static SheetAction ParseAction(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return SheetAction.Skip;
        if (string.Equals(raw, ActionRemove, StringComparison.OrdinalIgnoreCase))
            return SheetAction.Remove;
        if (string.Equals(raw, ActionTrial, StringComparison.OrdinalIgnoreCase))
            return SheetAction.ChangeToTrial;

        Console.WriteLine($"Warning: unknown action '{raw}' — treating as skip");
        return SheetAction.Skip;
    }

    private static void Require(Dictionary<string, int> map, params string[] names)
    {
        foreach (var name in names)
        {
            if (!map.ContainsKey(name))
                throw new InvalidOperationException($"Missing column '{name}'. Found: {string.Join(", ", map.Keys)}");
        }
    }

    private static bool IsSamePath(string candidate, string dbPath)
    {
        try
        {
            return string.Equals(Path.GetFullPath(candidate), dbPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private enum SheetAction
    {
        Skip,
        Remove,
        ChangeToTrial
    }

    private sealed record SheetActionRow(string Code, SheetAction Action, string RawAction);
}
