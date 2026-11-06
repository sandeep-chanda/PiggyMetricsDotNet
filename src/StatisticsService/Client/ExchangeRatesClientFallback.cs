using PiggyMetrics.StatisticsService.Domain;

namespace PiggyMetrics.StatisticsService.Client;

public sealed class ExchangeRatesClientFallback : ExchangeRatesClient
{
    public ExchangeRatesContainer GetRates(Currency baseCurrency)
    {
        return new ExchangeRatesContainer
        {
            Base = CurrencyCodes.GetBase(),
            Rates = new Dictionary<string, decimal>()
        };
    }
}
