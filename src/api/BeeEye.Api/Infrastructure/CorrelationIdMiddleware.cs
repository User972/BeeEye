namespace BeeEye.Api.Infrastructure;

/// <summary>
/// Ensures every request carries a correlation id, echoed back on the response and
/// available to logging, Problem Details and downstream module handlers. Honours an
/// inbound <c>X-Correlation-Id</c> so a trace can span the SPA, API and workers.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
            && !string.IsNullOrWhiteSpace(incoming)
                ? incoming.ToString()
                : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }
}
