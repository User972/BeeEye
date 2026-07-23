using System.Collections.Generic;
using System.Linq;
using BeeEye.Analytics.AfterSales;
using Xunit;

namespace BeeEye.Analytics.Tests;

/// <summary>
/// Deterministic tests for UC6 service-intensity.
///
/// Fleet scenario: "Fast" (100 vehicles, 200 events) and "Slow" (100 vehicles, 100 events).
///   fleet ratio = (200+100) / (100+100) = 1.5
///   Fast events-per-vehicle = 2.0 -> index 2.0/1.5 = 1.3333 (>= 1.25 -> high)
///   Slow events-per-vehicle = 1.0 -> index 1.0/1.5 = 0.6667 (not high)
/// </summary>
public class ServiceIntensityTests
{
    private static ServiceRecord Event(string model, int monthsSinceSale, string mileageBand, ServiceType type, string month)
        => new(model, "VX", "Riyadh", month, monthsSinceSale, mileageBand, type, 1.0);

    private static List<ServiceRecord> BuildEvents(string model, int count)
    {
        var bands = AfterSalesBands.MileageOrder;
        var types = new[] { ServiceType.Routine, ServiceType.Repair, ServiceType.Warranty, ServiceType.Recall };
        var months = new[] { "2024-01", "2024-02", "2024-03", "2024-04" };
        var list = new List<ServiceRecord>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(Event(model, i % 60, bands[i % bands.Length], types[i % types.Length], months[i % months.Length]));
        }

