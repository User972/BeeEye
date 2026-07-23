using BeeEye.Analytics.AfterSales;
using BeeEye.Analytics.Explainability;

namespace BeeEye.Modules.AfterSales.Application;

/// <summary>
/// Explains one model's service intensity and its sales↔service association (UC6, V3-DS-006).
/// <para>
/// <b>Every explanation this provider returns is demo data</b> and says so: UC6 runs on a synthetic
/// after-sales dataset derived deterministically from the real vehicle sales. The
/// <see cref="Explanation.IsDemoData"/> flag, a <see cref="LineageKind.Demo"/> node and the first
/// assumption all state it, because a workshop-capacity figure that looks measured and is not is the
/// single most dangerous thing this screen could produce.
/// </para>
/// <para>
/// It also states the assumption S2 recorded but buried: workshop exposure is valued at an assumed
/// <b>SAR 350/hour</b>, because service history records labour hours and no monetary rate. In the
/// cockpit that lived in an evidence string; here it is an assumption, which is what it is.
/// </para>
/// </summary>
public sealed class AfterSalesExplainabilityProvider(AfterSalesReadService afterSales)
    : IExplainabilityProvider
{
    /// <summary>The subject is a vehicle model, referenced by name.</summary>
    public const string ServiceModelKind = "service-model";

    /// <summary>
    /// The same assumed labour rate <c>AfterSalesDecisionSignalProvider</c> values workshop exposure
    /// at. Duplicated as a constant rather than shared through a service, because the two providers
    /// must stay independently testable — but the value and its disclosure are identical, and a test
    /// asserts the drawer discloses it.
    /// </summary>
    public const decimal AssumedLabourRateSarPerHour =
        AfterSalesDecisionSignalProvider.DefaultLabourRateSarPerHour;

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { ServiceModelKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        var analysis = await afterSales.AnalyseAsync(cancellationToken);

        var model = analysis.Models.FirstOrDefault(m =>
            string.Equals(m.Model, subjectRef, StringComparison.OrdinalIgnoreCase));
        if (model is null)
        {
            return null;
        }

        var settings = ServiceIntensitySettings.Default;
        var exposure = (decimal)model.TotalLaborHours * AssumedLabourRateSarPerHour;

        return new Explanation(
            Title: $"{model.Model} — service intensity",
            Module: "Sales ↔ Service Correlation",

            // Demo is a property of the *data*; the kind of output is still a calculation. Both are
            // recorded, and the drawer shows the demo flag beside the label rather than instead of it.
            Label: model.Coverage.ReliabilityTier == "Low"
                ? OutputLabel.LowConfidence
                : OutputLabel.Calculated,

            // UC6 correlates; it does not advise. The cockpit turns a high-intensity cohort into a
            // capacity decision — that recommendation belongs there, and is explained there.
            Recommendation: null,
            Impacts:
            [
                new("Service events", ExplanationFormat.Count(model.TotalEvents), ImpactTone.Neutral),
                new("Intensity index",
                    model.IntensityIndex is { } index
                        ? $"{ExplanationFormat.Number((decimal)index, 2)}× fleet mean"
                        : "not computable",
                    model.HighIntensity ? ImpactTone.Warning : ImpactTone.Neutral),
                new("Workshop hours",
                    $"{ExplanationFormat.Number((decimal)model.TotalLaborHours)} h", ImpactTone.Neutral),
                new("Workshop exposure", ExplanationFormat.Sar(exposure), ImpactTone.Warning),
            ],
            Confidence: new ConfidenceStatement(
                Band(model.Coverage.ReliabilityTier),
                Percent: model.Coverage.CoverageRate is { } rate
                    ? (int)Math.Round(rate * 100, MidpointRounding.AwayFromZero)
                    : null,
                Why: Why(model, settings)),
            Drivers: Drivers(model, settings),

            // The UC6 screen charts distributions rather than a single series the drawer can reuse,
            // and the distributions are already carried as drivers. Section omitted.
            Evidence: null,
            Assumptions:
            [
                "This is synthetic demo data derived deterministically from real vehicle sales. It is "
                    + "not real after-sales data and not Oracle Fusion.",
                $"Workshop exposure values labour hours at an assumed SAR "
                    + $"{ExplanationFormat.Count(AssumedLabourRateSarPerHour)}/hour. Service history records "
                    + "hours but carries no monetary rate, so this figure is only as good as that rate.",
                $"A model is flagged high-intensity at "
                    + $"{ExplanationFormat.Number((decimal)settings.HighIntensityThreshold, 2)}× the fleet mean "
                    + "events per vehicle — a configurable threshold, not a validated constant.",
                "The sales↔service relationship is an association at a lag, never causation. A "
                    + "correlation coefficient does not establish that selling more vehicles causes more "
                    + "service work.",
                "Vehicles in operation are counted from recorded sales, so vehicles sold before the "
                    + "history begins are absent from the denominator.",
            ],
            Lineage:
            [
                new LineageNode("Synthetic after-sales fixture (demo)", LineageKind.Demo),
                new LineageNode("Sales workbook (sales.json)", LineageKind.Workbook),
                new LineageNode("Service-intensity analysis (UC6)", LineageKind.Derived),
            ],
            Model: new ModelInfo(
                Name: "Service-intensity index & lagged correlation",
                Version: "UC6 · normalised rate model",
                Recalculated: "On request — computed live from the synthetic dataset",
                Horizon: $"{ExplanationFormat.Count(model.Coverage.MonthsOfHistory)} months of service history",
                Validation: $"Coverage-tiered reliability — {model.Coverage.ReliabilityTier}",
                Error: "rule-based"),

            // UC6 is analysis, not a decision queue. The cockpit's D-SVC-1 owns the decision.
            Ownership: null,
            IsDemoData: true);
    }

    private static IReadOnlyList<Driver> Drivers(ModelServiceIntensity m, ServiceIntensitySettings settings)
    {
        var drivers = new List<Driver>
        {
            new("Events per vehicle in operation",
                m.EventsPerVehicle is { } rate
                    ? $"{ExplanationFormat.Number((decimal)rate, 2)} across "
                      + $"{ExplanationFormat.Count(m.VehiclesInOperation)} vehicles"
                    : "no fleet recorded, so no rate can be computed"),
            new("Labour hours per vehicle",
                m.LaborHoursPerVehicle is { } hours
                    ? $"{ExplanationFormat.Number((decimal)hours, 2)} h"
                    : "not computable"),
        };

        var topType = m.ByServiceType.OrderByDescending(t => t.Events).FirstOrDefault();
        if (topType is not null && topType.Events > 0)
        {
            drivers.Add(new Driver(
                $"Dominant service type: {topType.ServiceType}",
                $"{ExplanationFormat.Count(topType.Events)} events · "
                + $"{ExplanationFormat.Percent((decimal)(topType.Share * 100))} of the total"));
        }

        var topMileage = m.ByMileageBand.OrderByDescending(b => b.Events).FirstOrDefault();
        if (topMileage is not null && topMileage.Events > 0)
        {
            drivers.Add(new Driver(
                $"Heaviest mileage band: {topMileage.Band}",
                $"{ExplanationFormat.Count(topMileage.Events)} events"));
        }

        drivers.Add(new Driver(
            "Sales↔service association",
            m.Correlation.Best is { } best
                ? $"r = {ExplanationFormat.Number((decimal)best, 2)} at a "
                  + $"{ExplanationFormat.Count(m.Correlation.BestLagMonths)}-month lag — "
                  + m.Correlation.Interpretation
                : m.Correlation.Interpretation));

        drivers.Add(new Driver(
            $"High-intensity threshold: {ExplanationFormat.Number((decimal)settings.HighIntensityThreshold, 2)}×",
            m.HighIntensity ? "this model is above it" : "this model is below it"));

        return drivers;
    }

    private static IReadOnlyList<string> Why(ModelServiceIntensity m, ServiceIntensitySettings settings)
    {
        var why = new List<string>
        {
            m.Coverage.CoverageRate is { } rate
                ? $"{ExplanationFormat.Percent((decimal)(rate * 100))} of the {ExplanationFormat.Count(m.Coverage.VehiclesInOperation)} "
                  + "vehicles in operation have at least one recorded service event."
                : "No fleet is recorded for this model, so coverage cannot be computed.",
            $"{ExplanationFormat.Count(m.Coverage.MonthsOfHistory)} months of service history.",
        };

        if (m.Coverage.MonthsOfHistory < settings.MediumHistoryMonths)
        {
            why.Add(
                $"Fewer than {settings.MediumHistoryMonths} months of history — seasonal workshop "
                + "patterns cannot be separated from the trend.");
        }

        why.Add("The underlying dataset is synthetic demo data, which caps how much any of this is worth.");

        return why;
    }

    private static ConfidenceBand Band(string reliabilityTier) => reliabilityTier switch
    {
        "High" => ConfidenceBand.High,
        "Low" => ConfidenceBand.Low,
        _ => ConfidenceBand.Medium,
    };
}
