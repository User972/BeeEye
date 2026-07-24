using System.Globalization;

namespace BeeEye.Shared.Decisions;

/// <summary>
/// The delta a human applied when accepting a recommendation with a change
/// (<c>docs/adr/0006-recommendation-decision-workflow.md</c> §2.3).
/// <para>
/// A <b>delta, never an edit</b>: <see cref="From"/> preserves what the engine recommended and
/// <see cref="To"/> records what the human chose, so the two stay readable side by side. Typed rather
/// than free JSON, because an unvalidated free-form field is how a 45% discount ends up stored as if
/// the engine had proposed it.
/// </para>
/// </summary>
/// <param name="Field">One of <see cref="ModificationRules.AllowedFields"/>.</param>
/// <param name="From">The value the engine recommended.</param>
/// <param name="To">The value the human chose.</param>
/// <param name="Rationale">Optional free text explaining the change.</param>
public sealed record Modification(string Field, decimal From, decimal To, string? Rationale = null)
{
    /// <summary>
    /// Human-readable "from → to", formatted invariantly so a comma-decimal locale on the server
    /// cannot change what a stored record says.
    /// </summary>
    public string Describe() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Field}: {From} → {To}");
}

/// <summary>Why a modification was refused. Typed, so the mapping to HTTP happens in exactly one place.</summary>
public enum ModificationRefusal
{
    None,

    /// <summary>The field is not one the workflow knows how to apply.</summary>
    UnknownField,

    /// <summary><c>From == To</c> — that is an acceptance, not a modification.</summary>
    NotAModification,

    /// <summary>A quantity was reduced below zero.</summary>
    NegativeQuantity,

    /// <summary>A discount fell outside the historically observed 0–20% band (ADR 0006 §7).</summary>
    DiscountOutOfRange,

    /// <summary>
    /// <c>From</c> does not match what the engine actually recommended — the client is modifying a
    /// value it read from a stale copy of the record.
    /// </summary>
    StaleOriginalValue,
}

/// <summary>The outcome of validating a proposed modification.</summary>
public readonly record struct ModificationValidation(bool Valid, ModificationRefusal Refusal)
{
    public static ModificationValidation Ok() => new(true, ModificationRefusal.None);

    public static ModificationValidation Refused(ModificationRefusal refusal) => new(false, refusal);

    /// <summary>A safe, non-technical explanation suitable for returning to a caller.</summary>
    public string Explain() => Refusal switch
    {
        ModificationRefusal.None => "Allowed.",
        ModificationRefusal.UnknownField =>
            "That is not a value this workflow can modify. Modifiable values are: "
            + string.Join(", ", ModificationRules.AllowedFields) + ".",
        ModificationRefusal.NotAModification =>
            "The new value is the same as the recommended one. Use the accept action instead of "
            + "accept-with-modification.",
        ModificationRefusal.NegativeQuantity =>
            "A quantity cannot be negative. Use zero to reduce the recommended quantity to nothing.",
        ModificationRefusal.DiscountOutOfRange =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"A discount must stay within the historically observed {ModificationRules.MinDiscountPct}–{ModificationRules.MaxDiscountPct}% range."),
        ModificationRefusal.StaleOriginalValue =>
            "The original value you are changing does not match the recommendation on record. Reload "
            + "the recommendation and try again.",
        _ => "That modification is not permitted.",
    };
}

/// <summary>
/// The rules governing a modification delta. Pure and deterministic — no clock, no I/O — so every
/// bound is exhaustively unit-testable and the server, not the browser, is the authority.
/// </summary>
public static class ModificationRules
{
    /// <summary>Quantity of vehicles proposed on a monthly order (UC1).</summary>
    public const string ProposedQty = "proposed_qty";

    /// <summary>Controlled discount percentage (UC5's discount action).</summary>
    public const string DiscountPct = "discount_pct";

    /// <summary>Units moved between locations (UC5's transfer action).</summary>
    public const string TransferQty = "transfer_qty";

    /// <summary>Quantity on a procurement proposal (UC4).</summary>
    public const string ProcurementQty = "procurement_qty";

    /// <summary>
    /// The allowlist. A field absent from this set is refused rather than stored: the workflow must
    /// know what a value means before it accepts a human's change to it.
    /// </summary>
    public static readonly IReadOnlySet<string> AllowedFields = new HashSet<string>(StringComparer.Ordinal)
    {
        ProposedQty, DiscountPct, TransferQty, ProcurementQty,
    };

    /// <summary>Fields that carry a count of things and therefore cannot go below zero.</summary>
    public static readonly IReadOnlySet<string> QuantityFields = new HashSet<string>(StringComparer.Ordinal)
    {
        ProposedQty, TransferQty, ProcurementQty,
    };

    /// <summary>
    /// The historically observed discount band from <c>docs/wireframes/docs/METHODOLOGY.md</c>, made
    /// binding by ADR 0006 §7. The engine never proposes outside it and neither may a human.
    /// </summary>
    public const decimal MinDiscountPct = 0m;

    /// <inheritdoc cref="MinDiscountPct"/>
    public const decimal MaxDiscountPct = 20m;

    /// <summary>
    /// Validates a proposed modification.
    /// </summary>
    /// <param name="modification">The delta the human submitted.</param>
    /// <param name="recommendedValue">
    /// The value the engine actually recommended, where it can be derived from the frozen record.
    /// When supplied it must equal <see cref="Modification.From"/>; when null the check is skipped,
    /// because refusing a modification on a value we cannot verify would block legitimate work.
    /// </param>
    public static ModificationValidation Validate(Modification modification, decimal? recommendedValue = null)
    {
        ArgumentNullException.ThrowIfNull(modification);

        if (!AllowedFields.Contains(modification.Field))
        {
            return ModificationValidation.Refused(ModificationRefusal.UnknownField);
        }

        // Checked before the bounds so "I changed nothing" is reported as such rather than as an
        // out-of-range value; it is the more useful message and it points at a different endpoint.
        if (modification.From == modification.To)
        {
            return ModificationValidation.Refused(ModificationRefusal.NotAModification);
        }

        if (modification.Field == DiscountPct
            && (modification.To < MinDiscountPct || modification.To > MaxDiscountPct))
        {
            return ModificationValidation.Refused(ModificationRefusal.DiscountOutOfRange);
        }

        // Zero is legal — reducing a recommended quantity to nothing is a real decision, and it is
        // distinguishable from "no modification supplied" because that is refused above.
        if (QuantityFields.Contains(modification.Field) && modification.To < 0m)
        {
            return ModificationValidation.Refused(ModificationRefusal.NegativeQuantity);
        }

        if (recommendedValue is { } recommended && recommended != modification.From)
        {
            return ModificationValidation.Refused(ModificationRefusal.StaleOriginalValue);
        }

        return ModificationValidation.Ok();
    }
}
