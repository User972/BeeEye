using System.Globalization;
using System.Text.RegularExpressions;
using BeeEye.Persistence.Entities;
using BeeEye.Shared.Decisions;

namespace BeeEye.Modules.DecisionsAndOutcomes.Application;

/// <summary>
/// Recovers the number the engine actually recommended from a frozen record, so a modification can be
/// checked against it (ADR 0006 §2.3 — the delta's <c>from</c> must be the engine's value, not a
/// number the client believed a while ago).
/// <para>
/// <b>Deliberately conservative.</b> The frozen <c>Recommendation</c> stores the engine's action as
/// the prose the approver was shown, not as structured parameters, so the value is derivable only when
/// the action states it unambiguously. When it cannot be derived this returns false and the stale-value
/// check is skipped — refusing a modification because we could not verify it would block legitimate
/// work to satisfy a check we are not actually able to perform.
/// </para>
/// <para>
/// The structured alternative — storing the engine's parameters as a typed column — belongs with the
/// generation path in the <c>Recommendations</c> module and is recorded in
/// <c>docs/architecture/tech-debt.md</c> rather than bolted on here.
/// </para>
/// </summary>
public static partial class RecommendedValues
{
    /// <summary>
    /// The engine's value for <paramref name="field"/>, if the frozen action states it.
    /// </summary>
    public static bool TryDerive(Recommendation recommendation, string field, out decimal value)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        value = 0m;

        var text = recommendation.Action ?? string.Empty;

        var match = field switch
        {
            ModificationRules.DiscountPct => DiscountPattern().Match(text),
            _ when ModificationRules.QuantityFields.Contains(field) => QuantityPattern().Match(text),
            _ => Match.Empty,
        };

        return match.Success
               && decimal.TryParse(
                   match.Groups[1].Value,
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    /// <summary>"… 3 unit(s) Riyadh → Jeddah" → 3. Invariant, so a comma-decimal locale changes nothing.</summary>
    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*units?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuantityPattern();

    /// <summary>"Apply a controlled discount of 15%" → 15.</summary>
    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex DiscountPattern();
}
