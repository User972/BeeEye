using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class PartSupersessionConfiguration : IEntityTypeConfiguration<PartSupersession>
{
    public void Configure(EntityTypeBuilder<PartSupersession> b)
    {
        b.ToTable("part_supersessions");
        b.HasKey(x => x.Id);

        b.HasIndex(x => x.OldPartId);
        b.HasIndex(x => x.NewPartId);
    }
}
