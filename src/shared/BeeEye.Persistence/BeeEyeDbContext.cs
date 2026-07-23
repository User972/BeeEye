using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Persistence;

/// <summary>The operational EF Core context for the BeeEye platform.</summary>
public class BeeEyeDbContext(DbContextOptions<BeeEyeDbContext> options) : DbContext(options)
{
    public DbSet<SalesFact> SalesFacts => Set<SalesFact>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<IngestionBatch> IngestionBatches => Set<IngestionBatch>();

    // After-sales & spare parts (UC6/UC7) — synthetic-demo data (see SyntheticAfterSalesImporter).
    public DbSet<VehicleSale> VehicleSales => Set<VehicleSale>();
    public DbSet<ServiceEvent> ServiceEvents => Set<ServiceEvent>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<PartCompatibility> PartCompatibilities => Set<PartCompatibility>();
    public DbSet<PartSupersession> PartSupersessions => Set<PartSupersession>();
    public DbSet<PartUsage> PartUsages => Set<PartUsage>();

    // Decision workflow (ADR 0006). Recommendations are frozen at generation; the status-event log is
    // append-only and is the source of truth for a recommendation's current state.
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RecommendationStatusEvent> RecommendationStatusEvents => Set<RecommendationStatusEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BeeEyeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
