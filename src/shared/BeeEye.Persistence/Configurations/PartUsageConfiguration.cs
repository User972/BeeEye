using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class PartUsageConfiguration : IEntityTypeConfiguration<PartUsage>
{
    public void Configure(EntityTypeBuilder<PartUsage> b)
    {
        b.ToTable("part_usages");
        b.HasKey(x => x.Id);

        b.Property(x => x.Vin).HasMaxLength(24).IsRequired();
        b.Property(x => x.Model).HasMaxLength(120).IsRequired();

        b.HasIndex(x => new { x.PartId, x.UsageDate });
        b.HasIndex(x => x.Model);
    }
}
