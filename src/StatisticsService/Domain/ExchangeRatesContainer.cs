using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain;

public sealed class ExchangeRatesContainer
{
    [JsonIgnore]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);

    [JsonPropertyName("base")]
    public Currency? Base { get; set; }

    [JsonPropertyName("rates")]
    public Dictionary<string, decimal>? Rates { get; set; }

    public override string ToString()
    {
        return "RateList{" +
               "date=" + Date +
               ", base=" + Base +
               ", rates=" + Rates +
               "}";
    }
}
