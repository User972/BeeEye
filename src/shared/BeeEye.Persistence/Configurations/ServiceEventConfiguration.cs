using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class ServiceEventConfiguration : IEntityTypeConfiguration<ServiceEvent>
{
    public void Configure(EntityTypeBuilder<ServiceEvent> b)
    {
        b.ToTable("service_events");
        b.HasKey(x => x.Id);

        b.Property(x => x.Vin).HasMaxLength(24).IsRequired();
        b.Property(x => x.Model).HasMaxLength(120).IsRequired();
        b.Property(x => x.Variant).HasMaxLength(60).IsRequired();
        b.Property(x => x.Location).HasMaxLength(120).IsRequired();
        b.Property(x => x.MileageBand).HasMaxLength(20).IsRequired();
        b.Property(x => x.ServiceType).HasMaxLength(20).IsRequired();

        b.Property(x => x.LaborHours).HasPrecision(9, 2);

        b.HasIndex(x => new { x.Model, x.ServiceDate });
        b.HasIndex(x => x.Vin);
    }
}
