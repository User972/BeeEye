using BeeEye.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BeeEye.Persistence.Configurations;

public sealed class ExplainabilityFeedbackConfiguration : IEntityTypeConfiguration<ExplainabilityFeedback>
{
    public void Configure(EntityTypeBuilder<ExplainabilityFeedback> b)
    {
        b.ToTable("explainability_feedback");
        b.HasKey(x => x.Id);

        b.Property(x => x.SubjectKind).HasMaxLength(40).IsRequired();
        b.Property(x => x.SubjectRef).HasMaxLength(200).IsRequired();
        b.Property(x => x.SubmittedBy).HasMaxLength(120).IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.IdempotencyKey).HasMaxLength(200).IsRequired();

        // Stored as text so the trail stays readable in the database and a renumbered enum cannot
        // silently reinterpret history — the same rule ManagementDecisionConfiguration applies to
        // an outcome.
        b.Property(x => x.Verdict).HasConversion<string>().HasMaxLength(20).IsRequired();

        // A retried submission converges on the row it already wrote rather than appending a second
        // identical verdict.
        b.HasIndex(x => x.IdempotencyKey).IsUnique();

        // Drives the read: newest verdict per subject. Deliberately *not* unique on
        // (SubjectKind, SubjectRef, SubmittedBy) — this table is append-only, so one person changing
        // their mind must be able to add a row rather than overwrite the one they left before.
        b.HasIndex(x => new { x.SubjectKind, x.SubjectRef, x.SubmittedAtUtc });
    }
}
