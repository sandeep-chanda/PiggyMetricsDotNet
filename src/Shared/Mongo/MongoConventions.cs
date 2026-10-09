using System.Globalization;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace PiggyMetrics.Shared.Mongo;

// Names the collection a document type lives in, so the .NET services keep the collection names the source's
// @Document(collection = "...") annotations established.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MongoCollectionAttribute : Attribute
{
	public MongoCollectionAttribute(string name)
	{
		Name = name;
	}

	public string Name { get; }
}

// Stores a decimal the way Spring Data MongoDB's BigDecimalToStringConverter does: as a BSON string. Reads
// tolerate the numeric shapes a hand-seeded document carries.
public sealed class BigDecimalSerializer : SerializerBase<decimal>
{
	public override decimal Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
	{
		var reader = context.Reader;
		var bsonType = reader.GetCurrentBsonType();
		switch (bsonType)
		{
			case BsonType.String:
				return decimal.Parse(reader.ReadString(), NumberStyles.Float, CultureInfo.InvariantCulture);
			case BsonType.Double:
				return (decimal)reader.ReadDouble();
			case BsonType.Int32:
				return reader.ReadInt32();
			case BsonType.Int64:
				return reader.ReadInt64();
			case BsonType.Decimal128:
				return (decimal)reader.ReadDecimal128();
			default:
				throw new FormatException($"A decimal cannot be read from BSON type {bsonType}.");
		}
	}

	public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, decimal value)
	{
		context.Writer.WriteString(value.ToString(CultureInfo.InvariantCulture));
	}
}

// One registration point for the document conventions every PiggyMetrics store shares.
public static class MongoConventions
{
	public const string ConventionPackName = "piggymetrics";

	private static int _registered;

	public static void Register()
	{
		if (Interlocked.Exchange(ref _registered, 1) == 1)
		{
			return;
		}

		BsonSerializer.RegisterSerializer(typeof(decimal), new BigDecimalSerializer());
		BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new BigDecimalSerializer()));
		BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
		BsonSerializer.RegisterSerializer(
			typeof(DateTime?),
			new NullableSerializer<DateTime>(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime)));

		var pack = new ConventionPack
		{
			new CamelCaseElementNameConvention(),
			new IgnoreExtraElementsConvention(true),
			new IgnoreIfNullConvention(true),
			new EnumRepresentationConvention(BsonType.String),
		};

		ConventionRegistry.Register(ConventionPackName, pack, _ => true);
	}

	public static string CollectionName<TDocument>()
	{
		var attribute = typeof(TDocument).GetCustomAttribute<MongoCollectionAttribute>();
		if (attribute is null)
		{
			throw new InvalidOperationException(
				$"{typeof(TDocument).FullName} must carry [MongoCollection] so its collection name stays pinned.");
		}

		return attribute.Name;
	}
}
