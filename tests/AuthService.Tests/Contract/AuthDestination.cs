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

namespace PiggyMetrics.AuthService.Tests.Contract;

internal sealed class AuthDestination : IAsyncDisposable
{
    private AuthDestination(WebApplicationFactory<Program> factory, HttpClient client, IssuedTokens tokens)
    {
        Factory = factory;
        Client = client;
        Tokens = tokens;
    }

    public WebApplicationFactory<Program> Factory { get; }

    public HttpClient Client { get; }

    public IssuedTokens Tokens { get; }

    public static async Task<AuthDestination> StartAsync()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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

        var client = factory.CreateClient();
        var server = await IssueAsync(client, "account-service", "account-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "server"
        });

        var create = new HttpRequestMessage(HttpMethod.Post, "/uaa/users")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new User { Username = "demo", Password = "secret" }),
                Encoding.UTF8,
                "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer " + server.AccessToken);
        var created = await client.SendAsync(create);
        AssertOk(created);

        var user = await IssueAsync(client, "browser", string.Empty, new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "demo",
            ["password"] = "secret",
            ["scope"] = "ui"
        });

        return new AuthDestination(factory, client, new IssuedTokens(server, user));
    }

    public static async Task<TokenBody> IssueAsync(
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
        AssertOk(response);
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var refreshToken = root.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));
        Assert.False(string.IsNullOrEmpty(refreshToken));
        return new TokenBody(accessToken!, refreshToken!);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
    }

    private static void AssertOk(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("destination returned " + (int)response.StatusCode);
        }
    }

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

internal sealed record IssuedTokens(TokenBody Server, TokenBody User);

internal sealed record TokenBody(string AccessToken, string RefreshToken);
