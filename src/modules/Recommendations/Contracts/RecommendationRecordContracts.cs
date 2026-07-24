using System.Text.Json;
using BeeEye.Persistence.Entities;

namespace BeeEye.Modules.Recommendations.Contracts;

/// <summary>A persisted, frozen recommendation as returned over HTTP.</summary>
public sealed record RecommendationRecordDto(
    Guid Id,
    string SubjectRef,
    string Area,
    string RuleId,
    string Action,
    string Rationale,
    IReadOnlyList<string> Evidence,
    string ExpectedOutcome,
    string Confidence,
    IReadOnlyList<string> Assumptions,
    decimal ImpactSar,
    int Priority,
    string OwnerRole,
    bool IsDemoData,
    string RulesetVersion,
    string DatasetVersion,
    DateOnly AnalysisDate,
    string CurrentStatus,
    DateTimeOffset? ValidUntilUtc,
    Guid? SupersededByRecommendationId,
    DateTimeOffset CreatedAtUtc)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static RecommendationRecordDto From(Recommendation r)
    {
        ArgumentNullException.ThrowIfNull(r);

        return new RecommendationRecordDto(
            r.Id,
            r.SubjectRef,
            r.Area,
            r.RuleId,
            r.Action,
            r.Rationale,
            Deserialise(r.EvidenceJson),
            r.ExpectedOutcome,
            r.Confidence,
            Deserialise(r.AssumptionsJson),
            r.ImpactSar,
            r.Priority,
            r.OwnerRole,
            r.IsDemoData,
            r.RulesetVersion,
            r.DatasetVersion,
            r.AnalysisDate,
            r.CurrentStatus.ToString(),
            r.ValidUntilUtc,
            r.SupersededByRecommendationId,
            r.CreatedAtUtc);
    }

    /// <summary>
    /// Stored JSON is written by this application, but a malformed value must not turn a read into a
    /// 500 — an unreadable evidence list degrades to empty rather than failing the whole page.
    /// </summary>
    private static IReadOnlyList<string> Deserialise(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>A page of recorded recommendations.</summary>
public sealed record RecommendationRecordPage(
    IReadOnlyList<RecommendationRecordDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>The outcome of a generation run.</summary>
/// <param name="Created">Records written by this run.</param>
/// <param name="AlreadyPresent">Candidates that already had a record — a successful idempotent no-op.</param>
/// <param name="AnalysisDate">The pinned analysis date used.</param>
public sealed record GenerationResponse(int Created, int AlreadyPresent, DateOnly AnalysisDate);
