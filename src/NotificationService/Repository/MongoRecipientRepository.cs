using MongoDB.Bson;
using MongoDB.Driver;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Repository.Converter;
using PiggyMetrics.Shared.Store;

namespace PiggyMetrics.NotificationService.Repository;

public sealed class MongoRecipientRepository : RecipientRepository
{
    private readonly IMongoCollection<BsonDocument> _documents;

    public MongoRecipientRepository(IMongoDatabase database)
    {
        _documents = database.GetCollection<BsonDocument>(MongoCollectionNames.Recipients);
    }

    public Recipient? FindByAccountName(string name)
    {
        var document = _documents.Find(new BsonDocument("_id", name)).FirstOrDefault();
        return document is null ? null : Read(document);
    }

    public void Save(Recipient recipient)
    {
        var name = recipient.AccountName ?? throw new ArgumentException("accountName required");
        var document = Write(recipient);
        _documents.ReplaceOne(
            new BsonDocument("_id", name),
            document,
            new ReplaceOptions { IsUpsert = true });
    }

    public List<Recipient> FindReadyForBackup()
    {
        return FindReady("BACKUP");
    }

    public List<Recipient> FindReadyForRemind()
    {
        return FindReady("REMIND");
    }

    private List<Recipient> FindReady(string type)
    {
        var filter = new BsonDocument("$and", new BsonArray
        {
            new BsonDocument("scheduledNotifications." + type + ".active", true),
            new BsonDocument(
                "$where",
                "this.scheduledNotifications." + type + ".lastNotified < new Date(new Date().setDate(new Date().getDate() - this.scheduledNotifications." + type + ".frequency ))")
        });
        return _documents.Find(filter).ToList().Select(Read).ToList();
    }

    private static BsonDocument Write(Recipient recipient)
    {
        var document = new BsonDocument
        {
            ["_id"] = recipient.AccountName
        };
        if (recipient.Email is not null)
        {
            document["email"] = recipient.Email;
        }

        if (recipient.ScheduledNotifications is not null)
        {
            var scheduled = new BsonDocument();
            foreach (var pair in recipient.ScheduledNotifications)
            {
                scheduled[pair.Key.ToString()] = WriteSettings(pair.Value);
            }

            document["scheduledNotifications"] = scheduled;
        }

        return document;
    }

    private static BsonDocument WriteSettings(NotificationSettings settings)
    {
        var document = new BsonDocument();
        if (settings.Active is { } active)
        {
            document["active"] = active;
        }

        if (settings.Frequency is { } frequency)
        {
            document["frequency"] = FrequencyWriterConverter.Convert(frequency);
        }

        if (settings.LastNotified is { } lastNotified)
        {
            document["lastNotified"] = ToBsonDate(lastNotified);
        }

        return document;
    }

    private static BsonDateTime ToBsonDate(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        var ticks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond);
        return new BsonDateTime(new DateTime(ticks, DateTimeKind.Utc));
    }

    private static Recipient Read(BsonDocument document)
    {
        var accountName = document.Contains("_id") && document["_id"].IsString
            ? document["_id"].AsString
            : null;
        var recipient = new Recipient
        {
            AccountName = accountName
        };
        if (document.Contains("email") && document["email"].IsString)
        {
            recipient.Email = document["email"].AsString;
        }

        if (document.Contains("scheduledNotifications") && document["scheduledNotifications"].IsBsonDocument)
        {
            recipient.ScheduledNotifications = ReadScheduled(document["scheduledNotifications"].AsBsonDocument);
        }

        return recipient;
    }

    private static Dictionary<NotificationType, NotificationSettings> ReadScheduled(BsonDocument document)
    {
        var scheduled = new Dictionary<NotificationType, NotificationSettings>();
        foreach (var element in document.Elements)
        {
            if (!Enum.TryParse<NotificationType>(element.Name, out var type) || !element.Value.IsBsonDocument)
            {
                continue;
            }

            scheduled[type] = ReadSettings(element.Value.AsBsonDocument);
        }

        return scheduled;
    }

    private static NotificationSettings ReadSettings(BsonDocument document)
    {
        var settings = new NotificationSettings();
        if (document.Contains("active") && document["active"].IsBoolean)
        {
            settings.Active = document["active"].AsBoolean;
        }

        if (document.Contains("frequency"))
        {
            settings.Frequency = FrequencyReaderConverter.Convert(ReadInt(document["frequency"]));
        }

        if (document.Contains("lastNotified") && document["lastNotified"].IsValidDateTime)
        {
            var utc = document["lastNotified"].ToUniversalTime();
            settings.LastNotified = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        }

        return settings;
    }

    private static int ReadInt(BsonValue value)
    {
        return value.BsonType switch
        {
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => (int)value.AsInt64,
            BsonType.Double => (int)value.AsDouble,
            _ => throw new ArgumentException()
        };
    }
}
