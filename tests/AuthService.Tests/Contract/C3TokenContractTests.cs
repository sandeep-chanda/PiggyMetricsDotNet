using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PiggyMetrics.AuthService.Config;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.AuthService.Tests.Contract;

public class C3TokenContractTests
{
    [Fact]
    public async Task Each_grant_issues_a_token_resource_servers_accept()
    {
        await using var destination = await AuthDestination.StartAsync();

        await AssertResourceServerAccepts(destination, destination.Tokens.Server.AccessToken, "account-service", "server");
        await AssertResourceServerAccepts(destination, destination.Tokens.User.AccessToken, "demo", "ui");

        var refreshed = await AuthDestination.IssueAsync(destination.Client, "browser", string.Empty, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = destination.Tokens.User.RefreshToken
        });
        await AssertResourceServerAccepts(destination, refreshed.AccessToken, "demo", "ui");

        var serviceRefresh = await AuthDestination.IssueAsync(destination.Client, "account-service", "account-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = destination.Tokens.Server.RefreshToken
        });
        await AssertResourceServerAccepts(destination, serviceRefresh.AccessToken, "account-service", "server");

        using var resource = CreateResourceServer(destination);
        var rejected = resource.CreateClient();
        rejected.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer not-issued");
        var refused = await rejected.GetAsync("/secure");
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public void Authorization_server_keeps_the_four_clients_grants_and_scopes()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ACCOUNT_SERVICE_PASSWORD"] = "account-secret",
                ["STATISTICS_SERVICE_PASSWORD"] = "statistics-secret",
                ["NOTIFICATION_SERVICE_PASSWORD"] = "notification-secret"
            })
            .Build();
        var config = new OAuth2AuthorizationConfig(configuration);

        Assert.Equal(4, config.Clients.Count);
        AssertClient(config, "browser", null, ["refresh_token", "password"], ["ui"]);
        AssertClient(config, "account-service", "account-secret", ["client_credentials", "refresh_token"], ["server"]);
        AssertClient(config, "statistics-service", "statistics-secret", ["client_credentials", "refresh_token"], ["server"]);
        AssertClient(config, "notification-service", "notification-secret", ["client_credentials", "refresh_token"], ["server"]);
    }

    [Fact]
    public async Task Clients_cannot_use_a_grant_the_source_did_not_authorize()
    {
        await using var destination = await AuthDestination.StartAsync();

        var browserClientCredentials = await PostTokenAsync(destination.Client, "browser", string.Empty, new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials"
        });
        await AssertInvalidClient(browserClientCredentials);

        var servicePassword = await PostTokenAsync(destination.Client, "account-service", "account-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "demo",
            ["password"] = "secret"
        });
        await AssertInvalidClient(servicePassword);
    }

    private static async Task AssertInvalidClient(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("invalid_client", doc.RootElement.GetProperty("error").GetString());
    }

    private static void AssertClient(
        OAuth2AuthorizationConfig config,
        string clientId,
        string? secret,
        string[] grants,
        string[] scopes)
    {
        var client = config.Find(clientId);
        Assert.NotNull(client);
        Assert.Equal(secret, client!.Secret);
        Assert.Equal(grants, client.GrantTypes);
        Assert.Equal(scopes, client.Scopes);
    }

    private static async Task AssertResourceServerAccepts(
        AuthDestination destination,
        string accessToken,
        string name,
        string scope)
    {
        using var resource = CreateResourceServer(destination);
        var client = resource.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
        var response = await client.GetAsync("/secure");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(name, body);

        var current = await Send.GetAsync(destination.Client, "/uaa/users/current", accessToken);
        var json = await current.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty("error", out _));
        Assert.Equal(name, root.GetProperty("name").GetString());
        var scopes = root.GetProperty("oauth2Request").GetProperty("scope").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains(scope, scopes);
    }

    private static TestServer CreateResourceServer(AuthDestination destination)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IHttpClientFactory>(_ =>
                {
                    var mock = new Mock<IHttpClientFactory>();
                    mock.Setup(factory => factory.CreateClient("userinfo"))
                        .Returns(() => destination.Factory.CreateClient());
                    return mock.Object;
                });
                services.AddAuthentication(UserInfoBearerOptions.DefaultScheme)
                    .AddUserInfoBearer(options =>
                    {
                        options.UserInfoEndpointUrl = "http://localhost/uaa/users/current";
                    });
                services.AddAuthorization();
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/secure", async ctx =>
                    {
                        if (ctx.User.Identity?.IsAuthenticated != true)
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        await ctx.Response.WriteAsync(ctx.User.Identity.Name ?? "");
                    }).RequireAuthorization();
                });
            });
        return new TestServer(builder);
    }

    private static async Task<HttpResponseMessage> PostTokenAsync(
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
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
        return await client.SendAsync(request);
    }
}
