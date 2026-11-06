using PiggyMetrics.AccountService.Domain;

namespace PiggyMetrics.AccountService.Client;

public interface StatisticsServiceClient
{
    void UpdateStatistics(string accountName, Account account);
}
