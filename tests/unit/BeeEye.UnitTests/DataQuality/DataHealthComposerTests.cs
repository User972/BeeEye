using BeeEye.Modules.DataQuality.Application;
using BeeEye.Modules.DataQuality.Contracts;
using Xunit;

namespace BeeEye.UnitTests.DataQuality;

/// <summary>
/// Tests for <see cref="DataHealthReadService.ComposeSources"/> — the pure part of the Data Health
/// assembly. The DB-touching count/coverage derivation is covered end-to-end in the integration suite;
/// here we pin the seven governed sources, the Ready ↔ Ready-with-assumptions flip and the honest
/// demo/blocked labelling without a database.
/// </summary>
public sealed class DataHealthComposerTests
{
    private static DataSourceDto Source(IReadOnlyList<DataSourceDto> sources, string name) =>
        sources.Single(s => s.Name == name);

    [Fact]
    public void There_are_exactly_seven_sources_in_the_governed_order()
    {
        var sources = DataHealthReadService.ComposeSources(3120, 291, "Jan 2024 → Jun 2026", "Snapshot @ Jun 2026", []);

        Assert.Equal(7, sources.Count);
        Assert.Equal(
            [
                "Sales history",
                "Inventory on-hand",
                "Supplier master & PO history",
                "Service / repair-order history",
                "Parts usage & parts inventory",
                "Vehicle mileage & warranty claims",
                "Open purchase orders / inbound",
            ],
            sources.Select(s => s.Name).ToArray());
    }

    [Fact]
    public void The_two_real_sources_report_their_measured_counts_and_coverage()
    {
        var sources = DataHealthReadService.ComposeSources(3120, 291, "Jan 2024 → Jun 2026", "Snapshot @ Jun 2026", []);

        var sales = Source(sources, "Sales history");
        Assert.Equal("Ready", sales.Status);
        Assert.Equal("ready", sales.StatusKind);
        Assert.Equal("3120", sales.Rows);
        Assert.Equal("Jan 2024 → Jun 2026", sales.Coverage);

        var inv = Source(sources, "Inventory on-hand");
        Assert.Equal("291", inv.Rows);
        Assert.Equal("Snapshot @ Jun 2026", inv.Coverage);
    }

    [Fact]
    public void Inventory_is_ready_when_no_sales_location_is_missing()
    {
        var sources = DataHealthReadService.ComposeSources(10, 5, "cov", "snap", []);

        var inv = Source(sources, "Inventory on-hand");
        Assert.Equal("Ready", inv.Status);
        Assert.Equal("ready", inv.StatusKind);
    }

    [Fact]
    public void Inventory_flips_to_ready_with_assumptions_when_a_sales_location_holds_no_stock()
    {
        var sources = DataHealthReadService.ComposeSources(10, 5, "cov", "snap", ["Jeddah", "Dammam"]);

        var inv = Source(sources, "Inventory on-hand");
        Assert.Equal("Ready with assumptions", inv.Status);
        Assert.Equal("assumptions", inv.StatusKind);
        Assert.Contains("Jeddah", inv.Note);
        Assert.Contains("Dammam", inv.Note);
    }

    [Fact]
    public void The_four_demo_sources_are_labelled_demo_and_never_present_a_measured_count()
    {
        var sources = DataHealthReadService.ComposeSources(3120, 291, "cov", "snap", []);

        var demo = sources.Where(s => s.StatusKind == "demo").ToList();
        Assert.Equal(4, demo.Count);
        Assert.All(demo, s => Assert.Equal("Demo data", s.Status));
        // A synthetic source never reports a bare measured number as its row count.
        Assert.All(demo, s => Assert.Equal("Synthetic", s.Rows));
        Assert.All(demo, s => Assert.Contains("Not supplied", s.Note));
    }

    [Fact]
    public void The_blocked_source_is_a_first_class_state_distinct_from_demo()
    {
        var sources = DataHealthReadService.ComposeSources(3120, 291, "cov", "snap", []);

        var blocked = Source(sources, "Vehicle mileage & warranty claims");
        Assert.Equal("Blocked", blocked.Status);
        Assert.Equal("blocked", blocked.StatusKind);
        Assert.Equal("0", blocked.Rows);
        Assert.Equal("—", blocked.Coverage);
        Assert.NotEqual("demo", blocked.StatusKind);
    }

    [Fact]
    public void An_empty_database_still_lists_every_source_coherently()
    {
        var sources = DataHealthReadService.ComposeSources(0, 0, "—", "—", []);

        Assert.Equal(7, sources.Count);
        Assert.Equal("0", Source(sources, "Sales history").Rows);
        Assert.Equal("—", Source(sources, "Sales history").Coverage);
        // The demo and blocked rows are declarative, so they survive an empty operational store.
        Assert.Equal("Demo data", Source(sources, "Supplier master & PO history").Status);
        Assert.Equal("Blocked", Source(sources, "Vehicle mileage & warranty claims").Status);
    }
}
