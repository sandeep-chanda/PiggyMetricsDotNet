using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Repository;
using Xunit;

namespace PiggyMetrics.AuthService.Tests;

public class AuthServiceApplicationTests
{
    [Fact]
    public void contextLoads()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
    }

    [Fact]
    public void settings_match_auth_service_and_bootstrap()
    {
        using var factory = new WebApplicationFactory<Program>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("auth-service", configuration["spring:application:name"]);
        Assert.Equal("http://config:8888", configuration["spring:cloud:config:uri"]);
        Assert.True(bool.Parse(configuration["spring:cloud:config:fail-fast"]!));
        Assert.Equal("user", configuration["spring:cloud:config:username"]);
        Assert.Equal("${CONFIG_SERVICE_PASSWORD}", configuration["spring:cloud:config:password"]);
        Assert.Equal("auth-mongodb", configuration["spring:data:mongodb:host"]);
        Assert.Equal("user", configuration["spring:data:mongodb:username"]);
        Assert.Equal("${MONGODB_PASSWORD}", configuration["spring:data:mongodb:password"]);
        Assert.Equal("piggymetrics", configuration["spring:data:mongodb:database"]);
        Assert.Equal("27017", configuration["spring:data:mongodb:port"]);
        Assert.Equal("/uaa", configuration["server:servlet:context-path"]);
        Assert.Equal("5000", configuration["server:port"]);
    }

    [Fact]
    public async Task client_credentials_password_and_refresh_tokens_are_accepted()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var account = await IssueToken(client, "account-service", "account-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "server"
        });
        await AssertAccepted(client, account, "account-service", "server");

        var statistics = await IssueToken(client, "statistics-service", "statistics-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });
        await AssertAccepted(client, statistics, "statistics-service", "server");

        var notification = await IssueToken(client, "notification-service", "notification-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });
        await AssertAccepted(client, notification, "notification-service", "server");

        var create = new HttpRequestMessage(HttpMethod.Post, "/uaa/users")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new User { Username = "demo", Password = "secret" }),
                Encoding.UTF8,
                "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer " + account.AccessToken);
        var created = await client.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var password = await IssueToken(client, "browser", string.Empty, new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "demo",
            ["password"] = "secret"
        });
        var userInfo = await AssertAccepted(client, password, "demo", "ui");
        Assert.Equal("browser", userInfo.GetProperty("oauth2Request").GetProperty("clientId").GetString());

        var missing = await client.GetAsync("/uaa/users/current");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var forbidden = new HttpRequestMessage(HttpMethod.Post, "/uaa/users")
        {
            Content = new StringContent(
                "{\"username\":\"other\",\"password\":\"secret\"}",
                Encoding.UTF8,
                "application/json")
        };
        forbidden.Headers.TryAddWithoutValidation("Authorization", "Bearer " + password.AccessToken);
        var denied = await client.SendAsync(forbidden);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var refreshed = await IssueToken(client, "browser", string.Empty, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = password.RefreshToken
        });
        await AssertAccepted(client, refreshed, "demo", "ui");

        var serviceRefresh = await IssueToken(client, "account-service", "account-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = account.RefreshToken
        });
        await AssertAccepted(client, serviceRefresh, "account-service", "server");
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ACCOUNT_SERVICE_PASSWORD"] = "account-secret",
                    ["STATISTICS_SERVICE_PASSWORD"] = "statistics-secret",
                    ["NOTIFICATION_SERVICE_PASSWORD"] = "notification-secret"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<UserRepository>();
                services.AddSingleton<UserRepository, InMemoryUserRepository>();
            });
        });
    }

    private static async Task<TokenBody> IssueToken(
        HttpClient client,
        string clientId,
        string clientSecret,
        Dictionary<string, string> form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/uaa/oauth/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.False(string.IsNullOrEmpty(root.GetProperty("access_token").GetString()));
        Assert.True(root.GetProperty("expires_in").TryGetInt32(out var expiresIn) && expiresIn > 0);
        return new TokenBody(
            root.GetProperty("access_token").GetString()!,
            root.GetProperty("refresh_token").GetString()!);
    }

    private static async Task<JsonElement> AssertAccepted(HttpClient client, TokenBody token, string name, string scope)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/uaa/users/current");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token.AccessToken);
        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("error", out _));
        Assert.Equal(name, root.GetProperty("name").GetString());
        var scopes = root.GetProperty("oauth2Request").GetProperty("scope").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(scope, scopes);
        return root.Clone();
    }

    private sealed record TokenBody(string AccessToken, string RefreshToken);

    private sealed class InMemoryUserRepository : UserRepository
    {
        private readonly Dictionary<string, User> _users = new();

        public User? FindById(string id)
        {
            return _users.TryGetValue(id, out var user) ? user : null;
        }

        public void Save(User user)
        {
            _users[user.Username ?? string.Empty] = user;
        }
    }
}
