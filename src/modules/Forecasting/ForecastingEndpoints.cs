using BeeEye.Shared.Web.Security;
using BeeEye.Shared.Security;
using BeeEye.Analytics.Forecasting;
using BeeEye.Modules.Forecasting.Application;
using BeeEye.Modules.Forecasting.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Results;
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
        var group = app.MapGroup($"{ApiRoutes.V1}/forecasting").WithTags("Forecasting")
            .RequireReadPermission(Permissions.ForecastView);

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
                var result = await svc.ForecastAsync(filter, options, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error);
            })
            .WithName("Forecasting_Forecast")
            .WithSummary("Back-test comparison, chosen model and future forecast with confidence intervals")
            .Produces<ForecastResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

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
                var result = await svc.AccuracyByAsync(dimension, filter, holdout ?? 6, ct);
                return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error);
            })
            .WithName("Forecasting_AccuracyBy")
            .WithSummary("Where forecasts consistently over- or under-perform, by product / region")
            .Produces<AccuracyByResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/filter-options", async (ForecastingReadService svc, CancellationToken ct) =>
            {
                var options = await svc.FilterOptionsAsync(ct);
                return options is null ? Results.Ok(EmptyOptions) : Results.Ok(options);
            })
            .WithName("Forecasting_FilterOptions")
            .WithSummary("Available filter dimension values and month range");
    }

    private static readonly ForecastFilterOptions EmptyOptions = new([], [], [], [], [], [], [], string.Empty, string.Empty);

    /// <summary>Maps read-service failures to accurate Problem Details — a no-match filter
    /// must not be reported as insufficient history.</summary>
    private static IResult ToProblem(Error error) => error.Code switch
    {
        "no_match" => Results.Problem(statusCode: StatusCodes.Status404NotFound,
            title: "No matching sales data", detail: error.Message),
        _ => Results.Problem(statusCode: StatusCodes.Status404NotFound,
            title: "Insufficient sales history", detail: error.Message),
    };
}
