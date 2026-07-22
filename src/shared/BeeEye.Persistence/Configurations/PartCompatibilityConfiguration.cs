using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class PartCompatibilityConfiguration : IEntityTypeConfiguration<PartCompatibility>
{
    public void Configure(EntityTypeBuilder<PartCompatibility> b)
    {
        b.ToTable("part_compatibilities");
        b.HasKey(x => x.Id);

        b.Property(x => x.Model).HasMaxLength(120).IsRequired();

        b.HasIndex(x => new { x.PartId, x.Model }).IsUnique();
        b.HasIndex(x => x.Model);
    }
}
