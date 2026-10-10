using PiggyMetrics.StatisticsService.Domain.Timeseries;

namespace PiggyMetrics.StatisticsService.Repository;

public interface DataPointRepository
{
    List<DataPoint> FindByIdAccount(string account);

    DataPoint Save(DataPoint dataPoint);
}
