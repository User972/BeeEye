using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class VehicleSaleConfiguration : IEntityTypeConfiguration<VehicleSale>
{
    public void Configure(EntityTypeBuilder<VehicleSale> b)
    {
        b.ToTable("vehicle_sales");
        b.HasKey(x => x.Id);

        b.Property(x => x.Vin).HasMaxLength(24).IsRequired();
        b.Property(x => x.Model).HasMaxLength(120).IsRequired();
        b.Property(x => x.Variant).HasMaxLength(60).IsRequired();
        b.Property(x => x.Colour).HasMaxLength(60).IsRequired();
        b.Property(x => x.Location).HasMaxLength(120).IsRequired();

        b.HasIndex(x => x.Vin).IsUnique();
        b.HasIndex(x => new { x.Model, x.SaleMonth });
        b.HasIndex(x => x.Location);
    }
}
