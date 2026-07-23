namespace BeeEye.Analytics.AfterSales;

/// <summary>
/// UC6 — Sales vs After-Sales Demand Correlation. Pure, deterministic engine.
///
/// <para>The Service-Intensity Index (SII) is <b>events per vehicle-in-operation</b>
/// normalised by the fleet-wide ratio (Σ events ÷ Σ vehicles-in-operation), so the
/// fleet mean is 1.0 and a model with index &gt; 1 is more service-heavy than the fleet.
/// A model with no vehicles-in-operation yields a <c>null</c> index — never a divide-by-zero.</para>
///
/// <para>The correlation is a lagged Pearson association between monthly vehicle sales and
/// monthly service volume — reported as association, never causation.</para>
/// </summary>
public static class ServiceIntensity
{
    public static ServiceIntensityAnalysis Analyse(
        IReadOnlyList<ServiceRecord> records,
        IReadOnlyDictionary<string, int> vehiclesInOperationByModel,
        IReadOnlyDictionary<string, int> vehiclesWithEventsByModel,
        IReadOnlyList<MonthlyVolume> monthlySales,
        ServiceIntensitySettings? settings = null)
    {
        settings ??= ServiceIntensitySettings.Default;

        var models = records.Select(r => r.Model)
            .Concat(vehiclesInOperationByModel.Keys)
            .Concat(monthlySales.Select(s => s.Model))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var months = MonthAxis(records, monthlySales);
        var eventsByModel = records.ToLookup(r => r.Model, StringComparer.Ordinal);

        // Fleet ratio over models that actually have vehicles-in-operation, so a model
        // with events but no known fleet does not distort the normaliser.
        var fleetEvents = 0;
        var fleetVio = 0;
        foreach (var m in models)
        {
            var vio = vehiclesInOperationByModel.GetValueOrDefault(m, 0);
            if (vio > 0)
            {
                fleetVio += vio;
                fleetEvents += eventsByModel[m].Count();
            }
        }

        double? fleetRatio = fleetVio > 0 ? fleetEvents / (double)fleetVio : null;

        var salesByModelMonth = monthlySales
            .GroupBy(s => s.Model, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.MonthKey, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.Sum(v => v.Units), StringComparer.Ordinal),
                StringComparer.Ordinal);

        var results = new List<ModelServiceIntensity>(models.Count);
        foreach (var model in models)
        {
            var evts = eventsByModel[model].ToList();
            var vio = vehiclesInOperationByModel.GetValueOrDefault(model, 0);
            var totalEvents = evts.Count;

            double? eventsPerVehicle = vio > 0 ? totalEvents / (double)vio : null;
            double? index = eventsPerVehicle is { } epv && fleetRatio is > 0 ? epv / fleetRatio.Value : null;
            var high = index is { } idx && idx >= settings.HighIntensityThreshold;

            var totalLabor = evts.Sum(e => e.LaborHours);
            double? laborPerVehicle = vio > 0 ? totalLabor / vio : null;

            var byMileage = BandBreakdown(evts, e => e.MileageBand, AfterSalesBands.MileageOrder, vio);
            var byTime = BandBreakdown(evts, e => AfterSalesBands.TimeSinceSaleBand(e.MonthsSinceSale), AfterSalesBands.TimeSinceSaleOrder, vio);
            var byType = TypeBreakdown(evts, totalEvents);

            var monthsOfHistory = evts.Select(e => e.ServiceMonthKey).Distinct(StringComparer.Ordinal).Count();
            var vehiclesWithEvents = vehiclesWithEventsByModel.GetValueOrDefault(model, 0);
            var coverage = Coverage(vio, vehiclesWithEvents, monthsOfHistory, settings);

            var correlation = Correlate(
                salesByModelMonth.GetValueOrDefault(model),
                evts,
                months,
                settings.MaxCorrelationLagMonths);

            results.Add(new ModelServiceIntensity(
                model, totalEvents, vio, eventsPerVehicle, index, high,
                totalLabor, laborPerVehicle, byMileage, byTime, byType, coverage, correlation));
        }

        results = results
            .OrderByDescending(r => r.IntensityIndex ?? double.NegativeInfinity)
            .ThenBy(r => r.Model, StringComparer.Ordinal)
            .ToList();

