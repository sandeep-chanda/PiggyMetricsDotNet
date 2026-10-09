using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using PiggyMetrics.Shared.Mongo;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Mongo;

// Registers the shared conventions once for the whole collection, because BsonSerializer and
// ConventionRegistry are process-wide and class maps freeze on first use.
public sealed class MongoConventionsFixture
{
	public MongoConventionsFixture()
	{
		MongoConventions.Register();
	}
}

[CollectionDefinition(Name)]
public sealed class MongoConventionsCollection : ICollectionFixture<MongoConventionsFixture>
{
	public const string Name = "mongo-conventions";
}

[MongoCollection("accounts")]
public sealed class AccountDocumentStub
{
	public string? Name { get; set; }

	public decimal Saving { get; set; }

	public decimal? Bonus { get; set; }

	public DateTime LastSeen { get; set; }

	public CurrencyStub Currency { get; set; }

	public string? Note { get; set; }
}

public sealed class DocumentWithoutACollectionStub
{
	public string? Name { get; set; }
}

public enum CurrencyStub
{
	USD,
	EUR,
	RUB,
}

// P1.T3 - the store conventions and serializers reproduce the write shape Spring Data MongoDB produced, so the
// .NET services read and write the documents the source left behind.
[Collection(MongoConventionsCollection.Name)]
public sealed class MongoConventionsTests
{
	private static readonly DateTime LastSeen = new(2018, 6, 1, 12, 0, 0, DateTimeKind.Utc);

	private static AccountDocumentStub Sample()
	{
		return new AccountDocumentStub
		{
			Name = "demo",
			Saving = 1500.50m,
			Bonus = null,
			LastSeen = LastSeen,
			Currency = CurrencyStub.EUR,
			Note = null,
		};
	}

	[Fact]
	public void ShouldWriteElementNamesInCamelCase()
	{
		var document = Sample().ToBsonDocument();

		Assert.Equal(
			new[] { "name", "saving", "lastSeen", "currency" },
			document.Names.ToArray());
	}

	[Fact]
	public void ShouldWriteADecimalAsABsonString()
	{
		var document = Sample().ToBsonDocument();

		Assert.Equal(BsonType.String, document["saving"].BsonType);
		Assert.Equal("1500.50", document["saving"].AsString);
	}

	[Fact]
	public void ShouldWriteADateAsAUtcBsonDateTime()
	{
		var document = Sample().ToBsonDocument();

		Assert.Equal(BsonType.DateTime, document["lastSeen"].BsonType);
		Assert.Equal(LastSeen, document["lastSeen"].ToUniversalTime());
	}

	[Fact]
	public void ShouldWriteAnEnumAsItsName()
	{
		var document = Sample().ToBsonDocument();

		Assert.Equal(BsonType.String, document["currency"].BsonType);
		Assert.Equal("EUR", document["currency"].AsString);
	}

	[Fact]
	public void ShouldOmitANullMember()
	{
		var document = Sample().ToBsonDocument();

		Assert.False(document.Contains("note"));
		Assert.False(document.Contains("bonus"));
	}

	[Fact]
	public void ShouldIgnoreElementsTheDocumentClassDoesNotDeclare()
	{
		var stored = Sample().ToBsonDocument();
		stored.Add("legacyField", "written by the Java service");
		stored.Add("note", BsonNull.Value);

		var read = BsonSerializer.Deserialize<AccountDocumentStub>(stored);

		Assert.Equal("demo", read.Name);
		Assert.Null(read.Note);
	}

	[Fact]
	public void ShouldRoundTripADocumentThroughTheConventions()
	{
		var read = BsonSerializer.Deserialize<AccountDocumentStub>(Sample().ToBsonDocument());

		Assert.Equal("demo", read.Name);
		Assert.Equal(1500.50m, read.Saving);
		Assert.Null(read.Bonus);
		Assert.Equal(LastSeen, read.LastSeen);
		Assert.Equal(DateTimeKind.Utc, read.LastSeen.Kind);
		Assert.Equal(CurrencyStub.EUR, read.Currency);
	}

	[Fact]
	public void ShouldReadADecimalLeftBehindAsAString()
	{
		Assert.Equal(1500.50m, ReadDecimal(new BsonString("1500.50")));
	}

	[Fact]
	public void ShouldReadADecimalLeftBehindAsANumber()
	{
		Assert.Equal(1500.5m, ReadDecimal(new BsonDouble(1500.5d)));
		Assert.Equal(1500m, ReadDecimal(new BsonInt32(1500)));
		Assert.Equal(1500m, ReadDecimal(new BsonInt64(1500L)));
		Assert.Equal(1500.50m, ReadDecimal(new BsonDecimal128(1500.50m)));
	}

	[Fact]
	public void ShouldRefuseADecimalLeftBehindAsSomethingElse()
	{
		Assert.Throws<FormatException>(() => ReadDecimal(BsonBoolean.True));
	}

	[Fact]
	public void ShouldWriteADecimalWithInvariantFormatting()
	{
		Assert.Equal("1234.56", WriteDecimal(1234.56m));
		Assert.Equal("-0.01", WriteDecimal(-0.01m));
		Assert.Equal("0", WriteDecimal(0m));
	}

	[Fact]
	public void ShouldWriteANullableDecimalAsABsonString()
	{
		var sample = Sample();
		sample.Bonus = 10.25m;

		var document = sample.ToBsonDocument();

		Assert.Equal(BsonType.String, document["bonus"].BsonType);
		Assert.Equal("10.25", document["bonus"].AsString);
	}

	[Fact]
	public void ShouldPinTheCollectionNameOnTheDocumentClass()
	{
		Assert.Equal("accounts", MongoConventions.CollectionName<AccountDocumentStub>());
	}

	[Fact]
	public void ShouldRefuseADocumentClassWithoutAPinnedCollectionName()
	{
		var error = Assert.Throws<InvalidOperationException>(
			() => MongoConventions.CollectionName<DocumentWithoutACollectionStub>());

		Assert.Contains("MongoCollection", error.Message);
	}

	[Fact]
	public void ShouldRegisterTheConventionPackOnlyOnce()
	{
		MongoConventions.Register();
		MongoConventions.Register();

		Assert.Equal("piggymetrics", MongoConventions.ConventionPackName);
	}

	private static decimal ReadDecimal(BsonValue value)
	{
		var document = new BsonDocument("value", value);
		using var reader = new BsonDocumentReader(document);
		var context = BsonDeserializationContext.CreateRoot(reader);
		reader.ReadStartDocument();
		reader.ReadName("value");
		var read = new BigDecimalSerializer().Deserialize(context, default);
		reader.ReadEndDocument();
		return read;
	}

	private static string WriteDecimal(decimal value)
	{
		var document = new BsonDocument();
		using var writer = new BsonDocumentWriter(document);
		var context = BsonSerializationContext.CreateRoot(writer);
		writer.WriteStartDocument();
		writer.WriteName("value");
		new BigDecimalSerializer().Serialize(context, default, value);
		writer.WriteEndDocument();
		return document["value"].AsString;
	}
}
