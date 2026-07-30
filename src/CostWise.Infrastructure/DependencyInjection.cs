using CostWise.Infrastructure.Data;
using CostWise.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CostWise.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCostWiseInfrastructure(this IServiceCollection services, string dbPath)
    {
        services.AddDbContextFactory<CostWiseDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var db = await services.GetRequiredService<IDbContextFactory<CostWiseDbContext>>()
            .CreateDbContextAsync();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
        await SystemIdGenerator.EnsureAllAssignedAsync(db);
    }

    public static string GetDefaultDatabasePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "costwise.db");
    }
}
