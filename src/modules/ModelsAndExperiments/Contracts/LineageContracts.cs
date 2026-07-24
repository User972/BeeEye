namespace BeeEye.Modules.ModelsAndExperiments.Contracts;

/// <summary>The Lineage payload (V3-GOV-009): the end-to-end pipeline and the per-metric provenance.</summary>
public sealed record LineageResponse(
    IReadOnlyList<PipelineStageDto> Pipeline,
    IReadOnlyList<LineageMetricDto> Metrics);

/// <summary>One stage of the source-to-decision pipeline. <paramref name="Kind"/> drives the stepper icon token.</summary>
public sealed record PipelineStageDto(string Title, string Description, string Icon, string Kind);

/// <summary>
/// One decision metric with its source and basis. <paramref name="State"/> is <c>confirmed</c> or
/// <c>demo</c>; a metric is <c>demo</c> exactly when its basis is a synthetic fixture.
/// </summary>
public sealed record LineageMetricDto(string Metric, string Source, string Basis, string State);
