using CostWise.Core.Entities;
using CostWise.Core.Services;
using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.Infrastructure.Services;

public static class SystemIdGenerator
{
    public static async Task<string> NextAsync(CostWiseDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.Formulations
            .AsNoTracking()
            .Select(f => f.SystemId)
            .Where(id => id.StartsWith(SystemIdFormat.Prefix))
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var id in existing)
        {
            if (SystemIdFormat.TryParseNumber(id, out var n) && n > max)
                max = n;
        }

        return SystemIdFormat.FromNumber(max + 1);
    }

    public static async Task EnsureAllAssignedAsync(CostWiseDbContext db, CancellationToken cancellationToken = default)
    {
        var missing = await db.Formulations
            .Where(f => string.IsNullOrEmpty(f.SystemId))
            .OrderBy(f => f.Id)
            .ToListAsync(cancellationToken);

        if (missing.Count == 0)
            return;

        var existing = await db.Formulations
            .AsNoTracking()
            .Select(f => f.SystemId)
            .Where(id => !string.IsNullOrEmpty(id) && id.StartsWith(SystemIdFormat.Prefix))
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var id in existing)
        {
            if (SystemIdFormat.TryParseNumber(id, out var n) && n > max)
                max = n;
        }

        foreach (var formulation in missing)
        {
            max++;
            formulation.SystemId = SystemIdFormat.FromNumber(max);
            if (formulation.CreatedAtUtc == default)
                formulation.CreatedAtUtc = DateTime.UtcNow;
            if (formulation.UpdatedAtUtc == default)
                formulation.UpdatedAtUtc = formulation.CreatedAtUtc;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

public static class FormulationAudit
{
    public static void Log(
        CostWiseDbContext db,
        Formulation formulation,
        FormulationChangeAction action,
        string summary,
        string? details = null,
        string? importBatchId = null)
    {
        db.FormulationChangeLogs.Add(new FormulationChangeLog
        {
            FormulationId = formulation.Id == 0 ? null : formulation.Id,
            SystemId = formulation.SystemId,
            Code = formulation.Code,
            Action = action,
            Summary = summary,
            Details = details,
            ImportBatchId = importBatchId,
            ChangedAtUtc = DateTime.UtcNow
        });
    }
}
