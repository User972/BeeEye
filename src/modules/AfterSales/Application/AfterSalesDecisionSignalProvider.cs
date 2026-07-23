using BeeEye.Analytics.Decisions;

namespace BeeEye.Modules.AfterSales.Application;

/// <summary>
/// Raises <b>D-SVC-1 — Prepare workshop capacity</b> for the Executive Decision Cockpit (UC8).
/// <para>
/// Of the cohorts the UC6 engine flags as high-intensity against their installed base, surfaces the
/// one carrying the greatest workshop-hours exposure (intensity index breaks ties), so capacity and
/// common parts can be positioned before demand lands.
/// </para>
/// <para>
/// <b>Stated assumption.</b> Service history records labour hours but carries no monetary rate, so
/// workshop exposure is valued at an assumed standard rate
/// (<see cref="DefaultLabourRateSarPerHour"/>). The rate is a constructor parameter so it can be set
/// from configuration once ADMC confirms it, and it is written into the decision's evidence so a
/// reader always sees the basis of the figure. The underlying service data is synthetic demo data,
/// so decisions are additionally marked <see cref="Decision.IsDemo"/>.
/// </para>
/// </summary>
public sealed class AfterSalesDecisionSignalProvider(
    AfterSalesReadService afterSales,
    decimal labourRateSarPerHour = AfterSalesDecisionSignalProvider.DefaultLabourRateSarPerHour)
    : IDecisionSignalProvider
{
    /// <summary>
    /// Assumed workshop labour rate in SAR per hour. A placeholder for a confirmed ADMC rate — it is
    /// disclosed in the decision evidence rather than applied silently.
    /// </summary>
    public const decimal DefaultLabourRateSarPerHour = 350m;

    /// <summary>Intensity index at or above which the cohort is treated as severe.</summary>
    private const double SevereIntensityIndex = 1.5;

    private readonly decimal _labourRate = labourRateSarPerHour > 0 ? labourRateSarPerHour : DefaultLabourRateSarPerHour;

    public string Area => "After-Sales";

    public async Task<IReadOnlyList<Decision>> GetDecisionsAsync(CancellationToken cancellationToken)
    {
        var analysis = await afterSales.AnalyseAsync(cancellationToken);

        var candidates = analysis.Models
            .Where(m => m is { HighIntensity: true, VehiclesInOperation: > 0 })
            .Where(m => m.TotalLaborHours > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        var best = candidates
            .OrderByDescending(m => m.TotalLaborHours)
            .ThenByDescending(m => m.IntensityIndex ?? 0)
            .ThenBy(m => m.Model, StringComparer.Ordinal)
            .First();

        var exposure = (decimal)best.TotalLaborHours * _labourRate;
        var index = best.IntensityIndex ?? 0;
        var severe = index >= SevereIntensityIndex;

        return
        [
            new Decision(
                Id: "D-SVC-1",
                Title: $"Prepare workshop capacity — {best.Model} cohort",
                Area: Area,
                Screen: "after-sales",
                Severity: severe ? DecisionSeverity.High : DecisionSeverity.Medium,
                ImpactSar: exposure,
                Kind: DecisionKind.Risk,
                Confidence01: 0.5,
                WhyNow:
                    $"{best.Model} is servicing at {index:0.##}× the fleet average across "
                    + $"{best.VehiclesInOperation} vehicles in operation.",
                Action: "Extend appointment capacity and pre-position common parts for this cohort.",
                Evidence:
                    $"{best.TotalEvents} service events · {best.TotalLaborHours:N0} labour hours, "
                    + $"valued at an assumed SAR {_labourRate:N0}/hour workshop rate",
                OwnerRole: "After-Sales Manager",
                Urgency: 0.5,
                Controllability: 0.65,
                IsDemo: true),
        ];
    }
}
