using BeeEye.Analytics.Explainability;
using BeeEye.Analytics.Forecasting;
using BeeEye.Modules.Forecasting.Contracts;

namespace BeeEye.Modules.Forecasting.Application;

/// <summary>
/// Explains one forecast scope — the model and location the UC2 screen is currently filtered to
/// (V3-DS-006).
/// <para>
/// This is the one provider whose evidence series is a genuine <b>time series</b>: the back-test puts
/// the chosen method's forecast beside the actual it was scored against, month by month. That is the
/// honest answer to "should I believe this number?", and it is already on screen — the drawer reuses
/// it rather than inventing a second view of the same data.
/// </para>
/// </summary>
public sealed class ForecastExplainabilityProvider(ForecastingReadService forecasting) : IExplainabilityProvider
{
    /// <summary>
    /// The subject is a forecast scope, referenced as <c>"{model}|{location}"</c>. Either part may be
    /// empty, meaning "all" — <c>"|"</c> is the total business, which is what the screen shows before
    /// any filter is applied.
    /// </summary>
    public const string ForecastScopeKind = "forecast-scope";

    /// <summary>The screen's own defaults, so the drawer explains the figure actually displayed.</summary>
    private const int DefaultHorizon = 6;
    private const int DefaultHoldout = 6;
    private const int DefaultConfidenceInterval = 80;

    public IReadOnlySet<string> SubjectKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { ForecastScopeKind };

    public async Task<Explanation?> ExplainAsync(
        string subjectKind, string subjectRef, CancellationToken cancellationToken)
    {
        if (!SubjectKinds.Contains(subjectKind))
        {
            return null;
        }

        var (model, location) = Scope(subjectRef);
        var filter = SalesFilter.From(
            brand: null,
            model: model is null ? null : [model],
            variant: null,
            type: null,
            location: location is null ? null : [location],
            colour: null,
            interior: null,
            dateFrom: null,
            dateTo: null);

        var options = new ForecastOptions(
            Horizon: DefaultHorizon, Holdout: DefaultHoldout, Algo: null, Ci: DefaultConfidenceInterval);

        var result = await forecasting.ForecastAsync(filter, options, cancellationToken);
        if (!result.IsSuccess)
        {
            // Insufficient history or a filter matching nothing are both "there is no forecast for
            // this scope" — a missing subject, not a failure. Throwing would report a data gap and
            // send someone looking for an outage that is not there.
            return null;
        }

        var f = result.Value!.Forecast;

        return new Explanation(
            Title: ScopeLabel(model, location),
            Module: "Forecast Accuracy",
            Label: OutputLabel.Forecast,
            Recommendation: null,
            Impacts:
            [
                new($"Next {f.Horizon} months",
                    $"{ExplanationFormat.Count((decimal)f.FutureSum)} units", ImpactTone.Neutral),
                new("Back-test error",
                    f.Accuracy.Wmape is { } wmape
                        ? $"wMAPE {ExplanationFormat.Percent((decimal)wmape)}"
                        : "not measurable",
                    ErrorTone(f.Accuracy.Wmape)),
                new("Forecast bias",
                    f.Accuracy.Bias is { } bias
                        ? ExplanationFormat.SignedPercent((decimal)bias)
                        : "not measurable",
                    BiasTone(f.Accuracy.Bias)),
                new("History used",
                    $"{ExplanationFormat.Count(f.TotalN)} months", ImpactTone.Neutral),
            ],
            Confidence: new ConfidenceStatement(
                Band(f.Accuracy.Wmape, f.TotalN),

                // The one provider with a real percentage to report: the confidence interval the
                // projection bands were drawn at. It is the interval, not a probability of being
                // right — the section's wording says so.
                Percent: DefaultConfidenceInterval,
                Why: Why(f)),
            Drivers: Drivers(f),
            Evidence: Evidence(f),
            Assumptions:
            [
                $"Projection bands are drawn at {DefaultConfidenceInterval}% confidence from the "
                    + "back-test residuals; they are not a guarantee of range.",
                $"Method selection is automatic — the lowest-wMAPE of {f.Methods.Count} baselines over a "
                    + $"{f.Holdout}-month hold-out. No method was chosen by hand.",
                "Demand is projected from history alone. Price changes, campaigns, supply constraints "
                    + "and competitor moves are not inputs.",
                "Ramadan seasonality is modelled where the history supports it.",
            ],
            Lineage:
            [
                new LineageNode("Oracle Fusion — sales (system of record)", LineageKind.Fusion),
                new LineageNode("Sales workbook (sales.json)", LineageKind.Workbook),
                new LineageNode($"Baseline forecaster — {f.ChosenName} (UC2)", LineageKind.Derived),
            ],
            Model: new ModelInfo(
                Name: f.ChosenName,
                Version: $"UC2 · auto-selected from {f.Methods.Count} baselines",
                Recalculated: $"On request — history to {f.LastMonth}",
                Horizon: $"{f.Horizon} months",
                Validation: $"{f.Holdout}-month hold-out back-test on {f.TrainN} training months",
                Error: f.Accuracy.Wmape is { } measuredError
                    ? $"wMAPE {ExplanationFormat.Percent((decimal)measuredError)} · MAE "
                      + $"{ExplanationFormat.Number((decimal)f.Accuracy.Mae)}"
                    : "not measurable — history too short to back-test"),

            // A forecast is not a decision, so there is nobody to assign and no workflow footer.
            Ownership: null,
            IsDemoData: false);
    }

