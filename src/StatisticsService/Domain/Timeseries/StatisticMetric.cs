using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain.Timeseries;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatisticMetric
{
    INCOMES_AMOUNT,
    EXPENSES_AMOUNT,
    SAVING_AMOUNT
}
