using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PiggyMetrics.Shared.Store;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class MongoConventionsTests
{
    [Fact]
    public void Collection_database_and_id_names_match_source_documents()
    {
        Assert.Equal("accounts", MongoCollectionNames.Accounts);
        Assert.Equal("users", MongoCollectionNames.Users);
        Assert.Equal("datapoints", MongoCollectionNames.Datapoints);
        Assert.Equal("recipients", MongoCollectionNames.Recipients);
        Assert.Equal("piggymetrics", MongoCollectionNames.Database);

        Assert.Equal("name", MongoIdFields.AccountName);
        Assert.Equal("username", MongoIdFields.UserUsername);
        Assert.Equal("accountName", MongoIdFields.RecipientAccountName);
        Assert.Equal("id", MongoIdFields.DataPointId);
    }

    [Fact]
    public void Register_is_idempotent_and_applies_shared_conventions()
    {
        MongoConventions.Register();
        var second = Record.Exception(() => MongoConventions.Register());
        Assert.Null(second);

        var account = new ConventionProbeAccount
        {
            Name = "demo",
            Currency = ConventionProbeCurrency.USD,
            Created = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        };
        var bson = account.ToBsonDocument();
        Assert.Equal("demo", bson["name"].AsString);
        Assert.Equal("USD", bson["currency"].AsString);
        Assert.Equal(BsonType.DateTime, bson["created"].BsonType);
        Assert.False(bson.Contains("Name"));

        var withExtra = BsonDocument.Parse("{ \"name\": \"demo\", \"currency\": \"EUR\", \"extra\": 1 }");
        var back = BsonSerializer.Deserialize<ConventionProbeAccount>(withExtra);
        Assert.Equal("demo", back.Name);
        Assert.Equal(ConventionProbeCurrency.EUR, back.Currency);
    }

    private enum ConventionProbeCurrency
    {
        USD,
        EUR
    }

    private sealed class ConventionProbeAccount
    {
        public string Name { get; set; } = "";
        public ConventionProbeCurrency Currency { get; set; }
        public DateTime Created { get; set; }
    }
}
