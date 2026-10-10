using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using PiggyMetrics.Shared.Http;
using PiggyMetrics.StatisticsService.Config;
using Xunit;

namespace PiggyMetrics.StatisticsService.Tests;

public class StatisticsServiceApplicationTests
{
    [Fact]
    public void contextLoads()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
    }

    [Fact]
    public void settings_match_statistics_service_and_bootstrap()
    {
        using var factory = new WebApplicationFactory<Program>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("statistics-service", configuration["spring:application:name"]);
        Assert.Equal("http://config:8888", configuration["spring:cloud:config:uri"]);
        Assert.True(bool.Parse(configuration["spring:cloud:config:fail-fast"]!));
        Assert.Equal("user", configuration["spring:cloud:config:username"]);
        Assert.Equal("${CONFIG_SERVICE_PASSWORD}", configuration["spring:cloud:config:password"]);
        Assert.Equal("statistics-mongodb", configuration["spring:data:mongodb:host"]);
        Assert.Equal("user", configuration["spring:data:mongodb:username"]);
        Assert.Equal("${MONGODB_PASSWORD}", configuration["spring:data:mongodb:password"]);
        Assert.Equal("piggymetrics", configuration["spring:data:mongodb:database"]);
        Assert.Equal("27017", configuration["spring:data:mongodb:port"]);
        Assert.Equal("/statistics", configuration["server:servlet:context-path"]);
        Assert.Equal("7000", configuration["server:port"]);
        Assert.Equal("statistics-service", configuration["security:oauth2:client:clientId"]);
        Assert.Equal("${STATISTICS_SERVICE_PASSWORD}", configuration["security:oauth2:client:clientSecret"]);
        Assert.Equal("http://auth-service:5000/uaa/oauth/token", configuration["security:oauth2:client:accessTokenUri"]);
        Assert.Equal("client_credentials", configuration["security:oauth2:client:grant-type"]);
        Assert.Equal("server", configuration["security:oauth2:client:scope"]);
        Assert.Equal("https://api.exchangeratesapi.io", configuration["rates:url"]);
    }

    [Fact]
    public void mongo_credentials_authenticate_against_the_service_database()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["spring:data:mongodb:host"] = "statistics-mongodb",
                ["spring:data:mongodb:port"] = "27017",
                ["spring:data:mongodb:database"] = "piggymetrics",
                ["spring:data:mongodb:username"] = "user",
                ["spring:data:mongodb:password"] = "s3cret"
            })
            .Build();

        var settings = StatisticsMongoSettings.From(configuration);
        var url = new MongoUrl(settings.ConnectionString);

        Assert.Equal("piggymetrics", settings.Database);
        Assert.Equal("piggymetrics", url.DatabaseName);
        Assert.Equal("piggymetrics", url.AuthenticationSource);
        Assert.Equal("user", url.Username);
    }

    [Fact]
    public void rates_client_timeout_matches_source()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("rates-client");
        Assert.Equal(EdgeHttpDefaults.Timeout, client.Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(10000), client.Timeout);
        Assert.Equal(new Uri("https://api.exchangeratesapi.io/"), client.BaseAddress);
    }
}
