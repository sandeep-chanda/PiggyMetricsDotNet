using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using PiggyMetrics.NotificationService.Config;
using PiggyMetrics.NotificationService.Service;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.NotificationService.Tests;

public class NotificationServiceApplicationTests
{
    [Fact]
    public void contextLoads()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
    }

    [Fact]
    public void settings_match_notification_service_and_bootstrap()
    {
        using var factory = new WebApplicationFactory<Program>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("notification-service", configuration["spring:application:name"]);
        Assert.Equal("http://config:8888", configuration["spring:cloud:config:uri"]);
        Assert.True(bool.Parse(configuration["spring:cloud:config:fail-fast"]!));
        Assert.Equal("user", configuration["spring:cloud:config:username"]);
        Assert.Equal("${CONFIG_SERVICE_PASSWORD}", configuration["spring:cloud:config:password"]);
        Assert.Equal("notification-mongodb", configuration["spring:data:mongodb:host"]);
        Assert.Equal("user", configuration["spring:data:mongodb:username"]);
        Assert.Equal("${MONGODB_PASSWORD}", configuration["spring:data:mongodb:password"]);
        Assert.Equal("piggymetrics", configuration["spring:data:mongodb:database"]);
        Assert.Equal("27017", configuration["spring:data:mongodb:port"]);
        Assert.Equal("/notifications", configuration["server:servlet:context-path"]);
        Assert.Equal("8000", configuration["server:port"]);
        Assert.Equal("notification-service", configuration["security:oauth2:client:clientId"]);
        Assert.Equal("${NOTIFICATION_SERVICE_PASSWORD}", configuration["security:oauth2:client:clientSecret"]);
        Assert.Equal("http://auth-service:5000/uaa/oauth/token", configuration["security:oauth2:client:accessTokenUri"]);
        Assert.Equal("client_credentials", configuration["security:oauth2:client:grant-type"]);
        Assert.Equal("server", configuration["security:oauth2:client:scope"]);
        Assert.Equal("0 0 0 * * *", configuration["remind:cron"]);
        Assert.Equal("PiggyMetrics reminder", configuration["remind:email:subject"]);
        Assert.Equal("0 0 12 * * *", configuration["backup:cron"]);
        Assert.Equal("PiggyMetrics account backup", configuration["backup:email:subject"]);
        Assert.Equal("backup.json", configuration["backup:email:attachment"]);
        Assert.Equal("smtp.gmail.com", configuration["spring:mail:host"]);
        Assert.Equal("465", configuration["spring:mail:port"]);
        Assert.Equal("dev-user", configuration["spring:mail:username"]);
        Assert.Equal("dev-password", configuration["spring:mail:password"]);
        Assert.True(bool.Parse(configuration["spring:mail:properties:mail:smtp:auth"]!));
        Assert.Equal("465", configuration["spring:mail:properties:mail:smtp:socketFactory:port"]);
        Assert.Equal("javax.net.ssl.SSLSocketFactory", configuration["spring:mail:properties:mail:smtp:socketFactory:class"]);
        Assert.False(bool.Parse(configuration["spring:mail:properties:mail:smtp:socketFactory:fallback"]!));
        Assert.True(bool.Parse(configuration["spring:mail:properties:mail:smtp:ssl:enable"]!));

        var remind = JavaMessageFormat.Format(configuration["remind:email:text"]!, "demo");
        Assert.Contains("Hey, demo!", remind);
        Assert.Contains("We've missed you", remind);
        var backup = JavaMessageFormat.Format(configuration["backup:email:text"]!, "demo");
        Assert.Contains("Howdy, demo.", backup);
    }

    [Fact]
    public void mongo_credentials_authenticate_against_the_service_database()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["spring:data:mongodb:host"] = "notification-mongodb",
                ["spring:data:mongodb:port"] = "27017",
                ["spring:data:mongodb:database"] = "piggymetrics",
                ["spring:data:mongodb:username"] = "user",
                ["spring:data:mongodb:password"] = "s3cret"
            })
            .Build();

        var settings = NotificationMongoSettings.From(configuration);
        var url = new MongoUrl(settings.ConnectionString);

        Assert.Equal("piggymetrics", settings.Database);
        Assert.Equal("piggymetrics", url.DatabaseName);
        Assert.Equal("piggymetrics", url.AuthenticationSource);
        Assert.Equal("user", url.Username);
    }

    [Fact]
    public void account_client_timeout_matches_source()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("account-service");
        Assert.Equal(EdgeHttpDefaults.Timeout, client.Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(10000), client.Timeout);
        Assert.Equal(new Uri("http://account-service:6000/"), client.BaseAddress);
    }

    [Fact]
    public void schedules_use_source_crons()
    {
        var beforeNoon = SpringCron.Next("0 0 12 * * *", new DateTimeOffset(2026, 10, 10, 11, 59, 59, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero), beforeNoon);
        var atNoon = SpringCron.Next("0 0 12 * * *", new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero), atNoon);
        var beforeMidnight = SpringCron.Next("0 0 0 * * *", new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero));
        Assert.Equal(new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero), beforeMidnight);
    }
}
