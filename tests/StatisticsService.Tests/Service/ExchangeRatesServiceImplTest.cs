using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PiggyMetrics.StatisticsService.Client;
using PiggyMetrics.StatisticsService.Domain;
using PiggyMetrics.StatisticsService.Service;
using Xunit;

namespace PiggyMetrics.StatisticsService.Tests.Service;

public class ExchangeRatesServiceImplTest
{
    private readonly Mock<ExchangeRatesClient> _client = new();
    private readonly ExchangeRatesServiceImpl _ratesService;

    public ExchangeRatesServiceImplTest()
    {
        _ratesService = new ExchangeRatesServiceImpl(_client.Object, NullLogger<ExchangeRatesServiceImpl>.Instance);
    }

    [Fact]
    public void shouldReturnCurrentRatesWhenContainerIsEmptySoFar()
    {
        var container = new ExchangeRatesContainer
        {
            Rates = new Dictionary<string, decimal>
            {
                [Currency.EUR.ToString()] = 0.8m,
                [Currency.RUB.ToString()] = 80m
            }
        };
        _client.Setup(client => client.GetRates(CurrencyCodes.GetBase())).Returns(container);

        var result = _ratesService.GetCurrentRates();
        _client.Verify(client => client.GetRates(CurrencyCodes.GetBase()), Times.Once);

        Assert.Equal(container.Rates[Currency.EUR.ToString()], result[Currency.EUR]);
        Assert.Equal(container.Rates[Currency.RUB.ToString()], result[Currency.RUB]);
        Assert.Equal(1m, result[Currency.USD]);
    }

    [Fact]
    public void shouldNotRequestRatesWhenTodaysContainerAlreadyExists()
    {
        var container = new ExchangeRatesContainer
        {
            Rates = new Dictionary<string, decimal>
            {
                [Currency.EUR.ToString()] = 0.8m,
                [Currency.RUB.ToString()] = 80m
            }
        };
        _client.Setup(client => client.GetRates(CurrencyCodes.GetBase())).Returns(container);

        _ratesService.GetCurrentRates();
        _ratesService.GetCurrentRates();

        _client.Verify(client => client.GetRates(CurrencyCodes.GetBase()), Times.Once);
    }

    [Fact]
    public void shouldRequestRatesAgainWhenContainerDateIsNotToday()
    {
        var container = new ExchangeRatesContainer
        {
            Date = new DateOnly(2016, 1, 1),
            Rates = new Dictionary<string, decimal>
            {
                [Currency.EUR.ToString()] = 0.8m,
                [Currency.RUB.ToString()] = 80m
            }
        };
        _client.Setup(client => client.GetRates(CurrencyCodes.GetBase())).Returns(container);

        _ratesService.GetCurrentRates();
        _ratesService.GetCurrentRates();

        _client.Verify(client => client.GetRates(CurrencyCodes.GetBase()), Times.Exactly(2));
    }

    [Fact]
    public void shouldConvertCurrency()
    {
        var container = new ExchangeRatesContainer
        {
            Rates = new Dictionary<string, decimal>
            {
                [Currency.EUR.ToString()] = 0.8m,
                [Currency.RUB.ToString()] = 80m
            }
        };
        _client.Setup(client => client.GetRates(CurrencyCodes.GetBase())).Returns(container);

        const decimal amount = 100m;
        const decimal expectedConvertionResult = 1.25m;
        var result = _ratesService.Convert(Currency.RUB, Currency.USD, amount);

        Assert.Equal(0, decimal.Compare(expectedConvertionResult, result));
    }

    [Fact]
    public void shouldFailToConvertWhenAmountIsNull()
    {
        Assert.Throws<ArgumentException>(() => _ratesService.Convert(Currency.EUR, Currency.RUB, null));
    }
}
