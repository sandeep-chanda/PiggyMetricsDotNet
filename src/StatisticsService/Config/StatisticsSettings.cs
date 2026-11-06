using PiggyMetrics.Shared.Store;

namespace PiggyMetrics.StatisticsService.Config;

public sealed class StatisticsMongoSettings
{
    public required string ConnectionString { get; init; }

    public required string Database { get; init; }

    public static StatisticsMongoSettings From(IConfiguration configuration)
    {
        var host = configuration["spring:data:mongodb:host"] ?? "statistics-mongodb";
        var port = configuration["spring:data:mongodb:port"] ?? "27017";
        var database = configuration["spring:data:mongodb:database"] ?? MongoCollectionNames.Database;
        var username = configuration["spring:data:mongodb:username"] ?? "user";
        var password = StatisticsSettings.ResolvePlaceholder(configuration["spring:data:mongodb:password"]);
        if (string.IsNullOrEmpty(password))
        {
            password = Environment.GetEnvironmentVariable("MONGODB_PASSWORD");
        }

        var databasePath = Uri.EscapeDataString(database);
        string connectionString;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            connectionString = $"mongodb://{host}:{port}/{databasePath}";
        }
        else
        {
            connectionString =
                $"mongodb://{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}@{host}:{port}/{databasePath}?authSource={databasePath}";
        }

        return new StatisticsMongoSettings
        {
            ConnectionString = connectionString,
            Database = database
        };
    }
}

public static class StatisticsSettings
{
    public static string? ResolvePlaceholder(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}'))
        {
            var name = value[2..^1];
            return Environment.GetEnvironmentVariable(name) ?? string.Empty;
        }

        return value;
    }
}
