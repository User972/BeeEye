using BeeEye.Analytics.DataQuality;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Tests for <see cref="DataQualityCalculator"/> — a faithful port of <c>engine.js</c>
/// <c>dataQuality()</c>. Each issue type is exercised in isolation, the exact score penalty formula and
/// its 0–100 clamp are hand-verified, and the V3-GOV-008 band thresholds are pinned at their ≥ boundaries.
/// </summary>
public sealed class DataQualityCalculatorTests
{
    // A clean unit's purchase − manufacture is exactly its lead time (30 days), so it reconciles.
    private static readonly DateOnly Dom = new(2024, 1, 2);
    private static readonly DateOnly Dop = new(2024, 2, 1); // 30 days after Dom

    private static DataQualitySalesRow Sale(
        string location = "Riyadh", int units = 1, decimal price = 100m, decimal? revenue = null, int discount = 0) =>
        new(location, units, price, revenue ?? units * price * (1m - discount / 100m), discount);

    private static DataQualityInventoryRow Unit(
        string stockId = "S1", string chassis = "C1", string location = "Riyadh",
        decimal price = 100m, decimal holding = 1m, DateOnly? dop = null, DateOnly? dom = null, int lead = 30) =>
        new(stockId, chassis, location, price, holding, dop ?? Dop, dom ?? Dom, lead);

    private static DataQualityIssue Issue(DataQualityReport r, string id) => r.Issues.Single(i => i.Id == id);

    // ---------------------------------------------------------------- clean & empty

    [Fact]
    public void A_clean_dataset_scores_100_and_is_healthy()
    {
        var report = DataQualityCalculator.Compute(
            [Sale(), Sale(units: 3, price: 200m)],
            [Unit(), Unit("S2", "C2")]);

        Assert.Equal(100, report.Score);
        Assert.Equal("Healthy", report.ScoreBand);
        Assert.All(report.Issues, i => Assert.Equal("ok", i.Severity));
        Assert.All(report.Issues, i => Assert.Equal(0, i.Count));
        Assert.Equal("No negative units, prices, revenue or holding costs.", Issue(report, "neg").Note);
        Assert.Empty(report.Mismatch);
    }

    [Fact]
    public void An_empty_dataset_scores_100_and_lists_every_issue_at_zero()
    {
        var report = DataQualityCalculator.Compute([], []);

        Assert.Equal(100, report.Score);
        Assert.Equal("Healthy", report.ScoreBand);
        Assert.Equal(0, report.SalesRows);
        Assert.Equal(0, report.InvRows);
        Assert.Empty(report.Mismatch);
        Assert.Equal(7, report.Issues.Count);
        Assert.All(report.Issues, i => Assert.Equal(0, i.Count));
    }

    // ---------------------------------------------------------------- each issue type

    [Fact]
    public void Duplicate_stock_ids_are_counted_and_penalised()
    {
        var report = DataQualityCalculator.Compute([Sale()], [Unit("S1", "C1"), Unit("S1", "C2")]);

        var issue = Issue(report, "dup_stock");
        Assert.Equal(1, issue.Count);
        Assert.Equal("high", issue.Severity);
        Assert.Equal(95, report.Score); // 100 − 1×5
    }

    [Fact]
    public void Duplicate_chassis_numbers_are_counted_and_penalised()
    {
        var report = DataQualityCalculator.Compute([Sale()], [Unit("S1", "C1"), Unit("S2", "C1")]);

        var issue = Issue(report, "dup_chassis");
        Assert.Equal(1, issue.Count);
        Assert.Equal("high", issue.Severity);
        Assert.Equal(95, report.Score);
    }

    [Fact]
    public void Negative_amounts_across_both_datasets_are_summed()
    {
        var report = DataQualityCalculator.Compute(
            [Sale(units: -1)],
            [Unit(price: -5m)]);

        var issue = Issue(report, "neg");
        Assert.Equal(2, issue.Count); // one negative sale + one negative inventory row
        Assert.Equal("high", issue.Severity);
        Assert.Equal("2 negative units, prices, revenue or holding costs.", issue.Note);
        Assert.Equal(90, report.Score); // 100 − 2×5
    }

    [Fact]
    public void Revenue_reconciliation_flags_rows_more_than_one_percent_out()
    {
        // Expected = 2 × 100 = 200; actual 500 is well over the max(1, 1%) tolerance.
        var report = DataQualityCalculator.Compute([Sale(units: 2, price: 100m, revenue: 500m)], [Unit()]);

        var issue = Issue(report, "rev");
        Assert.Equal(1, issue.Count);
        Assert.Equal("medium", issue.Severity);
    }

    [Fact]
    public void Revenue_within_one_percent_is_not_flagged()
    {
        // Expected 200, actual 201 — within max(1, 1% = 2), so it reconciles.
        var report = DataQualityCalculator.Compute([Sale(units: 2, price: 100m, revenue: 201m)], [Unit()]);

        Assert.Equal(0, Issue(report, "rev").Count);
    }

