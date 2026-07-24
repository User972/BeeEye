namespace BeeEye.Analytics.Decisions;

/// <summary>How urgently a decision needs attention. Drives the due-date window and the cockpit's
/// critical count.</summary>
public enum DecisionSeverity
{
    Low,
    Medium,
    High,
}

/// <summary>Whether acting on a decision avoids a loss or captures an upside. Kept separate from
/// <see cref="DecisionSeverity"/> so the cockpit can total risk and opportunity independently.</summary>
public enum DecisionKind
{
    Risk,
    Opportunity,
}

/// <summary>One named 0–100 contribution to a decision's priority, shown as a ranked driver in the
/// explainability drawer.</summary>
public sealed record DecisionFactor(string Name, int Percent);

/// <summary>
/// The Executive Decision Cockpit's priority model (UC8).
/// <para>
/// A faithful port of <c>priorityScore()</c> and <c>mkDecision()</c> from
/// <c>docs/wireframes-v3/engine2.js</c> (lines 511–513 and 559). The score is deliberately
/// <b>multiplicative</b>, not a weighted sum: a decision that is cheap, slow-burning, uncertain or
/// outside the business's control is suppressed on every one of those grounds independently, so a
/// single very weak factor cannot be masked by three strong ones.
/// </para>
/// <para>
/// This type is pure and deterministic — no clock, no randomness, no I/O — so the whole priority
/// model is exhaustively unit-testable. Monetary impact is <see cref="decimal"/> throughout, per the
/// platform's financial-integrity rule; the 0–1 weightings are unitless <see cref="double"/>s.
/// </para>
/// </summary>
public static class DecisionPriority
{
    /// <summary>Impact at or above which the impact factor saturates at 1.0 (SAR 5,000,000).</summary>
    public const decimal ImpactCeilingSar = 5_000_000m;

    /// <summary>
    /// Floor for the impact factor. A low-value decision is damped but never zeroed — otherwise a
    /// cheap, certain, urgent and fully controllable action would score 0 and never surface.
    /// </summary>
    public const double MinImpactFactor = 0.15;

    /// <summary>Confidence at or above which a decision is labelled "High".</summary>
    public const double HighConfidenceThreshold = 0.75;

    /// <summary>Confidence at or above which a decision is labelled "Medium".</summary>
    public const double MediumConfidenceThreshold = 0.5;

    /// <summary>
    /// Normalises a monetary impact to a unitless 0.15–1.0 factor. Negative impacts are treated as
    /// their magnitude: an exposure of −SAR 2M is as material as an upside of SAR 2M.
    /// </summary>
    public static double ImpactFactor(decimal impactSar)
    {
        var magnitude = Math.Abs(impactSar);
        var ratio = magnitude >= ImpactCeilingSar
            ? 1.0
            : (double)(magnitude / ImpactCeilingSar);

        return Clamp01(ratio, MinImpactFactor);
    }

    /// <summary>
    /// The 0–100 priority score. Each argument is clamped to 0–1 before multiplying, so an
    /// out-of-range caller cannot inflate a score.
    /// </summary>
    public static int Score(double impactFactor, double urgency, double confidence, double controllability)
    {
        var product =
            Clamp01(impactFactor) *
            Clamp01(urgency) *
            Clamp01(confidence) *
            Clamp01(controllability);

        // AwayFromZero matches JavaScript's Math.round; the .NET default is banker's rounding and
        // would disagree on exact .5 boundaries.
        return (int)Math.Round(Clamp01(product) * 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>Convenience overload that derives the impact factor from a monetary impact.</summary>
    public static int ScoreFor(decimal impactSar, double urgency, double confidence, double controllability) =>
        Score(ImpactFactor(impactSar), urgency, confidence, controllability);

    /// <summary>
    /// Review window in days before the decision is considered overdue. Fixed per severity so the
    /// due date is reproducible from the pinned analysis date rather than wall-clock "now".
    /// </summary>
    public static int DueDays(DecisionSeverity severity) => severity switch
    {
        DecisionSeverity.High => 5,
        DecisionSeverity.Medium => 12,
        _ => 20,
    };

    /// <summary>Human-facing confidence band for a 0–1 confidence.</summary>
    public static string ConfidenceBand(double confidence01) => confidence01 switch
    {
        > HighConfidenceThreshold => "High",
        > MediumConfidenceThreshold => "Medium",
        _ => "Low",
    };

    /// <summary>
    /// The inverse of <see cref="ConfidenceBand"/>: turns an analytics confidence label back into a
    /// 0–1 weighting for the priority model. Matches <c>conf01()</c> in
    /// <c>docs/wireframes-v3/engine2.js</c> (L522). Unrecognised labels fall to the low weighting,
    /// so an unexpected value suppresses a decision rather than promoting it.
    /// </summary>
    public static double ConfidenceWeight(string? band) => band switch
    {
        "High" => 0.9,
        "Medium" => 0.65,
        _ => 0.4,
    };

    /// <summary>
    /// The four ranked drivers behind a score, as whole percentages — the payload the
    /// "Why this recommendation?" drawer renders under "Top drivers".
    /// </summary>
    public static IReadOnlyList<DecisionFactor> Factors(
        decimal impactSar, double urgency, double confidence, double controllability) =>
    [
        new("Business impact", Percent(ImpactFactor(impactSar))),
        new("Urgency", Percent(urgency)),
        new("Confidence", Percent(confidence)),
        new("Controllability", Percent(controllability)),
    ];

    private static int Percent(double value) =>
        (int)Math.Round(Clamp01(value) * 100, MidpointRounding.AwayFromZero);

    /// <summary>Clamps to <paramref name="min"/>–1, mapping NaN to <paramref name="min"/>.</summary>
    private static double Clamp01(double value, double min = 0.0)
    {
        if (double.IsNaN(value))
        {
            return min;
        }

        return Math.Clamp(value, min, 1.0);
    }
}
