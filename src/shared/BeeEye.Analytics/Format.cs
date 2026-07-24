using System.Globalization;

namespace BeeEye.Analytics;

/// <summary>Display formatting helpers used in explanatory strings, matching engine.js.</summary>
public static class Format
{
    public static string Sar(decimal n)
    {
        var a = Math.Abs(n);
        string s;
        if (a >= 1_000_000_000m)
        {
            s = (n / 1_000_000_000m).ToString("F2", CultureInfo.InvariantCulture) + "B";
        }
        else if (a >= 1_000_000m)
        {
            s = (n / 1_000_000m).ToString("F2", CultureInfo.InvariantCulture) + "M";
        }
        else if (a >= 1_000m)
        {
            s = (n / 1_000m).ToString("F1", CultureInfo.InvariantCulture) + "K";
        }
        else
        {
            s = Math.Round(n).ToString("N0", CultureInfo.InvariantCulture);
        }

        return "SAR " + s;
    }

    public static string SignPct(double n, int decimals = 1)
    {
        var sign = n >= 0 ? "+" : "";
        return sign + n.ToString("F" + decimals, CultureInfo.InvariantCulture) + "%";
    }
}
