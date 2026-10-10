using System.Text.Json.Serialization;
using PiggyMetrics.StatisticsService.Domain;

namespace PiggyMetrics.StatisticsService.Domain.Timeseries;

public sealed class DataPoint
{
    [JsonPropertyName("id")]
    public DataPointId? Id { get; set; }

    [JsonPropertyName("incomes")]
    public HashSet<ItemMetric>? Incomes { get; set; }

    [JsonPropertyName("expenses")]
    public HashSet<ItemMetric>? Expenses { get; set; }

    [JsonPropertyName("statistics")]
    public Dictionary<StatisticMetric, decimal>? Statistics { get; set; }

    [JsonPropertyName("rates")]
    public Dictionary<Currency, decimal>? Rates { get; set; }
}
