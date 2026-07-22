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
        var group = app.MapGroup($"{ApiRoutes.V1}/recommendations").WithTags("Order Optimisation");

        group.MapGet("/", () => new ModuleInfo(
                "Recommendations", "recommendations", "Order-optimisation recommendations balancing demand and constraints (UC1).", "operational"))
            .WithName("Recommendations_Info");

        group.MapGet("/order-optimisation", async (
                OrderReadService svc, CancellationToken ct,
                int? horizon, double? targetCoverMonths, int? minOrderQuantity, int? orderMultiple,
                int? inbound, int? confirmedOrders, int? allocationLimit, string[]? model) =>
            {
                var scenario = OrderScenario.From(horizon, targetCoverMonths, minOrderQuantity, orderMultiple, inbound, confirmedOrders, allocationLimit);
                var all = await svc.RecommendAsync(scenario, ct);
                var items = model is { Length: > 0 }
                    ? all.Where(r => model.Contains(r.Model, StringComparer.OrdinalIgnoreCase)).ToList()
                    : all;
                var meta = new OrderMeta(items.Count, items.Sum(r => r.RecommendedQuantity), DateTimeOffset.UtcNow);
                return Results.Ok(new OrderResponse(scenario, items, meta));
            })
            .WithName("Recommendations_OrderOptimisation")
            .WithSummary("Recommended order quantities by configuration for a scenario");

        group.MapGet("/order-optimisation/filter-options", async (OrderReadService svc, CancellationToken ct) =>
            {
                var all = await svc.RecommendAsync(OrderScenario.From(null, null, null, null, null, null, null), ct);
                return Results.Ok(new
                {
                    models = all.Select(r => r.Model).Distinct().OrderBy(x => x).ToList(),
                    variants = all.Select(r => r.Variant).Distinct().OrderBy(x => x).ToList(),
                });
            })
            .WithName("Recommendations_FilterOptions");
    }
}
