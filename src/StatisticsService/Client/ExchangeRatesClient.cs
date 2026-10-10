using PiggyMetrics.StatisticsService.Domain;

namespace PiggyMetrics.StatisticsService.Client;

public interface ExchangeRatesClient
{
    ExchangeRatesContainer GetRates(Currency baseCurrency);
}
