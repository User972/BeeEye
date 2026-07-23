using BeeEye.Analytics;
using BeeEye.Analytics.SpareParts;
using BeeEye.Modules.SpareParts.Contracts;
using BeeEye.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.SpareParts.Application;

/// <summary>
/// UC7 read side. Builds a dense monthly usage series per <b>part × location</b> (explicit zeros over the
/// observed range — zero months are real signal), rolls superseded parts' history onto their successor,
/// then runs the pure <see cref="SparePartsForecaster"/>. Part-level catalogue stock is distributed across
/// a part's servicing locations in proportion to their demand share (a documented POC simplification, since
/// the synthetic catalogue holds one national stock figure). Orchestration only — the maths live in
/// <c>BeeEye.Analytics</c>.
/// </summary>
public sealed class SparePartsReadService(BeeEyeDbContext db)
{
    /// <summary>A part × location recommendation plus the supply fields the table shows.</summary>
    public sealed record PartResult(
        string PartNumber, string Name, string Category, string Location,
        int CurrentStock, int InboundStock, int LeadTimeDays,
        IReadOnlyList<string> Models, SparePartRecommendation Recommendation);

    private sealed record PartRow(
        Guid Id, string PartNumber, string Name, string Category, decimal UnitCost,
        int LeadTimeDays, int CurrentStock, int InboundStock, Guid? SupersededByPartId, bool IsActive);

    private sealed record Context(
        IReadOnlyList<PartRow> Parts,
        Dictionary<Guid, PartRow> ById,
        Dictionary<(Guid Part, string Location), Dictionary<string, double>> UsageByPartLocation,
        Dictionary<Guid, List<string>> LocationsByPart,
        Dictionary<Guid, double> TotalUsageByPart,
        Dictionary<(Guid Part, string Location), double> LocationUsageTotal,
        Dictionary<Guid, List<Guid>> DirectPredecessors,
        Dictionary<Guid, List<string>> ModelsByPart,
        IReadOnlyList<(Guid Old, Guid New, DateOnly Effective)> Supersessions);

    public async Task<bool> HasDataAsync(CancellationToken ct) => await db.Parts.AsNoTracking().AnyAsync(ct);

