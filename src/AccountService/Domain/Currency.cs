using System.Text.Json.Serialization;

namespace PiggyMetrics.AccountService.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Currency
{
    USD,
    EUR,
    RUB
}

public static class CurrencyCodes
{
    public static Currency GetDefault() => Currency.USD;
}
