using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class IngestionBatchConfiguration : IEntityTypeConfiguration<IngestionBatch>
{
    public void Configure(EntityTypeBuilder<IngestionBatch> b)
    {
        b.ToTable("ingestion_batches");
        b.HasKey(x => x.Id);

        b.Property(x => x.SourceSystem).HasMaxLength(80).IsRequired();
        b.Property(x => x.SourceObject).HasMaxLength(80).IsRequired();
        b.Property(x => x.Checksum).HasMaxLength(64).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.Status).HasMaxLength(40).IsRequired();

        // Batch identity — one completed batch per (object, checksum).
        b.HasIndex(x => new { x.SourceObject, x.Checksum }).IsUnique();
    }
}
