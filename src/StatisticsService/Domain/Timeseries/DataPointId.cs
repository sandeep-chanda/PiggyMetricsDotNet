using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain.Timeseries;

public sealed class DataPointId
{
    public DataPointId(string account, DateTime date)
    {
        Account = account;
        Date = ToUtcMillis(date);
    }

    [JsonPropertyName("account")]
    public string Account { get; }

    [JsonPropertyName("date")]
    public DateTime Date { get; }

    public static DateTime LocalTodayInstant()
    {
        var localMidnight = DateTime.SpecifyKind(
            DateOnly.FromDateTime(DateTime.Now).ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Unspecified);
        var offset = TimeZoneInfo.Local.GetUtcOffset(localMidnight);
        return ToUtcMillis(new DateTimeOffset(localMidnight, offset).UtcDateTime);
    }

    public static DateTime ToUtcMillis(DateTime date)
    {
        var utc = date.Kind switch
        {
            DateTimeKind.Utc => date,
            DateTimeKind.Local => date.ToUniversalTime(),
            _ => DateTime.SpecifyKind(date, DateTimeKind.Utc)
        };
        var ticks = utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond;
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    public override string ToString()
    {
        return "DataPointId{" +
               "account='" + Account + '\'' +
               ", date=" + Date +
               "}";
    }
}
