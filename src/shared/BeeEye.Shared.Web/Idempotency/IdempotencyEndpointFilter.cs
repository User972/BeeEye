using System.Text;
using System.Text.Json;
using BeeEye.Shared.Idempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// This file sits inside BeeEye.Shared.*, where the unqualified name `Results` binds to the kernel's
// BeeEye.Shared.Results namespace rather than the minimal-API helper. Aliased rather than fully
// qualified at each call site, so the intent stays readable.
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace BeeEye.Shared.Web.Idempotency;

/// <summary>
/// Enforces the <c>Idempotency-Key</c> protocol from ADR 0007 §2.1 on a state-changing endpoint.
/// <para>
/// The invariant it exists to hold (ADR 0007 I-6): <i>a repeated submission of the same intent
/// produces one effect and one answer</i>. A human clicking "Accept" twice, a flaky connection
/// retried by the browser, and a request re-driven after a timeout are all the same intent — and a
/// decision record must not be created twice for any of them.
/// </para>
/// <para>
/// The key is <b>required</b>, not merely honoured, for every S6 write. Optional-but-honoured leaves
/// the guarantee to whichever client remembers to opt in, and the first client that forgets is the one
/// that double-books a decision worth millions of SAR. A missing header therefore fails loudly at the
/// edge, where the fix is obvious, rather than silently at the ledger, where it is not.
/// </para>
/// </summary>
public sealed class IdempotencyEndpointFilter(
    IIdempotencyStore store,
    ILogger<IdempotencyEndpointFilter> logger) : IEndpointFilter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var http = context.HttpContext;
        var cancellationToken = http.RequestAborted;

        var problem = IdempotencyKey.Validate(
            http.Request.Headers[IdempotencyKey.HeaderName].ToString(), out var key);

        if (problem != IdempotencyKeyProblem.None)
        {
            return HttpResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: problem == IdempotencyKeyProblem.Missing
                    ? $"{IdempotencyKey.HeaderName} header required"
                    : $"Invalid {IdempotencyKey.HeaderName} header",
                detail: IdempotencyKey.Explain(problem));
        }

        var route = $"{http.Request.Method} {http.Request.Path}";
        var principalId = Security.PrincipalExtensions.SubjectId(http.User) ?? string.Empty;
        var fingerprint = RequestFingerprint.Compute(route, PayloadJson(context), principalId);

        var existing = await store.FindAsync(key, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                // Same key, different request. Replaying the stored answer would be a lie and running
                // the new one would break the client's own assumption that the key made it safe.
                return HttpResults.Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Idempotency key already used for a different request",
                    detail:
                        $"This '{IdempotencyKey.HeaderName}' has already been used for a different request. "
                        + "Use a new key for a new intent, and reuse a key only when retrying the same one.");
            }

            logger.LogInformation(
                "Replaying stored response for idempotency key on {Route}; the handler was not re-run.", route);

            return new StoredResponseResult(existing.ResponseStatus, existing.ResponseBody);
        }

        return await RunOnceAsync(context, next, key, route, fingerprint, principalId, cancellationToken);
    }

    /// <summary>
    /// Runs the handler with the effect and the key row inside one transaction, so they commit or roll
    /// back together. Nothing is written to the real response until the commit succeeds.
    /// </summary>
    private async ValueTask<object?> RunOnceAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        string key,
        string route,
        string fingerprint,
        string principalId,
        CancellationToken cancellationToken)
    {
        var http = context.HttpContext;

        await store.BeginAsync(cancellationToken);

        int status;
        string body;
        var originalBody = http.Response.Body;
        using var buffer = new MemoryStream();

        try
        {
            http.Response.Body = buffer;

            var produced = await next(context);
            await ToResult(produced).ExecuteAsync(http);

            status = http.Response.StatusCode;
            body = Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch
        {
            http.Response.Body = originalBody;
            await store.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            http.Response.Body = originalBody;
        }

        if (status is < 200 or > 299)
        {
            // A refused request changed nothing, so nothing is remembered: the client may correct the
            // problem and retry with the same key. Caching a 400 forever would turn a fixable mistake
            // into a permanent one.
            await store.RollbackAsync(cancellationToken);
            return new StoredResponseResult(status, body);
        }

        var committed = await CompleteAsync(
            new IdempotencyEntry(key, route, fingerprint, status, body, principalId), cancellationToken);

        if (!committed)
        {
            logger.LogInformation(
                "A concurrent request committed the same idempotency key on {Route}; this attempt was rolled back.",
                route);

            return HttpResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate request in flight",
                detail:
                    "An identical request using this idempotency key is already being processed. It was "
                    + "applied once; retry in a moment to read the result.");
        }

        return new StoredResponseResult(status, body);
    }

    private async ValueTask<bool> CompleteAsync(IdempotencyEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            return await store.TryCompleteAsync(entry, cancellationToken);
        }
        catch
        {
            // The effect is only durable once the key row commits with it. If recording the key
            // fails for any other reason, the effect must go with it — a persisted decision whose key
            // was lost would be applied a second time by the very next retry.
            await store.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// The JSON the fingerprint covers: the bound arguments that declare themselves part of the
    /// request body, plus the route values that identify the subject. See
    /// <see cref="IIdempotentPayload"/> for why the body is not re-read from the stream.
    /// </summary>
    private static string PayloadJson(EndpointFilterInvocationContext context)
    {
        // Serialised by *runtime* type, one payload at a time. Handing System.Text.Json a
        // List<IIdempotentPayload> would serialise against the declared interface, which has no
        // properties — so every body would hash to "{}" and two genuinely different requests would
        // look identical to the replay check. Found by an integration test, not by reading the code.
        var bodies = context.Arguments
            .OfType<IIdempotentPayload>()
            .Select(payload => JsonSerializer.Serialize(payload, payload.GetType(), Json));

        var routeValues = JsonSerializer.Serialize(
            context.HttpContext.Request.RouteValues
                .ToDictionary(v => v.Key, v => v.Value?.ToString(), StringComparer.Ordinal),
            Json);

        return $"{{\"route\":{routeValues},\"body\":[{string.Join(',', bodies)}]}}";
    }

    private static IResult ToResult(object? produced) => produced switch
    {
        IResult result => result,
        null => HttpResults.NoContent(),
        _ => HttpResults.Ok(produced),
    };

    /// <summary>
    /// Writes a status and body that have already been decided — either replayed from the store or
    /// captured from the handler that has now committed.
    /// </summary>
    private sealed class StoredResponseResult(int status, string body) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            httpContext.Response.StatusCode = status;

            if (body.Length == 0)
            {
                httpContext.Response.ContentLength = 0;
                return;
            }

            // Problem Details and success payloads are both JSON here; the content type is set
            // explicitly because the buffered write never reached the real response headers.
            httpContext.Response.ContentType = status is >= 400
                ? "application/problem+json; charset=utf-8"
                : "application/json; charset=utf-8";

            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Response.ContentLength = bytes.Length;
            await httpContext.Response.Body.WriteAsync(bytes, httpContext.RequestAborted);
        }
    }
}

/// <summary>Applies the <c>Idempotency-Key</c> protocol to an endpoint.</summary>
public static class IdempotencyEndpointExtensions
{
    /// <summary>
    /// Requires and enforces an <c>Idempotency-Key</c> on this endpoint (ADR 0007 §2.1), and declares
    /// the responses that enforcement can produce so the OpenAPI document stays honest.
    /// </summary>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddEndpointFilterFactory((context, next) =>
            {
                // Resolved per request from the request's own scope, because the store shares the
                // request's DbContext and its transaction.
                return invocationContext =>
                {
                    var services = invocationContext.HttpContext.RequestServices;
                    var filter = new IdempotencyEndpointFilter(
                        services.GetRequiredService<IIdempotencyStore>(),
                        services.GetRequiredService<ILogger<IdempotencyEndpointFilter>>());

                    return filter.InvokeAsync(invocationContext, next);
                };
            })
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
