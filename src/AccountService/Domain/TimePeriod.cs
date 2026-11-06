using System.Text.Json.Serialization;

namespace PiggyMetrics.AccountService.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TimePeriod
{
    YEAR,
    QUARTER,
    MONTH,
    DAY,
    HOUR
}
