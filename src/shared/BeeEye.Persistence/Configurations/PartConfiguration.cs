using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class PartConfiguration : IEntityTypeConfiguration<Part>
{
    public void Configure(EntityTypeBuilder<Part> b)
    {
        b.ToTable("parts");
        b.HasKey(x => x.Id);

        b.Property(x => x.PartNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.Category).HasMaxLength(60).IsRequired();

        // Part unit cost carries higher precision than sale money.
        b.Property(x => x.UnitCost).HasPrecision(18, 4);

        b.HasIndex(x => x.PartNumber).IsUnique();
        b.HasIndex(x => x.Category);
    }
}
