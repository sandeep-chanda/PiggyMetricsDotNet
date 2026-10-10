using MongoDB.Bson;
using MongoDB.Driver;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.Shared.Store;

namespace PiggyMetrics.AccountService.Repository;

public sealed class MongoAccountRepository : AccountRepository
{
    private readonly IMongoCollection<BsonDocument> _documents;

    public MongoAccountRepository(IMongoDatabase database)
    {
        _documents = database.GetCollection<BsonDocument>(MongoCollectionNames.Accounts);
    }

    public Account? FindByName(string name)
    {
        var document = _documents.Find(new BsonDocument("_id", name)).FirstOrDefault();
        return document is null ? null : Read(document);
    }

    public void Save(Account account)
    {
        var name = account.Name ?? throw new ArgumentException("name required");
        var document = Write(account);
        _documents.ReplaceOne(
            new BsonDocument("_id", name),
            document,
            new ReplaceOptions { IsUpsert = true });
    }

    private static BsonDocument Write(Account account)
    {
        var document = new BsonDocument
        {
            ["_id"] = account.Name
        };
        if (account.LastSeen is { } lastSeen)
        {
            document["lastSeen"] = new BsonDateTime(lastSeen.UtcDateTime);
        }

        if (account.Note is not null)
        {
            document["note"] = account.Note;
        }

        if (account.Incomes is not null)
        {
            document["incomes"] = WriteItems(account.Incomes);
        }

        if (account.Expenses is not null)
        {
            document["expenses"] = WriteItems(account.Expenses);
        }

        if (account.Saving is not null)
        {
            document["saving"] = WriteSaving(account.Saving);
        }

        return document;
    }

    private static BsonArray WriteItems(IEnumerable<Item> items)
    {
        var array = new BsonArray();
        foreach (var item in items)
        {
            var document = new BsonDocument();
            if (item.Title is not null)
            {
                document["title"] = item.Title;
            }

            if (item.Amount is { } amount)
            {
                document["amount"] = new BsonDecimal128(amount);
            }

            if (item.Currency is { } currency)
            {
                document["currency"] = currency.ToString();
            }

            if (item.Period is { } period)
            {
                document["period"] = period.ToString();
            }

            if (item.Icon is not null)
            {
                document["icon"] = item.Icon;
            }

            array.Add(document);
        }

        return array;
    }

    private static BsonDocument WriteSaving(Saving saving)
    {
        var document = new BsonDocument();
        if (saving.Amount is { } amount)
        {
            document["amount"] = new BsonDecimal128(amount);
        }

        if (saving.Currency is { } currency)
        {
            document["currency"] = currency.ToString();
        }

        if (saving.Interest is { } interest)
        {
            document["interest"] = new BsonDecimal128(interest);
        }

        if (saving.Deposit is { } deposit)
        {
            document["deposit"] = deposit;
        }

        if (saving.Capitalization is { } capitalization)
        {
            document["capitalization"] = capitalization;
        }

        return document;
    }

    private static Account Read(BsonDocument document)
    {
        var name = document.Contains("_id") && document["_id"].IsString
            ? document["_id"].AsString
            : null;
        var account = new Account
        {
            Name = name
        };
        if (document.Contains("lastSeen") && document["lastSeen"].IsValidDateTime)
        {
            account.LastSeen = new DateTimeOffset(document["lastSeen"].ToUniversalTime());
        }

        if (document.Contains("note") && document["note"].IsString)
        {
            account.Note = document["note"].AsString;
        }

        if (document.Contains("incomes") && document["incomes"].IsBsonArray)
        {
            account.Incomes = ReadItems(document["incomes"].AsBsonArray);
        }

        if (document.Contains("expenses") && document["expenses"].IsBsonArray)
        {
            account.Expenses = ReadItems(document["expenses"].AsBsonArray);
        }

        if (document.Contains("saving") && document["saving"].IsBsonDocument)
        {
            account.Saving = ReadSaving(document["saving"].AsBsonDocument);
        }

        return account;
    }

    private static List<Item> ReadItems(BsonArray array)
    {
        var items = new List<Item>();
        foreach (var value in array)
        {
            if (!value.IsBsonDocument)
            {
                continue;
            }

            var document = value.AsBsonDocument;
            items.Add(new Item
            {
                Title = ReadString(document, "title"),
                Amount = document.Contains("amount") ? ReadDecimal(document["amount"]) : null,
                Currency = ReadEnum<Currency>(document, "currency"),
                Period = ReadEnum<TimePeriod>(document, "period"),
                Icon = ReadString(document, "icon")
            });
        }

        return items;
    }

    private static Saving ReadSaving(BsonDocument document)
    {
        return new Saving
        {
            Amount = document.Contains("amount") ? ReadDecimal(document["amount"]) : null,
            Currency = ReadEnum<Currency>(document, "currency"),
            Interest = document.Contains("interest") ? ReadDecimal(document["interest"]) : null,
            Deposit = document.Contains("deposit") && document["deposit"].IsBoolean ? document["deposit"].AsBoolean : null,
            Capitalization = document.Contains("capitalization") && document["capitalization"].IsBoolean
                ? document["capitalization"].AsBoolean
                : null
        };
    }

    private static string? ReadString(BsonDocument document, string name)
    {
        return document.Contains(name) && document[name].IsString ? document[name].AsString : null;
    }

    private static TEnum? ReadEnum<TEnum>(BsonDocument document, string name)
        where TEnum : struct, Enum
    {
        if (!document.Contains(name) || !document[name].IsString)
        {
            return null;
        }

        return Enum.Parse<TEnum>(document[name].AsString);
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