    public async Task<IReadOnlyList<PartResult>> RecommendAllAsync(SparePartsSettings settings, CancellationToken ct)
    {
        var ctx = await LoadAsync(ct);
        return BuildRows(ctx, settings)
            .OrderByDescending(r => r.Recommendation.PredictedMonthlyDemand ?? -1)
            .ThenBy(r => r.PartNumber, StringComparer.Ordinal)
            .ThenBy(r => r.Location, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<SparePartsSummary> SummaryAsync(SparePartsSettings settings, CancellationToken ct)
    {
        var ctx = await LoadAsync(ct);
        var rows = BuildRows(ctx, settings).ToList();
        return new SparePartsSummary(
            DistinctParts: rows.Select(r => r.PartNumber).Distinct(StringComparer.Ordinal).Count(),
            StockingPoints: rows.Count,
            LowDataPoints: rows.Count(r => r.Recommendation.InsufficientData),
            AtRiskPoints: rows.Count(r => r.Recommendation.StockoutRisk == "High"),
            PredictedMonthlyDemandTotal: rows.Sum(r => r.Recommendation.PredictedMonthlyDemand ?? 0),
            ByDemandClass: rows
                .GroupBy(r => r.Recommendation.Class)
                .Select(g => new DemandClassCount(g.Key.ToString(), g.Count()))
                .OrderByDescending(c => c.Count).ThenBy(c => c.DemandClass, StringComparer.Ordinal)
                .ToList());
    }

    public async Task<PartDetailResponse?> PartDetailAsync(string partNumber, SparePartsScenario scenario, CancellationToken ct)
    {
        var ctx = await LoadAsync(ct);
        var part = ctx.Parts.FirstOrDefault(p => string.Equals(p.PartNumber, partNumber, StringComparison.OrdinalIgnoreCase));
        if (part is null)
        {
            return null;
        }

        var settings = scenario.ToSettings();
        var predecessors = TransitivePredecessors(part.Id, ctx);

        // National series = rolled up across every location and the whole supersession chain.
        var (nationalSeries, months) = NationalSeries(part, predecessors, ctx);
        var nationalInput = new SparePartInput(
            part.PartNumber, part.Name, part.Category, part.LeadTimeDays, part.CurrentStock, part.InboundStock, part.UnitCost);
        var national = SparePartsForecaster.Recommend(nationalInput, nationalSeries, settings);

        var history = months.Select((m, i) => new UsagePoint(m, nationalSeries[i])).ToList();
        var byLocation = BuildRowsForPart(ctx, part, settings)
            .OrderByDescending(r => r.Recommendation.PredictedMonthlyDemand ?? -1)
            .ThenBy(r => r.Location, StringComparer.Ordinal)
            .Select(ToRow)
            .ToList();

        var models = ctx.ModelsByPart.GetValueOrDefault(part.Id, []).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var chainIds = new HashSet<Guid>(predecessors) { part.Id };
        var rolled = ctx.Supersessions
            .Where(s => chainIds.Contains(s.New))
            .Select(s => new SupersessionInfo(ctx.ById[s.Old].PartNumber, ctx.ById[s.New].PartNumber, s.Effective))
            .OrderBy(s => s.EffectiveDate)
            .ToList();
        var supersededBy = part.SupersededByPartId is { } sid && ctx.ById.TryGetValue(sid, out var successor)
            ? successor.PartNumber
            : null;

        return new PartDetailResponse(scenario, national, history, byLocation, models, rolled, supersededBy, SparePartsProvenance.Now());
    }

    public async Task<SparePartsFilterOptions> FilterOptionsAsync(CancellationToken ct)
    {
        var categories = await db.Parts.AsNoTracking().Select(p => p.Category).Distinct().ToListAsync(ct);
        var models = await db.PartCompatibilities.AsNoTracking().Select(c => c.Model).Distinct().ToListAsync(ct);
        return new SparePartsFilterOptions(
            categories.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            models.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            Enum.GetValues<DemandClass>().Select(c => c.ToString()).ToList());
    }

    private static PartDemandRow ToRow(PartResult r)
    {
        var rec = r.Recommendation;
        return new PartDemandRow(
            r.PartNumber, r.Name, r.Category, r.Location, rec.Class.ToString(), rec.Method,
            rec.PredictedMonthlyDemand, rec.StockingRangeLow, rec.StockingRangeHigh,
            r.CurrentStock, r.InboundStock, r.LeadTimeDays, rec.ReorderPoint,
            rec.StockoutRisk, rec.HoldingRisk, rec.Confidence, rec.InsufficientData);
    }

    public static PartDemandRow ToPublicRow(PartResult r) => ToRow(r);

    private IEnumerable<PartResult> BuildRows(Context ctx, SparePartsSettings settings)
    {
        foreach (var part in ctx.Parts.Where(p => p.IsActive))
        {
            foreach (var row in BuildRowsForPart(ctx, part, settings))
            {
                yield return row;
            }
        }
    }

    // Rows for a single part (regardless of active status), so the part-detail view still shows a
    // superseded (inactive) part's per-location breakdown, and a detail request forecasts one part
    // rather than the whole catalogue.
    private IEnumerable<PartResult> BuildRowsForPart(Context ctx, PartRow part, SparePartsSettings settings)
    {
        var predecessors = TransitivePredecessors(part.Id, ctx);
        var locations = ctx.LocationsByPart.GetValueOrDefault(part.Id, []);
        var partTotal = ctx.TotalUsageByPart.GetValueOrDefault(part.Id, 0);
        var models = ctx.ModelsByPart.GetValueOrDefault(part.Id, []);

        if (locations.Count == 0)
        {
            // A part with no usage anywhere (e.g. a brand-new niche part) — one flagged, low-data row.
            var input = new SparePartInput(part.PartNumber, part.Name, part.Category, part.LeadTimeDays, part.CurrentStock, part.InboundStock, part.UnitCost);
            yield return new PartResult(part.PartNumber, part.Name, part.Category, "(no usage)",
                part.CurrentStock, part.InboundStock, part.LeadTimeDays, models,
                SparePartsForecaster.Recommend(input, [], settings));
            yield break;
        }

        foreach (var location in locations)
        {
            var (series, _) = LocationSeries(part, predecessors, location, ctx);
            var share = partTotal > 0 ? ctx.LocationUsageTotal.GetValueOrDefault((part.Id, location), 0) / partTotal : 0;
            var stock = DistributeStock(part.CurrentStock, share);
            var inbound = DistributeStock(part.InboundStock, share);

            var input = new SparePartInput(part.PartNumber, part.Name, part.Category, part.LeadTimeDays, stock, inbound, part.UnitCost);
            yield return new PartResult(part.PartNumber, part.Name, part.Category, location,
                stock, inbound, part.LeadTimeDays, models, SparePartsForecaster.Recommend(input, series, settings));
        }
    }

    // Distribute a national stock figure to a location by its demand share (at least 1 unit where any is held).
    private static int DistributeStock(int nationalStock, double share)
    {
        if (nationalStock <= 0)
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Round(nationalStock * share, MidpointRounding.AwayFromZero));
    }

    private (double[] Series, IReadOnlyList<string> Months) LocationSeries(
        PartRow part, IReadOnlyList<Guid> predecessors, string location, Context ctx)
    {
        var ids = new List<Guid> { part.Id };
        ids.AddRange(predecessors);
        return RollUp(ids.Select(id => ctx.UsageByPartLocation.GetValueOrDefault((id, location), EmptyUsage)).ToList());
    }

    private (double[] Series, IReadOnlyList<string> Months) NationalSeries(
        PartRow part, IReadOnlyList<Guid> predecessors, Context ctx)
    {
        var ids = new List<Guid> { part.Id };
        ids.AddRange(predecessors);

        // Sum each part-id's usage across all locations, then roll up the chain.
        var perId = ids.Select(id =>
        {
            var merged = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var loc in ctx.LocationsByPart.GetValueOrDefault(id, []))
            {
                foreach (var (month, qty) in ctx.UsageByPartLocation.GetValueOrDefault((id, loc), EmptyUsage))
                {
                    merged[month] = merged.GetValueOrDefault(month, 0) + qty;
                }
            }

            return (IReadOnlyDictionary<string, double>)merged;
        }).ToList();

        return RollUp(perId);
    }

