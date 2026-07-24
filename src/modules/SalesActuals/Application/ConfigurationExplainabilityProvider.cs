using BeeEye.Analytics.Configuration;
using BeeEye.Analytics.Explainability;

namespace BeeEye.Modules.SalesActuals.Application;

/// <summary>
/// Explains one model·variant·colour·interior configuration's demand classification (UC3,
/// V3-DS-006).
/// <para>
/// UC3 produces an <b>observation</b>, not a recommendation: it says how fast a configuration is
/// rotating and whether that has decayed, and it stops there. The output label says so — labelling
/// it "Recommendation" would imply an action the engine never proposed.
/// </para>
/// <para>
/// The one judgement it does make is worth explaining carefully: <i>stockout-suspected</i>
/// distinguishes "nobody wants it" from "there was nothing to sell". Getting those two the wrong way
/// round is the difference between discontinuing a configuration and reordering it.
/// </para>
/// </summary>
public sealed class ConfigurationExplainabilityProvider(ConfigurationReadService configurations)
    : IExplainabilityProvider
{
    /// <summary>The subject is a configuration, referenced as <c>"{Model}|{Variant}|{Colour}|{Interior}"</c>.</summary>
    public const string ConfigurationKind = "configuration";

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { ConfigurationKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        var settings = new ConfigDemandSettings();
        var results = await configurations.AnalyseAsync(settings, cancellationToken);

        var config = results.FirstOrDefault(c =>
            string.Equals(
                ConfigurationDemand.Key(c.Model, c.Variant, c.Colour, c.Interior),
                subjectRef,
                StringComparison.OrdinalIgnoreCase));

        if (config is null)
        {
            return null;
        }

        var prices = await configurations.AverageSellingPricesAsync(cancellationToken);
        var price = prices.TryGetValue((config.Model, config.Variant), out var p) ? p : 0m;

        return new Explanation(
            Title: $"{config.Model} {config.Variant} · {config.Colour} · {config.Interior}",
            Module: "Configuration Insights",

            // A data-quality finding takes precedence over the demand reading, because the demand
            // reading is the thing it casts doubt on.
            Label: config.StockoutSuspected
                ? OutputLabel.DataQuality
                : config.IsColdStart
                    ? OutputLabel.LowConfidence
                    : OutputLabel.Calculated,

            // UC3 observes; it does not advise. The section is omitted rather than filled with a
            // sentence the engine never wrote.
            Recommendation: null,
            Impacts:
            [
                new("Rotation class", config.RotationClass, RotationTone(config.RotationClass)),
                new("Recent velocity",
                    $"{ExplanationFormat.Number((decimal)config.RecentVelocity)} units/month",
                    config.RecentVelocity > 0 ? ImpactTone.Neutral : ImpactTone.Negative),
                new("Change vs prior period",
                    ExplanationFormat.SignedPercent((decimal)config.DecayPct),
                    config.DecayAlert ? ImpactTone.Negative : ImpactTone.Neutral),
                new("Stock still held",
                    price > 0
                        ? $"{ExplanationFormat.Count(config.CurrentStock)} units · "
                          + ExplanationFormat.Sar(config.CurrentStock * price)
                        : $"{ExplanationFormat.Count(config.CurrentStock)} units",
                    config.CurrentStock > 0 && config.RecentVelocity <= 0
                        ? ImpactTone.Negative
                        : ImpactTone.Neutral),
            ],
            Confidence: new ConfidenceStatement(
                Band(config),
                Percent: null,
                Why: Why(config, settings)),
            Drivers: Drivers(config, settings),

            // The UC3 screen shows a regional split, not a time series, and the regional split is
            // already carried as drivers. There is no chart on that screen for the drawer to reuse,
            // so the section is omitted.
            Evidence: null,
            Assumptions: Assumptions(config, settings),
            Lineage:
            [
                new LineageNode("Oracle Fusion — sales (system of record)", LineageKind.Fusion),
                new LineageNode("Sales workbook (sales.json)", LineageKind.Workbook),
                new LineageNode("Inventory workbook (inventory.json)", LineageKind.Workbook),
                new LineageNode("Configuration demand analysis (UC3)", LineageKind.Derived),
            ],
            Model: new ModelInfo(
                Name: "Configuration rotation & decay analysis",
                Version: "UC3 · threshold rule set",
                Recalculated: $"On request — history {config.FirstMonth} to {config.LastMonth}",
                Horizon: $"{ExplanationFormat.Count(settings.TrailingMonths)}-month trailing window",
                Validation: "Deterministic thresholds — reproducible from the same inputs",
                Error: "rule-based"),

            // An observation has no owner and no workflow. The screen's own decision footer routes
            // into the Decision Log where a persisted recommendation exists.
            Ownership: null,
            IsDemoData: false);
    }

    private static IReadOnlyList<Driver> Drivers(ConfigDemandResult c, ConfigDemandSettings settings)
    {
        var drivers = new List<Driver>
        {
            new($"Velocity over the last {settings.TrailingMonths} months",
                $"{ExplanationFormat.Number((decimal)c.RecentVelocity)} units/month vs "
                + $"{ExplanationFormat.Number((decimal)c.PriorVelocity)} in the prior "
                + $"{settings.TrailingMonths}"),
            new($"Rotation threshold: {c.RotationClass}",
                $"fast ≥ {ExplanationFormat.Number((decimal)settings.FastThreshold)} · medium ≥ "
                + $"{ExplanationFormat.Number((decimal)settings.MediumThreshold)} units/month"),
            new($"Demand trend: {c.TrendDirection}",
                $"{ExplanationFormat.SignedPercent((decimal)c.DecayPct)} change"),
            new("Lifetime volume",
                $"{ExplanationFormat.Count((decimal)c.TotalUnits)} units over "
                + $"{ExplanationFormat.Count(c.MonthsWithSales)} months with sales"),
        };

        if (c.ByRegion.Count > 0)
        {
            var top = c.ByRegion[0];
            drivers.Add(new Driver(
                $"Concentrated in {top.Region}",
                $"{ExplanationFormat.Percent((decimal)(c.TopRegionShare * 100))} of units, across "
                + $"{ExplanationFormat.Count(c.ByRegion.Count)} regions"));
        }

        if (c.StockoutSuspected)
        {
            drivers.Add(new Driver(
                "Availability, not demand, may explain the zero",
                "sold historically · nothing recent · no stock on hand"));
        }

        return drivers;
    }

    private static IReadOnlyList<string> Why(ConfigDemandResult c, ConfigDemandSettings settings)
    {
        var why = new List<string>
        {
            $"Based on {ExplanationFormat.Count(c.MonthsWithSales)} months with recorded sales, "
            + $"{c.FirstMonth} to {c.LastMonth}.",
        };

        if (c.IsColdStart)
        {
            why.Add(
                $"Fewer than {settings.ColdStartMinMonths} months of history — the rotation class is "
                + "provisional and will move as the configuration accumulates sales.");
        }

        if (c.StockoutSuspected)
        {
            why.Add(
                "This configuration sold historically, has sold nothing recently, and has no stock on "
                + "hand. The zero may be a supply gap rather than absent demand, so treat the "
                + "classification as unreliable until stock is available again.");
        }

        if (c.DecayAlert)
        {
            why.Add(
                $"Demand has fallen {ExplanationFormat.Percent((decimal)Math.Abs(c.DecayPct))} against the "
                + $"prior period, past the {ExplanationFormat.Percent((decimal)Math.Abs(settings.DecayAlertPct))} "
                + "alert threshold.");
        }

        return why;
    }

    private static IReadOnlyList<string> Assumptions(ConfigDemandResult c, ConfigDemandSettings settings)
    {
        var assumptions = new List<string>
        {
            $"Velocity compares the last {settings.TrailingMonths} months against the "
                + $"{settings.TrailingMonths} before them; a longer window would smooth the change.",
            "Current stock is used as a proxy for availability. It is a point-in-time count, not a "
                + "history, so a configuration that was out of stock and has since been replenished "
                + "reads as available.",
            "Stock value uses this model·variant's units-weighted average selling price from sales "
                + "history — not a quoted or listed price.",
        };

        if (c.StockoutSuspected)
        {
            assumptions.Add(
                "Stockout suspicion is inferred from the absence of both sales and stock. It is not "
                + "confirmed against a stock-movement history, which the platform does not yet hold.");
        }

        return assumptions;
    }

    private static ConfidenceBand Band(ConfigDemandResult c) => c switch
    {
        { StockoutSuspected: true } => ConfidenceBand.Low,
        { IsColdStart: true } => ConfidenceBand.Low,
        { MonthsWithSales: >= 12 } => ConfidenceBand.High,
        _ => ConfidenceBand.Medium,
    };

    private static ImpactTone RotationTone(string rotation) => rotation switch
    {
        "Fast" => ImpactTone.Positive,
        "Medium" => ImpactTone.Neutral,
        "Slow" => ImpactTone.Warning,
        _ => ImpactTone.Negative,
    };
}
