using PiggyMetrics.StatisticsService.Domain;
using PiggyMetrics.StatisticsService.Domain.Timeseries;

namespace PiggyMetrics.StatisticsService.Service;

public interface StatisticsService
{
    List<DataPoint> FindByAccountName(string? accountName);

    DataPoint Save(string accountName, Account account);
}
