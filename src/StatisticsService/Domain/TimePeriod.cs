using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimePeriod
{
    YEAR,
    QUARTER,
    MONTH,
    DAY,
    HOUR
}

public static class TimePeriodRatios
{
    public static TimePeriod GetBase() => TimePeriod.DAY;

    public static decimal GetBaseRatio(this TimePeriod period) => period switch
    {
        TimePeriod.YEAR => 365.2425m,
        TimePeriod.QUARTER => 91.3106m,
        TimePeriod.MONTH => 30.4368m,
        TimePeriod.DAY => 1m,
        TimePeriod.HOUR => 0.0416m,
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };
}
