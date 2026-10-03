using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace PiggyMetrics.Shared.Mongo;

/// <summary>Declares the collection a document lives in, mirroring @Document(collection = "...").</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MongoCollectionAttribute : Attribute
{
    public MongoCollectionAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }
}

/// <summary>One service's store settings, carrying the source's spring.data.mongodb values.</summary>
public sealed class MongoStoreOptions
{
    public const string SectionName = "Mongo";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 27017;
    public string Database { get; set; } = "piggymetrics";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ConnectionString { get; set; }

    public MongoClientSettings ToClientSettings()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return MongoClientSettings.FromConnectionString(ConnectionString);
        }

        var settings = new MongoClientSettings { Server = new MongoServerAddress(Host, Port) };
        if (!string.IsNullOrWhiteSpace(Username))
        {
            // Spring Boot authenticates against the configured database, so the auth source is Database.
            settings.Credential = MongoCredential.CreateCredential(Database, Username, Password ?? string.Empty);
        }

        return settings;
    }
}

internal sealed class DictionaryRepresentationConvention : ConventionBase, IMemberMapConvention
{
    private readonly DictionaryRepresentation _representation;

    public DictionaryRepresentationConvention(DictionaryRepresentation representation)
        : base("PiggyDictionaryRepresentation") => _representation = representation;

    public void Apply(BsonMemberMap memberMap)
    {
        if (memberMap.GetSerializer() is IDictionaryRepresentationConfigurable configurable)
        {
            memberMap.SetSerializer(configurable.WithDictionaryRepresentation(_representation));
        }
    }
}

/// <summary>Reports the store the way Spring Boot's MongoHealthIndicator does, through buildInfo.</summary>
public sealed class MongoStoreHealthCheck : IHealthCheck
{
    private readonly IMongoDatabase _database;

    public MongoStoreHealthCheck(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _database
                .RunCommandAsync<BsonDocument>(new BsonDocument("buildInfo", 1), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, exception.Message, exception);
        }
    }
}

/// <summary>Document conventions, serializers and registration for the source's stores (decisions D-008, D-009).</summary>
public static class PiggyMongo
{
    public const string ConventionPackName = "piggymetrics";
    public const string HealthCheckName = "mongo";

    private static int _conventionsRegistered;

    public static void RegisterConventions()
    {
        if (Interlocked.Exchange(ref _conventionsRegistered, 1) == 1)
        {
            return;
        }

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            // topLevelOnly: false also rewrites the enum key serializer of a dictionary member, which a
            // Java Map with an enum key needs; the one-argument constructor leaves those keys as Int32.
            new EnumRepresentationConvention(BsonType.String, topLevelOnly: false),
            new IgnoreIfNullConvention(false),
            new DictionaryRepresentationConvention(DictionaryRepresentation.Document),
        };
        ConventionRegistry.Register(ConventionPackName, pack, _ => true);

        // Spring Data wrote BigDecimal through BigDecimalToStringConverter, so decimal is a BSON string.
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.String));
        BsonSerializer.TryRegisterSerializer(new NullableSerializer<decimal>(new DecimalSerializer(BsonType.String)));
        BsonSerializer.TryRegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
    }

    public static string CollectionNameOf<TDocument>()
    {
        var attribute = typeof(TDocument).GetCustomAttribute<MongoCollectionAttribute>(inherit: false);
        return attribute is null
            ? throw new InvalidOperationException($"{typeof(TDocument).FullName} carries no [MongoCollection] attribute.")
            : attribute.Name;
    }

    public static IMongoCollection<TDocument> GetCollectionFor<TDocument>(this IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return database.GetCollection<TDocument>(CollectionNameOf<TDocument>());
    }

    public static IServiceCollection AddPiggyMongo(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        RegisterConventions();
        services.AddOptions<MongoStoreOptions>().Bind(configuration.GetSection(MongoStoreOptions.SectionName));
        services.AddSingleton<IMongoClient>(provider =>
            new MongoClient(provider.GetRequiredService<IOptions<MongoStoreOptions>>().Value.ToClientSettings()));
        services.AddSingleton(provider =>
            provider.GetRequiredService<IMongoClient>()
                .GetDatabase(provider.GetRequiredService<IOptions<MongoStoreOptions>>().Value.Database));
        services.AddHealthChecks().AddCheck<MongoStoreHealthCheck>(HealthCheckName, HealthStatus.Unhealthy);

        return services;
    }
}
