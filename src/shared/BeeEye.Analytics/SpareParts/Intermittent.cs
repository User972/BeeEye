namespace BeeEye.Analytics.SpareParts;

/// <summary>
/// UC7 — intermittent-demand methods, ported and extended from the <c>ml/beeeye_ml/spare_parts</c>
/// seed. Pure and deterministic. <b>Zero-demand periods are real signal</b>: the series must be a
/// dense monthly grid with explicit zeros (build it with <c>MonthKey.Range</c>) — the zeros define the
/// inter-demand intervals these methods depend on.
/// </summary>
public static class Intermittent
{
    /// <summary>Average inter-Demand Interval — periods per non-zero demand. <c>null</c> when there is no demand.</summary>
    public static double? Adi(IReadOnlyList<double> series)
    {
        if (series.Count == 0)
        {
            return null;
        }

        var k = series.Count(v => v > 0);
        return k == 0 ? null : series.Count / (double)k;
    }

    /// <summary>Squared coefficient of variation of the <b>non-zero</b> demand sizes. 0 when &lt; 2 demands.</summary>
    public static double Cv2(IReadOnlyList<double> series)
    {
        var nz = series.Where(v => v > 0).ToList();
        if (nz.Count < 2)
        {
            return 0;
        }

        var cv = Statistics.CoefficientOfVariation(nz);
        return cv * cv;
    }

    /// <summary>Simple exponential smoothing over the full (zero-inclusive) series — the level after the last period.</summary>
    public static double Ses(IReadOnlyList<double> series, double alpha)
    {
        if (series.Count == 0)
        {
            return 0;
        }

        var level = series[0];
        for (var i = 1; i < series.Count; i++)
        {
            level += alpha * (series[i] - level);
        }

        return level;
    }

    /// <summary>
    /// Croston's method: smooth the demand size and the inter-demand interval separately, then divide.
    /// The interval of the first demand is its position from the series start. Returns the per-period rate.
    /// </summary>
    public static double Croston(IReadOnlyList<double> series, double alpha)
    {
        var (level, interval, _) = CrostonState(series, alpha);
        return level is { } l && interval is { } iv && iv > 0 ? l / iv : 0;
    }

    /// <summary>
    /// Syntetos–Boylan Approximation: Croston with the multiplicative bias correction (1 − α/2),
    /// because Croston is known to over-forecast intermittent series.
    /// </summary>
    public static double Sba(IReadOnlyList<double> series, double alpha)
        => (1 - (alpha / 2)) * Croston(series, alpha);

    /// <summary>
    /// Teunter–Sani–Babai: smooth the demand <b>probability</b> every period (so it decays on long zero
    /// runs, handling obsolescence and supersession gracefully) times the smoothed demand size.
    /// </summary>
    public static double Tsb(IReadOnlyList<double> series, double alphaProbability, double alphaSize)
    {
        var nz = series.Where(v => v > 0).ToList();
        if (nz.Count == 0 || series.Count == 0)
        {
            return 0;
        }

        var probability = nz.Count / (double)series.Count; // initial demand probability
        var size = Statistics.Mean(nz);                    // initial demand size

        foreach (var v in series)
        {
            if (v > 0)
            {
                probability += alphaProbability * (1 - probability);
                size += alphaSize * (v - size);
            }
            else
            {
                probability += alphaProbability * (0 - probability);
            }
        }

        return probability * size;
    }

    /// <summary>Croston's smoothed (level, interval, non-zero count). Level/interval are null when there is no demand.</summary>
    internal static (double? Level, double? Interval, int NonZero) CrostonState(IReadOnlyList<double> series, double alpha)
    {
        double? level = null;
        double? interval = null;
        var sinceLast = 0;
        var nonZero = 0;

        foreach (var v in series)
        {
            sinceLast++;
            if (v <= 0)
            {
                continue;
            }

            nonZero++;
            if (level is null)
            {
                level = v;
                interval = sinceLast;
            }
            else
            {
                level += alpha * (v - level);
                interval += alpha * (sinceLast - interval.GetValueOrDefault());
            }

            sinceLast = 0;
        }

        return (level, interval, nonZero);
    }

    /// <summary>All four candidate per-period rates for the transparent method-comparison view.</summary>
    public static MethodComparison Compare(IReadOnlyList<double> series, SparePartsSettings settings)
        => new(
            Ses(series, settings.Alpha),
            Croston(series, settings.Alpha),
            Sba(series, settings.Alpha),
            Tsb(series, settings.TsbAlphaProbability, settings.TsbAlphaSize));

