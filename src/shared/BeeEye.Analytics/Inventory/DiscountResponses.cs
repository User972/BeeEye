using BeeEye.Analytics.Demand;

namespace BeeEye.Analytics.Inventory;

/// <summary>
/// Builds per-model-variant discount responsiveness from sales history, ported from
/// engine.js <c>discountResponsive()</c>. Association only — used to inform, not to
/// assert causation.
/// </summary>
public static class DiscountResponses
{
    public static IReadOnlyDictionary<string, DiscountResponse> Build(IReadOnlyList<SalesRow> sales)
    {
        var groups = new Dictionary<string, List<SalesRow>>();
        foreach (var r in sales)
        {
            var key = $"{r.Model}|{r.Variant}";
            if (!groups.TryGetValue(key, out var list))
            {
                list = [];
                groups[key] = list;
            }

            list.Add(r);
        }

        var result = new Dictionary<string, DiscountResponse>(groups.Count);
        foreach (var (key, rows) in groups)
        {
            var discounted = rows.Where(r => r.Discounted).ToList();
            var notDiscounted = rows.Where(r => !r.Discounted).ToList();
            var da = discounted.Count > 0 ? Statistics.Mean(discounted.Select(r => r.Units).ToArray()) : 0;
            var na = notDiscounted.Count > 0 ? Statistics.Mean(notDiscounted.Select(r => r.Units).ToArray()) : 0;
            var pcts = discounted.Select(r => r.DiscountPct).Where(p => p > 0).Distinct().OrderBy(p => p).ToArray();

            var suggest = pcts.Length > 0 ? Math.Min(15, pcts[pcts.Length / 2]) : 10;
            var range = pcts.Length > 0 ? $"{pcts[0]}%–{pcts[^1]}%" : "0%";
            var responsive = da > na * 1.03 && discounted.Count >= 5;
            result[key] = new DiscountResponse(responsive, suggest, range, da, na);
        }

        return result;
    }
}
