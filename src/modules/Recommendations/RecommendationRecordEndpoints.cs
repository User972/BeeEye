using BeeEye.Modules.Recommendations.Application;
using BeeEye.Modules.Recommendations.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Decisions;
using BeeEye.Shared.Security;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.Recommendations;

/// <summary>
/// Frozen recommendation records and the platform's first write path
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c>).
/// </summary>
internal static class RecommendationRecordEndpoints
{
    private const int MaxPageSize = 200;

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/recommendations/records").WithTags("Recommendation Records");

        group.MapGet("/", async (
                RecommendationRecordService svc, CancellationToken ct,
                string? status, int? page, int? pageSize) =>
            {
                RecommendationStatus? parsed = null;
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (!Enum.TryParse<RecommendationStatus>(status, ignoreCase: true, out var value))
                    {
                        return Results.Problem(
                            statusCode: StatusCodes.Status400BadRequest,
                            title: "Invalid status",
                            detail: $"'{status}' is not a recommendation status. Valid values: "
                                + string.Join(", ", Enum.GetNames<RecommendationStatus>()) + ".");
                    }

                    parsed = value;
                }

                // Clamped rather than rejected: a caller asking for too much gets a bounded page, and
                // an unbounded result set never reaches the browser.
                var take = Math.Clamp(pageSize ?? 50, 1, MaxPageSize);
                var skip = Math.Max(page ?? 1, 1);

                var (items, total) = await svc.ListAsync(parsed, skip, take, ct);

                return Results.Ok(new RecommendationRecordPage(
                    [.. items.Select(RecommendationRecordDto.From)], skip, take, total));
            })
            .RequireReadPermission(Permissions.RecommendationReview)
            .WithName("Recommendations_Records")
            .WithSummary("Recorded recommendations, newest and highest-priority first")
            .Produces<RecommendationRecordPage>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/generate", async (RecommendationRecordService svc, CancellationToken ct) =>
            {
                var (analysisDate, datasetVersion) = await svc.ResolveContextAsync(ct);
                var result = await svc.GenerateAsync(analysisDate, datasetVersion, ct);

                // 200, not 201: the run is idempotent, so a repeat creates nothing and there is no
                // single new resource to point a Location header at.
                return Results.Ok(new GenerationResponse(
                    result.Created, result.AlreadyPresent, result.AnalysisDate));
            })
            // RequirePermission, never RequireReadPermission: this changes state, so no configuration
            // setting can relax it in any environment (ADR 0008 §2.4).
            .RequirePermission(Permissions.RecommendationGenerate)
            .WithName("Recommendations_GenerateRecords")
            .WithSummary("Run the ruleset and persist any recommendation not already recorded")
            .Produces<GenerationResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
