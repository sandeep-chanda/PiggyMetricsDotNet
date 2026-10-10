using MongoDB.Bson;
using PiggyMetrics.StatisticsService.Domain.Timeseries;

namespace PiggyMetrics.StatisticsService.Repository.Converter;

public static class DataPointIdWriterConverter
{
    public static BsonDocument Convert(DataPointId id)
    {
        var document = new BsonDocument();
        document.Add("date", new BsonDateTime(id.Date));
        document.Add("account", id.Account);
        return document;
    }
}
