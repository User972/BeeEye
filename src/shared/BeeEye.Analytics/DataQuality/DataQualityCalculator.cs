namespace BeeEye.Analytics.DataQuality;

/// <summary>Minimal sales projection the data-quality checks need — one row per sales fact.</summary>
public sealed record DataQualitySalesRow(
    string Location,
    int UnitsSold,
    decimal UnitPrice,
    decimal Revenue,
    int DiscountPct);

/// <summary>Minimal inventory projection the data-quality checks need — one row per unit.</summary>
public sealed record DataQualityInventoryRow(
    string StockId,
    string ChassisNo,
    string Location,
    decimal PurchasePrice,
    decimal HoldingCostPerDay,
    DateOnly DateOfPurchase,
    DateOnly DateOfManufacture,
    int LeadTimeDays);

/// <summary>One data-quality finding — a named check, its count, a severity word and a plain note.</summary>
public sealed record DataQualityIssue(string Id, string Label, int Count, string Severity, string Note);

/// <summary>
/// The result of the data-quality assessment: the 0–100 score, its band, the input row counts,
/// the list of sales locations that hold no inventory, and the itemised issues.
/// </summary>
public sealed record DataQualityReport(
    int Score,
    string ScoreBand,
    int SalesRows,
    int InvRows,
    IReadOnlyList<string> Mismatch,
    IReadOnlyList<DataQualityIssue> Issues);

/// <summary>
/// A faithful C# port of <c>engine.js</c> <c>dataQuality()</c> (L490–514). Given the sales and
/// inventory rows it reproduces the duplicate/negative/reconciliation/location checks, the exact score
/// formula and its 0–100 clamp, plus the score band thresholds V3-GOV-008 defines.
/// <para>
/// This is not under the strict UC2/UC5 parity rule (it has no wireframe counterpart in the analytics
/// contract), but it is treated as a faithful, tested port: the score arithmetic mirrors the JavaScript
/// exactly, including the round-half-up behaviour of <c>Math.round</c>.
/// </para>
/// </summary>
public static class DataQualityCalculator
{
    /// <summary>V3-GOV-008 score bands: ≥85 Healthy, ≥70 Warning, otherwise Critical.</summary>
    public static string ScoreBand(int score) => score switch
    {
        >= 85 => "Healthy",
        >= 70 => "Warning",
        _ => "Critical",
    };

    public static DataQualityReport Compute(
        IReadOnlyList<DataQualitySalesRow> sales,
        IReadOnlyList<DataQualityInventoryRow> inventory)
    {
        ArgumentNullException.ThrowIfNull(sales);
        ArgumentNullException.ThrowIfNull(inventory);

        // Duplicate natural keys: total rows minus the distinct-key count (engine.js: length − uniq().length).
        var dupStock = inventory.Count - inventory.Select(r => r.StockId).Distinct(StringComparer.Ordinal).Count();
        var dupChassis = inventory.Count - inventory.Select(r => r.ChassisNo).Distinct(StringComparer.Ordinal).Count();

        // Negative quantities / amounts, split across the two datasets exactly as the engine does.
        var negS = sales.Count(r => r.UnitsSold < 0 || r.Revenue < 0 || r.UnitPrice < 0);
        var negI = inventory.Count(r => r.PurchasePrice < 0 || r.HoldingCostPerDay < 0);

        // Revenue reconciliation: revenue = units × price × (1 − discount%), within max(1, 1% of expected).
        // Money is decimal (CLAUDE.md rule 6) — the reconciliation runs in decimal, never floating point.
        var revBad = 0;
        foreach (var r in sales)
        {
            var expected = r.UnitsSold * r.UnitPrice * (1m - r.DiscountPct / 100m);
            var error = Math.Abs(expected - r.Revenue);
            var tolerance = Math.Max(1m, Math.Abs(expected) * 0.01m);
            if (error > tolerance)
            {
                revBad++;
            }
        }

        // Lead-time reconciliation: purchase − manufacture should equal lead_time_days, within 2 days.
        var ltBad = inventory.Count(r =>
            Math.Abs((r.DateOfPurchase.DayNumber - r.DateOfManufacture.DayNumber) - r.LeadTimeDays) > 2);

        // Sales locations that hold no inventory snapshot — order-preserving distinct, so the note and
        // the Inventory-on-hand status read the same list.
        var invLocations = inventory.Select(r => r.Location).ToHashSet(StringComparer.Ordinal);
        var mismatch = sales
            .Select(r => r.Location)
            .Where(l => !invLocations.Contains(l))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Dates arrive already typed (DateOnly) from the operational store, so none are unparseable —
        // the check is retained for parity and transparency, and contributes 0 to a clean dataset.
        const int badDates = 0;

        // engine.js: Math.round(100 − …) then clamp. Math.round is round-half-up (floor(x + 0.5)); the
        // only fractional term is revBad × 0.5, so matching that rounding keeps the score identical.
        var raw = 100.0
                  - (dupStock + dupChassis) * 5
                  - revBad * 0.5
                  - (negS + negI) * 5
                  - badDates * 2
                  - mismatch.Count * 1.5;
        var score = Math.Clamp((int)Math.Floor(raw + 0.5), 0, 100);

        var issues = new List<DataQualityIssue>
        {
            new("dup_stock", "Duplicate stock IDs", dupStock, dupStock > 0 ? "high" : "ok",
                dupStock > 0 ? $"{dupStock} stock_id values are duplicated." : "All stock_id values are unique."),
            new("dup_chassis", "Duplicate chassis numbers", dupChassis, dupChassis > 0 ? "high" : "ok",
                dupChassis > 0 ? $"{dupChassis} chassis_no values are duplicated." : "All chassis_no values are unique."),
            new("rev", "Revenue reconciliation mismatches (>1%)", revBad, revBad > 0 ? "medium" : "ok",
                "revenue = units × price × (1 − discount%) verified per row."),
            new("lead", "Lead-time reconciliation mismatches (>2d)", ltBad, ltBad > 0 ? "medium" : "ok",
                "lead_time_days = purchase − manufacture verified."),
            new("neg", "Negative quantities / amounts", negS + negI, negS + negI > 0 ? "high" : "ok",
                negS + negI > 0
                    ? $"{negS + negI} negative units, prices, revenue or holding costs."
                    : "No negative units, prices, revenue or holding costs."),
            new("dates", "Invalid / unparseable dates", badDates, badDates > 0 ? "high" : "ok",
                "All dates parsed to valid calendar dates."),
            new("loc", "Sales locations absent from inventory", mismatch.Count, mismatch.Count > 0 ? "medium" : "ok",
                mismatch.Count > 0
                    ? string.Join(", ", mismatch) + " sell but hold no inventory snapshot."
                    : "None."),
        };

        return new DataQualityReport(score, ScoreBand(score), sales.Count, inventory.Count, mismatch, issues);
    }
}
