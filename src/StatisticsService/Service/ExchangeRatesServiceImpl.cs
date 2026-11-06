using PiggyMetrics.StatisticsService.Client;
using PiggyMetrics.StatisticsService.Domain;

namespace PiggyMetrics.StatisticsService.Service;

public sealed class ExchangeRatesServiceImpl : ExchangeRatesService
{
    private readonly ExchangeRatesClient _client;
    private readonly ILogger<ExchangeRatesServiceImpl> _logger;
    private ExchangeRatesContainer? _container;

    public ExchangeRatesServiceImpl(ExchangeRatesClient client, ILogger<ExchangeRatesServiceImpl> logger)
    {
        _client = client;
        _logger = logger;
    }

    public Dictionary<Currency, decimal> GetCurrentRates()
    {
        if (_container is null || _container.Date != DateOnly.FromDateTime(DateTime.Now))
        {
            _container = _client.GetRates(CurrencyCodes.GetBase());
            _logger.LogInformation("exchange rates has been updated: {Container}", _container);
        }

        var rates = _container.Rates ?? throw new InvalidOperationException("rates missing");
        return new Dictionary<Currency, decimal>
        {
            [Currency.EUR] = rates[Currency.EUR.ToString()],
            [Currency.RUB] = rates[Currency.RUB.ToString()],
            [Currency.USD] = 1m
        };
    }

    public decimal Convert(Currency from, Currency to, decimal? amount)
    {
        if (amount is null)
        {
            throw new ArgumentException("amount must not be null", nameof(amount));
        }

        var rates = GetCurrentRates();
        var ratio = Money.Divide(rates[to], rates[from], 4);
        return amount.Value * ratio;
    }
}
