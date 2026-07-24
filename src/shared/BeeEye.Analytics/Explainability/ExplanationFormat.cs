using System.Globalization;

namespace BeeEye.Analytics.Explainability;

/// <summary>
/// Formats the figures a provider puts into an <see cref="ImpactTile"/> or a <see cref="Driver"/>.
/// <para>
/// Every method formats with <see cref="CultureInfo.InvariantCulture"/>, explicitly, at every call
/// site. <c>InvariantGlobalization</c> is on for the solution, so the ambient culture is already
/// invariant — but relying on a build property to keep money readable is relying on a setting nobody
/// will remember when it is changed. Stating the culture makes the guarantee local to the code that
/// depends on it, and it is what lets the unit tests assert the output under a comma-decimal culture.
/// </para>
/// <para>
/// The abbreviations mirror <c>src/web/src/lib/format.ts</c>'s <c>fmtSar</c>, so the same figure reads
/// identically on a screen and in the drawer that explains it. Two different roundings of one number,
/// side by side, is the fastest way to lose a reader's trust in both.
/// </para>
/// </summary>
public static class ExplanationFormat
{
    private const decimal Billion = 1_000_000_000m;
    private const decimal Million = 1_000_000m;
    private const decimal Thousand = 1_000m;

    /// <summary>A SAR amount, abbreviated the way the screens abbreviate it.</summary>
    public static string Sar(decimal value)
    {
        var magnitude = Math.Abs(value);

        var body = magnitude switch
        {
            >= Billion => (value / Billion).ToString("0.00", CultureInfo.InvariantCulture) + "B",
            >= Million => (value / Million).ToString("0.00", CultureInfo.InvariantCulture) + "M",
            >= Thousand => (value / Thousand).ToString("0.0", CultureInfo.InvariantCulture) + "K",
            _ => Math.Round(value, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture),
        };

        return $"SAR {body}";
    }

    /// <summary>A whole count with thousands separators, e.g. <c>1,204</c>.</summary>
    public static string Count(decimal value) =>
        Math.Round(value, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture);

    /// <summary>A number to a fixed number of decimals, e.g. <c>1.4</c>.</summary>
    public static string Number(decimal value, int decimals = 1) =>
        value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>A percentage, e.g. <c>12.5%</c>.</summary>
    public static string Percent(decimal value, int decimals = 1) =>
        value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%";

    /// <summary>A percentage carrying its sign, e.g. <c>+12.5%</c> — a change, not a level.</summary>
    public static string SignedPercent(decimal value, int decimals = 1) =>
        (value >= 0 ? "+" : string.Empty) + Percent(value, decimals);

    /// <summary>A whole number of days, e.g. <c>184 days</c>.</summary>
    public static string Days(int value) =>
        value.ToString("#,##0", CultureInfo.InvariantCulture) + (value == 1 ? " day" : " days");

    /// <summary>A date as the drawer displays it, e.g. <c>30 Jun 2026</c>.</summary>
    public static string Date(DateOnly value) => value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>A timestamp as the drawer displays it, e.g. <c>24 Jul 2026 09:15 UTC</c>.</summary>
    public static string Timestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
