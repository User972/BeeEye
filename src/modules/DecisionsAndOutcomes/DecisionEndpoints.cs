using System.Security.Claims;
using BeeEye.Modules.DecisionsAndOutcomes.Application;
using BeeEye.Modules.DecisionsAndOutcomes.Contracts;
using BeeEye.Persistence.Entities;
using BeeEye.Shared.Api;
using BeeEye.Shared.Decisions;
using BeeEye.Shared.Idempotency;
using BeeEye.Shared.Results;
using BeeEye.Shared.Security;
using BeeEye.Shared.Web.Idempotency;
using BeeEye.Shared.Web.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.DecisionsAndOutcomes;

/// <summary>
/// The Decision Log and the human decision workflow
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c>).
/// <para>
/// <b>There is deliberately no DELETE route, at any layer.</b> The v3 prototype offered a delete
/// button on every row; ADR 0006 rejects it outright, because a decision that can be deleted is not an
/// audit trail. Rejection is the terminal state that ends a record's life — it keeps <i>why</i>, which
/// is the part anyone reading this months later will need. Do not add one to match the prototype.
/// </para>
/// </summary>
internal static class DecisionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/decisions").WithTags("Decision Log");

        MapLog(group);
        MapDetail(group);
        MapClaim(group);
        MapAccept(group);
        MapAcceptWithModification(group);
        MapReject(group);
        MapSignOff(group);
        MapImplemented(group);
        MapOutcome(group);
    }

    // ---------------------------------------------------------------- reads

    private static void MapLog(RouteGroupBuilder group) =>
        group.MapGet("/", async (
                DecisionService svc, ClaimsPrincipal user, CancellationToken ct,
                string? status, string? area, string? outcome, DateTimeOffset? from, DateTimeOffset? to,
                string? q, int? page, int? pageSize) =>
            {
                if (!TryParseEnum<RecommendationStatus>(status, out var parsedStatus, out var statusProblem))
                {
                    return statusProblem!;
                }

                if (!TryParseEnum<DecisionOutcome>(outcome, out var parsedOutcome, out var outcomeProblem))
                {
                    return outcomeProblem!;
                }

                var filters = new DecisionLogFilters(parsedStatus, area, parsedOutcome, from, to, q);

                var result = await svc.ListAsync(
                    filters, user.Permissions(), page ?? 1, pageSize ?? 50, ct);

                return Results.Ok(result);
            })
            .RequireReadPermission(Permissions.RecommendationReview)
            .WithName("Decisions_Log")
            .WithSummary("The governed decision log — every recommendation, with the human decision on it")
            .Produces<DecisionLogPageDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static void MapDetail(RouteGroupBuilder group) =>
        // {id} is the *recommendation* id: the log row's identity is the recommendation, because a
        // record nobody has claimed yet still belongs in the trail. The write routes below are keyed
        // by decision id, which only exists once someone has claimed it.
        group.MapGet("/{id:guid}", async (
                Guid id, DecisionService svc, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var result = await svc.GetDetailAsync(id, user.Permissions(), ct);
                return result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);
            })
            .RequireReadPermission(Permissions.RecommendationReview)
            .WithName("Decisions_Detail")
            .WithSummary("What the system recommended, beside what the human decided")
            .Produces<DecisionDetailDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    // ---------------------------------------------------------------- writes
    //
    // Every one uses RequirePermission, never RequireReadPermission: these change state, so no
    // configuration setting may relax them in any environment (ADR 0008 §2.4). Declaring one as a read
    // throws at start-up rather than shipping a relaxable write.

    private static void MapClaim(RouteGroupBuilder group) =>
        group.MapPost("/recommendations/{recommendationId:guid}/claim", async (
                Guid recommendationId, DecisionService svc, ClaimsPrincipal user,
                HttpContext http, CancellationToken ct) =>
            {
                if (!TryActor(user, out var actor, out var unauthorised))
                {
                    return unauthorised!;
                }

                var key = http.Request.Headers[IdempotencyKey.HeaderName].ToString().Trim();
                var result = await svc.ClaimAsync(recommendationId, actor, key, ct);

                return Respond(result);
            })
            .RequirePermission(Permissions.RecommendationReview)
            .WithIdempotency()
            .WithName("Decisions_Claim")
            .WithSummary("Claim a recommendation for review, opening a decision on it")
            .Produces<TransitionResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static void MapAccept(RouteGroupBuilder group) =>
        group.MapPost("/{decisionId:guid}/accept", async (
                Guid decisionId, DecisionService svc, ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!TryActor(user, out var actor, out var unauthorised))
                {
                    return unauthorised!;
                }

                return Respond(await svc.AcceptAsync(decisionId, actor, ct));
            })
            .RequirePermission(Permissions.RecommendationApprove)
            .WithIdempotency()
            .WithName("Decisions_Accept")
            .WithSummary("Accept the recommendation exactly as the engine wrote it")
            .Produces<TransitionResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static void MapAcceptWithModification(RouteGroupBuilder group) =>
        group.MapPost("/{decisionId:guid}/accept-with-modification", async (
                Guid decisionId, ModificationRequest request, DecisionService svc,
                ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!TryActor(user, out var actor, out var unauthorised))
                {
                    return unauthorised!;
                }

                return Respond(await svc.AcceptWithModificationAsync(decisionId, actor, request, ct));
            })
            .RequirePermission(Permissions.RecommendationApprove)
            .WithIdempotency()
            .WithName("Decisions_AcceptWithModification")
            .WithSummary("Accept with a change, stored as a delta beside the untouched original")
            .Produces<TransitionResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static void MapReject(RouteGroupBuilder group) =>
        group.MapPost("/{decisionId:guid}/reject", async (
                Guid decisionId, RejectRequest request, DecisionService svc,
                ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!TryActor(user, out var actor, out var unauthorised))
                {
                    return unauthorised!;
                }

                return Respond(await svc.RejectAsync(decisionId, actor, request?.Note, ct));
            })
            .RequirePermission(Permissions.RecommendationApprove)
            .WithIdempotency()
            .WithName("Decisions_Reject")
            .WithSummary("Decline the recommendation. A reason is mandatory and is retained")
            .Produces<TransitionResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static void MapSignOff(RouteGroupBuilder group) =>
        group.MapPost("/{decisionId:guid}/approvals/{stepNumber:int}", async (
                Guid decisionId, int stepNumber, ApprovalRequest request, DecisionService svc,
                ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!TryActor(user, out var actor, out var unauthorised))
                {
                    return unauthorised!;
                }

                return Respond(await svc.SignOffAsync(
                    decisionId, stepNumber, actor, request?.Approved ?? false, request?.Note, ct));
            })
            .RequirePermission(Permissions.RecommendationApprove)
            .WithIdempotency()
            .WithName("Decisions_SignOff")
            .WithSummary("Sign off one step of the approval chain. A decision needs a second person")
            .Produces<TransitionResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static void MapImplemented(RouteGroupBuilder group) =>
        group.MapPost("/{decisionId:guid}/implemented", async (
                Guid decisionId, DecisionService svc, ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!TryActor(user, out var actor, out var unauthorised))
                {
                    return unauthorised!;
                }

                return Respond(await svc.MarkImplementedAsync(decisionId, actor, ct));
            })
            .RequirePermission(Permissions.RecommendationApprove)
            .WithIdempotency()
            .WithName("Decisions_MarkImplemented")
            .WithSummary("Confirm a human executed the action downstream. BeeEye never writes to Oracle Fusion")
            .Produces<TransitionResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static void MapOutcome(RouteGroupBuilder group) =>
        group.MapPost("/{decisionId:guid}/outcome", async (
                Guid decisionId, OutcomeRequest request, DecisionService svc,
                ClaimsPrincipal user, CancellationToken ct) =>
            {
                if (!TryActor(user, out var actor, out var unauthorised))
                {
                    return unauthorised!;
                }

                return Respond(await svc.RecordOutcomeAsync(decisionId, actor, request, ct));
            })
            .RequirePermission(Permissions.DecisionOutcomeRecord)
            .WithIdempotency()
            .WithName("Decisions_RecordOutcome")
            .WithSummary("Record the realised effect, closing the learning loop")
            .Produces<TransitionResponseDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

    // ---------------------------------------------------------------- shared plumbing

    private static IResult Respond(Result<TransitionResponseDto> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    /// <summary>
    /// The <b>single</b> place a domain failure becomes an HTTP status. Every refusal in this slice —
    /// a guard, a bound, a race, a missing permission — arrives here as a typed error and leaves as a
    /// Problem Details response whose <c>detail</c> is the explanation the domain already wrote.
    /// <para>
    /// Nothing technical crosses this boundary: no SQLSTATE, no EF message, no table name, no stack.
    /// The person reading the detail is an approver deciding what to do next, not an engineer.
    /// </para>
    /// </summary>
    private static IResult Problem(Error error) => error.Code switch
    {
        "not_found" => Results.Problem(
            statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: error.Message),

        "conflict" => Results.Problem(
            statusCode: StatusCodes.Status409Conflict, title: "That is not possible right now",
            detail: error.Message),

        "unprocessable" => Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity, title: "That change cannot be accepted",
            detail: error.Message),

        "forbidden" => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden, title: "Not permitted", detail: error.Message),

        _ => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: error.Message),
    };

    /// <summary>
    /// The caller's stable subject id. A write with no identifiable principal is refused: ADR 0006's
    /// whole point is that a decision names the human accountable for it.
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
            detail: "A decision must name the person who made it. Sign in and try again.");

        return false;
    }

    private static bool TryParseEnum<T>(string? raw, out T? parsed, out IResult? problem)
        where T : struct, Enum
    {
        parsed = null;
        problem = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (Enum.TryParse<T>(raw, ignoreCase: true, out var value))
        {
            parsed = value;
            return true;
        }

        problem = Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: $"Invalid {typeof(T).Name}",
            detail: $"'{raw}' is not recognised. Valid values: " + string.Join(", ", Enum.GetNames<T>()) + ".");

        return false;
    }
}
