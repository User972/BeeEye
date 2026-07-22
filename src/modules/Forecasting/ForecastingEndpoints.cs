using BeeEye.Analytics.Forecasting;
using BeeEye.Modules.Forecasting.Application;
using BeeEye.Modules.Forecasting.Contracts;
using BeeEye.Shared.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.Forecasting;

/// <summary>UC2 — Sales Forecast Accuracy Improvement endpoints.</summary>
internal static class ForecastingEndpoints
{
    private static readonly string[] Dimensions = ["model", "variant", "location", "brand", "type", "region"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/forecasting").WithTags("Forecasting");

        group.MapGet("/", () => new ModuleInfo(
                "Forecasting", "forecasting", "Forecast plans, versions, snapshots and accuracy metrics (UC2).", "operational"))
            .WithName("Forecasting_Info")
            .WithSummary("Forecasting module information");

        group.MapGet("/forecast", async (
                ForecastingReadService svc, CancellationToken ct,
                int? horizon, int? holdout, int? ci, string? algo,
                string[]? brand, string[]? model, string[]? variant, string[]? type,
                string[]? location, string[]? colour, string[]? interior, string? dateFrom, string? dateTo) =>
            {
                var options = new ForecastOptions(
                    Horizon: horizon ?? 6,
                    Holdout: holdout ?? 6,
                    Algo: algo,
                    Ci: ci ?? 80);
                var filter = SalesFilter.From(brand, model, variant, type, location, colour, interior, dateFrom, dateTo);
                var response = await svc.ForecastAsync(filter, options, ct);
                return response is null
                    ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Insufficient sales history",
                        detail: "At least three months of sales history are required to forecast.")
                    : Results.Ok(response);
            })
            .WithName("Forecasting_Forecast")
            .WithSummary("Back-test comparison, chosen model and future forecast with confidence intervals");

        group.MapGet("/accuracy-by/{dimension}", async (
                string dimension, ForecastingReadService svc, CancellationToken ct, int? holdout,
                string[]? brand, string[]? model, string[]? variant, string[]? type,
                string[]? location, string[]? colour, string[]? interior, string? dateFrom, string? dateTo) =>
            {
                if (!Dimensions.Contains(dimension, StringComparer.OrdinalIgnoreCase))
                {
                    return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unknown dimension",
                        detail: $"Dimension must be one of: {string.Join(", ", Dimensions)}.");
                }

                var filter = SalesFilter.From(brand, model, variant, type, location, colour, interior, dateFrom, dateTo);
                var response = await svc.AccuracyByAsync(dimension, filter, holdout ?? 6, ct);
                return response is null
                    ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Insufficient sales history")
                    : Results.Ok(response);
            })
            .WithName("Forecasting_AccuracyBy")
            .WithSummary("Where forecasts consistently over- or under-perform, by product / region");

        group.MapGet("/filter-options", async (ForecastingReadService svc, CancellationToken ct) =>
            {
                var options = await svc.FilterOptionsAsync(ct);
                return options is null ? Results.Ok(EmptyOptions) : Results.Ok(options);
            })
            .WithName("Forecasting_FilterOptions")
            .WithSummary("Available filter dimension values and month range");
    }

    private static readonly ForecastFilterOptions EmptyOptions = new([], [], [], [], [], [], [], string.Empty, string.Empty);
}
