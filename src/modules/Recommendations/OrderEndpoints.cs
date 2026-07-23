using BeeEye.Shared.Web.Security;
using BeeEye.Shared.Security;
using BeeEye.Modules.Recommendations.Application;
using BeeEye.Modules.Recommendations.Contracts;
using BeeEye.Shared.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.Recommendations;

/// <summary>UC1 — Monthly Vehicle Order Optimisation endpoints.</summary>
internal static class OrderEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/recommendations").WithTags("Order Optimisation")
            .RequireReadPermission(Permissions.RecommendationReview);

        group.MapGet("/", () => new ModuleInfo(
                "Recommendations", "recommendations", "Order-optimisation recommendations balancing demand and constraints (UC1).", "operational"))
            .WithName("Recommendations_Info")
            .WithSummary("Recommendations module information");

        group.MapGet("/order-optimisation", async (
                OrderReadService svc, CancellationToken ct,
                int? horizon, double? targetCoverMonths, int? minOrderQuantity, int? orderMultiple,
                int? inbound, int? confirmedOrders, int? allocationLimit, string[]? model) =>
            {
                var scenario = OrderScenario.From(horizon, targetCoverMonths, minOrderQuantity, orderMultiple, inbound, confirmedOrders, allocationLimit);
                var errors = scenario.Validate();
                if (errors.Count > 0)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid scenario", detail: string.Join(" ", errors));
                }

                var all = await svc.RecommendAsync(scenario, ct);
                var items = model is { Length: > 0 }
                    ? all.Where(r => model.Contains(r.Model, StringComparer.OrdinalIgnoreCase)).ToList()
                    : all;
                var meta = new OrderMeta(items.Count, items.Sum(r => r.RecommendedQuantity), DateTimeOffset.UtcNow);
                return Results.Ok(new OrderResponse(scenario, items, meta));
            })
            .WithName("Recommendations_OrderOptimisation")
            .WithSummary("Recommended order quantities by configuration for a scenario")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/order-optimisation/filter-options", async (OrderReadService svc, CancellationToken ct) =>
            {
                // Distinct dimension values only — must not run the (expensive) per-config forecast.
                var (models, variants) = await svc.FilterOptionsAsync(ct);
                return Results.Ok(new { models, variants });
            })
            .WithName("Recommendations_FilterOptions");
    }
}
