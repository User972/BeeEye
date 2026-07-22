using BeeEye.Analytics.Inventory;
using BeeEye.Modules.Inventory.Application;
using BeeEye.Modules.Inventory.Contracts;
using BeeEye.Shared.Api;
using BeeEye.Shared.Paging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BeeEye.Modules.Inventory;

/// <summary>UC5 — Inventory Aging &amp; Overstock Risk endpoints.</summary>
internal static class InventoryEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"{ApiRoutes.V1}/inventory").WithTags("Inventory");

        group.MapGet("/", () => new ModuleInfo(
                "Inventory", "inventory", "Inventory items, snapshots, aging and overstock-risk scoring (UC5).", "operational"))
            .WithName("Inventory_Info")
            .WithSummary("Inventory module information");

        group.MapGet("/summary", async (
                InventoryReadService svc, CancellationToken ct,
                DateOnly? analysisDate,
                string[]? brand, string[]? model, string[]? variant, string[]? type,
                string[]? location, string[]? colour, string[]? interior, string[]? riskBand) =>
            {
                var settings = BuildSettings(analysisDate);
                var all = await svc.ComputeAsync(settings, ct);
                var filter = InventoryFilter.From(brand, model, variant, type, location, colour, interior, riskBand);
                var filtered = all.Where(filter.Matches).ToList();
                var summary = InventoryAggregator.Aggregate(filtered);
                return Results.Ok(new InventorySummaryResponse(summary, Meta(settings, all.Count, filtered.Count)));
            })
            .WithName("Inventory_Summary")
            .WithSummary("Portfolio KPIs and risk / aging distributions");

        group.MapGet("/items", async (
                InventoryReadService svc, CancellationToken ct,
                DateOnly? analysisDate, string? sort, int? page, int? pageSize,
                string[]? brand, string[]? model, string[]? variant, string[]? type,
                string[]? location, string[]? colour, string[]? interior, string[]? riskBand) =>
            {
                var settings = BuildSettings(analysisDate);
                var all = await svc.ComputeAsync(settings, ct);
                var filter = InventoryFilter.From(brand, model, variant, type, location, colour, interior, riskBand);
                var filtered = all.Where(filter.Matches).ToList();
                var pageReq = new PageRequest(page ?? 1, pageSize ?? PageRequest.DefaultPageSize);
                var rows = filtered.Sort(sort).Skip(pageReq.Offset).Take(pageReq.PageSize).Select(u => u.ToRow()).ToList();
                return Results.Ok(new InventoryItemsResponse(
                    rows, pageReq.Page, pageReq.PageSize, filtered.Count, Meta(settings, all.Count, filtered.Count)));
            })
            .WithName("Inventory_Items")
            .WithSummary("Server-side paged, filtered and sorted inventory grid");

        group.MapGet("/items/{stockId}", async (
                string stockId, InventoryReadService svc, CancellationToken ct, DateOnly? analysisDate) =>
            {
                var all = await svc.ComputeAsync(BuildSettings(analysisDate), ct);
                var unit = all.FirstOrDefault(u => u.StockId == stockId);
                return unit is null
                    ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Inventory unit not found",
                        detail: $"No inventory unit with stock id '{stockId}'.")
                    : Results.Ok(unit);
            })
            .WithName("Inventory_ItemDetail")
            .WithSummary("Full risk breakdown, factors and recommendation for one unit");

        group.MapGet("/filter-options", async (InventoryReadService svc, CancellationToken ct) =>
            {
                var all = await svc.ComputeAsync(RiskSettings.Default, ct);
                return Results.Ok(new FilterOptions(
                    Distinct(all, u => u.Brand), Distinct(all, u => u.Model), Distinct(all, u => u.Variant),
                    Distinct(all, u => u.Type), Distinct(all, u => u.Location), Distinct(all, u => u.Colour),
                    Distinct(all, u => u.Interior), ["Low", "Medium", "High", "Critical"]));
            })
            .WithName("Inventory_FilterOptions")
            .WithSummary("Available filter dimension values");
    }

    private static RiskSettings BuildSettings(DateOnly? analysisDate)
        => analysisDate is { } d ? RiskSettings.Default with { AnalysisDate = d } : RiskSettings.Default;

    private static InventoryMeta Meta(RiskSettings settings, int total, int filtered)
        => new(settings.AnalysisDate, total, filtered, DateTimeOffset.UtcNow);

    private static IReadOnlyList<string> Distinct(
        IEnumerable<InventoryUnitRisk> units, Func<InventoryUnitRisk, string> selector)
        => units.Select(selector).Distinct().OrderBy(x => x).ToList();
}
