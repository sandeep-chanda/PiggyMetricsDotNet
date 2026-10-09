using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace PiggyMetrics.Shared.Mongo;

// The source's spring.data.mongodb.* keys, carried over key for key.
public sealed class MongoStoreOptions
{
	public const string SectionName = "Spring:Data:MongoDB";

	public string Host { get; set; } = "localhost";

	public int Port { get; set; } = 27017;

	public string Database { get; set; } = "piggymetrics";

	public string Username { get; set; } = string.Empty;

	public string Password { get; set; } = string.Empty;

	public string ConnectionString { get; set; } = string.Empty;

	// The source authenticates against the same database it reads, because mongodb/init.sh creates the user inside
	// the piggymetrics database.
	public string BuildConnectionString()
	{
		if (!string.IsNullOrWhiteSpace(ConnectionString))
		{
			return ConnectionString;
		}

		var database = Uri.EscapeDataString(Database);
		if (string.IsNullOrEmpty(Username))
		{
			return $"mongodb://{Host}:{Port}/{database}";
		}

		var user = Uri.EscapeDataString(Username);
		var password = Uri.EscapeDataString(Password);
		return $"mongodb://{user}:{password}@{Host}:{Port}/{database}?authSource={database}";
	}
}

public static class MongoExtensions
{
	public static IServiceCollection AddPiggyMetricsStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		MongoConventions.Register();

		services.Configure<MongoStoreOptions>(configuration.GetSection(MongoStoreOptions.SectionName));

		services.AddSingleton<IMongoClient>(provider =>
		{
			var options = provider.GetRequiredService<IOptions<MongoStoreOptions>>().Value;
			return new MongoClient(options.BuildConnectionString());
		});

		services.AddSingleton(provider =>
		{
			var options = provider.GetRequiredService<IOptions<MongoStoreOptions>>().Value;
			return provider.GetRequiredService<IMongoClient>().GetDatabase(options.Database);
		});

		return services;
	}

	public static IMongoCollection<TDocument> GetDocuments<TDocument>(this IMongoDatabase database)
	{
		return database.GetCollection<TDocument>(MongoConventions.CollectionName<TDocument>());
	}
}
