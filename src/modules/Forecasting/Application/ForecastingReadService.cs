using BeeEye.Analytics;
using BeeEye.Analytics.Forecasting;
using BeeEye.Modules.Forecasting.Contracts;
using BeeEye.Persistence;
using BeeEye.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace BeeEye.Modules.Forecasting.Application;

/// <summary>
/// UC2 read side. Builds monthly demand series from the (filtered) sales facts over
/// the observed month range clipped to the requested date window, then back-tests the
/// baseline models, selects the lowest-WMAPE model and projects the future — a direct
/// use of BeeEye.Analytics.
/// </summary>
public sealed class ForecastingReadService(BeeEyeDbContext db)
{
    private static readonly Error InsufficientHistory =
        new("insufficient_history", "At least three months of sales history are required to forecast.");

    private static readonly Error NoMatch =
        new("no_match", "No sales rows match the requested filters.");

    private sealed record Row(
        string Brand, string Model, string Variant, string Type, string Location,
        string Colour, string Interior, string MonthKey, double Units, bool Ramadan);

    public async Task<bool> HasDataAsync(CancellationToken ct) => await db.SalesFacts.AsNoTracking().AnyAsync(ct);

    public async Task<Result<ForecastResponse>> ForecastAsync(SalesFilter filter, ForecastOptions options, CancellationToken ct)
    {
        var (rows, allMonths) = await LoadAsync(ct);

        // The month axis must honour the date window: otherwise months the filter excluded
        // re-enter the series as fabricated zero-demand observations that poison the
        // back-test and the refit, and "future" months start after the wrong month.
        var months = ClipToDateWindow(allMonths, filter.DateFrom, filter.DateTo);
        if (months.Count < 3)
        {
            return Result<ForecastResponse>.Failure(InsufficientHistory);
        }

        var filtered = rows.Where(r => Matches(r, filter)).ToList();
        if (filtered.Count == 0)
        {
            // No rows match the filter: the month axis and history guard were computed over all
            // sales, so without this the series would be all-zero and we'd return a bogus forecast.
            return Result<ForecastResponse>.Failure(NoMatch);
        }

        var series = BuildSeries(filtered, months);
        var result = Forecaster.Run(series, months, options, RamadanLift(filtered));
        return Result<ForecastResponse>.Success(
            new ForecastResponse(result, new ForecastMeta(months.Count, Statistics.Sum(series), DateTimeOffset.UtcNow)));
    }

    public async Task<Result<AccuracyByResponse>> AccuracyByAsync(
        string dimension, SalesFilter filter, int holdout, CancellationToken ct)
    {
        var (rows, allMonths) = await LoadAsync(ct);
        var months = ClipToDateWindow(allMonths, filter.DateFrom, filter.DateTo);
        if (months.Count < 3)
        {
            return Result<AccuracyByResponse>.Failure(InsufficientHistory);
        }

        var selector = DimensionSelector(dimension);
        var filtered = rows.Where(r => Matches(r, filter)).ToList();
        var options = new ForecastOptions(Horizon: 1, Holdout: holdout);

        var result = filtered
            .GroupBy(selector)
            .Select(g =>
            {
                var series = BuildSeries(g.ToList(), months);
                var fc = Forecaster.Run(series, months, options);
                var tendency = fc.Accuracy.Bias switch
                {
                    null => "insufficient",
                    > 5 => "over-forecasting",
                    < -5 => "under-forecasting",
                    _ => "balanced",
                };
                return new DimensionAccuracy(
                    g.Key, fc.Accuracy.Wmape, fc.Accuracy.Bias, fc.Accuracy.Mae,
                    (int)g.Sum(r => r.Units), fc.ChosenName, tendency);
            })
            .OrderByDescending(d => d.Units)
            .ToList();

        return Result<AccuracyByResponse>.Success(
            new AccuracyByResponse(dimension, result, new ForecastMeta(months.Count, filtered.Sum(r => r.Units), DateTimeOffset.UtcNow)));
    }

