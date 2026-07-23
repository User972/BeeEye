using BeeEye.Shared.Web.Security;
using BeeEye.Shared.Security;
using BeeEye.Modules.Procurement.Application;
using BeeEye.Modules.Procurement.Contracts;
using BeeEye.Shared.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.Procurement;

/// <summary>UC4 — Procurement Quantity Optimisation endpoints.</summary>
internal static class ProcurementEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/procurement").WithTags("Procurement")
            .RequireReadPermission(Permissions.ProcurementView);

        group.MapGet("/", () => new ModuleInfo(
                "Procurement", "procurement", "Procurement quantity optimisation balancing demand, lead time and cost (UC4).", "operational"))
            .WithName("Procurement_Info")
            .WithSummary("Procurement module information");

        group.MapGet("/recommendations", async (
                ProcurementReadService svc, CancellationToken ct,
                double? serviceLevel, double? leadTimeMonths, double? reviewPeriodMonths,
                int? minOrderQuantity, int? orderMultiple, int? inbound, string[]? model) =>
            {
                var scenario = ProcurementScenario.From(serviceLevel, leadTimeMonths, reviewPeriodMonths, minOrderQuantity, orderMultiple, inbound);
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
                var meta = new ProcurementMeta(items.Count, items.Sum(r => r.RecommendedQuantity), DateTimeOffset.UtcNow);
                return Results.Ok(new ProcurementResponse(scenario, items, meta));
            })
            .WithName("Procurement_Recommendations")
            .WithSummary("Procurement quantity ranges and safety stock for a scenario")
            .Produces<ProcurementResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/filter-options", async (ProcurementReadService svc, CancellationToken ct) =>
            {
                // Distinct dimension values only — must not run the (expensive) procurement optimiser.
                var (models, variants) = await svc.FilterOptionsAsync(ct);
                return Results.Ok(new { models, variants });
            })
            .WithName("Procurement_FilterOptions");
    }
}