    private static (double[] Series, IReadOnlyList<string> Months) RollUp(IReadOnlyList<IReadOnlyDictionary<string, double>> components)
    {
        var monthsPresent = components.SelectMany(c => c.Keys).ToList();
        if (monthsPresent.Count == 0)
        {
            return ([], []);
        }

        var months = MonthKey.Range(monthsPresent.Min(StringComparer.Ordinal)!, monthsPresent.Max(StringComparer.Ordinal)!);
        var series = components
            .Select(c => (IReadOnlyList<double>)Intermittent.DenseSeries(c, months))
            .ToList();
        return (SparePartsForecaster.RollUpUsage(series), months);
    }

    private static readonly Dictionary<string, double> EmptyUsage = new(StringComparer.Ordinal);

    private static IReadOnlyList<Guid> TransitivePredecessors(Guid partId, Context ctx)
    {
        var result = new List<Guid>();
        var seen = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(partId);
        while (queue.Count > 0)
        {
            if (!ctx.DirectPredecessors.TryGetValue(queue.Dequeue(), out var preds))
            {
                continue;
            }

            foreach (var p in preds)
            {
                if (seen.Add(p))
                {
                    result.Add(p);
                    queue.Enqueue(p);
                }
            }
        }

        return result;
    }

    private async Task<Context> LoadAsync(CancellationToken ct)
    {
        var parts = (await db.Parts.AsNoTracking()
                .Select(p => new PartRow(
                    p.Id, p.PartNumber, p.Name, p.Category, p.UnitCost, p.LeadTimeDays, p.CurrentStock, p.InboundStock,
                    p.SupersededByPartId, p.IsActive))
                .ToListAsync(ct))
            .OrderBy(p => p.PartNumber, StringComparer.Ordinal)
            .ToList();
        var byId = parts.ToDictionary(p => p.Id);

        // Monthly usage aggregated per part × location in SQL (join to the service event for its location).
        var agg = await (from u in db.PartUsages.AsNoTracking()
                         join e in db.ServiceEvents.AsNoTracking() on u.ServiceEventId equals e.Id
                         group u by new { u.PartId, e.Location, u.UsageDate } into g
                         select new { g.Key.PartId, g.Key.Location, g.Key.UsageDate, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);

        var usageByPartLocation = new Dictionary<(Guid, string), Dictionary<string, double>>();
        var locationsByPart = new Dictionary<Guid, List<string>>();
        var totalUsageByPart = new Dictionary<Guid, double>();
        var locationUsageTotal = new Dictionary<(Guid, string), double>();

        foreach (var a in agg)
        {
            var month = MonthKey.Of(a.UsageDate);
            var key = (a.PartId, a.Location);
            if (!usageByPartLocation.TryGetValue(key, out var byMonth))
            {
                byMonth = new Dictionary<string, double>(StringComparer.Ordinal);
                usageByPartLocation[key] = byMonth;
                if (!locationsByPart.TryGetValue(a.PartId, out var locs))
                {
                    locs = [];
                    locationsByPart[a.PartId] = locs;
                }

                locs.Add(a.Location);
            }

            byMonth[month] = byMonth.GetValueOrDefault(month, 0) + a.Qty;
            totalUsageByPart[a.PartId] = totalUsageByPart.GetValueOrDefault(a.PartId, 0) + a.Qty;
            locationUsageTotal[key] = locationUsageTotal.GetValueOrDefault(key, 0) + a.Qty;
        }

        foreach (var locs in locationsByPart.Values)
        {
            locs.Sort(StringComparer.Ordinal);
        }

        var directPredecessors = new Dictionary<Guid, List<Guid>>();
        foreach (var p in parts.Where(p => p.SupersededByPartId is not null))
        {
            var successor = p.SupersededByPartId!.Value;
            if (!directPredecessors.TryGetValue(successor, out var list))
            {
                list = [];
                directPredecessors[successor] = list;
            }

            list.Add(p.Id);
        }

        var compat = await db.PartCompatibilities.AsNoTracking().Select(c => new { c.PartId, c.Model }).ToListAsync(ct);
        var modelsByPart = compat
            .GroupBy(c => c.PartId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Model).Distinct().ToList());

        var supers = (await db.PartSupersessions.AsNoTracking()
                .Select(s => new { s.OldPartId, s.NewPartId, s.EffectiveDate })
                .ToListAsync(ct))
            .Select(s => (s.OldPartId, s.NewPartId, s.EffectiveDate))
            .ToList();

        return new Context(parts, byId, usageByPartLocation, locationsByPart, totalUsageByPart, locationUsageTotal,
            directPredecessors, modelsByPart, supers);
    }
}
