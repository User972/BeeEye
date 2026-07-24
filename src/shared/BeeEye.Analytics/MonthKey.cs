using System.Globalization;

namespace BeeEye.Analytics;

/// <summary>
/// Arithmetic over "YYYY-MM" month keys, ported from engine.js. String comparison
/// of these keys is chronological, which the callers rely on.
/// </summary>
public static class MonthKey
{
    private static readonly string[] MonthNames =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>Add <paramref name="n"/> months (may be negative) to a "YYYY-MM" key.</summary>
    public static string Add(string mk, int n)
    {
        var y = int.Parse(mk[..4], CultureInfo.InvariantCulture);
        var m = int.Parse(mk.Substring(5, 2), CultureInfo.InvariantCulture) - 1 + n;
        y += (int)Math.Floor(m / 12.0);
        m = ((m % 12) + 12) % 12;
        return $"{y:D4}-{m + 1:D2}";
    }

    /// <summary>Inclusive range of month keys from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static IReadOnlyList<string> Range(string from, string to)
    {
        var outp = new List<string>();
        var c = from;
        while (string.CompareOrdinal(c, to) <= 0)
        {
            outp.Add(c);
            c = Add(c, 1);
        }

        return outp;
    }

    /// <summary>Short display label, e.g. "Jan 26".</summary>
    public static string Label(string mk)
    {
        var month = int.Parse(mk.Substring(5, 2), CultureInfo.InvariantCulture);
        return $"{MonthNames[month - 1]} {mk.Substring(2, 2)}";
    }

    /// <summary>The <paramref name="n"/> most recent month keys ending at <paramref name="endMonth"/> (descending).</summary>
    public static IReadOnlyList<string> Trailing(int n, string endMonth)
    {
        var outp = new List<string>(n);
        var c = endMonth;
        for (var i = 0; i < n; i++)
        {
            outp.Add(c);
            c = Add(c, -1);
        }

        return outp;
    }

    /// <summary>Month key ("YYYY-MM") for a date.</summary>
    public static string Of(DateOnly date) => $"{date.Year:D4}-{date.Month:D2}";
}
