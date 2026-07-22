using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> b)
    {
        b.ToTable("inventory_items");
        b.HasKey(x => x.Id);

        b.Property(x => x.StockId).HasMaxLength(60).IsRequired();
        b.Property(x => x.ChassisNo).HasMaxLength(60).IsRequired();
        b.Property(x => x.Model).HasMaxLength(120).IsRequired();
        b.Property(x => x.Variant).HasMaxLength(60).IsRequired();
        b.Property(x => x.Colour).HasMaxLength(60).IsRequired();
        b.Property(x => x.Interior).HasMaxLength(60).IsRequired();
        b.Property(x => x.Brand).HasMaxLength(120).IsRequired();
        b.Property(x => x.Type).HasMaxLength(60).IsRequired();
        b.Property(x => x.Location).HasMaxLength(120).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        b.Property(x => x.PurchasePrice).HasPrecision(18, 2);
        b.Property(x => x.HoldingCostPerDay).HasPrecision(18, 4);

        b.HasIndex(x => x.StockId).IsUnique(); // idempotent upsert
        b.HasIndex(x => new { x.Location, x.Model, x.Variant });
    }
}
