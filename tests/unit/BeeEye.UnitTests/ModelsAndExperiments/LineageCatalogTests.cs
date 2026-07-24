using BeeEye.Modules.ModelsAndExperiments.Application;
using Xunit;

namespace BeeEye.UnitTests.ModelsAndExperiments;

/// <summary>
/// Tests for <see cref="LineageCatalog"/> (V3-GOV-009): the six-stage pipeline in order, the eight
/// metrics, and — crucially — that each metric's confirmed/demo state is derived from its basis rather
/// than asserted a second time, so a metric can never disagree with the provenance printed beside it.
/// </summary>
public sealed class LineageCatalogTests
{
    [Fact]
    public void The_pipeline_has_the_six_stages_in_source_to_decision_order()
    {
        var titles = LineageCatalog.Pipeline.Select(s => s.Title).ToArray();

        Assert.Equal(
            [
                "Oracle Fusion ERP / CRM",
                "Secure read-only integration",
                "Curated analytics layer",
                "Forecast & decision models",
                "Explainability service",
                "Decision Intelligence application",
            ],
            titles);
    }

    [Fact]
    public void Each_pipeline_stage_carries_an_icon_and_a_kind()
    {
        Assert.All(LineageCatalog.Pipeline, stage =>
        {
            Assert.False(string.IsNullOrWhiteSpace(stage.Icon));
            Assert.False(string.IsNullOrWhiteSpace(stage.Kind));
            Assert.False(string.IsNullOrWhiteSpace(stage.Description));
        });
    }

    [Fact]
    public void The_integration_stage_keeps_the_no_write_back_promise()
    {
        // BeeEye never writes to Oracle Fusion (CLAUDE.md). The lineage must state this literally.
        var integration = LineageCatalog.Pipeline.Single(s => s.Kind == "integration");
        Assert.Contains("no write-back", integration.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void There_are_eight_metrics_with_the_expected_names()
    {
        var metrics = LineageCatalog.Metrics.Select(m => m.Metric).ToArray();

        Assert.Equal(
            [
                "Recommended order mix",
                "Procurement range",
                "Inventory risk & aging",
                "Sales forecast",
                "Configuration demand",
                "Service-intensity index",
                "Spare-parts forecast",
                "Executive priority score",
            ],
            metrics);
    }

    [Fact]
    public void Every_metric_is_confirmed_or_demo()
    {
        Assert.All(LineageCatalog.Metrics, m => Assert.Contains(m.State, new[] { "confirmed", "demo" }));
    }

    [Fact]
    public void A_metric_is_demo_exactly_when_its_basis_is_a_synthetic_fixture()
    {
        // The single source of the confirmed/demo truth: the state follows from the stated basis, so it
        // cannot drift from the provenance it labels.
        foreach (var m in LineageCatalog.Metrics)
        {
            var basisIsSynthetic = m.Basis.Contains("Synthetic", StringComparison.OrdinalIgnoreCase);
            Assert.Equal(basisIsSynthetic ? "demo" : "confirmed", m.State);
        }
    }

    [Fact]
    public void The_demo_set_is_exactly_procurement_service_intensity_and_spare_parts()
    {
        // The platform's synthetic-demo set: UC4 (Procurement), UC6 (Service-intensity), UC7 (Spare
        // parts) — the same three the v3 wireframe tags demo.
        var demo = LineageCatalog.Metrics.Where(m => m.State == "demo").Select(m => m.Metric).OrderBy(x => x).ToArray();

        Assert.Equal(
            ["Procurement range", "Service-intensity index", "Spare-parts forecast"],
            demo);
    }

    [Theory]
    [InlineData("Synthetic supplier & PO fixture", "demo")]
    [InlineData("Synthetic parts fixture", "demo")]
    [InlineData("Sales history workbook", "confirmed")]
    [InlineData("Inventory workbook", "confirmed")]
    [InlineData("Derived across modules", "confirmed")]
    public void StateOf_derives_the_flag_from_the_basis(string basis, string expected) =>
        Assert.Equal(expected, LineageCatalog.StateOf(basis));

    [Fact]
    public void Build_returns_the_pipeline_and_metrics()
    {
        var response = LineageCatalog.Build();

        Assert.Equal(6, response.Pipeline.Count);
        Assert.Equal(8, response.Metrics.Count);
    }
}
