using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PiggyMetrics.StatisticsService.Domain;
using PiggyMetrics.StatisticsService.Domain.Timeseries;
using PiggyMetrics.StatisticsService.Repository;
using PiggyMetrics.StatisticsService.Service;
using Xunit;

namespace PiggyMetrics.StatisticsService.Tests.Service;

public class StatisticsServiceImplTest
{
    private readonly Mock<ExchangeRatesService> _ratesService = new();
    private readonly Mock<DataPointRepository> _repository = new();
    private readonly StatisticsServiceImpl _statisticsService;

    public StatisticsServiceImplTest()
    {
        _statisticsService = new StatisticsServiceImpl(
            _repository.Object,
            _ratesService.Object,
            NullLogger<StatisticsServiceImpl>.Instance);
    }

    [Fact]
    public void shouldFindDataPointListByAccountName()
    {
        var list = new List<DataPoint> { new() };
        _repository.Setup(repository => repository.FindByIdAccount("test")).Returns(list);

        var result = _statisticsService.FindByAccountName("test");
        Assert.Equal(list, result);
    }

    [Fact]
    public void shouldFailToFindDataPointWhenAccountNameIsNull()
    {
        Assert.Throws<ArgumentException>(() => _statisticsService.FindByAccountName(null));
    }

    [Fact]
    public void shouldFailToFindDataPointWhenAccountNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _statisticsService.FindByAccountName(""));
    }

    [Fact]
    public void shouldSaveDataPoint()
    {
        var salary = new Item
        {
            Title = "Salary",
            Amount = 9100m,
            Currency = Currency.USD,
            Period = TimePeriod.MONTH
        };
        var grocery = new Item
        {
            Title = "Grocery",
            Amount = 500m,
            Currency = Currency.RUB,
            Period = TimePeriod.DAY
        };
        var vacation = new Item
        {
            Title = "Vacation",
            Amount = 3400m,
            Currency = Currency.EUR,
            Period = TimePeriod.YEAR
        };
        var saving = new Saving
        {
            Amount = 1000m,
            Currency = Currency.EUR,
            Interest = 3.2m,
            Deposit = true,
            Capitalization = false
        };
        var account = new Account
        {
            Incomes = new List<Item> { salary },
            Expenses = new List<Item> { grocery, vacation },
            Saving = saving
        };
        var rates = new Dictionary<Currency, decimal>
        {
            [Currency.EUR] = 0.8m,
            [Currency.RUB] = 80m,
            [Currency.USD] = 1m
        };

        _ratesService.Setup(service => service.Convert(It.IsAny<Currency>(), It.IsAny<Currency>(), It.IsAny<decimal?>()))
            .Returns((Currency from, Currency _, decimal? amount) =>
                Math.Round(amount!.Value / rates[from], 4, MidpointRounding.AwayFromZero));
        _ratesService.Setup(service => service.GetCurrentRates()).Returns(rates);
        _repository.Setup(repository => repository.Save(It.IsAny<DataPoint>())).Returns<DataPoint>(point => point);

        var dataPoint = _statisticsService.Save("test", account);

        const decimal expectedExpensesAmount = 17.8861m;
        const decimal expectedIncomesAmount = 298.9802m;
        const decimal expectedSavingAmount = 1250m;
        const decimal expectedNormalizedSalaryAmount = 298.9802m;
        const decimal expectedNormalizedVacationAmount = 11.6361m;
        const decimal expectedNormalizedGroceryAmount = 6.25m;

        Assert.Equal("test", dataPoint.Id!.Account);
        Assert.Equal(DataPointId.LocalTodayInstant(), dataPoint.Id.Date);
        Assert.Equal(0, decimal.Compare(expectedExpensesAmount, dataPoint.Statistics![StatisticMetric.EXPENSES_AMOUNT]));
        Assert.Equal(0, decimal.Compare(expectedIncomesAmount, dataPoint.Statistics[StatisticMetric.INCOMES_AMOUNT]));
        Assert.Equal(0, decimal.Compare(expectedSavingAmount, dataPoint.Statistics[StatisticMetric.SAVING_AMOUNT]));

        var salaryItemMetric = dataPoint.Incomes!.Single(item => item.Title == salary.Title);
        var vacationItemMetric = dataPoint.Expenses!.Single(item => item.Title == vacation.Title);
        var groceryItemMetric = dataPoint.Expenses!.Single(item => item.Title == grocery.Title);

        Assert.Equal(0, decimal.Compare(expectedNormalizedSalaryAmount, salaryItemMetric.Amount));
        Assert.Equal(0, decimal.Compare(expectedNormalizedVacationAmount, vacationItemMetric.Amount));
        Assert.Equal(0, decimal.Compare(expectedNormalizedGroceryAmount, groceryItemMetric.Amount));
        Assert.Equal(rates, dataPoint.Rates);
        _repository.Verify(repository => repository.Save(dataPoint), Times.Once);
    }
}
