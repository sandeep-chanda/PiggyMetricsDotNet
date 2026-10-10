using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using PiggyMetrics.AccountService.Config;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.AccountService.Tests;

public class AccountServiceApplicationTests
{
    [Fact]
    public void contextLoads()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
    }

    [Fact]
    public void settings_match_account_service_and_bootstrap()
    {
        using var factory = new WebApplicationFactory<Program>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("account-service", configuration["spring:application:name"]);
        Assert.Equal("http://config:8888", configuration["spring:cloud:config:uri"]);
        Assert.True(bool.Parse(configuration["spring:cloud:config:fail-fast"]!));
        Assert.Equal("user", configuration["spring:cloud:config:username"]);
        Assert.Equal("${CONFIG_SERVICE_PASSWORD}", configuration["spring:cloud:config:password"]);
        Assert.Equal("account-mongodb", configuration["spring:data:mongodb:host"]);
        Assert.Equal("user", configuration["spring:data:mongodb:username"]);
        Assert.Equal("${MONGODB_PASSWORD}", configuration["spring:data:mongodb:password"]);
        Assert.Equal("piggymetrics", configuration["spring:data:mongodb:database"]);
        Assert.Equal("27017", configuration["spring:data:mongodb:port"]);
        Assert.Equal("/accounts", configuration["server:servlet:context-path"]);
        Assert.Equal("6000", configuration["server:port"]);
        Assert.Equal("account-service", configuration["security:oauth2:client:clientId"]);
        Assert.Equal("${ACCOUNT_SERVICE_PASSWORD}", configuration["security:oauth2:client:clientSecret"]);
        Assert.Equal("http://auth-service:5000/uaa/oauth/token", configuration["security:oauth2:client:accessTokenUri"]);
        Assert.Equal("client_credentials", configuration["security:oauth2:client:grant-type"]);
        Assert.Equal("server", configuration["security:oauth2:client:scope"]);
        Assert.True(bool.Parse(configuration["feign:hystrix:enabled"]!));
    }

    [Fact]
    public void mongo_credentials_authenticate_against_the_service_database()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["spring:data:mongodb:host"] = "account-mongodb",
                ["spring:data:mongodb:port"] = "27017",
                ["spring:data:mongodb:database"] = "piggymetrics",
                ["spring:data:mongodb:username"] = "user",
                ["spring:data:mongodb:password"] = "s3cret"
            })
            .Build();

        var settings = AccountMongoSettings.From(configuration);
        var url = new MongoUrl(settings.ConnectionString);

        Assert.Equal("piggymetrics", settings.Database);
        Assert.Equal("piggymetrics", url.DatabaseName);
        Assert.Equal("piggymetrics", url.AuthenticationSource);
        Assert.Equal("user", url.Username);
    }

    [Fact]
    public void edge_client_timeouts_match_source()
    {
        using var factory = new WebApplicationFactory<Program>();
        var clients = factory.Services.GetRequiredService<IHttpClientFactory>();
        var auth = clients.CreateClient("auth-service");
        var statistics = clients.CreateClient("statistics-service");

        Assert.Equal(EdgeHttpDefaults.Timeout, auth.Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(10000), auth.Timeout);
        Assert.Equal(new Uri("http://auth-service:5000/"), auth.BaseAddress);
        Assert.Equal(EdgeHttpDefaults.Timeout, statistics.Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(10000), statistics.Timeout);
        Assert.Equal(new Uri("http://statistics-service:7000/"), statistics.BaseAddress);
    }
}
