using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CostWise.Infrastructure.Services;

/// <summary>Keeps Formulation.IsActive in sync with Production group membership.</summary>
public static class FormulationActiveSync
{
    public static async Task SyncFromProductionGroupsAsync(CostWiseDbContext db, CancellationToken ct = default)
    {
        var inGroup = (await db.ProductionGroupFormulations
                .Select(x => x.FormulationId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var f in await db.Formulations.ToListAsync(ct))
            f.IsActive = inGroup.Contains(f.Id);
    }
}
