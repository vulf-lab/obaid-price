using CostWise.Core.Entities;
using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CostWise.Infrastructure.Services;

/// <summary>One-shot cleanup for orphan formulation codes not present in nutrition master sheet.</summary>
public static class FormulationOrphanCleanup
{
    public const string OrphanCode = "S2V725V";

    public static async Task DeleteOrphanIfPresentAsync(CostWiseDbContext db, CancellationToken ct = default)
    {
        var entity = await db.Formulations
            .FirstOrDefaultAsync(f => f.Code == OrphanCode, ct);
        if (entity is null) return;

        FormulationAudit.Log(db, entity, FormulationChangeAction.Deleted,
            $"Deleted orphan {entity.Code} (not in nutrition profile sheet)");
        db.Formulations.Remove(entity);
        await db.SaveChangesAsync(ct);
    }
}
