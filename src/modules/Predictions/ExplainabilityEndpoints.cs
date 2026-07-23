using System.Security.Claims;
using BeeEye.Modules.Predictions.Application;
using BeeEye.Modules.Predictions.Contracts;
using BeeEye.Shared.Idempotency;
using BeeEye.Shared.Results;
using BeeEye.Shared.Security;
using BeeEye.Shared.Time;
using BeeEye.Shared.Web.Idempotency;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BeeEye.Modules.Predictions;

/// <summary>
/// The global explainability endpoints (V3-DS-006) — one route that answers "why does the platform
/// say this?" for any subject any bounded context can explain, and one that records what the reader
/// thought of the answer.
/// <para>
/// They live in <c>Predictions</c> because that module's stated purpose is already <i>"model runs,
/// predictions and prediction explanations"</i>, and because putting them anywhere else would have
/// made one intelligence module the owner of every other module's explanations.
/// </para>
/// <para>
/// <b>No model is called here.</b> The payload restates what the deterministic engine already
/// computed. Live narration is S10 (V3-PLAT-002); the seam is the <c>IExplainabilityProvider</c>
/// contract, and nothing in this slice crosses it.
/// </para>
/// <para>
/// <b>There is deliberately no DELETE route.</b> Feedback is append-only, like every other derived
/// record on this platform: changing your mind appends a row. Do not add one.
/// </para>
/// </summary>
internal static class ExplainabilityEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapExplain(group);
        MapFeedback(group);
    }

    // ---------------------------------------------------------------- read

    private static void MapExplain(RouteGroupBuilder group) =>
        group.MapGet("/explain", async (
                [FromQuery(Name = "kind")] string? kind,
                [FromQuery(Name = "ref")] string? subjectRef,
                ExplainabilityService svc,
                ExplainabilityFeedbackService feedback,
                IClock clock,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(kind))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "A subject kind is required",
                        detail: "Supply 'kind'. Valid values: " + Kinds(svc) + ".");
                }

                if (string.IsNullOrWhiteSpace(subjectRef))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "A subject reference is required",
                        detail: $"Supply 'ref' — the identifier of the '{kind}' being explained.");
                }

                var trimmedKind = kind.Trim();
                var trimmedRef = subjectRef.Trim();
                var outcome = await svc.ExplainAsync(trimmedKind, trimmedRef, ct);

                if (outcome.Status == ExplanationStatus.UnknownKind)
                {
                    // A caller error, and the fix is named in the message rather than left in the
                    // source — the same treatment S5 and S6 give an unrecognised status filter.
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Unknown subject kind",
                        detail: $"'{trimmedKind}' is not explainable. Valid values: " + Kinds(svc) + ".");
                }

                if (outcome.Status == ExplanationStatus.NotFound)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Nothing to explain",
                        detail:
                            $"No '{trimmedKind}' matching '{trimmedRef}' carries a recorded explanation. It "
                            + "may have been removed from the dataset since the screen loaded.");
                }

                // Explained, or Failed. A failed provider is a *gap*, never a 500 and never a silent
                // omission: the drawer says what is missing. Exception detail stayed in the log.
                return Results.Ok(new ExplanationResponse(
                    trimmedKind,
                    trimmedRef,
                    outcome.Explanation is null ? null : ExplanationDto.From(outcome.Explanation),
                    [.. outcome.Gaps.Select(ExplanationGapDto.From)],
                    await feedback.LatestAsync(trimmedKind, trimmedRef, ct),
                    ExplainabilityFeedbackService.RetrainingCaveat,
                    clock.UtcNow));
            })
            // Deliberately not a new read permission. Anyone who may see a figure may see why it says
            // what it says: a role that could read the number but not its basis would be strictly
            // worse informed than one that could read neither, and there is no ADMC role that wants
            // that.
            .RequireReadPermission(Permissions.ExecutiveCockpitView)
            .WithName("Predictions_Explain")
            .WithSummary("Why the platform says what it says about one subject")
            .Produces<ExplanationResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    // ---------------------------------------------------------------- write

    private static void MapFeedback(RouteGroupBuilder group) =>
        group.MapPost("/explain/feedback", async (
                FeedbackRequest request,
                ExplainabilityFeedbackService svc,
                ClaimsPrincipal user,
                HttpContext http,
                CancellationToken ct) =>
            {
                if (!TryActor(user, out var actor, out var unauthorised))
                {
                    return unauthorised!;
                }

                var key = http.Request.Headers[IdempotencyKey.HeaderName].ToString().Trim();
                var result = await svc.SubmitAsync(request, actor, key, ct);

                return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
            })
            // RequirePermission, never RequireReadPermission: this writes an attributed row, so no
            // configuration setting may relax it in any environment (ADR 0008 §2.4). Declaring it as a
            // read throws at start-up rather than shipping a relaxable write.
            .RequirePermission(Permissions.ExplanationFeedbackSubmit)
            .WithIdempotency()
            .WithName("Predictions_ExplainFeedback")
            .WithSummary("Record whether an explanation was useful. Recorded only; it retrains nothing")
            .Produces<FeedbackResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    // ---------------------------------------------------------------- shared plumbing

    private static string Kinds(ExplainabilityService svc) => string.Join(", ", svc.RegisteredKinds);

    /// <summary>
    /// The single place a domain failure becomes an HTTP status on this route. Nothing technical
    /// crosses it: the detail is the explanation the service already wrote, for a person.
    /// </summary>
    private static IResult Problem(Error error) => error.Code switch
    {
        "not_found" => Results.Problem(
            statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: error.Message),

        _ => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: error.Message),
    };

    /// <summary>
    /// The caller's stable subject id. Feedback with no identifiable principal is refused: a verdict
    /// nobody is attached to cannot be superseded by its own author, which is the whole read model.
    /// </summary>
    private static bool TryActor(ClaimsPrincipal user, out string actor, out IResult? problem)
    {
        actor = user.SubjectId()?.Trim() ?? string.Empty;

        if (actor.Length > 0)
        {
            problem = null;
            return true;
        }

        problem = Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Not signed in",
            detail: "Feedback names the person who gave it. Sign in and try again.");

        return false;
    }

    /// <summary>
    /// Forces the duplicate-kind check at start-up by resolving the service once during endpoint
    /// mapping. A kind claimed by two providers is a composition-root bug, and the request path is
    /// the wrong place to discover it — whichever provider answered would depend on DI registration
    /// order, intermittently and invisibly.
    /// </summary>
    public static void AssertProvidersAreWellFormed(IEndpointRouteBuilder endpoints)
    {
        using var scope = endpoints.ServiceProvider.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<ExplainabilityService>();
    }
}
