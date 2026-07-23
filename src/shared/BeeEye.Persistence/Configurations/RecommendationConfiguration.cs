using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> b)
    {
        b.ToTable("recommendations");
        b.HasKey(x => x.Id);

        b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.SubjectRef).HasMaxLength(200).IsRequired();
        b.Property(x => x.Area).HasMaxLength(60).IsRequired();
        b.Property(x => x.RuleId).HasMaxLength(40).IsRequired();

        b.Property(x => x.Action).HasMaxLength(120).IsRequired();
        b.Property(x => x.Rationale).HasMaxLength(2000).IsRequired();
        b.Property(x => x.EvidenceJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.ExpectedOutcome).HasMaxLength(500).IsRequired();
        b.Property(x => x.Confidence).HasMaxLength(20).IsRequired();
        b.Property(x => x.AssumptionsJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.OwnerRole).HasMaxLength(80).IsRequired();

        // Money as decimal with explicit precision — never floating point.
        b.Property(x => x.ImpactSar).HasPrecision(18, 2);

        b.Property(x => x.RulesetVersion).HasMaxLength(40).IsRequired();
        b.Property(x => x.DatasetVersion).HasMaxLength(80).IsRequired();

        // Stored as text so the log stays readable in the database and a renumbered enum cannot
        // silently reinterpret history.
        b.Property(x => x.CurrentStatus).HasConversion<string>().HasMaxLength(30).IsRequired();

        // PostgreSQL xmin as the concurrency token: a stale update fails instead of overwriting.
        b.Property(x => x.Version).IsRowVersion();

        // Idempotency (ADR 0007): re-running a generation for the same subject, ruleset and analysis
        // date collides here rather than creating a duplicate business record.
        b.HasIndex(x => x.IdempotencyKey).IsUnique();

        b.HasIndex(x => new { x.CurrentStatus, x.Priority });
        b.HasIndex(x => x.SubjectRef);
        b.HasIndex(x => x.AnalysisDate);

        b.HasOne<Recommendation>()
            .WithMany()
            .HasForeignKey(x => x.SupersededByRecommendationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.StatusEvents)
            .WithOne(e => e.Recommendation!)
            .HasForeignKey(e => e.RecommendationId)
            // Restrict, not Cascade: the audit trail must survive any attempt to remove its subject.
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RecommendationStatusEventConfiguration : IEntityTypeConfiguration<RecommendationStatusEvent>
{
    public void Configure(EntityTypeBuilder<RecommendationStatusEvent> b)
    {
        b.ToTable("recommendation_status_events");
        b.HasKey(x => x.Id);

        b.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Actor).HasMaxLength(120).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000);

        b.HasIndex(x => new { x.RecommendationId, x.AtUtc });
    }
}
