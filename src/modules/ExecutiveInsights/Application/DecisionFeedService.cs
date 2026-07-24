using BeeEye.Analytics.Decisions;
using BeeEye.Modules.ExecutiveInsights.Contracts;
using Microsoft.Extensions.Logging;

namespace BeeEye.Modules.ExecutiveInsights.Application;

/// <summary>
/// Builds the Executive Decision Cockpit feed (UC8) by aggregating the material exceptions each
/// bounded context publishes through <see cref="IDecisionSignalProvider"/>.
/// <para>
/// This service holds no domain rules and touches no database. Every rule lives with the context
/// that owns its data, which is what allows the cockpit to span seven use cases without referencing
/// a single other module.
/// </para>
/// </summary>
public sealed class DecisionFeedService(
    IEnumerable<IDecisionSignalProvider> providers,
    ILogger<DecisionFeedService> logger)
{
    /// <summary>Areas grouped into the narrative's "supply side" clause.</summary>
    private static readonly string[] SupplyAreas = ["Inventory", "Order Planning", "Procurement"];

    /// <summary>Areas grouped into the narrative's "after-sales" clause.</summary>
    private static readonly string[] AfterSalesAreas = ["After-Sales", "Parts"];

    public async Task<DecisionFeedResponse> BuildAsync(DateTimeOffset generatedAtUtc, CancellationToken cancellationToken)
    {
        var collected = new List<Decision>();
        var gaps = new List<DecisionFeedGapDto>();

        // Sequential by design. Providers resolve read services that share this request's scoped
        // DbContext, and EF Core's DbContext is not thread-safe — running them concurrently would
        // race on the same connection. Each provider is a handful of queries; correctness wins.
        foreach (var provider in providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var decisions = await provider.GetDecisionsAsync(cancellationToken);
                collected.AddRange(decisions);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller went away — propagate rather than reporting a false data gap.
                throw;
            }
            catch (Exception ex)
            {
                // One failing context must not blank the whole cockpit, but it must not be hidden
                // either: the gap is reported so the user knows the feed is incomplete. The
                // exception detail goes to the log only — never to the browser.
                logger.LogError(
                    ex,
                    "Decision signal provider for area {Area} failed; the cockpit feed will report a gap.",
                    provider.Area);

                gaps.Add(new DecisionFeedGapDto(
                    provider.Area,
                    "This area could not be assessed. Its decisions are missing from the list below."));
            }
        }

        var ranked = DecisionFeed.Rank(collected);
        var summary = DecisionFeed.Summarise(ranked);

        return new DecisionFeedResponse(
            [.. ranked.Select(DecisionDto.From)],
            DecisionFeedSummaryDto.From(summary),
            Narrate(ranked),
            gaps,
            generatedAtUtc);
    }

    /// <summary>
    /// A plain-language summary of where attention is needed, grouped the way the v3 cockpit groups
    /// it. Deterministic — it restates counts and never editorialises or infers causation.
    /// </summary>
    internal static string Narrate(IReadOnlyList<Decision> decisions)
    {
        if (decisions.Count == 0)
        {
            return "No material exceptions need a decision this period.";
        }

        var supply = decisions.Count(d => SupplyAreas.Contains(d.Area, StringComparer.Ordinal));
        var afterSales = decisions.Count(d => AfterSalesAreas.Contains(d.Area, StringComparer.Ordinal));
        var other = decisions.Count - supply - afterSales;

        var parts = new List<string>();
        if (supply > 0)
        {
            parts.Add($"{supply} relate{Plural(supply)} to inventory, ordering and procurement exposure");
        }

        if (afterSales > 0)
        {
            parts.Add($"{afterSales} to after-sales and parts availability");
        }

        if (other > 0)
        {
            parts.Add($"{other} to sales and configuration mix");
        }

        var noun = decisions.Count == 1 ? "decision needs" : "decisions need";
        return $"{decisions.Count} {noun} attention: {JoinWithAnd(parts)}.";
    }

    private static string Plural(int n) => n == 1 ? "s" : string.Empty;

    private static string JoinWithAnd(IReadOnlyList<string> parts) => parts.Count switch
    {
        0 => "no areas are affected",
        1 => parts[0],
        _ => string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[^1],
    };
}
