using System.Globalization;

namespace BeeEye.Analytics.Explainability;

/// <summary>
/// One tile in the drawer's "Expected impact" grid.
/// </summary>
/// <param name="Label">What is being quantified, e.g. "Holding cost avoided".</param>
/// <param name="Value">
/// The figure, <b>pre-formatted on the server</b> with <see cref="CultureInfo.InvariantCulture"/>.
/// <para>
/// A string rather than a number by deliberate choice. The server knows whether a figure is money,
/// a count, a percentage or a range; the browser would have to be told, and every client that
/// forgot would render "72095231.5" to an executive. Formatting once, invariantly, on the side that
/// owns the unit is the only version of this that stays correct in every locale
/// (<c>InvariantGlobalization</c> is on for the whole solution).
/// </para>
/// </param>
/// <param name="Tone">How the figure reads. An enum, never a colour — see <see cref="ImpactTone"/>.</param>
public sealed record ImpactTile(string Label, string Value, ImpactTone Tone);

/// <summary>
/// How much weight the engine's output deserves, and why.
/// </summary>
/// <param name="Band">The band the engine actually computed.</param>
/// <param name="Percent">
/// An optional 0–100 confidence, where the engine produces one. Absent is not zero.
/// </param>
/// <param name="Why">
/// The reasons behind the band, in the engine's own words. Empty is permitted but discouraged: a
/// band with no reasons is an assertion, and the whole point of this panel is to stop asserting.
/// </param>
public sealed record ConfidenceStatement(ConfidenceBand Band, int? Percent, IReadOnlyList<string> Why);

/// <summary>
/// One ranked reason behind the output, most significant first.
/// </summary>
/// <param name="Label">The driver in plain language.</param>
/// <param name="Detail">The number behind it, pre-formatted invariantly. Rendered in a mono face.</param>
public sealed record Driver(string Label, string? Detail);

/// <summary>One point on the historical-evidence chart.</summary>
/// <param name="Label">The x-axis label — a month key, a factor name, a band.</param>
/// <param name="Value">The measured or computed value.</param>
/// <param name="Comparison">
/// The value to compare against, where the series has one (fitted vs actual). Null for a single-series
/// chart, which is what the additive risk-factor breakdown produces.
/// </param>
public sealed record EvidencePoint(string Label, decimal Value, decimal? Comparison);

/// <summary>
/// The evidence behind an output, as a series the drawer can chart.
/// <para>
/// Only supplied where the screen the drawer opens over already has a chart to give it — the UC2
/// forecast and the UC5 aging breakdown. Everywhere else the section is <b>omitted</b>. A placeholder
/// chart drawn from nothing would be the exact failure this panel exists to prevent.
/// </para>
/// </summary>
/// <param name="Period">The window the series covers, appended to the section heading.</param>
/// <param name="Points">Ordered points. An empty list omits the section.</param>
/// <param name="Note">An optional caveat rendered beneath the chart.</param>
/// <param name="ValueLabel">Legend text for <see cref="EvidencePoint.Value"/>.</param>
/// <param name="ComparisonLabel">
/// Legend text for <see cref="EvidencePoint.Comparison"/>, where the series carries one.
/// </param>
public sealed record EvidenceSeries(
    string Period,
    IReadOnlyList<EvidencePoint> Points,
    string? Note,
    string ValueLabel = "Value",
    string? ComparisonLabel = null);

/// <summary>One input the output was computed from.</summary>
/// <param name="Label">The source in the reader's language, e.g. "Sales workbook (ADMC)".</param>
/// <param name="Kind">Its provenance, which drives the chip's icon and colour.</param>
public sealed record LineageNode(string Label, LineageKind Kind);

