using BeeEye.Analytics.Configuration;
using BeeEye.Modules.SalesActuals.Application;
using BeeEye.Modules.SalesActuals.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Paging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.SalesActuals;

/// <summary>UC3 — Configuration-Level Demand Insights endpoints.</summary>
internal static class ConfigurationEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/sales-actuals").WithTags("Configuration Demand");

        group.MapGet("/", () => new ModuleInfo(
                "Sales Actuals", "sales-actuals", "Configuration-level demand insights and dead-stock signals (UC3).", "operational"))
            .WithName("SalesActuals_Info")
            .WithSummary("Sales Actuals module information");

        group.MapGet("/config-demand/summary", async (
                ConfigurationReadService svc, CancellationToken ct,
                string[]? model, string[]? variant, string[]? colour, string[]? interior, string[]? rotation) =>
            {
                var all = await svc.AnalyseAsync(ConfigDemandSettings.Default, ct);
                var filter = ConfigFilter.From(model, variant, colour, interior, rotation);
                var filtered = all.Where(filter.Matches).ToList();
                return Results.Ok(new ConfigSummaryResponse(
                    ConfigurationDemand.Summarise(filtered), new ConfigMeta(all.Count, filtered.Count, DateTimeOffset.UtcNow)));
            })
            .WithName("SalesActuals_ConfigSummary")
            .WithSummary("Rotation mix, decay alerts and dead-stock counts");

        group.MapGet("/config-demand/configs", async (
                ConfigurationReadService svc, CancellationToken ct, string? sort, int? page, int? pageSize,
                string[]? model, string[]? variant, string[]? colour, string[]? interior, string[]? rotation) =>
            {
                var all = await svc.AnalyseAsync(ConfigDemandSettings.Default, ct);
                var filter = ConfigFilter.From(model, variant, colour, interior, rotation);
                var filtered = all.Where(filter.Matches).ToList();
                var pageReq = new PageRequest(page ?? 1, pageSize ?? PageRequest.DefaultPageSize);
                var rows = filtered.Sort(sort).Skip(pageReq.Offset).Take(pageReq.PageSize).Select(c => c.ToRow()).ToList();
                return Results.Ok(new ConfigListResponse(
                    rows, pageReq.Page, pageReq.PageSize, filtered.Count, new ConfigMeta(all.Count, filtered.Count, DateTimeOffset.UtcNow)));
            })
            .WithName("SalesActuals_Configs")
            .WithSummary("Server-side paged configuration demand grid");

        group.MapGet("/config-demand/decay-alerts", async (ConfigurationReadService svc, CancellationToken ct) =>
            {
                var all = await svc.AnalyseAsync(ConfigDemandSettings.Default, ct);
                return Results.Ok(all.Where(c => c.DecayAlert).OrderBy(c => c.DecayPct).Select(c => c.ToRow()).ToList());
            })
            .WithName("SalesActuals_DecayAlerts")
            .WithSummary("Configurations with material demand decay");

        group.MapGet("/config-demand/config", async (
                ConfigurationReadService svc, CancellationToken ct, string model, string variant, string colour, string interior) =>
            {
                var all = await svc.AnalyseAsync(ConfigDemandSettings.Default, ct);
                var match = all.FirstOrDefault(c => c.Model == model && c.Variant == variant && c.Colour == colour && c.Interior == interior);
                return match is null
                    ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Configuration not found")
                    : Results.Ok(match);
            })
            .WithName("SalesActuals_ConfigDetail")
            .WithSummary("Full demand insight (incl. regional breakdown) for one configuration");

        group.MapGet("/config-demand/filter-options", async (ConfigurationReadService svc, CancellationToken ct) =>
            {
                // Distinct dimension values only — must not run the full demand analysis.
                var (models, variants, colours, interiors) = await svc.FilterOptionsAsync(ct);
                return Results.Ok(new ConfigFilterOptions(models, variants, colours, interiors, ["Fast", "Medium", "Slow", "Dead"]));
            })
            .WithName("SalesActuals_FilterOptions");
    }
}
