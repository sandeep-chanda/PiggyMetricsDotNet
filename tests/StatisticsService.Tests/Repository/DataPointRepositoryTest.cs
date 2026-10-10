using EphemeralMongo;
using MongoDB.Bson;
using MongoDB.Driver;
using PiggyMetrics.StatisticsService.Domain.Timeseries;
using PiggyMetrics.StatisticsService.Repository;
using Xunit;

namespace PiggyMetrics.StatisticsService.Tests.Repository;

public class DataPointRepositoryTest : IDisposable
{
    private readonly IMongoRunner _runner;
    private readonly IMongoDatabase _database;
    private readonly MongoDataPointRepository _repository;

    public DataPointRepositoryTest()
    {
        _runner = MongoRunner.Run();
        var client = new MongoClient(_runner.ConnectionString);
        _database = client.GetDatabase("piggymetrics");
        _repository = new MongoDataPointRepository(_database);
    }

    public void Dispose()
    {
        _runner.Dispose();
    }

    [Fact]
    public void shouldSaveDataPoint()
    {
        var salary = new ItemMetric("salary", 20_000m);
        var grocery = new ItemMetric("grocery", 1_000m);
        var vacation = new ItemMetric("vacation", 2_000m);
        var pointId = new DataPointId("test-account", DateTime.UnixEpoch);
        var point = new DataPoint
        {
            Id = pointId,
            Incomes = new HashSet<ItemMetric> { salary },
            Expenses = new HashSet<ItemMetric> { grocery, vacation },
            Statistics = new Dictionary<StatisticMetric, decimal>
            {
                [StatisticMetric.SAVING_AMOUNT] = 400_000m,
                [StatisticMetric.INCOMES_AMOUNT] = 20_000m,
                [StatisticMetric.EXPENSES_AMOUNT] = 3_000m
            }
        };

        _repository.Save(point);

        var stored = _database.GetCollection<BsonDocument>("datapoints")
            .Find(new BsonDocument("_id.account", pointId.Account))
            .First();
        Assert.Equal("test-account", stored["_id"]["account"].AsString);
        Assert.Equal(pointId.Date, stored["_id"]["date"].ToUniversalTime());
        Assert.Equal("salary", stored["incomes"][0]["title"].AsString);
        Assert.Equal(20000m, stored["incomes"][0]["amount"].AsDecimal);
        Assert.True(stored["statistics"].AsBsonDocument.Contains("SAVING_AMOUNT"));
        Assert.True(stored["statistics"].AsBsonDocument.Contains("INCOMES_AMOUNT"));
        Assert.True(stored["statistics"].AsBsonDocument.Contains("EXPENSES_AMOUNT"));

        var points = _repository.FindByIdAccount(pointId.Account);
        Assert.Single(points);
        Assert.Equal(pointId.Date, points[0].Id!.Date);
        Assert.Equal(point.Statistics.Count, points[0].Statistics!.Count);
        Assert.Equal(point.Incomes.Count, points[0].Incomes!.Count);
        Assert.Equal(point.Expenses.Count, points[0].Expenses!.Count);
    }

    [Fact]
    public void shouldRewriteDataPointWithinADay()
    {
        const decimal earlyAmount = 100m;
        const decimal lateAmount = 200m;
        var pointId = new DataPointId("test-account", DateTime.UnixEpoch);

        var earlier = new DataPoint
        {
            Id = pointId,
            Statistics = new Dictionary<StatisticMetric, decimal>
            {
                [StatisticMetric.SAVING_AMOUNT] = earlyAmount
            }
        };
        _repository.Save(earlier);

        var later = new DataPoint
        {
            Id = pointId,
            Statistics = new Dictionary<StatisticMetric, decimal>
            {
                [StatisticMetric.SAVING_AMOUNT] = lateAmount
            }
        };
        _repository.Save(later);

        var points = _repository.FindByIdAccount(pointId.Account);
        Assert.Single(points);
        Assert.Equal(lateAmount, points[0].Statistics![StatisticMetric.SAVING_AMOUNT]);
    }
}
