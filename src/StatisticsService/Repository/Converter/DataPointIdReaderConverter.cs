using MongoDB.Bson;
using PiggyMetrics.StatisticsService.Domain.Timeseries;

namespace PiggyMetrics.StatisticsService.Repository.Converter;

public static class DataPointIdReaderConverter
{
    public static DataPointId Convert(BsonDocument document)
    {
        var date = document["date"].AsBsonDateTime.ToUniversalTime();
        var account = document["account"].AsString;
        return new DataPointId(account, date);
    }
}
