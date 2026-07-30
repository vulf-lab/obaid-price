using CostWise.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostWise.Infrastructure.Data;

public class CostWiseDbContext : DbContext
{
    public CostWiseDbContext(DbContextOptions<CostWiseDbContext> options) : base(options)
    {
    }

    public DbSet<FeedType> FeedTypes => Set<FeedType>();
    public DbSet<Species> Species => Set<Species>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<SubCategory> SubCategories => Set<SubCategory>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<RawIngredient> RawIngredients => Set<RawIngredient>();
    public DbSet<SpecParameter> SpecParameters => Set<SpecParameter>();
    public DbSet<Formulation> Formulations => Set<Formulation>();
    public DbSet<FormulationIngredient> FormulationIngredients => Set<FormulationIngredient>();
    public DbSet<FormulationSpec> FormulationSpecs => Set<FormulationSpec>();
    public DbSet<CostingScenario> CostingScenarios => Set<CostingScenario>();
    public DbSet<PriceBook> PriceBooks => Set<PriceBook>();
    public DbSet<PricingCostOption> PricingCostOptions => Set<PricingCostOption>();
    public DbSet<ProductionGroup> ProductionGroups => Set<ProductionGroup>();
    public DbSet<ProductionGroupFormulation> ProductionGroupFormulations => Set<ProductionGroupFormulation>();
    public DbSet<FormulationChangeLog> FormulationChangeLogs => Set<FormulationChangeLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeedType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Species>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<SubCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Size>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.DiameterMm).HasPrecision(8, 2);
            e.Property(x => x.ConversionCost).HasPrecision(18, 2);
            e.HasOne(x => x.FeedType)
                .WithMany(x => x.Sizes)
                .HasForeignKey(x => x.FeedTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RawIngredient>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.PricePerMt).HasPrecision(18, 2);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<SpecParameter>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Formulation>(e =>
        {
            e.Property(x => x.SystemId).HasMaxLength(20).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Revision).HasMaxLength(30).IsRequired();
            e.Property(x => x.ImportBatchId).HasMaxLength(50);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.SystemId).IsUnique();
            e.HasOne(x => x.FeedType).WithMany(x => x.Formulations).HasForeignKey(x => x.FeedTypeId);
            e.HasOne(x => x.Species).WithMany(x => x.Formulations).HasForeignKey(x => x.SpeciesId);
            e.HasOne(x => x.Size).WithMany(x => x.Formulations).HasForeignKey(x => x.SizeId);
            e.HasOne(x => x.Category).WithMany(x => x.Formulations).HasForeignKey(x => x.CategoryId);
            e.HasOne(x => x.SubCategory).WithMany(x => x.Formulations).HasForeignKey(x => x.SubCategoryId);
        });

        modelBuilder.Entity<FormulationChangeLog>(e =>
        {
            e.Property(x => x.SystemId).HasMaxLength(20).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(300).IsRequired();
            e.Property(x => x.ImportBatchId).HasMaxLength(50);
            e.HasOne(x => x.Formulation)
                .WithMany(x => x.ChangeLogs)
                .HasForeignKey(x => x.FormulationId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.ChangedAtUtc);
            e.HasIndex(x => x.SystemId);
        });

        modelBuilder.Entity<FormulationIngredient>(e =>
        {
            e.Property(x => x.InclusionPercent).HasPrecision(8, 4);
            e.HasOne(x => x.Formulation)
                .WithMany(x => x.Ingredients)
                .HasForeignKey(x => x.FormulationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RawIngredient)
                .WithMany(x => x.FormulationIngredients)
                .HasForeignKey(x => x.RawIngredientId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.FormulationId, x.RawIngredientId }).IsUnique();
        });

        modelBuilder.Entity<FormulationSpec>(e =>
        {
            e.Property(x => x.TargetValue).HasPrecision(12, 4);
            e.Property(x => x.MinValue).HasPrecision(12, 4);
            e.Property(x => x.MaxValue).HasPrecision(12, 4);
            e.HasOne(x => x.Formulation)
                .WithMany(x => x.Specs)
                .HasForeignKey(x => x.FormulationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SpecParameter)
                .WithMany(x => x.FormulationSpecs)
                .HasForeignKey(x => x.SpecParameterId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.FormulationId, x.SpecParameterId }).IsUnique();
        });

        modelBuilder.Entity<PricingCostOption>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Cost).HasPrecision(18, 2);
            e.HasIndex(x => new { x.Kind, x.Name }).IsUnique();
        });

        modelBuilder.Entity<CostingScenario>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.PackingCost).HasPrecision(18, 2);
            e.Property(x => x.ExportDocCost).HasPrecision(18, 2);
            e.Property(x => x.SpecialAdditiveCost).HasPrecision(18, 2);
            e.Property(x => x.TransportationCost).HasPrecision(18, 2);
            e.Property(x => x.MarginPercent).HasPrecision(8, 2);
            e.Property(x => x.SnapshotRmCost).HasPrecision(18, 2);
            e.Property(x => x.SnapshotConversionCost).HasPrecision(18, 2);
            e.Property(x => x.SnapshotTotalCost).HasPrecision(18, 2);
            e.Property(x => x.SnapshotSellingPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Formulation)
                .WithMany(x => x.CostingScenarios)
                .HasForeignKey(x => x.FormulationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PackingOption)
                .WithMany()
                .HasForeignKey(x => x.PackingOptionId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ExportDocOption)
                .WithMany()
                .HasForeignKey(x => x.ExportDocOptionId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AdditiveOption)
                .WithMany()
                .HasForeignKey(x => x.AdditiveOptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PriceBook>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.PackingCost).HasPrecision(18, 2);
            e.Property(x => x.TransportationCost).HasPrecision(18, 2);
            e.Property(x => x.SpecialAdditiveCost).HasPrecision(18, 2);
            e.Property(x => x.ExportDocCost).HasPrecision(18, 2);
            e.Property(x => x.MarginPercent).HasPrecision(8, 2);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.PackingOption)
                .WithMany()
                .HasForeignKey(x => x.PackingOptionId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ExportDocOption)
                .WithMany()
                .HasForeignKey(x => x.ExportDocOptionId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AdditiveOption)
                .WithMany()
                .HasForeignKey(x => x.AdditiveOptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductionGroup>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ProductionGroupFormulation>(e =>
        {
            e.HasOne(x => x.ProductionGroup)
                .WithMany(x => x.Formulations)
                .HasForeignKey(x => x.ProductionGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Formulation)
                .WithMany()
                .HasForeignKey(x => x.FormulationId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ProductionGroupId, x.FormulationId }).IsUnique();
        });
    }
}
