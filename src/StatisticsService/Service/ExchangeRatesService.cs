using PiggyMetrics.StatisticsService.Domain;

namespace PiggyMetrics.StatisticsService.Service;

public interface ExchangeRatesService
{
    Dictionary<Currency, decimal> GetCurrentRates();

    decimal Convert(Currency from, Currency to, decimal? amount);
}