        return list;
    }

    private static ServiceIntensityAnalysis Analyse()
    {
        var records = BuildEvents("Fast", 200).Concat(BuildEvents("Slow", 100)).ToList();
        var vio = new Dictionary<string, int> { ["Fast"] = 100, ["Slow"] = 100 };
        var withEvents = new Dictionary<string, int> { ["Fast"] = 90, ["Slow"] = 60 };
        var sales = new List<MonthlyVolume>();
        return ServiceIntensity.Analyse(records, vio, withEvents, sales, ServiceIntensitySettings.Default);
    }

    [Fact]
    public void FleetRatio_NormalisesTheIndex()
    {
        var a = Analyse();
        Assert.Equal(1.5, a.Summary.FleetEventsPerVehicle!.Value, 10);

        var fast = a.Models.Single(m => m.Model == "Fast");
        var slow = a.Models.Single(m => m.Model == "Slow");
        Assert.Equal(2.0, fast.EventsPerVehicle!.Value, 10);
        Assert.Equal(1.3333333333, fast.IntensityIndex!.Value, 8);
        Assert.Equal(0.6666666667, slow.IntensityIndex!.Value, 8);
    }

    [Fact]
    public void HighIntensity_FlagsModelsAboveThreshold()
    {
        var a = Analyse();
        Assert.True(a.Models.Single(m => m.Model == "Fast").HighIntensity);
        Assert.False(a.Models.Single(m => m.Model == "Slow").HighIntensity);
        Assert.Equal(1, a.Summary.HighIntensityModels);
    }

    [Fact]
    public void Models_OrderedByIntensityDescending()
    {
        var a = Analyse();
        Assert.Equal("Fast", a.Models[0].Model);
        Assert.Equal("Slow", a.Models[1].Model);
    }

    [Fact]
    public void ZeroFleet_YieldsNullIndexNeverDividesByZero()
    {
        var records = BuildEvents("Ghost", 10);
        var vio = new Dictionary<string, int> { ["Ghost"] = 0 };
        var withEvents = new Dictionary<string, int>();
        var a = ServiceIntensity.Analyse(records, vio, withEvents, [], ServiceIntensitySettings.Default);

        var ghost = a.Models.Single(m => m.Model == "Ghost");
        Assert.Null(ghost.EventsPerVehicle);
        Assert.Null(ghost.IntensityIndex);
        Assert.False(ghost.HighIntensity);
        Assert.Null(ghost.Coverage.CoverageRate);
        Assert.Equal("Low", ghost.Coverage.ReliabilityTier);
        Assert.Null(a.Summary.FleetEventsPerVehicle);
    }

    [Fact]
    public void ServiceTypeBreakdown_AlwaysHasAllFourTypes()
    {
        var a = Analyse();
        var fast = a.Models.Single(m => m.Model == "Fast");
        Assert.Equal(4, fast.ByServiceType.Count);
        Assert.Equal(new[] { "Routine", "Repair", "Warranty", "Recall" }, fast.ByServiceType.Select(t => t.ServiceType));
        // Shares sum to 1 across the four types.
        Assert.Equal(1.0, fast.ByServiceType.Sum(t => t.Share), 8);
        // 200 events split evenly across 4 types -> 50 each.
        Assert.All(fast.ByServiceType, t => Assert.Equal(50, t.Events));
    }

    [Fact]
    public void MileageBreakdown_SurfacesAllCanonicalBandsWithExplicitZeros()
    {
        // A model with events only in the first band still lists every canonical band.
        var records = Enumerable.Range(0, 20)
            .Select(i => Event("Solo", 1, "0–20k", ServiceType.Routine, "2024-01")).ToList();
        var vio = new Dictionary<string, int> { ["Solo"] = 40 };
        var a = ServiceIntensity.Analyse(records, vio, new Dictionary<string, int> { ["Solo"] = 20 }, [], ServiceIntensitySettings.Default);

        var solo = a.Models.Single(m => m.Model == "Solo");
        Assert.Equal(AfterSalesBands.MileageOrder, solo.ByMileageBand.Select(b => b.Band).ToArray());
        Assert.Equal(20, solo.ByMileageBand[0].Events);
        Assert.All(solo.ByMileageBand.Skip(1), b => Assert.Equal(0, b.Events));
    }

    [Fact]
    public void Coverage_ComputesRateAndTier()
    {
        var a = Analyse();
        var fast = a.Models.Single(m => m.Model == "Fast");
        // 90 of 100 vehicles have >=1 event, 4 months of history -> High coverage but < 12 months -> Medium tier.
        Assert.Equal(0.9, fast.Coverage.CoverageRate!.Value, 10);
        Assert.Equal(90, fast.Coverage.VehiclesWithEvents);
        Assert.Equal(4, fast.Coverage.MonthsOfHistory);
        Assert.Equal("Medium", fast.Coverage.ReliabilityTier);
    }

    [Fact]
    public void Correlation_WithAlignedSales_IsStronglyPositive()
    {
        // Service volume tracks sales one month later.
        var records = new List<ServiceRecord>();
        void Add(string month, int n)
        {
            for (var i = 0; i < n; i++)
            {
                records.Add(Event("Trend", 6, "0–20k", ServiceType.Routine, month));
            }
        }

        Add("2024-02", 10);
        Add("2024-03", 20);
        Add("2024-04", 30);
        var sales = new List<MonthlyVolume>
        {
            new("Trend", "2024-01", 10),
            new("Trend", "2024-02", 20),
            new("Trend", "2024-03", 30),
            new("Trend", "2024-04", 5),
        };
        var vio = new Dictionary<string, int> { ["Trend"] = 50 };
        var a = ServiceIntensity.Analyse(records, vio, new Dictionary<string, int> { ["Trend"] = 40 }, sales, ServiceIntensitySettings.Default);
        var trend = a.Models.Single(m => m.Model == "Trend");

        Assert.NotNull(trend.Correlation.Best);
        Assert.True(trend.Correlation.Best!.Value > 0.5);
        Assert.Contains("association", trend.Correlation.Interpretation, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Correlation_NoSales_IsNullWithoutThrowing()
    {
        var a = Analyse(); // no sales supplied
        var fast = a.Models.Single(m => m.Model == "Fast");
        Assert.Null(fast.Correlation.Best);
        Assert.Null(fast.Correlation.Lag0);
    }

    [Fact]
    public void LaborHours_AggregateAndPerVehicle()
    {
        var a = Analyse();
        var fast = a.Models.Single(m => m.Model == "Fast");
        Assert.Equal(200.0, fast.TotalLaborHours, 10);  // 200 events x 1.0h
        Assert.Equal(2.0, fast.LaborHoursPerVehicle!.Value, 10);
    }

    [Fact]
    public void TimeSinceSaleBand_MapsMonthsToBands()
    {
        Assert.Equal("0–3m", AfterSalesBands.TimeSinceSaleBand(0));
        Assert.Equal("0–3m", AfterSalesBands.TimeSinceSaleBand(2));
        Assert.Equal("3–6m", AfterSalesBands.TimeSinceSaleBand(3));
        Assert.Equal("6–12m", AfterSalesBands.TimeSinceSaleBand(11));
        Assert.Equal("12–24m", AfterSalesBands.TimeSinceSaleBand(12));
        Assert.Equal("24–36m", AfterSalesBands.TimeSinceSaleBand(30));
        Assert.Equal("36–48m", AfterSalesBands.TimeSinceSaleBand(47));
        Assert.Equal("48m+", AfterSalesBands.TimeSinceSaleBand(48));
        Assert.Equal("48m+", AfterSalesBands.TimeSinceSaleBand(100));
    }

    [Fact]
    public void Analyse_EmptyInputs_ReturnsEmptyAnalysis()
    {
        var a = ServiceIntensity.Analyse([], new Dictionary<string, int>(), new Dictionary<string, int>(), []);
        Assert.Empty(a.Models);
        Assert.Equal(0, a.Summary.ModelsTracked);
        Assert.Null(a.Summary.FleetEventsPerVehicle);
        Assert.Null(a.Summary.OverallCoverageRate);
    }

    [Fact]
    public void Correlation_TooFewMonths_IsInsufficient()
    {
        // Only two overlapping months -> correlation cannot be assessed.
        var records = new List<ServiceRecord> { Event("Short", 1, "0–20k", ServiceType.Routine, "2024-01") };
        var sales = new List<MonthlyVolume> { new("Short", "2024-01", 5), new("Short", "2024-02", 6) };
        var vio = new Dictionary<string, int> { ["Short"] = 10 };
        var a = ServiceIntensity.Analyse(records, vio, new Dictionary<string, int> { ["Short"] = 1 }, sales);
        var short_ = a.Models.Single(m => m.Model == "Short");
        Assert.Null(short_.Correlation.Best);
        Assert.Contains("Insufficient", short_.Correlation.Interpretation, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OrderIndex_UnknownBand_SortsLast()
    {
        Assert.Equal(0, AfterSalesBands.OrderIndex(AfterSalesBands.MileageOrder, "0–20k"));
        Assert.Equal(AfterSalesBands.MileageOrder.Length, AfterSalesBands.OrderIndex(AfterSalesBands.MileageOrder, "unknown"));
    }

    [Fact]
    public void Summary_AggregatesFleetAndCoverage()
    {
        var a = Analyse();
        Assert.Equal(2, a.Summary.ModelsTracked);
        Assert.Equal(300, a.Summary.TotalEvents);
        Assert.Equal(200, a.Summary.TotalVehiclesInOperation);
        // Overall coverage = (90+60) / (100+100) = 0.75
        Assert.Equal(0.75, a.Summary.OverallCoverageRate!.Value, 10);
        // Average of the two indices (1.3333 + 0.6667) / 2 = 1.0
        Assert.Equal(1.0, a.Summary.AverageIntensityIndex!.Value, 8);
    }
}
