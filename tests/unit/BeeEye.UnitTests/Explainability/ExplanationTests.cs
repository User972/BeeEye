using System.Globalization;
using BeeEye.Analytics.Explainability;
using BeeEye.Modules.Predictions.Contracts;
using Xunit;

namespace BeeEye.UnitTests.Explainability;

/// <summary>
/// Tests for the explainability contract itself: the vocabulary maps, the section-presence rules, and
/// invariant money formatting.
/// <para>
/// The completeness tests are written <b>by reflection over the enums</b>, following the pattern
/// <c>PermissionModelTests</c> established: adding a ninth output label without a wire key must fail
/// here, at build time, rather than shipping a chip the browser silently renders as "Recommendation".
/// </para>
/// </summary>
public sealed class ExplanationTests
{
    // ---------------------------------------------------------------- vocabulary completeness

    [Fact]
    public void Every_output_label_has_a_wire_key()
    {
        foreach (var label in Enum.GetValues<OutputLabel>())
        {
            var key = ExplanationVocabulary.KeyFor(label);

            Assert.False(
                string.IsNullOrWhiteSpace(key),
                $"{nameof(OutputLabel)}.{label} has no wire key. Add one to "
                + $"{nameof(ExplanationVocabulary)} and a chip to the AiLabel component.");
        }
    }

    [Fact]
    public void The_eight_v3_labels_are_all_implemented()
    {
        // V3-CONFLICT-8: README documented seven, engine2.js's LABELS table defines eight. The code
        // is the source of truth, and Data Quality — the one the README omitted — is here.
        Assert.Equal(8, Enum.GetValues<OutputLabel>().Length);

        Assert.Equal(
            ["observed", "calculated", "forecast", "recommendation", "simulation", "demo", "low", "dq"],
            ExplanationVocabulary.AllLabelKeys);
    }

