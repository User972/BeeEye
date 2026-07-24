namespace BeeEye.Modules.PlatformAdministration.Contracts;

/// <summary>
/// The read-only Settings payload (V3-GOV-010): the actual risk configuration the UC5 engine uses,
/// surfaced for transparency. Every value is projected from the C# constants, never re-typed — so the
/// screen can never silently disagree with the engine it describes. There is deliberately no
/// cover-target field: the wireframe's <c>coverTarget</c> was never ported to <c>RiskSettings</c>, and a
/// transparency screen must not invent a value the engine does not use.
/// </summary>
public sealed record SettingsResponse(
    RiskWeightsDto Weights,
    IReadOnlyList<BandDto> RiskBands,
    IReadOnlyList<BandDto> AgingBands,
    string AnalysisDate,
    int TrailingMonths,
    double CoverMax,
    string Note);

/// <summary>
/// The five risk-factor weights and their sum. The engine renormalises by the sum, so they need not
/// total 100 — <paramref name="Note"/> states this.
/// </summary>
public sealed record RiskWeightsDto(
    double Cover,
    double Aging,
    double Demand,
    double Holding,
    double Lead,
    double Sum,
    string Note);

/// <summary>
/// One labelled band. <paramref name="Threshold"/> is the inclusive upper bound taken straight from the
/// engine's thresholds (null for the open-ended top band); <paramref name="Range"/> is a display string.
/// </summary>
public sealed record BandDto(string Label, int? Threshold, string Range);
