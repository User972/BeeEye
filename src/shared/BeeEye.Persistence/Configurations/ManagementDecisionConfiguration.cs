using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class ManagementDecisionConfiguration : IEntityTypeConfiguration<ManagementDecision>
{
    public void Configure(EntityTypeBuilder<ManagementDecision> b)
    {
        b.ToTable("management_decisions");
        b.HasKey(x => x.Id);

        b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.OpenedBy).HasMaxLength(120).IsRequired();
        b.Property(x => x.DecidedBy).HasMaxLength(120);
        b.Property(x => x.ImplementedBy).HasMaxLength(120);
        b.Property(x => x.Note).HasMaxLength(2000);

        // Stored as text so the trail stays readable in the database and a renumbered enum cannot
        // silently reinterpret history — the same rule RecommendationConfiguration applies to status.
        b.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30).IsRequired();

        // The modification delta. jsonb so it is queryable, and nullable because most decisions
        // change nothing.
        b.Property(x => x.ModificationJson).HasColumnType("jsonb");

        // PostgreSQL xmin as the concurrency token: a stale update fails instead of overwriting.
        b.Property(x => x.Version).IsRowVersion();

        // A retried claim converges on the decision it already opened rather than opening a second.
        b.HasIndex(x => x.IdempotencyKey).IsUnique();

        // ---------------------------------------------------------------------
        // Exactly one *open* decision per recommendation.
        //
        // The application checks for an existing open decision before claiming, but that check is
        // only an optimisation — two requests can both read "none" before either writes. This
        // filtered unique index is the actual guarantee: the second insert loses on the index and is
        // reported as "someone else claimed it first" rather than quietly producing two owners for
        // one recommendation. The same reasoning S5 applied to the generation key applies here.
        //
        // Filtered on the *decided* states rather than a boolean flag, so a rejected decision does
        // not block a later one if the workflow ever permits re-opening.
        // ---------------------------------------------------------------------
        b.HasIndex(x => x.RecommendationId)
            .IsUnique()
            .HasFilter("\"Outcome\" = 'Open'")
            .HasDatabaseName("IX_management_decisions_open_per_recommendation");

        // Drives the decision-log query: filter by outcome, order by when it was opened.
        b.HasIndex(x => new { x.Outcome, x.OpenedAtUtc });

        b.HasOne(x => x.Recommendation)
            .WithMany()
            .HasForeignKey(x => x.RecommendationId)
            // Restrict, not Cascade: the trail must survive any attempt to remove its subject,
            // exactly as the status-event log does.
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.ApprovalSteps)
            .WithOne(s => s.Decision!)
            .HasForeignKey(s => s.DecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ActionOutcome)
            .WithOne(o => o.Decision!)
            .HasForeignKey<ActionOutcome>(o => o.DecisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> b)
    {
        b.ToTable("approval_steps");
        b.HasKey(x => x.Id);

        b.Property(x => x.ApproverRole).HasMaxLength(80).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.ActedBy).HasMaxLength(120);
        b.Property(x => x.Note).HasMaxLength(2000);

        // One row per position in the chain: a second "step 2" would make the order meaningless.
        b.HasIndex(x => new { x.DecisionId, x.StepNumber }).IsUnique();
    }
}

public sealed class ActionOutcomeConfiguration : IEntityTypeConfiguration<ActionOutcome>
{
    public void Configure(EntityTypeBuilder<ActionOutcome> b)
    {
        b.ToTable("action_outcomes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Metric).HasMaxLength(120).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(20);
        b.Property(x => x.RecordedBy).HasMaxLength(120).IsRequired();
        b.Property(x => x.Note).HasMaxLength(2000);

        // Money as decimal with explicit precision — never floating point.
        b.Property(x => x.RealisedValue).HasPrecision(18, 2);

        // One outcome per decision (canonical model Cluster 8's ||--o|).
        b.HasIndex(x => x.DecisionId).IsUnique();
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> b)
    {
        b.ToTable("idempotency_records");
        b.HasKey(x => x.Id);

        b.Property(x => x.Key).HasMaxLength(128).IsRequired();
        b.Property(x => x.Route).HasMaxLength(300).IsRequired();
        b.Property(x => x.RequestFingerprint).HasMaxLength(64).IsRequired();
        b.Property(x => x.PrincipalId).HasMaxLength(120).IsRequired();
        b.Property(x => x.ResponseBody).IsRequired();

        // The concurrency guard from ADR 0007 §2.1: two simultaneous submissions of one key race
        // here, and the loser is refused rather than applied twice.
        b.HasIndex(x => x.Key).IsUnique();

        // Supports the retention sweep.
        b.HasIndex(x => x.ExpiresAtUtc);
    }
}
