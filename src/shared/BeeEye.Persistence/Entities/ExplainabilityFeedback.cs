namespace BeeEye.Persistence.Entities;

/// <summary>
/// What a reader thought of an explanation. Stored as text, never as an ordinal, so a renumbered
/// enum cannot silently reinterpret history.
/// </summary>
public enum FeedbackVerdict
{
    /// <summary>The explanation answered the question.</summary>
    Useful,

    /// <summary>Plausible, but the reader wants someone to check it.</summary>
    NeedsReview,

    /// <summary>The reader believes the explanation is wrong.</summary>
    Incorrect,

    /// <summary>Not wrong, but missing something the reader needed.</summary>
    MissingContext,
}

/// <summary>
/// One person's verdict on one explanation (V3-DS-006, "Was this useful?").
/// <para>
/// <b>Why this table exists at all.</b> The v3 prototype renders the same four buttons and its own
/// caption says the answer "is recorded in the analytics platform only" — but <c>explainFeedback()</c>
/// writes to component state and the answer is gone on reload. That is precisely the pattern ADR 0006
/// rejects (V3-GOV-011, browser-local persistence of something the caption claims is recorded), and a
/// control that silently discards input is worse than no control: it spends a reader's goodwill and
/// returns nothing. Either the answer is kept or the buttons do not ship. It is kept.
/// </para>
/// <para>
/// <b>Append-only, like everything else derived here.</b> Changing your mind appends a second row;
/// nothing is updated in place and <b>there is no delete path at any layer</b>. The read returns the
/// latest row per (subject, submitter), so the current opinion is easy to get while the history of how
/// it moved stays intact — which is the part worth having when someone asks whether a change to the
/// engine helped.
/// </para>
/// <para>
/// <b>It retrains nothing.</b> No model consumes this table, and the endpoint's response and the
/// drawer's caption both say so. Feedback that quietly steered a recommendation engine would make
/// every figure on the platform unattributable, which is the opposite of what this panel is for.
/// </para>
/// </summary>
public class ExplainabilityFeedback
{
    /// <summary>Time-ordered v7 GUID, so insertion order is readable from the key itself.</summary>
    public Guid Id { get; set; }

    /// <summary>The kind of subject explained, e.g. <c>inventory-unit</c>. Matches the provider's claim.</summary>
    public string SubjectKind { get; set; } = string.Empty;

    /// <summary>
    /// The subject's identifier, in whatever form the owning context uses — a stock id, a
    /// <c>model|variant</c> pair, a part number. Stored verbatim rather than parsed, because this
    /// table must not need updating when a context changes how it references its own subjects.
    /// </summary>
    public string SubjectRef { get; set; } = string.Empty;

    public FeedbackVerdict Verdict { get; set; }

    /// <summary>Optional free text. Where "Missing context" is chosen, this is the whole value of the row.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// Stable subject id of whoever submitted it — <b>never a display name</b>. The same rule ADR 0006
    /// applies to a decision applies here: the record must survive a rename, a re-grant and a
    /// directory migration.
    /// </summary>
    public string SubmittedBy { get; set; } = string.Empty;

    public DateTimeOffset SubmittedAtUtc { get; set; }

    /// <summary>
    /// The client-supplied <c>Idempotency-Key</c> that created this row (ADR 0007 §2.1). Unique, so a
    /// double-click or a retried request converges on the row it already wrote instead of appending a
    /// second identical verdict.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}