    [Fact]
    public void A_single_revenue_mismatch_rounds_half_up_to_leave_the_score_at_100()
    {
        // 100 − 1×0.5 = 99.5, and engine.js Math.round is round-half-up → 100.
        var report = DataQualityCalculator.Compute([Sale(units: 2, price: 100m, revenue: 500m)], [Unit()]);

        Assert.Equal(1, Issue(report, "rev").Count);
        Assert.Equal(100, report.Score);
    }

    [Fact]
    public void Lead_time_more_than_two_days_out_is_flagged_but_does_not_move_the_score()
    {
        var report = DataQualityCalculator.Compute([Sale()], [Unit(lead: 25)]); // 30 observed vs 25 claimed

        var issue = Issue(report, "lead");
        Assert.Equal(1, issue.Count);
        Assert.Equal("medium", issue.Severity);
        Assert.Equal(100, report.Score); // lead-time is not in the score formula
    }

    [Fact]
    public void Lead_time_within_two_days_is_not_flagged()
    {
        var report = DataQualityCalculator.Compute([Sale()], [Unit(lead: 32)]); // |30 − 32| = 2

        Assert.Equal(0, Issue(report, "lead").Count);
    }

    [Fact]
    public void Sales_locations_absent_from_inventory_are_listed_and_penalised()
    {
        var report = DataQualityCalculator.Compute(
            [Sale(location: "Riyadh"), Sale(location: "Jeddah"), Sale(location: "Jeddah")],
            [Unit(location: "Riyadh")]);

        var issue = Issue(report, "loc");
        Assert.Equal(1, issue.Count); // Jeddah, distinct
        Assert.Equal("medium", issue.Severity);
        Assert.Contains("Jeddah", issue.Note);
        Assert.Equal(["Jeddah"], report.Mismatch);
        Assert.Equal(99, report.Score); // 100 − 1×1.5 = 98.5 → 99
    }

    [Fact]
    public void No_location_mismatch_when_every_sales_location_holds_inventory()
    {
        var report = DataQualityCalculator.Compute(
            [Sale(location: "Riyadh"), Sale(location: "Jeddah")],
            [Unit("S1", "C1", "Riyadh"), Unit("S2", "C2", "Jeddah")]);

        Assert.Empty(report.Mismatch);
        Assert.Equal("None.", Issue(report, "loc").Note);
    }

    // ---------------------------------------------------------------- score formula & clamp

    [Fact]
    public void Score_applies_the_exact_penalty_weights()
    {
        // dupStock 2 (−10), one negative sale (−5), two absent locations (−3) → 100 − 18 = 82.
        var report = DataQualityCalculator.Compute(
            [Sale(units: -1, location: "Riyadh"), Sale(location: "Jeddah"), Sale(location: "Dammam")],
            [Unit("S1", "C1", "Riyadh"), Unit("S1", "C2", "Riyadh"), Unit("S1", "C3", "Riyadh")]);

        Assert.Equal(82, report.Score);
        Assert.Equal("Warning", report.ScoreBand);
    }

    [Fact]
    public void The_score_clamps_at_zero()
    {
        var manyNegatives = Enumerable.Range(0, 25).Select(i => Sale(units: -1)).ToList();

        var report = DataQualityCalculator.Compute(manyNegatives, [Unit()]);

        Assert.Equal(0, report.Score); // 100 − 25×5 = −25 → clamped
        Assert.Equal("Critical", report.ScoreBand);
    }

    // ---------------------------------------------------------------- band boundaries (≥85 / ≥70)

    [Theory]
    [InlineData(100, "Healthy")]
    [InlineData(85, "Healthy")]
    [InlineData(84, "Warning")]
    [InlineData(70, "Warning")]
    [InlineData(69, "Critical")]
    [InlineData(0, "Critical")]
    public void ScoreBand_classifies_at_the_inclusive_thresholds(int score, string band) =>
        Assert.Equal(band, DataQualityCalculator.ScoreBand(score));

    [Fact]
    public void Compute_classifies_exactly_85_as_healthy()
    {
        // 3 duplicate stock ids → 100 − 15 = 85.
        var inv = new[] { Unit("S1", "C1"), Unit("S1", "C2"), Unit("S1", "C3"), Unit("S1", "C4") };
        var report = DataQualityCalculator.Compute([Sale()], inv);

        Assert.Equal(85, report.Score);
        Assert.Equal("Healthy", report.ScoreBand);
    }

    [Fact]
    public void Compute_classifies_exactly_70_as_warning()
    {
        // 6 duplicate stock ids → 100 − 30 = 70.
        var inv = Enumerable.Range(0, 7).Select(i => Unit("S1", $"C{i}")).ToArray();
        var report = DataQualityCalculator.Compute([Sale()], inv);

        Assert.Equal(70, report.Score);
        Assert.Equal("Warning", report.ScoreBand);
    }
}
