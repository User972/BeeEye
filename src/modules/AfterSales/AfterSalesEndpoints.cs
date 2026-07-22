using BeeEye.Analytics.AfterSales;
using BeeEye.Modules.AfterSales.Application;
using BeeEye.Modules.AfterSales.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Paging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.AfterSales;

/// <summary>UC6 — Sales vs After-Sales Demand Correlation endpoints. Every response discloses synthetic provenance.</summary>
internal static class AfterSalesEndpoints
{
    private static readonly string[] SortKeys = ["intensity", "events", "coverage", "labor", "model"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/after-sales").WithTags("After-Sales");

        group.MapGet("/", () => new ModuleInfo(
                "After-Sales", "after-sales", "Sales-to-service correlation and service-intensity analysis (UC6).", "operational"))
            .WithName("AfterSales_Info");

        group.MapGet("/service-intensity/summary", async (AfterSalesReadService svc, CancellationToken ct) =>
            {
                if (!await svc.HasDataAsync(ct))
                {
                    return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No after-sales data",
                        detail: "The synthetic after-sales dataset has not been generated.");
                }

                var analysis = await svc.AnalyseAsync(ct);
                return Results.Ok(new ServiceIntensitySummaryResponse(analysis.Summary, AfterSalesProvenance.Now()));
            })
            .WithName("AfterSales_Summary")
            .WithSummary("Fleet-level service-intensity KPIs with coverage and synthetic-data provenance");

        group.MapGet("/service-intensity/by-model", async (
                AfterSalesReadService svc, CancellationToken ct,
                int? page, int? pageSize, string? sort, bool? highOnly, string[]? model) =>
            {
                if (!await svc.HasDataAsync(ct))
                {
                    return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No after-sales data");
                }

                var analysis = await svc.AnalyseAsync(ct);
                IEnumerable<ModelServiceIntensity> models = analysis.Models;
                if (highOnly == true)
                {
                    models = models.Where(m => m.HighIntensity);
                }

                if (model is { Length: > 0 })
                {
                    models = models.Where(m => model.Contains(m.Model, StringComparer.OrdinalIgnoreCase));
                }

                var rows = Sort(models, sort).Select(ModelIntensityRow.From).ToList();
                var request = new PageRequest(page ?? 1, pageSize ?? 20);
                var pageItems = rows.Skip(request.Offset).Take(request.PageSize).ToList();
                var paged = new PagedResult<ModelIntensityRow>(pageItems, request.Page, request.PageSize, rows.Count);
                return Results.Ok(new ByModelResponse(paged, AfterSalesProvenance.Now()));
            })
            .WithName("AfterSales_ByModel")
            .WithSummary("Per-model service-intensity index (paged, sortable)");

        group.MapGet("/service-intensity/model/{model}", async (string model, AfterSalesReadService svc, CancellationToken ct) =>
            {
                if (!await svc.HasDataAsync(ct))
                {
                    return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "No after-sales data");
                }

                var analysis = await svc.AnalyseAsync(ct);
                var detail = analysis.Models.FirstOrDefault(m => string.Equals(m.Model, model, StringComparison.OrdinalIgnoreCase));
                return detail is null
                    ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Unknown model",
                        detail: $"No service-intensity data for model '{model}'.")
                    : Results.Ok(new ModelDetailResponse(detail, AfterSalesProvenance.Now()));
            })
            .WithName("AfterSales_ModelDetail")
            .WithSummary("Mileage-band, time-since-sale and service-type breakdowns for one model");

        group.MapGet("/filter-options", async (AfterSalesReadService svc, CancellationToken ct) =>
                Results.Ok(await svc.FilterOptionsAsync(ct)))
            .WithName("AfterSales_FilterOptions");
    }

    private static IEnumerable<ModelServiceIntensity> Sort(IEnumerable<ModelServiceIntensity> models, string? sort)
    {
        var key = sort is not null && SortKeys.Contains(sort, StringComparer.OrdinalIgnoreCase) ? sort.ToLowerInvariant() : "intensity";
        return key switch
        {
            "events" => models.OrderByDescending(m => m.TotalEvents).ThenBy(m => m.Model, StringComparer.Ordinal),
            "coverage" => models.OrderByDescending(m => m.Coverage.CoverageRate ?? -1).ThenBy(m => m.Model, StringComparer.Ordinal),
            "labor" => models.OrderByDescending(m => m.LaborHoursPerVehicle ?? -1).ThenBy(m => m.Model, StringComparer.Ordinal),
            "model" => models.OrderBy(m => m.Model, StringComparer.Ordinal),
            _ => models.OrderByDescending(m => m.IntensityIndex ?? double.NegativeInfinity).ThenBy(m => m.Model, StringComparer.Ordinal),
        };
    }
}