    [Fact]
    public void Label_keys_are_unique()
    {
        // Two labels sharing a key would make the chip ambiguous in exactly one direction — the one
        // nobody tests.
        var keys = ExplanationVocabulary.AllLabelKeys;

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_lineage_kind_has_a_wire_key()
    {
        foreach (var kind in Enum.GetValues<LineageKind>())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(ExplanationVocabulary.KeyFor(kind)),
                $"{nameof(LineageKind)}.{kind} has no wire key.");
        }
    }

    [Fact]
    public void The_four_v3_lineage_kinds_are_all_implemented()
    {
        Assert.Equal(["fusion", "workbook", "demo", "derived"], ExplanationVocabulary.AllLineageKeys);
    }

    [Fact]
    public void Every_impact_tone_has_a_wire_key()
    {
        foreach (var tone in Enum.GetValues<ImpactTone>())
        {
            Assert.False(
                string.IsNullOrWhiteSpace(ExplanationVocabulary.KeyFor(tone)),
                $"{nameof(ImpactTone)}.{tone} has no wire key.");
        }
    }

    [Fact]
    public void An_unmapped_label_throws_rather_than_falling_back()
    {
        // Cast from an out-of-range ordinal, which is what a renumbered or newly added enum member
        // looks like to already-compiled code. A silent fallback here would put a "Recommendation"
        // chip on something that is not a recommendation — the most misleading thing this vocabulary
        // could do, so it is refused loudly instead.
        Assert.Throws<ArgumentOutOfRangeException>(() => ExplanationVocabulary.KeyFor((OutputLabel)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExplanationVocabulary.KeyFor((LineageKind)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExplanationVocabulary.KeyFor((ImpactTone)99));
    }

    // ---------------------------------------------------------------- section presence

    private static Explanation Minimal(
        string? recommendation = null,
        IReadOnlyList<ImpactTile>? impacts = null,
        ConfidenceStatement? confidence = null,
        IReadOnlyList<Driver>? drivers = null,
        EvidenceSeries? evidence = null,
        IReadOnlyList<string>? assumptions = null,
        IReadOnlyList<LineageNode>? lineage = null,
        ModelInfo? model = null,
        Ownership? ownership = null,
        bool isDemoData = false) =>
        new(
            Title: "ES 350 ZX · STK-1",
            Module: "Inventory Intelligence",
            Label: OutputLabel.Recommendation,
            Recommendation: recommendation,
            Impacts: impacts ?? [],
            Confidence: confidence,
            Drivers: drivers ?? [],
            Evidence: evidence,
            Assumptions: assumptions ?? [],
            Lineage: lineage ?? [],
            Model: model,
            Ownership: ownership,
            IsDemoData: isDemoData);

    [Fact]
    public void An_explanation_with_nothing_optional_projects_every_optional_section_as_absent()
    {
        var dto = ExplanationDto.From(Minimal());

        Assert.Null(dto.Recommendation);
        Assert.Empty(dto.Impacts);
        Assert.Null(dto.Confidence);
        Assert.Empty(dto.Drivers);
        Assert.Null(dto.Evidence);
        Assert.Empty(dto.Assumptions);
        Assert.Empty(dto.Lineage);
        Assert.Null(dto.Model);
        Assert.Null(dto.Ownership);
    }

    [Fact]
    public void The_output_label_is_always_present()
    {
        // Required by the contract: a provider that cannot say what kind of output it produced has
        // not finished thinking about it, so there is no "no label" state to project.
        Assert.Equal("recommendation", ExplanationDto.From(Minimal()).Label);
    }

    [Fact]
    public void Each_optional_section_is_present_once_its_data_is()
    {
        var dto = ExplanationDto.From(Minimal(
            recommendation: "Transfer stock to Riyadh.",
            impacts: [new ImpactTile("Holding cost", "SAR 18.6K", ImpactTone.Negative)],
            confidence: new ConfidenceStatement(ConfidenceBand.High, 82, ["Strong demand signal."]),
            drivers: [new Driver("estimated stock cover", "7.2 months of cover")],
            evidence: new EvidenceSeries("2026-01 → 2026-06", [new EvidencePoint("Jan", 4m, 5m)], "note"),
            assumptions: ["Logistics cost not modelled."],
            lineage: [new LineageNode("Sales workbook", LineageKind.Workbook)],
            model: new ModelInfo("Overstock risk", "v1", "30 Jun 2026", "3 months", "Rule set", "rule-based"),
            ownership: new Ownership("Inventory Manager", "High risk")));

        Assert.Equal("Transfer stock to Riyadh.", dto.Recommendation);
        Assert.Single(dto.Impacts);
        Assert.NotNull(dto.Confidence);
        Assert.Single(dto.Drivers);
        Assert.NotNull(dto.Evidence);
        Assert.Single(dto.Assumptions);
        Assert.Single(dto.Lineage);
        Assert.NotNull(dto.Model);
        Assert.NotNull(dto.Ownership);
    }

    [Fact]
    public void An_absent_confidence_survives_the_whole_pipeline_without_a_band_being_invented()
    {
        // v3 defaults a missing confidence to "Medium". Asserting a band the engine never computed is
        // a correctness failure wearing the costume of a default, so it is not reproduced: an absent
        // band stays absent all the way to the wire, and the drawer omits the section.
        var dto = ExplanationDto.From(Minimal(confidence: null));

        Assert.Null(dto.Confidence);
    }

    [Fact]
    public void A_confidence_band_may_carry_no_percentage()
    {
        var dto = ExplanationDto.From(Minimal(
            confidence: new ConfidenceStatement(ConfidenceBand.Medium, null, ["Demand basis is thin."])));

        Assert.NotNull(dto.Confidence);
        Assert.Equal("Medium", dto.Confidence.Band);
        Assert.Null(dto.Confidence.Percent);
        Assert.Single(dto.Confidence.Why);
    }

    [Fact]
    public void Demo_data_is_stated_explicitly_rather_than_inferred_from_lineage()
    {
        // The two are recorded separately on purpose: inferring demo-ness from a lineage node would be
        // a rule two places must agree on forever, and the day they disagree a synthetic figure loses
        // its label in front of an executive.
        var demoLineageOnly = ExplanationDto.From(Minimal(
            lineage: [new LineageNode("Synthetic fixture", LineageKind.Demo)],
            isDemoData: false));

        var flaggedOnly = ExplanationDto.From(Minimal(isDemoData: true));

        Assert.False(demoLineageOnly.IsDemoData);
        Assert.True(flaggedOnly.IsDemoData);
    }

    [Fact]
    public void A_demo_derived_forecast_keeps_its_forecast_label_and_carries_a_demo_lineage_node()
    {
        // Demo is a property of the data, not of the kind of output produced from it, so the two are
        // never mutually exclusive.
        var explanation = Minimal(lineage: [new LineageNode("Synthetic demo fixture", LineageKind.Demo)], isDemoData: true)
            with
        { Label = OutputLabel.Forecast };

        var dto = ExplanationDto.From(explanation);

        Assert.Equal("forecast", dto.Label);
        Assert.True(dto.IsDemoData);
        Assert.Equal("demo", Assert.Single(dto.Lineage).Kind);
    }

    [Fact]
    public void Tones_project_as_keys_never_as_colours()
    {
        var dto = ExplanationDto.From(Minimal(impacts:
        [
            new ImpactTile("A", "1", ImpactTone.Neutral),
            new ImpactTile("B", "2", ImpactTone.Positive),
            new ImpactTile("C", "3", ImpactTone.Negative),
            new ImpactTile("D", "4", ImpactTone.Warning),
        ]));

        Assert.Equal(["neutral", "positive", "negative", "warning"], dto.Impacts.Select(i => i.Tone));
        Assert.DoesNotContain(dto.Impacts, i => i.Tone.Contains("var(--", StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- invariant formatting

    private static CultureInfo CommaDecimal()
    {
        // InvariantGlobalization is on, so new CultureInfo("de-DE") throws. Cloning the invariant
        // culture and changing its separators reproduces the hazard without needing ICU.
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";
        return culture;
    }

    [Fact]
    public void Money_is_formatted_invariantly_under_a_comma_decimal_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CommaDecimal();

            Assert.Equal("SAR 1.23M", ExplanationFormat.Sar(1_234_567m));
            Assert.Equal("SAR 12.3K", ExplanationFormat.Sar(12_345m));
            Assert.Equal("SAR 987", ExplanationFormat.Sar(987.4m));
            Assert.Equal("1,204", ExplanationFormat.Count(1_204m));
            Assert.Equal("7.3", ExplanationFormat.Number(7.25m));
            Assert.Equal("12.5%", ExplanationFormat.Percent(12.5m));
            Assert.Equal("+12.5%", ExplanationFormat.SignedPercent(12.5m));
            Assert.Equal("-3.0%", ExplanationFormat.SignedPercent(-3m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Dates_are_formatted_invariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CommaDecimal();

            Assert.Equal("30 Jun 2026", ExplanationFormat.Date(new DateOnly(2026, 6, 30)));
            Assert.Equal(
                "24 Jul 2026 09:15 UTC",
                ExplanationFormat.Timestamp(new DateTimeOffset(2026, 7, 24, 9, 15, 0, TimeSpan.Zero)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Day_counts_are_singular_for_one()
    {
        Assert.Equal("1 day", ExplanationFormat.Days(1));
        Assert.Equal("184 days", ExplanationFormat.Days(184));
        Assert.Equal("0 days", ExplanationFormat.Days(0));
    }

    [Fact]
    public void Sar_abbreviations_match_the_screens_they_explain()
    {
        // src/web/src/lib/format.ts's fmtSar uses the same thresholds and precisions. Two different
        // roundings of one number, side by side on the same screen, costs a reader's trust in both.
        Assert.Equal("SAR 2.50B", ExplanationFormat.Sar(2_500_000_000m));
        Assert.Equal("SAR 5.00M", ExplanationFormat.Sar(5_000_000m));
        Assert.Equal("SAR 1.0K", ExplanationFormat.Sar(1_000m));
        Assert.Equal("SAR 999", ExplanationFormat.Sar(999m));
        Assert.Equal("SAR -1.5K", ExplanationFormat.Sar(-1_500m));
    }
}
