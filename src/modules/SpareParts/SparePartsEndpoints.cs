using BeeEye.Shared.Web.Security;
using BeeEye.Shared.Security;
using BeeEye.Modules.SpareParts.Application;
using BeeEye.Modules.SpareParts.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Paging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.SpareParts;

/// <summary>UC7 — Spare Parts Demand Prediction endpoints. Every response discloses synthetic provenance.</summary>
internal static class SparePartsEndpoints
{
    private static readonly string[] SortKeys = ["demand", "risk", "part", "lowdata"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/spare-parts").WithTags("Spare Parts")
            .RequireReadPermission(Permissions.SparePartsView);

        group.MapGet("/", () => new ModuleInfo(
                "Spare Parts", "spare-parts", "Intermittent spare-parts demand prediction and stocking ranges (UC7).", "operational"))
            .WithName("SpareParts_Info");

        group.MapGet("/demand/summary", async (
                SparePartsReadService svc, CancellationToken ct, double? serviceLevel, double? reviewPeriodMonths) =>
            {
                if (!await svc.HasDataAsync(ct))
                {
                    return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No parts data",
                        detail: "The synthetic spare-parts dataset has not been generated.");
                }

                var scenario = SparePartsScenario.From(serviceLevel, reviewPeriodMonths);
                var errors = scenario.Validate();
                if (errors.Count > 0)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid scenario", detail: string.Join(" ", errors));
                }

                var summary = await svc.SummaryAsync(scenario.ToSettings(), ct);
                return Results.Ok(new SparePartsSummaryResponse(scenario, summary, SparePartsProvenance.Now()));
            })
            .WithName("SpareParts_Summary")
            .WithSummary("Portfolio KPIs: parts, predicted demand, low-data and at-risk counts, demand-class mix");

        group.MapGet("/demand/parts", async (
                SparePartsReadService svc, CancellationToken ct,
                double? serviceLevel, double? reviewPeriodMonths,
                int? page, int? pageSize, string? sort, string[]? category, string[]? model, bool? lowDataOnly, bool? atRiskOnly) =>
            {
                if (!await svc.HasDataAsync(ct))
                {
                    return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No parts data");
                }

                var scenario = SparePartsScenario.From(serviceLevel, reviewPeriodMonths);
                var errors = scenario.Validate();
                if (errors.Count > 0)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid scenario", detail: string.Join(" ", errors));
                }

                var all = await svc.RecommendAllAsync(scenario.ToSettings(), ct);

                IEnumerable<SparePartsReadService.PartResult> filtered = all;
                if (category is { Length: > 0 })
                {
                    filtered = filtered.Where(r => category.Contains(r.Category, StringComparer.OrdinalIgnoreCase));
                }

                if (model is { Length: > 0 })
                {
                    filtered = filtered.Where(r => r.Models.Any(m => model.Contains(m, StringComparer.OrdinalIgnoreCase)));
                }

                if (lowDataOnly == true)
                {
                    filtered = filtered.Where(r => r.Recommendation.InsufficientData);
                }

                if (atRiskOnly == true)
                {
                    filtered = filtered.Where(r => r.Recommendation.StockoutRisk == "High");
                }

                var rows = Sort(filtered, sort).Select(SparePartsReadService.ToPublicRow).ToList();
                var request = new PageRequest(page ?? 1, pageSize ?? 25);
                var pageItems = rows.Skip(request.Offset).Take(request.PageSize).ToList();
                var paged = new PagedResult<PartDemandRow>(pageItems, request.Page, request.PageSize, rows.Count);
                return Results.Ok(new PartsDemandResponse(scenario, paged, SparePartsProvenance.Now()));
            })
            .WithName("SpareParts_Parts")
            .WithSummary("Per-part demand, recommended stocking range, risk and low-data flag (paged, sortable, filterable)");

        group.MapGet("/demand/part/{partNumber}", async (
                string partNumber, SparePartsReadService svc, CancellationToken ct, double? serviceLevel, double? reviewPeriodMonths) =>
            {
                var scenario = SparePartsScenario.From(serviceLevel, reviewPeriodMonths);
                var errors = scenario.Validate();
                if (errors.Count > 0)
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid scenario", detail: string.Join(" ", errors));
                }

                var detail = await svc.PartDetailAsync(partNumber, scenario, ct);
                return detail is null
                    ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Unknown part",
                        detail: $"No part with number '{partNumber}'.")
                    : Results.Ok(detail);
            })
            .WithName("SpareParts_PartDetail")
            .WithSummary("Usage history, method comparison (Croston/SBA/TSB), forecast range, supersession & compatibility");

        group.MapGet("/filter-options", async (SparePartsReadService svc, CancellationToken ct) =>
                Results.Ok(await svc.FilterOptionsAsync(ct)))
            .WithName("SpareParts_FilterOptions");
    }

    private static IEnumerable<SparePartsReadService.PartResult> Sort(IEnumerable<SparePartsReadService.PartResult> parts, string? sort)
    {
        var key = sort is not null && SortKeys.Contains(sort, StringComparer.OrdinalIgnoreCase) ? sort.ToLowerInvariant() : "demand";
        return key switch
        {
            "risk" => parts
                .OrderByDescending(r => RiskRank(r.Recommendation.StockoutRisk))
                .ThenByDescending(r => r.Recommendation.PredictedMonthlyDemand ?? -1)
                .ThenBy(r => r.PartNumber, StringComparer.Ordinal).ThenBy(r => r.Location, StringComparer.Ordinal),
            "part" => parts.OrderBy(r => r.PartNumber, StringComparer.Ordinal).ThenBy(r => r.Location, StringComparer.Ordinal),
            "lowdata" => parts
                .OrderByDescending(r => r.Recommendation.InsufficientData)
                .ThenBy(r => r.PartNumber, StringComparer.Ordinal).ThenBy(r => r.Location, StringComparer.Ordinal),
            _ => parts
                .OrderByDescending(r => r.Recommendation.PredictedMonthlyDemand ?? -1)
                .ThenBy(r => r.PartNumber, StringComparer.Ordinal).ThenBy(r => r.Location, StringComparer.Ordinal),
        };
    }

    private static int RiskRank(string risk) => risk switch { "High" => 3, "Medium" => 2, "Low" => 1, _ => 0 };
}
