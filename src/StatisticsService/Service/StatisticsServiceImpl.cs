using PiggyMetrics.StatisticsService.Domain;
using PiggyMetrics.StatisticsService.Domain.Timeseries;
using PiggyMetrics.StatisticsService.Repository;

namespace PiggyMetrics.StatisticsService.Service;

public sealed class StatisticsServiceImpl : StatisticsService
{
    private readonly DataPointRepository _repository;
    private readonly ExchangeRatesService _ratesService;
    private readonly ILogger<StatisticsServiceImpl> _logger;

    public StatisticsServiceImpl(
        DataPointRepository repository,
        ExchangeRatesService ratesService,
        ILogger<StatisticsServiceImpl> logger)
    {
        _repository = repository;
        _ratesService = ratesService;
        _logger = logger;
    }

    public List<DataPoint> FindByAccountName(string? accountName)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("accountName must not be empty", nameof(accountName));
        }

        return _repository.FindByIdAccount(accountName);
    }

    public DataPoint Save(string accountName, Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(account.Incomes);
        ArgumentNullException.ThrowIfNull(account.Expenses);
        ArgumentNullException.ThrowIfNull(account.Saving);

        var pointId = new DataPointId(accountName, DataPointId.LocalTodayInstant());
        var incomes = account.Incomes.Select(CreateItemMetric).ToHashSet();
        var expenses = account.Expenses.Select(CreateItemMetric).ToHashSet();
        var statistics = CreateStatisticMetrics(incomes, expenses, account.Saving);

        var dataPoint = new DataPoint
        {
            Id = pointId,
            Incomes = incomes,
            Expenses = expenses,
            Statistics = statistics,
            Rates = _ratesService.GetCurrentRates()
        };

        _logger.LogDebug("new datapoint has been created: {PointId}", pointId);
        return _repository.Save(dataPoint);
    }

    private Dictionary<StatisticMetric, decimal> CreateStatisticMetrics(
        HashSet<ItemMetric> incomes,
        HashSet<ItemMetric> expenses,
        Saving saving)
    {
        var savingAmount = _ratesService.Convert(saving.Currency!.Value, CurrencyCodes.GetBase(), saving.Amount);
        var expensesAmount = expenses.Select(item => item.Amount).Aggregate(0m, (left, right) => left + right);
        var incomesAmount = incomes.Select(item => item.Amount).Aggregate(0m, (left, right) => left + right);
        return new Dictionary<StatisticMetric, decimal>
        {
            [StatisticMetric.EXPENSES_AMOUNT] = expensesAmount,
            [StatisticMetric.INCOMES_AMOUNT] = incomesAmount,
            [StatisticMetric.SAVING_AMOUNT] = savingAmount
        };
    }

    private ItemMetric CreateItemMetric(Item item)
    {
        var converted = _ratesService.Convert(item.Currency!.Value, CurrencyCodes.GetBase(), item.Amount);
        var amount = Money.Divide(converted, item.Period!.Value.GetBaseRatio(), 4);
        return new ItemMetric(item.Title ?? string.Empty, amount);
    }
}
