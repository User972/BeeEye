namespace BeeEye.Analytics.Inventory;

/// <summary>Aging, manufacturing-age and risk banding, ported from engine.js.</summary>
public static class Bands
{
    public static string Aging(int ageDays, int[] thresholds)
    {
        if (ageDays <= thresholds[0])
        {
            return "New";
        }

        if (ageDays <= thresholds[1])
        {
            return "Healthy";
        }

        if (ageDays <= thresholds[2])
        {
            return "Watch";
        }

        if (ageDays <= thresholds[3])
        {
            return "High attention";
        }

        return "Critical aging";
    }

    public static string Manufacturing(int ageDays)
    {
        if (ageDays <= 180)
        {
            return "0–180 days";
        }

        if (ageDays <= 270)
        {
            return "181–270 days";
        }

        if (ageDays <= 365)
        {
            return "271–365 days";
        }

        return "365+ days";
    }

    public static string Risk(int score, int[] thresholds)
    {
        if (score <= thresholds[0])
        {
            return "Low";
        }

        if (score <= thresholds[1])
        {
            return "Medium";
        }

        if (score <= thresholds[2])
        {
            return "High";
        }

        return "Critical";
    }
}
