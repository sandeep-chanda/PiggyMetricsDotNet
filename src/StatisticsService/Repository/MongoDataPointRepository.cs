using MongoDB.Bson;
using MongoDB.Driver;
using PiggyMetrics.Shared.Store;
using PiggyMetrics.StatisticsService.Domain;
using PiggyMetrics.StatisticsService.Domain.Timeseries;
using PiggyMetrics.StatisticsService.Repository.Converter;

namespace PiggyMetrics.StatisticsService.Repository;

public sealed class MongoDataPointRepository : DataPointRepository
{
    private readonly IMongoCollection<BsonDocument> _documents;

    public MongoDataPointRepository(IMongoDatabase database)
    {
        _documents = database.GetCollection<BsonDocument>(MongoCollectionNames.Datapoints);
    }

    public List<DataPoint> FindByIdAccount(string account)
    {
        var filter = new BsonDocument("_id.account", account);
        return _documents.Find(filter).ToList().Select(Read).ToList();
    }

    public DataPoint Save(DataPoint dataPoint)
    {
        var document = Write(dataPoint);
        _documents.ReplaceOne(
            new BsonDocument("_id", document["_id"]),
            document,
            new ReplaceOptions { IsUpsert = true });
        return dataPoint;
    }

    private static BsonDocument Write(DataPoint point)
    {
        var document = new BsonDocument
        {
            ["_id"] = DataPointIdWriterConverter.Convert(point.Id ?? throw new ArgumentException("id required"))
        };
        if (point.Incomes is not null)
        {
            document["incomes"] = WriteItems(point.Incomes);
        }

        if (point.Expenses is not null)
        {
            document["expenses"] = WriteItems(point.Expenses);
        }

        if (point.Statistics is not null)
        {
            var statistics = new BsonDocument();
            foreach (var pair in point.Statistics)
            {
                statistics.Add(pair.Key.ToString(), new BsonDecimal128(pair.Value));
            }

            document["statistics"] = statistics;
        }

        if (point.Rates is not null)
        {
            var rates = new BsonDocument();
            foreach (var pair in point.Rates)
            {
                rates.Add(pair.Key.ToString(), new BsonDecimal128(pair.Value));
            }

            document["rates"] = rates;
        }

        return document;
    }

    private static BsonArray WriteItems(IEnumerable<ItemMetric> items)
    {
        var array = new BsonArray();
        foreach (var item in items)
        {
            array.Add(new BsonDocument
            {
                ["title"] = item.Title is null ? BsonNull.Value : item.Title,
                ["amount"] = new BsonDecimal128(item.Amount)
            });
        }

        return array;
    }

    private static DataPoint Read(BsonDocument document)
    {
        var point = new DataPoint
        {
            Id = DataPointIdReaderConverter.Convert(document["_id"].AsBsonDocument)
        };
        if (document.Contains("incomes") && document["incomes"].IsBsonArray)
        {
            point.Incomes = ReadItems(document["incomes"].AsBsonArray);
        }

        if (document.Contains("expenses") && document["expenses"].IsBsonArray)
        {
            point.Expenses = ReadItems(document["expenses"].AsBsonArray);
        }

        if (document.Contains("statistics") && document["statistics"].IsBsonDocument)
        {
            point.Statistics = new Dictionary<StatisticMetric, decimal>();
            foreach (var element in document["statistics"].AsBsonDocument)
            {
                point.Statistics[Enum.Parse<StatisticMetric>(element.Name)] = ReadDecimal(element.Value);
            }
        }

        if (document.Contains("rates") && document["rates"].IsBsonDocument)
        {
            point.Rates = new Dictionary<Currency, decimal>();
            foreach (var element in document["rates"].AsBsonDocument)
            {
                point.Rates[Enum.Parse<Currency>(element.Name)] = ReadDecimal(element.Value);
            }
        }

        return point;
    }

    private static HashSet<ItemMetric> ReadItems(BsonArray array)
    {
        var items = new HashSet<ItemMetric>();
        foreach (var value in array)
        {
            var item = value.AsBsonDocument;
            var title = item.Contains("title") && item["title"].IsString ? item["title"].AsString : string.Empty;
            var amount = item.Contains("amount") ? ReadDecimal(item["amount"]) : 0m;
            items.Add(new ItemMetric(title, amount));
        }

        return items;
    }

    private static decimal ReadDecimal(BsonValue value)
    {
        return value.BsonType switch
        {
            BsonType.Decimal128 => value.AsDecimal,
            BsonType.Double => (decimal)value.AsDouble,
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            BsonType.String => decimal.Parse(value.AsString, System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException("Unsupported numeric BSON type " + value.BsonType)
        };
    }
}
