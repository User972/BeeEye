using System.Globalization;
using System.Text.Json;
using BeeEye.Shared.Decisions;
using Xunit;

namespace BeeEye.UnitTests.Decisions;

/// <summary>
/// Tests for the modification delta (<c>docs/adr/0006-recommendation-decision-workflow.md</c> §2.3
/// and §7).
/// <para>
/// These bounds are the reason the delta is typed rather than free JSON. An unvalidated field is how a
/// 45% discount ends up stored beside a recommendation that never proposed one — and, months later,
/// indistinguishable from something the engine advised.
/// </para>
/// </summary>
public sealed class ModificationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------------------------------------------------------------- the allowlist

    [Theory]
    [InlineData(ModificationRules.ProposedQty)]
    [InlineData(ModificationRules.DiscountPct)]
    [InlineData(ModificationRules.TransferQty)]
    [InlineData(ModificationRules.ProcurementQty)]
    public void An_allowlisted_field_is_accepted(string field)
    {
        var value = field == ModificationRules.DiscountPct ? 10m : 30m;

        Assert.True(ModificationRules.Validate(new Modification(field, 40m, value)).Valid);
    }

    [Theory]
    [InlineData("selling_price")]
    [InlineData("")]
    [InlineData("PROPOSED_QTY")]
    [InlineData("proposed_qty ")]
    public void An_unknown_field_is_refused(string field)
    {
        var verdict = ModificationRules.Validate(new Modification(field, 40m, 30m));

        Assert.False(verdict.Valid);
        Assert.Equal(ModificationRefusal.UnknownField, verdict.Refusal);
    }

    [Fact]
    public void The_allowlist_names_exactly_the_four_modifiable_values()
    {
        Assert.Equal(
            new[] { "discount_pct", "procurement_qty", "proposed_qty", "transfer_qty" },
            ModificationRules.AllowedFields.Order(StringComparer.Ordinal));
    }

    // ---------------------------------------------------------------- the discount band

    [Theory]
    [InlineData("-0.1", false)]
    [InlineData("0", true)]
    [InlineData("0.01", true)]
    [InlineData("10", true)]
    [InlineData("19.99", true)]
    [InlineData("20", true)]
    [InlineData("20.01", false)]
    [InlineData("45", false)]
    public void A_discount_must_stay_inside_the_historically_observed_band(string to, bool allowed)
    {
        // Parsed invariantly from a string so the decimal literal is exact — the whole point of the
        // 20.01 case is that it is *just* outside, which a double would not reliably represent.
        var value = decimal.Parse(to, CultureInfo.InvariantCulture);
        var verdict = ModificationRules.Validate(new Modification(ModificationRules.DiscountPct, 15m, value));

        Assert.Equal(allowed, verdict.Valid);

        if (!allowed)
        {
            Assert.Equal(ModificationRefusal.DiscountOutOfRange, verdict.Refusal);
        }
    }

    [Fact]
    public void The_discount_band_is_the_zero_to_twenty_percent_range_from_the_methodology()
    {
        Assert.Equal(0m, ModificationRules.MinDiscountPct);
        Assert.Equal(20m, ModificationRules.MaxDiscountPct);
    }

    [Fact]
    public void A_quantity_is_not_subject_to_the_discount_band()
    {
        // 40 units is far outside 0–20, and entirely legitimate.
        Assert.True(ModificationRules.Validate(new Modification(ModificationRules.ProposedQty, 30m, 40m)).Valid);
    }

    // ---------------------------------------------------------------- quantities

    [Fact]
    public void A_quantity_may_be_reduced_to_zero()
    {
        // "Order nothing" is a real decision, and it must be distinguishable from "no modification
        // supplied" — which is refused separately, below.
        var verdict = ModificationRules.Validate(new Modification(ModificationRules.ProposedQty, 40m, 0m));

        Assert.True(verdict.Valid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public void A_negative_quantity_is_refused(decimal to)
    {
        var verdict = ModificationRules.Validate(new Modification(ModificationRules.TransferQty, 3m, to));

        Assert.False(verdict.Valid);
        Assert.Equal(ModificationRefusal.NegativeQuantity, verdict.Refusal);
    }

    // ---------------------------------------------------------------- not a modification

    [Theory]
    [InlineData(ModificationRules.ProposedQty, 40)]
    [InlineData(ModificationRules.DiscountPct, 15)]
    [InlineData(ModificationRules.TransferQty, 0)]
    public void An_unchanged_value_is_an_acceptance_not_a_modification(string field, decimal value)
    {
        var verdict = ModificationRules.Validate(new Modification(field, value, value));

        Assert.False(verdict.Valid);
        Assert.Equal(ModificationRefusal.NotAModification, verdict.Refusal);
        Assert.Contains("accept", verdict.Explain(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Equality_ignores_scale_so_thirty_and_thirty_point_zero_are_the_same_value()
    {
        var verdict = ModificationRules.Validate(new Modification(ModificationRules.ProposedQty, 30m, 30.00m));

        Assert.Equal(ModificationRefusal.NotAModification, verdict.Refusal);
    }

    // ---------------------------------------------------------------- the stale original

    [Fact]
    public void A_from_value_matching_the_engine_is_accepted()
    {
        var verdict = ModificationRules.Validate(
            new Modification(ModificationRules.ProposedQty, 40m, 30m), recommendedValue: 40m);

        Assert.True(verdict.Valid);
    }

    [Fact]
    public void A_from_value_the_engine_never_recommended_is_refused_as_stale()
    {
        // The client is modifying a number it read from a copy of the record that has since moved on.
        var verdict = ModificationRules.Validate(
            new Modification(ModificationRules.ProposedQty, 55m, 30m), recommendedValue: 40m);

        Assert.False(verdict.Valid);
        Assert.Equal(ModificationRefusal.StaleOriginalValue, verdict.Refusal);
    }

    [Fact]
    public void The_stale_check_is_skipped_when_the_engine_value_is_not_derivable()
    {
        // Refusing a change because we could not verify it would block legitimate work to satisfy a
        // check we cannot actually perform.
        var verdict = ModificationRules.Validate(
            new Modification(ModificationRules.ProposedQty, 55m, 30m), recommendedValue: null);

        Assert.True(verdict.Valid);
    }

    // ---------------------------------------------------------------- precision and culture

    [Fact]
    public void Decimal_precision_survives_a_serialise_deserialise_round_trip()
    {
        var original = new Modification(ModificationRules.DiscountPct, 12.5m, 7.25m, "Trimmed on review");

        var restored = JsonSerializer.Deserialize<Modification>(
            JsonSerializer.Serialize(original, Json), Json);

        Assert.NotNull(restored);
        Assert.Equal(12.5m, restored.From);
        Assert.Equal(7.25m, restored.To);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void A_value_beyond_double_precision_is_preserved_exactly()
    {
        // The reason money and quantities are decimal: this value is not representable as a double.
        var original = new Modification(ModificationRules.ProcurementQty, 100.05m, 100.03m);

        var restored = JsonSerializer.Deserialize<Modification>(
            JsonSerializer.Serialize(original, Json), Json)!;

        Assert.Equal(100.03m, restored.To);
        Assert.Equal(0.02m, restored.From - restored.To);
    }

    /// <summary>
    /// A comma-decimal culture, built by hand rather than by name.
    /// <para>
    /// The platform runs with <c>InvariantGlobalization</c> on, so <c>new CultureInfo("de-DE")</c>
    /// throws here — which would make a culture test pass for the wrong reason on a machine that does
    /// have ICU. Cloning the invariant culture and moving the separator reproduces the hazard exactly,
    /// under either setting.
    /// </para>
    /// </summary>
    private static CultureInfo CommaDecimal()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";
        return culture;
    }

    [Fact]
    public void The_description_is_formatted_invariantly_under_a_comma_decimal_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // Under this culture 7.25 formats as "7,25". A stored audit record must not change meaning
            // because of where the server happened to be running.
            CultureInfo.CurrentCulture = CommaDecimal();

            var description = new Modification(ModificationRules.DiscountPct, 12.5m, 7.25m).Describe();

            Assert.Contains("12.5", description, StringComparison.Ordinal);
            Assert.Contains("7.25", description, StringComparison.Ordinal);
            Assert.DoesNotContain("12,5", description, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void The_out_of_range_explanation_states_the_band_invariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CommaDecimal();

            var explanation = ModificationValidation
                .Refused(ModificationRefusal.DiscountOutOfRange)
                .Explain();

            Assert.Contains("0–20%", explanation, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // ---------------------------------------------------------------- explanations

    [Theory]
    [InlineData(ModificationRefusal.None)]
    [InlineData(ModificationRefusal.UnknownField)]
    [InlineData(ModificationRefusal.NotAModification)]
    [InlineData(ModificationRefusal.NegativeQuantity)]
    [InlineData(ModificationRefusal.DiscountOutOfRange)]
    [InlineData(ModificationRefusal.StaleOriginalValue)]
    public void Every_refusal_explains_itself_without_leaking_internals(ModificationRefusal refusal)
    {
        var explanation = ModificationValidation.Refused(refusal).Explain();

        Assert.False(string.IsNullOrWhiteSpace(explanation));
        Assert.DoesNotContain(nameof(ModificationRefusal), explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => ModificationRules.Validate(null!));
    }
}
