using BeeEye.Analytics.Explainability;
using BeeEye.Analytics.SpareParts;
using BeeEye.Modules.SpareParts.Contracts;

namespace BeeEye.Modules.SpareParts.Application;

/// <summary>
/// Explains one spare part's demand classification and stocking recommendation (UC7, V3-DS-006).
/// <para>
/// The interesting thing to explain here is not the number but the <b>method</b>: UC7 classifies each
/// part by how its demand arrives — smooth, erratic, intermittent, lumpy or obsolescent — and picks
/// Croston, SBA or TSB accordingly. Someone asking "why 4 units a month?" is usually really asking
/// "why is this method appropriate for a part that sells three times a year?", so the classification
/// and the intervals behind it lead the drivers.
/// </para>
/// <para>
/// <b>Every explanation is demo data</b>: UC7 runs on the synthetic parts fixture derived from real
/// vehicle sales. The flag, the lineage node and the first assumption all say so.
/// </para>
/// </summary>
public sealed class SparePartsExplainabilityProvider(SparePartsReadService spareParts)
    : IExplainabilityProvider
{
    /// <summary>The subject is a part, referenced by part number.</summary>
    public const string PartKind = "part";

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { PartKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        var scenario = SparePartsScenario.From(null, null);
        var detail = await spareParts.PartDetailAsync(subjectRef, scenario, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var r = detail.National;
        var costs = await spareParts.UnitCostsAsync(cancellationToken);
        var unitCost = costs.TryGetValue(r.PartNumber, out var c) ? c : 0m;

        return new Explanation(
            Title: $"{r.PartNumber} · {r.Name}",
            Module: "Spare Parts Prediction",
            Label: r.InsufficientData ? OutputLabel.LowConfidence : OutputLabel.Forecast,
            Recommendation: r.RecommendedQuantity is { } quantity
                ? $"{r.Action} — about {ExplanationFormat.Count(quantity)} units. {r.Rationale}"
                : $"{r.Action}. {r.Rationale}",
            Impacts: Impacts(r, unitCost),
            Confidence: new ConfidenceStatement(
                Band(r),
                Percent: null,
                Why: Why(r, scenario)),
            Drivers: Drivers(r, detail, scenario),
            Evidence: Evidence(detail),
            Assumptions: Assumptions(r, scenario, detail),
            Lineage:
            [
                new LineageNode("Synthetic parts & usage fixture (demo)", LineageKind.Demo),
                new LineageNode("Sales workbook (sales.json)", LineageKind.Workbook),
                new LineageNode($"Intermittent-demand forecaster — {r.Method} (UC7)", LineageKind.Derived),
            ],
            Model: new ModelInfo(
                Name: $"{r.Method} · {r.Class} demand",
                Version: "UC7 · Croston / SBA / TSB family",
                Recalculated: "On request — computed live from the synthetic dataset",
                Horizon: $"{ExplanationFormat.Number((decimal)r.LeadTimeMonths, 2)}-month lead time + "
                    + $"{ExplanationFormat.Number((decimal)scenario.ReviewPeriodMonths)}-month review",
                Validation: $"Method selected by ADI/CV² classification over {r.Periods} periods",

                // UC7 selects its method by demand classification rather than by back-test, so there
                // is no hold-out error to report. Saying "rule-based" is the honest answer; putting a
                // number here would imply a validation that never ran.
                Error: "rule-based — classification, not back-test"),
            Ownership: new Ownership("Parts Manager", $"Stockout risk {r.StockoutRisk}"),
            IsDemoData: true);
    }

    private static IReadOnlyList<ImpactTile> Impacts(SparePartRecommendation r, decimal unitCost)
    {
        var tiles = new List<ImpactTile>
        {
            new("Predicted monthly demand",
                r.PredictedMonthlyDemand is { } demand
                    ? $"{ExplanationFormat.Number((decimal)demand, 2)} units"
                    : "not predictable",
                r.PredictedMonthlyDemand is null ? ImpactTone.Warning : ImpactTone.Neutral),
            new("Recommended stocking",
                r.RecommendedQuantity is { } quantity
                    ? $"{ExplanationFormat.Count(quantity)} units"
                    : "—",
                r.RecommendedQuantity is > 0 ? ImpactTone.Positive : ImpactTone.Neutral),
            new("Stockout risk", r.StockoutRisk, Tone(r.StockoutRisk)),
            new("Holding risk", r.HoldingRisk, Tone(r.HoldingRisk)),
        };

        if (unitCost > 0 && r.RecommendedQuantity is { } qty)
        {
            tiles.Add(new ImpactTile(
                "Stocking cost", ExplanationFormat.Sar(qty * unitCost), ImpactTone.Negative));
        }

        return tiles;
    }

    private static IReadOnlyList<Driver> Drivers(
        SparePartRecommendation r, PartDetailResponse detail, SparePartsScenario scenario)
    {
        var drivers = new List<Driver>
        {
            new($"Demand class: {r.Class}",
                $"ADI {ExplanationFormat.Number((decimal)r.Adi, 2)} · CV² "
                + ExplanationFormat.Number((decimal)r.Cv2, 2)),
            new($"Method chosen: {r.Method}",
                "selected by the ADI/CV² classification, not by hand · other candidates: SES "
                + $"{ExplanationFormat.Number((decimal)r.Comparison.Ses, 2)} · Croston "
                + $"{ExplanationFormat.Number((decimal)r.Comparison.Croston, 2)} · SBA "
                + $"{ExplanationFormat.Number((decimal)r.Comparison.Sba, 2)} · TSB "
                + $"{ExplanationFormat.Number((decimal)r.Comparison.Tsb, 2)}"),
            new("Observed demand periods",
                $"{ExplanationFormat.Count(r.NonZeroPeriods)} months with usage out of "
                + $"{ExplanationFormat.Count(r.Periods)}"),
            new("Lead-time demand",
                r.LeadTimeDemand is { } lead
                    ? $"{ExplanationFormat.Number((decimal)lead, 2)} units over "
                      + $"{ExplanationFormat.Number((decimal)r.LeadTimeMonths, 2)} months"
                    : "not computable"),
            new($"Service level target {ExplanationFormat.Percent((decimal)(scenario.ServiceLevel * 100), 0)}",
                r.SafetyStock is { } safety
                    ? $"drives {ExplanationFormat.Number((decimal)safety, 2)} units of safety stock"
                    : "no safety stock computable"),
            new("Stock on hand and inbound",
                $"{ExplanationFormat.Count(r.Available)} units available"),
        };

        if (detail.RolledUpSupersessions.Count > 0)
        {
            drivers.Add(new Driver(
                "Supersession chain rolled up",
                string.Join(" → ", detail.RolledUpSupersessions.Select(s => s.OldPartNumber).Append(r.PartNumber))));
        }

        if (detail.CompatibleModels.Count > 0)
        {
            drivers.Add(new Driver(
                "Fitted to",
                string.Join(", ", detail.CompatibleModels)));
        }

        return drivers;
    }

    /// <summary>
    /// The national usage history. UC7's screen already charts it, so the drawer reuses the series
    /// rather than inventing a second view of the same data. Omitted when there is no history.
    /// </summary>
    private static EvidenceSeries? Evidence(PartDetailResponse detail)
    {
        if (detail.UsageHistory.Count == 0)
        {
            return null;
        }

        return new EvidenceSeries(
            Period: $"{detail.UsageHistory[0].Month} → {detail.UsageHistory[^1].Month}",
            Points:
            [
                .. detail.UsageHistory.Select(u => new EvidencePoint(
                    u.Month, decimal.Round((decimal)u.Quantity, 2), null)),
            ],
            Note:
                "National usage, rolled up across every location and the whole supersession chain. The "
                + "zero months are real observations, not missing data — that is what makes this demand "
                + "intermittent and why an ordinary moving average would mis-state it.",
            ValueLabel: "Units used");
    }

    private static IReadOnlyList<string> Why(SparePartRecommendation r, SparePartsScenario scenario)
    {
        var why = new List<string>
        {
            $"{ExplanationFormat.Count(r.NonZeroPeriods)} of {ExplanationFormat.Count(r.Periods)} months "
            + "recorded any usage at all.",
            $"Classified {r.Class} from an average demand interval of "
            + $"{ExplanationFormat.Number((decimal)r.Adi, 2)} months and a squared coefficient of variation "
            + $"of {ExplanationFormat.Number((decimal)r.Cv2, 2)}.",
        };

        if (r.InsufficientData)
        {
            why.Add(
                "Too few observations to forecast. The stocking range is withheld rather than "
                + "extrapolated from one or two data points.");
        }

        why.Add("The underlying parts and usage data are synthetic demo fixtures.");

        return why;
    }

    private static IReadOnlyList<string> Assumptions(
        SparePartRecommendation r, SparePartsScenario scenario, PartDetailResponse detail)
    {
        var assumptions = new List<string>
        {
            "This is synthetic demo data derived deterministically from real vehicle sales. It is not "
                + "real parts or service data and not Oracle Fusion.",
            $"Service level of {ExplanationFormat.Percent((decimal)(scenario.ServiceLevel * 100), 0)} over a "
                + $"{ExplanationFormat.Number((decimal)scenario.ReviewPeriodMonths)}-month review period — the "
                + "default scenario, not an ADMC-confirmed policy.",
            $"Lead time of {ExplanationFormat.Number((decimal)r.LeadTimeMonths, 2)} months comes from the parts "
                + "catalogue. No supplier delivery history exists to validate it against.",
            "The national figure rolls the whole supersession chain together, so a predecessor's "
                + "history counts toward its successor's demand.",
        };

        if (detail.SupersededByPartNumber is { } successor)
        {
            assumptions.Add(
                $"This part is superseded by {successor}. Stocking it may not be the right action even "
                + "where the demand figure supports it.");
        }

        return assumptions;
    }

    private static ConfidenceBand Band(SparePartRecommendation r) => r switch
    {
        { InsufficientData: true } => ConfidenceBand.Low,
        _ => r.Confidence switch
        {
            "High" => ConfidenceBand.High,
            "Low" => ConfidenceBand.Low,
            _ => ConfidenceBand.Medium,
        },
    };

    private static ImpactTone Tone(string risk) => risk switch
    {
        "High" => ImpactTone.Negative,
        "Medium" => ImpactTone.Warning,
        _ => ImpactTone.Neutral,
    };
}
