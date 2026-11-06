using PiggyMetrics.AccountService.Domain;

namespace PiggyMetrics.AccountService.Client;

public sealed class StatisticsServiceClientFallback : StatisticsServiceClient
{
    private readonly ILogger<StatisticsServiceClientFallback> _logger;

    public StatisticsServiceClientFallback(ILogger<StatisticsServiceClientFallback> logger)
    {
        _logger = logger;
    }

    public void UpdateStatistics(string accountName, Account account)
    {
        _logger.LogError("Error during update statistics for account: {AccountName}", accountName);
    }
}
