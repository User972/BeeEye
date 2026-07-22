namespace BeeEye.Analytics.Demand;

/// <summary>A monthly sales observation — the shared analytics sales input.</summary>
public sealed record SalesRow(
    string Location,
    string Model,
    string Variant,
    string MonthKey,
    double Units,
    bool Discounted = false,
    int DiscountPct = 0);

/// <summary>Result of the demand-velocity fallback hierarchy.</summary>
public sealed record DemandVelocityResult(double Value, string Basis, string Confidence, string Detail);

/// <summary>Recent-vs-prior demand trend.</summary>
public sealed record DemandTrendResult(double Recent, double Prior, double ChangePct, string Direction);

/// <summary>
/// Pre-aggregated sales maps supporting the demand fallback hierarchy, ported from
/// engine.js <c>aggMaps()</c>. Built once per request over the relevant sales set.
/// </summary>
public sealed class DemandAggregates
{
    private readonly Dictionary<string, double> _lmv = new();   // loc|model|variant|month
    private readonly Dictionary<string, double> _mv = new();    // model|variant|month
    private readonly Dictionary<string, double> _mdl = new();   // model|month
    private readonly Dictionary<string, double> _mvTot = new(); // model|variant
    private readonly Dictionary<string, double> _lmvTot = new();// loc|model|variant
    private readonly Dictionary<string, HashSet<string>> _modelLocs = new();

    private DemandAggregates(string lastMonth) => LastMonth = lastMonth;

    public string LastMonth { get; private set; }

    public static DemandAggregates Build(IEnumerable<SalesRow> rows)
    {
        var lastMonth = "0000-00";
        var agg = new DemandAggregates(lastMonth);
        foreach (var r in rows)
        {
            if (string.CompareOrdinal(r.MonthKey, lastMonth) > 0)
            {
                lastMonth = r.MonthKey;
            }

            Add(agg._lmv, $"{r.Location}|{r.Model}|{r.Variant}|{r.MonthKey}", r.Units);
            Add(agg._mv, $"{r.Model}|{r.Variant}|{r.MonthKey}", r.Units);
            Add(agg._mdl, $"{r.Model}|{r.MonthKey}", r.Units);
            Add(agg._mvTot, $"{r.Model}|{r.Variant}", r.Units);
            Add(agg._lmvTot, $"{r.Location}|{r.Model}|{r.Variant}", r.Units);
            if (!agg._modelLocs.TryGetValue(r.Model, out var set))
            {
                set = [];
                agg._modelLocs[r.Model] = set;
            }

            set.Add(r.Location);
        }

        agg.LastMonth = lastMonth;
        return agg;
    }

    public double Lmv(string loc, string model, string variant, string month) => Get(_lmv, $"{loc}|{model}|{variant}|{month}");

    public double Mv(string model, string variant, string month) => Get(_mv, $"{model}|{variant}|{month}");

    public double Mdl(string model, string month) => Get(_mdl, $"{model}|{month}");

    public double MvTot(string model, string variant) => Get(_mvTot, $"{model}|{variant}");

    public double LmvTot(string loc, string model, string variant) => Get(_lmvTot, $"{loc}|{model}|{variant}");

    public int ModelLocationCount(string model) => _modelLocs.TryGetValue(model, out var set) ? set.Count : 0;

    private static void Add(Dictionary<string, double> map, string key, double value)
        => map[key] = (map.TryGetValue(key, out var v) ? v : 0) + value;

    private static double Get(Dictionary<string, double> map, string key) => map.TryGetValue(key, out var v) ? v : 0;
}