    /// <summary>
    /// Classify a series (SBC scheme + obsolescence check). Returns <see cref="DemandClass.InsufficientData"/>
    /// when history is below the configured minimums.
    /// </summary>
    public static DemandClass Classify(IReadOnlyList<double> series, SparePartsSettings settings)
    {
        var nonZero = series.Count(v => v > 0);
        if (series.Count < settings.MinMonths || nonZero < settings.MinNonZeroPeriods)
        {
            return DemandClass.InsufficientData;
        }

        if (IsObsolescent(series, settings.ObsolescenceRatio))
        {
            return DemandClass.Obsolescent;
        }

        var adi = Adi(series) ?? double.PositiveInfinity;
        var cv2 = Cv2(series);
        var infrequent = adi >= settings.AdiThreshold;
        var variable = cv2 >= settings.Cv2Threshold;

        return (infrequent, variable) switch
        {
            (false, false) => DemandClass.Smooth,
            (false, true) => DemandClass.Erratic,
            (true, false) => DemandClass.Intermittent,
            (true, true) => DemandClass.Lumpy,
        };
    }

    /// <summary>Demand occurrence rate in the second half is at/below <paramref name="ratio"/>× the first half.</summary>
    public static bool IsObsolescent(IReadOnlyList<double> series, double ratio)
    {
        if (series.Count < 4)
        {
            return false;
        }

        var mid = series.Count / 2;
        var firstRate = CountPositive(series, 0, mid) / (double)mid;
        var secondRate = CountPositive(series, mid, series.Count) / (double)(series.Count - mid);
        return firstRate > 0 && secondRate <= ratio * firstRate;
    }

    private static int CountPositive(IReadOnlyList<double> series, int start, int end)
    {
        var c = 0;
        for (var i = start; i < end; i++)
        {
            if (series[i] > 0)
            {
                c++;
            }
        }

        return c;
    }

    /// <summary>The method chosen for a demand class, per the UC7 methodology table.</summary>
    public static string MethodFor(DemandClass demandClass) => demandClass switch
    {
        DemandClass.Smooth => "SES",
        DemandClass.Erratic => "SBA",
        DemandClass.Intermittent => "SBA",
        DemandClass.Lumpy => "TSB",
        DemandClass.Obsolescent => "TSB",
        _ => "None",
    };

    /// <summary>The chosen method's rate from an already-computed comparison.</summary>
    public static double RateFor(DemandClass demandClass, MethodComparison c) => demandClass switch
    {
        DemandClass.Smooth => c.Ses,
        DemandClass.Erratic => c.Sba,
        DemandClass.Intermittent => c.Sba,
        DemandClass.Lumpy => c.Tsb,
        DemandClass.Obsolescent => c.Tsb,
        _ => 0,
    };

    /// <summary>Classify, choose a method, and produce a per-period rate with a monthly range and confidence.</summary>
    public static IntermittentForecast Forecast(IReadOnlyList<double> series, SparePartsSettings? settings = null)
    {
        settings ??= SparePartsSettings.Default;
        var comparison = Compare(series, settings);
        var demandClass = Classify(series, settings);
        var nonZero = series.Count(v => v > 0);
        var adi = Adi(series) ?? 0;
        var cv2 = Cv2(series);

        if (demandClass == DemandClass.InsufficientData)
        {
            // No fabricated forecast: expose the empirical average only as raw context, flagged and low-confidence.
            var empirical = series.Count > 0 ? Statistics.Mean(series) : 0;
            return new IntermittentForecast(
                demandClass, "None", adi, cv2, nonZero, series.Count,
                empirical, empirical, empirical, "Low", true, comparison);
        }

        var method = MethodFor(demandClass);
        var rate = RateFor(demandClass, comparison);

        // Monthly forecast range from non-zero size variability (wider for lumpy/erratic parts).
        var nz = series.Where(v => v > 0).ToList();
        var spread = Statistics.Clamp(nz.Count >= 2 ? Statistics.CoefficientOfVariation(nz) : 0, 0, 1);
        var low = Math.Max(0, rate * (1 - spread));
        var high = rate * (1 + spread);

        return new IntermittentForecast(
            demandClass, method, adi, cv2, nonZero, series.Count,
            rate, low, high, Confidence(demandClass, nonZero), false, comparison);
    }

    private static string Confidence(DemandClass demandClass, int nonZero)
    {
        var baseTier = demandClass switch
        {
            DemandClass.Smooth => "High",
            DemandClass.Intermittent => "Medium",
            DemandClass.Erratic => "Medium",
            _ => "Low",
        };

        // A method is only as trustworthy as the demand observations behind it.
        return nonZero < 3 ? "Low" : baseTier;
    }

    /// <summary>Builds a dense monthly series (explicit zeros) over <paramref name="months"/> from sparse usage.</summary>
    public static double[] DenseSeries(IReadOnlyDictionary<string, double> usageByMonth, IReadOnlyList<string> months)
        => months.Select(m => usageByMonth.GetValueOrDefault(m, 0)).ToArray();
}
