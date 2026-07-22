using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Persistence;

/// <summary>The operational EF Core context for the BeeEye platform.</summary>
public class BeeEyeDbContext(DbContextOptions<BeeEyeDbContext> options) : DbContext(options)
{
    public DbSet<SalesFact> SalesFacts => Set<SalesFact>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<IngestionBatch> IngestionBatches => Set<IngestionBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BeeEyeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