    /// <summary>
    /// Splits <c>"{model}|{location}"</c>. An empty part means "no filter on this dimension", which is
    /// exactly what the screen's "All" option sends.
    /// </summary>
    private static (string? Model, string? Location) Scope(string subjectRef)
    {
        var parts = subjectRef.Split('|');
        var model = parts.Length > 0 ? parts[0].Trim() : string.Empty;
        var location = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        return (
            model.Length == 0 ? null : model,
            location.Length == 0 ? null : location);
    }

    private static string ScopeLabel(string? model, string? location) => (model, location) switch
    {
        (null, null) => "Total business",
        ({ } m, null) => m,
        (null, { } l) => l,
        var (m, l) => $"{l} · {m}",
    };

    private static IReadOnlyList<Driver> Drivers(ForecastResult f)
    {
        // The engine already writes its own explanation points; they are the drivers, verbatim.
        // Re-wording them here would be this slice authoring narrative, which it must not do.
        var drivers = f.Explanation.Points
            .Select(point => new Driver(point, null))
            .ToList();

        drivers.Insert(0, new Driver(
            "Recent demand against the prior year",
            $"{ExplanationFormat.Number((decimal)f.Explanation.Recent3)} units in the last 3 months vs "
            + $"{ExplanationFormat.Number((decimal)f.Explanation.Prior12)} monthly average over the prior 12 "
            + $"({ExplanationFormat.SignedPercent((decimal)f.Explanation.ChangePct)})"));

        drivers.Add(new Driver(
            "Method comparison",
            string.Join(" · ", f.Methods
                .OrderBy(m => m.Wmape ?? double.MaxValue)
                .Take(3)
                .Select(m => $"{m.Name} {(m.Wmape is { } w ? ExplanationFormat.Percent((decimal)w) : "n/a")}"))));

        return drivers;
    }

    /// <summary>
    /// The back-test, month by month: what the chosen method predicted beside what actually happened.
    /// Empty when the history was too short to hold anything out, in which case the section is omitted
    /// rather than drawn from one point.
    /// </summary>
    private static EvidenceSeries? Evidence(ForecastResult f)
    {
        if (f.Backtest.Count == 0)
        {
            return null;
        }

        return new EvidenceSeries(
            Period: $"{f.Backtest[0].Month} → {f.Backtest[^1].Month}",
            Points:
            [
                .. f.Backtest.Select(b => new EvidencePoint(
                    b.Label,
                    decimal.Round((decimal)b.Actual, 1),
                    decimal.Round((decimal)b.Forecast, 1))),
            ],
            Note:
                $"The {f.Holdout}-month hold-out the method was selected on. These months were withheld "
                + "from training, so the gap between the two lines is the error actually measured — not "
                + "a fit to data the model had already seen.",
            ValueLabel: "Actual",
            ComparisonLabel: f.ChosenName);
    }

    private static IReadOnlyList<string> Why(ForecastResult f)
    {
        var why = new List<string>
        {
            f.Accuracy.Wmape is { } wmape
                ? $"{f.ChosenName} was the most accurate of {f.Methods.Count} baselines on the hold-out, at "
                  + $"{ExplanationFormat.Percent((decimal)wmape)} wMAPE."
                : $"{f.ChosenName} was selected, but the history is too short to measure its error.",
            $"{ExplanationFormat.Count(f.TotalN)} months of history, of which "
              + $"{ExplanationFormat.Count(f.TrainN)} were used for training.",
        };

        if (f.Accuracy.Bias is { } bias && Math.Abs(bias) > 5)
        {
            why.Add(bias > 0
                ? $"The method over-forecast by {ExplanationFormat.Percent((decimal)bias)} on the hold-out, "
                  + "so treat the projection as an upper-leaning estimate."
                : $"The method under-forecast by {ExplanationFormat.Percent((decimal)Math.Abs(bias))} on the "
                  + "hold-out, so treat the projection as a lower-leaning estimate.");
        }

        if (f.TotalN < 12)
        {
            why.Add("Fewer than twelve months of history: seasonality cannot be separated from trend.");
        }

        return why;
    }

    /// <summary>
    /// The band is the measured back-test error, not a judgement. No error measurable means the
    /// history was too short — which is a <i>low</i>-confidence fact, not a missing one.
    /// </summary>
    private static ConfidenceBand Band(double? wmape, int months) => wmape switch
    {
        null => ConfidenceBand.Low,
        < 15 when months >= 12 => ConfidenceBand.High,
        < 30 => ConfidenceBand.Medium,
        _ => ConfidenceBand.Low,
    };

    private static ImpactTone ErrorTone(double? wmape) => wmape switch
    {
        null => ImpactTone.Warning,
        < 15 => ImpactTone.Positive,
        < 30 => ImpactTone.Neutral,
        _ => ImpactTone.Negative,
    };

    private static ImpactTone BiasTone(double? bias) => bias switch
    {
        null => ImpactTone.Warning,
        _ when Math.Abs(bias.Value) <= 5 => ImpactTone.Positive,
        _ => ImpactTone.Warning,
    };
}