/// <summary>
/// What produced the number, in the six fields v3's "Model / rule information" grid shows.
/// <para>
/// Every live use case is <b>rule- or statistics-based</b>, not a trained model, and the fields say
/// so rather than inventing model metadata to fill a grid. <see cref="Validation"/> reading
/// "Rule set" and <see cref="Error"/> reading "rule-based" is the honest answer for a deterministic
/// optimiser, and is what v3 itself writes for its priority model.
/// </para>
/// </summary>
/// <param name="Name">The model or rule set, e.g. "Overstock risk model".</param>
/// <param name="Version">Its version. Rendered in a mono face.</param>
/// <param name="Recalculated">When the figure was last computed, as a display string.</param>
/// <param name="Horizon">The window it speaks to, e.g. "3 months".</param>
/// <param name="Validation">How it was validated, e.g. "Hold-out back-test (6 months)".</param>
/// <param name="Error">Its measured error, or "rule-based" where the question does not apply.</param>
public sealed record ModelInfo(
    string Name,
    string Version,
    string Recalculated,
    string Horizon,
    string Validation,
    string Error);

/// <summary>
/// Who owns the decision and where it stands. Present only where the subject <i>is</i> a decision —
/// its presence is what makes the drawer render workflow actions in its footer.
/// </summary>
/// <param name="OwnerRole">The role expected to act, never a person's display name.</param>
/// <param name="Status">Where it stands, in the Decision Log's own vocabulary.</param>
public sealed record Ownership(string OwnerRole, string Status);

/// <summary>
/// The complete answer to "why does the platform say this?" for one subject.
/// <para>
/// <b>Nothing here is authored by a model.</b> Every field restates what the deterministic engine
/// already computed and the provenance it computed it from (ADR 0006 §2.6,
/// <c>docs/architecture/overview.md</c> §8 — <i>GenAI narrates, never decides</i>). Live narration is
/// S10; this slice deliberately ships none.
/// </para>
/// <para>
/// Optional members are optional <b>on purpose</b>. A null section is omitted by the drawer rather
/// than rendered empty, and — critically — <see cref="Confidence"/> is null when the engine computed
/// no band. v3 defaults it to "Medium"; that is an invented assertion wearing the costume of a
/// default, and it is not reproduced here.
/// </para>
/// </summary>
/// <param name="Title">The subject, named the way the screen names it.</param>
/// <param name="Module">The business area answering, shown beneath the title.</param>
/// <param name="Label">
/// What kind of output this is. <b>Required.</b> A provider that cannot say whether it is publishing
/// an observation, a calculation or a recommendation has not finished thinking about it.
/// </param>
/// <param name="Recommendation">The advised action, where there is one. Null omits the section.</param>
/// <param name="Impacts">Expected-impact tiles. Empty omits the section.</param>
/// <param name="Confidence">The computed band, or null when none was computed.</param>
/// <param name="Drivers">Ranked reasons, most significant first. Empty omits the section.</param>
/// <param name="Evidence">A chartable series, where the subject has one. Null omits the section.</param>
/// <param name="Assumptions">
/// Every assumption the figure rests on, stated plainly. An assumed labour rate or an assumed lead
/// time belongs <b>here</b>, not buried in an evidence string where nobody reads it.
/// </param>
/// <param name="Lineage">Where the inputs came from. Empty omits the section.</param>
/// <param name="Model">Model or rule-set metadata. Null omits the section.</param>
/// <param name="Ownership">Owner and status, where the subject is a decision. Null omits the section.</param>
/// <param name="IsDemoData">
/// True when the figure derives from the synthetic-demo dataset.
/// <para>
/// An explicit flag rather than something a caller infers from a <see cref="LineageKind.Demo"/> node.
/// Inference would be a rule two places have to agree on forever, and the day they disagree is the day
/// a synthetic figure loses its label in front of an executive.
/// </para>
/// </param>
public sealed record Explanation(
    string Title,
    string Module,
    OutputLabel Label,
    string? Recommendation,
    IReadOnlyList<ImpactTile> Impacts,
    ConfidenceStatement? Confidence,
    IReadOnlyList<Driver> Drivers,
    EvidenceSeries? Evidence,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<LineageNode> Lineage,
    ModelInfo? Model,
    Ownership? Ownership,
    bool IsDemoData = false);