        var summary = Summarise(results, records, fleetRatio, fleetVio, vehiclesWithEventsByModel, vehiclesInOperationByModel);
        return new ServiceIntensityAnalysis(results, summary);
    }

    private static IReadOnlyList<string> MonthAxis(IReadOnlyList<ServiceRecord> records, IReadOnlyList<MonthlyVolume> sales)
    {
        var keys = records.Select(r => r.ServiceMonthKey).Concat(sales.Select(s => s.MonthKey)).ToList();
        if (keys.Count == 0)
        {
            return [];
        }

        var min = keys[0];
        var max = keys[0];
        foreach (var k in keys)
        {
            if (string.CompareOrdinal(k, min) < 0)
            {
                min = k;
            }

            if (string.CompareOrdinal(k, max) > 0)
            {
                max = k;
            }
        }

        return MonthKey.Range(min, max);
    }

    private static IReadOnlyList<BandCount> BandBreakdown(
        IReadOnlyList<ServiceRecord> evts, Func<ServiceRecord, string> band, IReadOnlyList<string> order, int vio)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var e in evts)
        {
            var b = band(e);
            counts[b] = counts.GetValueOrDefault(b, 0) + 1;
        }

        // Always surface every canonical band (explicit zeros), plus any non-canonical band observed.
        var bands = order.Concat(counts.Keys).Distinct(StringComparer.Ordinal).ToList();
        return bands
            .OrderBy(b => AfterSalesBands.OrderIndex(order, b))
            .ThenBy(b => b, StringComparer.Ordinal)
            .Select(b =>
            {
                var c = counts.GetValueOrDefault(b, 0);
                return new BandCount(b, c, vio > 0 ? c / (double)vio : null);
            })
            .ToList();
    }

    private static IReadOnlyList<ServiceTypeCount> TypeBreakdown(IReadOnlyList<ServiceRecord> evts, int totalEvents)
        => Enum.GetValues<ServiceType>()
            .Select(t =>
            {
                var c = evts.Count(e => e.ServiceType == t);
                return new ServiceTypeCount(t.ToString(), c, totalEvents > 0 ? c / (double)totalEvents : 0);
            })
            .ToList();

    private static ServiceCoverage Coverage(int vio, int vehiclesWithEvents, int monthsOfHistory, ServiceIntensitySettings s)
    {
        double? coverageRate = vio > 0 ? Statistics.Clamp(vehiclesWithEvents / (double)vio, 0, 1) : null;
        var tier = coverageRate switch
        {
            null => "Low",
            var r when r >= s.HighCoverageRate && monthsOfHistory >= s.HighHistoryMonths => "High",
            var r when r >= s.MediumCoverageRate || monthsOfHistory >= s.MediumHistoryMonths => "Medium",
            _ => "Low",
        };

        return new ServiceCoverage(vio, vehiclesWithEvents, coverageRate, monthsOfHistory, tier);
    }

    private static ServiceCorrelation Correlate(
        IReadOnlyDictionary<string, double>? salesByMonth,
        IReadOnlyList<ServiceRecord> evts,
        IReadOnlyList<string> months,
        int maxLag)
    {
        if (salesByMonth is null || months.Count < 3)
        {
            return new ServiceCorrelation(null, null, 0, "Insufficient overlapping history to assess association.");
        }

        var serviceByMonth = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var e in evts)
        {
            serviceByMonth[e.ServiceMonthKey] = serviceByMonth.GetValueOrDefault(e.ServiceMonthKey, 0) + 1;
        }

        var sales = months.Select(m => salesByMonth.GetValueOrDefault(m, 0)).ToList();
        var service = months.Select(m => serviceByMonth.GetValueOrDefault(m, 0)).ToList();

        var lag0 = Statistics.Correlation(sales, service);
        double? best = lag0;
        var bestLag = 0;

        // lag 0 is already captured by lag0/best above — start at 1 to avoid recomputing it.
        var cap = Math.Min(maxLag, months.Count - 3);
        for (var lag = 1; lag <= Math.Max(0, cap); lag++)
        {
            // Service at month t associated with sales at month t-lag (service trails sales).
            var x = new List<double>();
            var y = new List<double>();
            for (var t = lag; t < months.Count; t++)
            {
                x.Add(sales[t - lag]);
                y.Add(service[t]);
            }

            var c = Statistics.Correlation(x, y);
            if (c is { } cv && (best is null || cv > best))
            {
                best = cv;
                bestLag = lag;
            }
        }

        return new ServiceCorrelation(lag0, best, bestLag, Interpret(best, bestLag));
    }

    private static string Interpret(double? best, int lag)
    {
        if (best is null)
        {
            return "No measurable association between monthly sales and service volume.";
        }

        var strength = best.Value switch
        {
            >= 0.5 => "a strong positive",
            >= 0.2 => "a moderate positive",
            >= -0.2 => "little to no",
            _ => "a negative",
        };

        var lagText = lag == 0 ? "in the same month" : $"at a {lag}-month lag";
        return $"Monthly service volume shows {strength} association with vehicle sales {lagText} (association, not causation).";
    }

    private static ServiceIntensitySummary Summarise(
        IReadOnlyList<ModelServiceIntensity> results,
        IReadOnlyList<ServiceRecord> records,
        double? fleetRatio,
        int fleetVio,
        IReadOnlyDictionary<string, int> vehiclesWithEvents,
        IReadOnlyDictionary<string, int> vio)
    {
        var indices = results.Where(r => r.IntensityIndex is not null).Select(r => r.IntensityIndex!.Value).ToList();
        double? avgIndex = indices.Count > 0 ? Statistics.Mean(indices) : null;

        var totalVwe = 0;
        var totalVioCovered = 0;
        foreach (var (model, v) in vio)
        {
            if (v > 0)
            {
                totalVioCovered += v;
                totalVwe += vehiclesWithEvents.GetValueOrDefault(model, 0);
            }
        }

        double? overallCoverage = totalVioCovered > 0 ? Statistics.Clamp(totalVwe / (double)totalVioCovered, 0, 1) : null;
        var monthsOfHistory = records.Select(r => r.ServiceMonthKey).Distinct(StringComparer.Ordinal).Count();

        return new ServiceIntensitySummary(
            results.Count(r => r.TotalEvents > 0 || r.VehiclesInOperation > 0),
            records.Count,
            fleetVio,
            fleetRatio,
            avgIndex,
            results.Count(r => r.HighIntensity),
            overallCoverage,
            monthsOfHistory);
    }
}
