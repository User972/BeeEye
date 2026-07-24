namespace BeeEye.Modules.DataQuality.Contracts;

/// <summary>
/// The Data Health payload (V3-GOV-008): the data-quality score and its band, the real row counts and
/// coverage, and the seven governed data sources with an honest real/demo/blocked status on each.
/// </summary>
/// <param name="Score">Data-quality score, 0–100.</param>
/// <param name="ScoreBand">Healthy (≥85) · Warning (≥70) · Critical (&lt;70).</param>
/// <param name="SalesRows">Real sales-fact rows in the operational store.</param>
/// <param name="InvRows">Real inventory-item rows in the operational store.</param>
/// <param name="Coverage">Human-readable sales-history month range, or "—" when there are no sales.</param>
/// <param name="Models">Distinct models observed in sales.</param>
/// <param name="Locations">Distinct sales locations.</param>
/// <param name="Sources">The seven governed data sources.</param>
/// <param name="Issues">The itemised data-quality checks.</param>
/// <param name="GeneratedAtUtc">When this assessment was computed.</param>
public sealed record DataHealthResponse(
    int Score,
    string ScoreBand,
    int SalesRows,
    int InvRows,
    string Coverage,
    int Models,
    int Locations,
    IReadOnlyList<DataSourceDto> Sources,
    IReadOnlyList<DataQualityIssueDto> Issues,
    DateTimeOffset GeneratedAtUtc);

/// <summary>
/// One governed data source. <paramref name="StatusKind"/> drives the word+icon+colour chip on the
/// client (never colour alone): <c>ready</c>, <c>assumptions</c>, <c>demo</c> or <c>blocked</c>.
/// </summary>
/// <param name="Rows">A display string, so a synthetic count is never presented as a measured one.</param>
public sealed record DataSourceDto(
    string Name,
    string System,
    string Status,
    string StatusKind,
    string Rows,
    string Coverage,
    string Note);

/// <summary>One data-quality check, itemised for the issues list.</summary>
public sealed record DataQualityIssueDto(string Id, string Label, int Count, string Severity, string Note);
