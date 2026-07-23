namespace BeeEye.Analytics.AfterSales;

/// <summary>
/// Canonical band definitions for UC6 breakdowns. Ordering here drives the display
/// order deterministically; mileage bands originate on the service record (from the
/// odometer feed), while time-since-sale bands are derived from months-since-sale.
/// </summary>
public static class AfterSalesBands
{
    /// <summary>Mileage bands in display order (matches the synthetic odometer generator).</summary>
    public static readonly string[] MileageOrder = ["0–20k", "20–60k", "60–120k", "120k+"];

    /// <summary>Time-since-sale bands in display order.</summary>
    public static readonly string[] TimeSinceSaleOrder =
        ["0–3m", "3–6m", "6–12m", "12–24m", "24–36m", "36–48m", "48m+"];

    /// <summary>Maps months-since-sale to a time-since-sale band. Negative months clamp to the first band.</summary>
    public static string TimeSinceSaleBand(int monthsSinceSale) => monthsSinceSale switch
    {
        < 3 => "0–3m",
        < 6 => "3–6m",
        < 12 => "6–12m",
        < 24 => "12–24m",
        < 36 => "24–36m",
        < 48 => "36–48m",
        _ => "48m+",
    };

    /// <summary>Stable sort key for a band string against a canonical order (unknown bands sort last, alphabetically).</summary>
    public static int OrderIndex(IReadOnlyList<string> order, string band)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (string.Equals(order[i], band, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return order.Count;
    }
}
