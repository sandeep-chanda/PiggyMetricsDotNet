using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace PiggyMetrics.Shared.Store;

public static class MongoCollectionNames
{
    public const string Accounts = "accounts";
    public const string Users = "users";
    public const string Datapoints = "datapoints";
    public const string Recipients = "recipients";
    public const string Database = "piggymetrics";
}

public static class MongoIdFields
{
    public const string AccountName = "name";
    public const string UserUsername = "username";
    public const string RecipientAccountName = "accountName";
    public const string DataPointId = "id";
}

public static class MongoConventions
{
    private static int _registered;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1)
        {
            return;
        }

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String)
        };
        ConventionRegistry.Register("PiggyMetricsShared", pack, _ => true);
        BsonSerializer.RegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
    }
}
