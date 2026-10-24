using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PiggyMetrics.Shared.Mongo;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Mongo;

// P1.T3 - the store points at the same databases the source pointed at, with the source's
// spring.data.mongodb.* keys carried over key for key.
[Collection(MongoConventionsCollection.Name)]
public sealed class MongoStoreOptionsTests
{
	[Fact]
	public void ShouldDefaultToTheSourcesHostPortAndDatabase()
	{
		var options = new MongoStoreOptions();

		Assert.Equal("localhost", options.Host);
		Assert.Equal(27017, options.Port);
		Assert.Equal("piggymetrics", options.Database);
		Assert.Equal("mongodb://localhost:27017/piggymetrics", options.BuildConnectionString());
	}

	[Fact]
	public void ShouldAuthenticateAgainstTheDatabaseItReads()
	{
		var options = new MongoStoreOptions
		{
			Host = "mongodb",
			Port = 27017,
			Database = "piggymetrics",
			Username = "user",
			Password = "password",
		};

		Assert.Equal(
			"mongodb://user:password@mongodb:27017/piggymetrics?authSource=piggymetrics",
			options.BuildConnectionString());
	}

	[Fact]
	public void ShouldEscapeCredentialsAndDatabaseName()
	{
		var options = new MongoStoreOptions
		{
			Host = "mongodb",
			Database = "piggy metrics",
			Username = "us:er",
			Password = "p@ss word",
		};

		var connectionString = options.BuildConnectionString();

		Assert.Equal(
			"mongodb://us%3Aer:p%40ss%20word@mongodb:27017/piggy%20metrics?authSource=piggy%20metrics",
			connectionString);
	}

	[Fact]
	public void ShouldOmitCredentialsWhenNoUsernameIsConfigured()
	{
		var options = new MongoStoreOptions
		{
			Host = "mongodb",
			Database = "piggymetrics",
			Password = "password",
		};

		Assert.Equal("mongodb://mongodb:27017/piggymetrics", options.BuildConnectionString());
	}

	[Fact]
	public void ShouldPreferAnExplicitConnectionString()
	{
		var options = new MongoStoreOptions
		{
			ConnectionString = "mongodb://elsewhere:27018/other",
			Host = "mongodb",
			Username = "user",
			Password = "password",
		};

		Assert.Equal("mongodb://elsewhere:27018/other", options.BuildConnectionString());
	}

	[Fact]
	public void ShouldBindTheSourcesConfigurationKeys()
	{
		using var provider = BuildProvider(new Dictionary<string, string?>
		{
			["Spring:Data:MongoDB:Host"] = "mongodb",
			["Spring:Data:MongoDB:Port"] = "27017",
			["Spring:Data:MongoDB:Database"] = "piggymetrics",
			["Spring:Data:MongoDB:Username"] = "user",
			["Spring:Data:MongoDB:Password"] = "password",
		});

		var options = provider.GetRequiredService<IOptions<MongoStoreOptions>>().Value;

		Assert.Equal("Spring:Data:MongoDB", MongoStoreOptions.SectionName);
		Assert.Equal("mongodb", options.Host);
		Assert.Equal("piggymetrics", options.Database);
		Assert.Equal("user", options.Username);
		Assert.Equal("password", options.Password);
	}

	[Fact]
	public void ShouldRegisterOneClientAndOneDatabaseForTheConfiguredStore()
	{
		using var provider = BuildProvider(new Dictionary<string, string?>
		{
			["Spring:Data:MongoDB:Host"] = "mongodb",
			["Spring:Data:MongoDB:Database"] = "piggymetrics",
		});

		var client = provider.GetRequiredService<IMongoClient>();
		var database = provider.GetRequiredService<IMongoDatabase>();

		Assert.Same(client, provider.GetRequiredService<IMongoClient>());
		Assert.Same(database, provider.GetRequiredService<IMongoDatabase>());
		Assert.Equal("piggymetrics", database.DatabaseNamespace.DatabaseName);
		Assert.Equal("mongodb", client.Settings.Server.Host);
		Assert.Equal(27017, client.Settings.Server.Port);
	}

	[Fact]
	public void ShouldResolveACollectionByItsPinnedName()
	{
		using var provider = BuildProvider(new Dictionary<string, string?>
		{
			["Spring:Data:MongoDB:Database"] = "piggymetrics",
		});
		var database = provider.GetRequiredService<IMongoDatabase>();

		var collection = database.GetDocuments<AccountDocumentStub>();

		Assert.Equal("accounts", collection.CollectionNamespace.CollectionName);
		Assert.Equal("piggymetrics", collection.CollectionNamespace.DatabaseNamespace.DatabaseName);
	}

	private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
		var services = new ServiceCollection();
		services.AddPiggyMetricsStore(configuration);
		return services.BuildServiceProvider();
	}
}
