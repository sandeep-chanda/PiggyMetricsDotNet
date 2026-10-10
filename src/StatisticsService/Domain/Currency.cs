using System.Text.Json.Serialization;

namespace PiggyMetrics.StatisticsService.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Currency
{
    USD,
    EUR,
    RUB
}

public static class CurrencyCodes
{
    public static Currency GetBase() => Currency.USD;
}
