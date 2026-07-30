using CostWise.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CostWise.Infrastructure.Data;

public sealed class CostWiseDbContextFactory : IDesignTimeDbContextFactory<CostWiseDbContext>
{
    public CostWiseDbContext CreateDbContext(string[] args)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CostWise");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "costwise.db");

        var options = new DbContextOptionsBuilder<CostWiseDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new CostWiseDbContext(options);
    }
}
