namespace BeeEye.Analytics.Explainability;

/// <summary>
/// What kind of output a figure is — the platform's shared vocabulary for telling an observed fact
/// apart from a calculation, a forecast, a recommendation and a simulation.
/// <para>
/// Ported from the <c>LABELS</c> table in <c>docs/wireframes-v3/engine2.js</c> (L28–37). <b>All eight
/// labels are implemented.</b> <c>README.md</c> previously documented seven, omitting
/// <see cref="DataQuality"/>; the code was the source of truth and the README was corrected
/// (V3-CONFLICT-8).
/// </para>
/// <para>
/// <see cref="Demo"/> is <b>not exclusive</b> with the others. A forecast computed from the
/// synthetic-demo UC6/UC7 dataset is a <see cref="Forecast"/> that also carries
/// <c>Explanation.IsDemoData</c> and a <see cref="LineageKind.Demo"/> node. Demo-ness is a property of
/// the <i>data</i>, not of the kind of output produced from it, so the two are recorded separately.
/// </para>
/// </summary>
public enum OutputLabel
{
    /// <summary>A fact read from a source system, unmodified.</summary>
    Observed,

    /// <summary>Derived arithmetically from observed facts. Deterministic and reproducible.</summary>
    Calculated,

    /// <summary>A projection beyond the observed window, carrying uncertainty.</summary>
    Forecast,

    /// <summary>An advisory action the engine proposes. A human decides (ADR 0006).</summary>
    Recommendation,

    /// <summary>A what-if result under caller-supplied assumptions, not a prediction of what will happen.</summary>
    Simulation,

    /// <summary>Derived from the clearly-labelled synthetic-demo dataset, not from Oracle Fusion.</summary>
    Demo,

    /// <summary>Produced from evidence too thin to rely on. Shown, but shown as weak.</summary>
    LowConfidence,

    /// <summary>A data-quality finding rather than a business figure.</summary>
    DataQuality,
}

/// <summary>The provenance of one input to an explanation.</summary>
public enum LineageKind
{
    /// <summary>Oracle Fusion. BeeEye reads it and never writes to it.</summary>
    Fusion,

    /// <summary>A supplied ADMC workbook, imported by the sample-data importer.</summary>
    Workbook,

    /// <summary>A synthetic-demo fixture. Must always be visible as such.</summary>
    Demo,

    /// <summary>Computed by BeeEye from the nodes above.</summary>
    Derived,
}

/// <summary>How an expected-impact figure should read: good news, bad news, or neither.</summary>
/// <remarks>
/// An enum, never a CSS colour. v3 inlines <c>var(--risk-high)</c> into its view model because it has
/// no stylesheet; this platform does, and a colour baked into a contract is a colour that cannot be
/// restyled, themed or made accessible without a new API version.
/// </remarks>
public enum ImpactTone
{
    Neutral,
    Positive,
    Negative,
    Warning,
}

/// <summary>How much weight the engine's own output deserves.</summary>
public enum ConfidenceBand
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The wire vocabulary for the enums above.
/// <para>
/// The keys are v3's, verbatim, so the <c>AiLabel</c> component and the <c>LABELS</c> table it was
/// ported from agree without a translation step. Each map is asserted <b>complete by reflection</b> in
/// <c>tests/unit/BeeEye.UnitTests/Explainability/ExplanationTests.cs</c>: adding a ninth label without
/// a key fails there rather than shipping a chip the browser cannot render.
/// </para>
/// </summary>
public static class ExplanationVocabulary
{
    private static readonly IReadOnlyDictionary<OutputLabel, string> LabelKeys =
        new Dictionary<OutputLabel, string>
        {
            [OutputLabel.Observed] = "observed",
            [OutputLabel.Calculated] = "calculated",
            [OutputLabel.Forecast] = "forecast",
            [OutputLabel.Recommendation] = "recommendation",
            [OutputLabel.Simulation] = "simulation",
            [OutputLabel.Demo] = "demo",

            // v3's keys, not the enum names: "low" and "dq" are what engine2.js writes and what the
            // AiLabel component keys on.
            [OutputLabel.LowConfidence] = "low",
            [OutputLabel.DataQuality] = "dq",
        };

    private static readonly IReadOnlyDictionary<LineageKind, string> LineageKeys =
        new Dictionary<LineageKind, string>
        {
            [LineageKind.Fusion] = "fusion",
            [LineageKind.Workbook] = "workbook",
            [LineageKind.Demo] = "demo",
            [LineageKind.Derived] = "derived",
        };

    private static readonly IReadOnlyDictionary<ImpactTone, string> ToneKeys =
        new Dictionary<ImpactTone, string>
        {
            [ImpactTone.Neutral] = "neutral",
            [ImpactTone.Positive] = "positive",
            [ImpactTone.Negative] = "negative",
            [ImpactTone.Warning] = "warning",
        };

    /// <summary>Every label key, in enum order. Used by the completeness tests and the OpenAPI docs.</summary>
    public static IReadOnlyList<string> AllLabelKeys => [.. LabelKeys.Values];

    /// <summary>Every lineage key, in enum order.</summary>
    public static IReadOnlyList<string> AllLineageKeys => [.. LineageKeys.Values];

    /// <summary>The wire key for an output label.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If the label has no key. Thrown rather than defaulted: a silent fallback would put a
    /// "Recommendation" chip on something that is not one, which is the single most misleading thing
    /// this vocabulary could do.
    /// </exception>
    public static string KeyFor(OutputLabel label) =>
        LabelKeys.TryGetValue(label, out var key)
            ? key
            : throw new ArgumentOutOfRangeException(
                nameof(label),
                label,
                $"No wire key is defined for {nameof(OutputLabel)}.{label}. Add one to "
                + $"{nameof(ExplanationVocabulary)} and to the AiLabel component's chip table.");

    /// <summary>The wire key for a lineage node kind.</summary>
    /// <exception cref="ArgumentOutOfRangeException">If the kind has no key.</exception>
    public static string KeyFor(LineageKind kind) =>
        LineageKeys.TryGetValue(kind, out var key)
            ? key
            : throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                $"No wire key is defined for {nameof(LineageKind)}.{kind}.");

    /// <summary>The wire key for an impact tone.</summary>
    /// <exception cref="ArgumentOutOfRangeException">If the tone has no key.</exception>
    public static string KeyFor(ImpactTone tone) =>
        ToneKeys.TryGetValue(tone, out var key)
            ? key
            : throw new ArgumentOutOfRangeException(
                nameof(tone),
                tone,
                $"No wire key is defined for {nameof(ImpactTone)}.{tone}.");
}