    public async Task<ForecastFilterOptions?> FilterOptionsAsync(CancellationToken ct)
    {
        var (rows, months) = await LoadAsync(ct);
        if (months.Count == 0)
        {
            return null;
        }

        return new ForecastFilterOptions(
            Distinct(rows, r => r.Brand), Distinct(rows, r => r.Model), Distinct(rows, r => r.Variant),
            Distinct(rows, r => r.Type), Distinct(rows, r => r.Location), Distinct(rows, r => r.Colour),
            Distinct(rows, r => r.Interior), months[0], months[^1]);
    }

    private async Task<(List<Row> Rows, IReadOnlyList<string> Months)> LoadAsync(CancellationToken ct)
    {
        var raw = await db.SalesFacts.AsNoTracking()
            .Select(s => new
            {
                s.Brand, s.Model, s.Variant, s.Type, s.Location, s.Colour, s.Interior,
                s.Year, s.Month, s.UnitsSold, s.IsRamadan,
            })
            .ToListAsync(ct);

        var rows = raw.Select(s => new Row(
            s.Brand, s.Model, s.Variant, s.Type, s.Location, s.Colour, s.Interior,
            $"{s.Year:D4}-{s.Month:D2}", s.UnitsSold, s.IsRamadan)).ToList();

        if (rows.Count == 0)
        {
            return (rows, []);
        }

        var min = rows.Min(r => r.MonthKey);
        var max = rows.Max(r => r.MonthKey);
        return (rows, MonthKey.Range(min, max));
    }

    private static double[] BuildSeries(IReadOnlyList<Row> rows, IReadOnlyList<string> months)
    {
        var byMonth = new Dictionary<string, double>();
        foreach (var r in rows)
        {
            byMonth[r.MonthKey] = (byMonth.TryGetValue(r.MonthKey, out var v) ? v : 0) + r.Units;
        }

        return months.Select(m => byMonth.TryGetValue(m, out var v) ? v : 0).ToArray();
    }

    private static double? RamadanLift(IReadOnlyList<Row> rows)
    {
        var ram = rows.Where(r => r.Ramadan).ToList();
        var non = rows.Where(r => !r.Ramadan).ToList();
        var ramMonths = ram.Select(r => r.MonthKey).Distinct().Count();
        var nonMonths = non.Select(r => r.MonthKey).Distinct().Count();
        if (ramMonths == 0 || nonMonths == 0)
        {
            return null;
        }

        var ramAvg = ram.Sum(r => r.Units) / ramMonths;
        var nonAvg = non.Sum(r => r.Units) / nonMonths;
        return nonAvg != 0 ? (ramAvg - nonAvg) / nonAvg * 100 : null;
    }

    private static bool Matches(Row r, SalesFilter f)
        => In(f.Brand, r.Brand) && In(f.Model, r.Model) && In(f.Variant, r.Variant) && In(f.Type, r.Type)
           && In(f.Location, r.Location) && In(f.Colour, r.Colour) && In(f.Interior, r.Interior)
           && (f.DateFrom is null || string.CompareOrdinal(r.MonthKey, f.DateFrom) >= 0)
           && (f.DateTo is null || string.CompareOrdinal(r.MonthKey, f.DateTo) <= 0);

    private static bool In(IReadOnlyList<string> allowed, string value)
        => allowed.Count == 0 || allowed.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ClipToDateWindow(IReadOnlyList<string> months, string? from, string? to)
        => months
            .Where(m => (from is null || string.CompareOrdinal(m, from) >= 0)
                        && (to is null || string.CompareOrdinal(m, to) <= 0))
            .ToList();

    private static Func<Row, string> DimensionSelector(string dimension) => dimension.ToLowerInvariant() switch
    {
        "variant" => r => r.Variant,
        "location" => r => r.Location,
        "region" => r => r.Location,
        "brand" => r => r.Brand,
        "type" => r => r.Type,
        _ => r => r.Model,
    };

    private static IReadOnlyList<string> Distinct(IEnumerable<Row> rows, Func<Row, string> selector)
        => rows.Select(selector).Distinct().OrderBy(x => x).ToList();
}
