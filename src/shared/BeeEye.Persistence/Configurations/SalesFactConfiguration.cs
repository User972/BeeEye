using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class SalesFactConfiguration : IEntityTypeConfiguration<SalesFact>
{
    public void Configure(EntityTypeBuilder<SalesFact> b)
    {
        b.ToTable("sales_facts");
        b.HasKey(x => x.Id);

        b.Property(x => x.Location).HasMaxLength(120).IsRequired();
        b.Property(x => x.Model).HasMaxLength(120).IsRequired();
        b.Property(x => x.Variant).HasMaxLength(60).IsRequired();
        b.Property(x => x.Colour).HasMaxLength(60).IsRequired();
        b.Property(x => x.Interior).HasMaxLength(60).IsRequired();
        b.Property(x => x.Brand).HasMaxLength(120).IsRequired();
        b.Property(x => x.Type).HasMaxLength(60).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();

        // Money as decimal — never floating point.
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.Revenue).HasPrecision(18, 2);

        b.Property(x => x.RowHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.RowHash).IsUnique(); // ingestion idempotency

        b.HasIndex(x => new { x.Model, x.Variant, x.Location, x.SaleMonth });
        b.HasIndex(x => x.SaleMonth);
    }
}
