using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PiggyMetrics.Shared.Json;
using PiggyMetrics.Shared.Mongo;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public sealed class PiggySerializationTests
{
    private static readonly DateTime SampleMoment =
        new(2018, 6, 13, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DateTimeIsWrittenInTheSourceFormat()
    {
        var json = JsonSerializer.Serialize(new SamplePayload { LastSeen = SampleMoment }, PiggyJson.Default);

        Assert.Contains("\"lastSeen\":\"2018-06-13T11:00:00.000+0000\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2018-06-13T11:00:00.000+0000")]
    [InlineData("2018-06-13T11:00:00.000+00:00")]
    [InlineData("2018-06-13T11:00:00.000Z")]
    public void DateTimeIsReadFromEveryOffsetFormTheSourceEmits(string text)
    {
        var payload = JsonSerializer.Deserialize<SamplePayload>($"{{\"lastSeen\":\"{text}\"}}", PiggyJson.Default);

        Assert.Equal(SampleMoment, payload!.LastSeen);
    }

    [Fact]
    public void DateTimeIsReadFromEpochMilliseconds()
    {
        var payload = JsonSerializer.Deserialize<SamplePayload>("{\"lastSeen\":1528887600000}", PiggyJson.Default);

        Assert.Equal(SampleMoment, payload!.LastSeen);
    }

    [Fact]
    public void EnumsAreWrittenAsNames()
    {
        var json = JsonSerializer.Serialize(new SamplePayload { Currency = SampleCurrency.EUR }, PiggyJson.Default);

        Assert.Contains("\"currency\":\"EUR\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownMembersAreSkipped()
    {
        var payload = JsonSerializer.Deserialize<SamplePayload>(
            "{\"currency\":\"RUB\",\"thisMemberDoesNotExist\":42}", PiggyJson.Default);

        Assert.Equal(SampleCurrency.RUB, payload!.Currency);
    }

    [Fact]
    public void DocumentsCarryTheSourceCollectionNameAndShape()
    {
        PiggyMongo.RegisterConventions();

        Assert.Equal("accounts", PiggyMongo.CollectionNameOf<SampleAccount>());

        var document = new SampleAccount
        {
            Name = "demo",
            LastSeen = SampleMoment,
            Note = null,
            Saving = 1.5m,
            Currency = SampleCurrency.USD,
            Rates = new Dictionary<SampleCurrency, decimal> { [SampleCurrency.EUR] = 0.85m },
        }.ToBsonDocument();

        Assert.Equal("demo", document["_id"].AsString);
        Assert.Equal(SampleMoment, document["lastSeen"].ToUniversalTime());
        Assert.Equal(BsonNull.Value, document["note"]);
        Assert.Equal("1.5", document["saving"].AsString);
        Assert.Equal("USD", document["currency"].AsString);
        Assert.Equal("0.85", document["rates"]["EUR"].AsString);
        Assert.False(document.Contains("Name"));
    }

    private sealed class SamplePayload
    {
        public DateTime LastSeen { get; set; }

        public SampleCurrency Currency { get; set; }
    }

    [MongoCollection("accounts")]
    private sealed class SampleAccount
    {
        [BsonId]
        public string Name { get; set; } = string.Empty;

        public DateTime LastSeen { get; set; }

        public string? Note { get; set; }

        public decimal Saving { get; set; }

        public SampleCurrency Currency { get; set; }

        public Dictionary<SampleCurrency, decimal> Rates { get; set; } = new();
    }

    private enum SampleCurrency
    {
        USD,
        EUR,
        RUB,
    }
}
