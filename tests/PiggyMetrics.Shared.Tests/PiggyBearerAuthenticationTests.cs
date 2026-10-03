using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public sealed class PiggyBearerAuthenticationTests
{
    private const string UserInfoUri = "http://auth-service:5000/uaa/users/current";

    // The body /uaa/users/current returns for a token the authorization server issued to the browser client.
    private const string UserTokenBody =
        """{"authorities":[],"authenticated":true,"principal":"demo","oauth2Request":{"clientId":"browser","scope":["ui"]},"clientOnly":false,"name":"demo"}""";

    // The same endpoint for a client-credentials token issued to a service client.
    private const string ServerTokenBody =
        """{"authorities":[],"authenticated":true,"principal":"statistics-service","oauth2Request":{"clientId":"statistics-service","scope":["server"]},"clientOnly":true,"name":"statistics-service"}""";

    // What CustomUserInfoTokenServices.getMap answers when the token is not recognised.
    private const string ErrorBody = """{"error":"Could not fetch user details"}""";

    [Fact]
    public async Task TokenTheAuthorizationServerIssuedIsAccepted()
    {
        await using var host = await StartAsync(Answer(UserTokenBody));

        using var response = await host.GetAsync("/accounts/current", "user-token-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("demo", await response.Content.ReadAsStringAsync());
        Assert.Equal("user-token-1", Assert.Single(host.PresentedTokens));
    }

    [Fact]
    public async Task TokenTheAuthorizationServerDidNotIssueIsRefused()
    {
        await using var host = await StartAsync(Answer(ErrorBody));

        using var response = await host.GetAsync("/accounts/current", "forged-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            """{"error":"invalid_token","error_description":"forged-token"}""",
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "Bearer realm=\"oauth2-resource\", error=\"invalid_token\", error_description=\"forged-token\"",
            response.Headers.WwwAuthenticate.ToString());
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task UnreachableAuthorizationServerRefusesTheToken()
    {
        await using var host = await StartAsync(_ => throw new HttpRequestException("auth-service is down"));

        using var response = await host.GetAsync("/accounts/current", "user-token-1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            """{"error":"invalid_token","error_description":"user-token-1"}""",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RequestWithoutATokenIsRefused()
    {
        await using var host = await StartAsync(Answer(UserTokenBody));

        using var response = await host.GetAsync("/accounts/current", accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            """{"error":"unauthorized","error_description":"Full authentication is required to access this resource"}""",
            await response.Content.ReadAsStringAsync());
        Assert.Empty(host.PresentedTokens);
    }

    [Fact]
    public async Task UserTokenIsForbiddenOnAServerScopedEndpoint()
    {
        await using var host = await StartAsync(Answer(UserTokenBody));

        using var response = await host.GetAsync("/accounts/server-only", "user-token-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            """{"error":"access_denied","error_description":"Access is denied"}""",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ServerTokenIsAcceptedOnAServerScopedEndpoint()
    {
        await using var host = await StartAsync(Answer(ServerTokenBody));

        using var response = await host.GetAsync("/accounts/server-only", "server-token-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("statistics-service", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HealthIsAnonymousUnderTheServicePath()
    {
        await using var host = await StartAsync(Answer(UserTokenBody));

        using var response = await host.GetAsync("/accounts/actuator/health", accessToken: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"status":"UP"}""", await response.Content.ReadAsStringAsync());
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> Answer(string body) =>
        _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private static async Task<TestHostFixture> StartAsync(Func<HttpRequestMessage, HttpResponseMessage> userInfo)
    {
        var stub = new StubUserInfoHandler(userInfo);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:UserInfoUri"] = UserInfoUri,
            ["Auth:ClientId"] = "account-service",
        });

        builder.AddPiggyServiceDefaults(new PiggyServiceOptions
        {
            ServiceName = "account-service",
            PathBase = "/accounts",
        });
        builder.Services.AddPiggyBearerAuthentication(builder.Configuration);
        builder.Services.AddHttpClient(PiggyBearerDefaults.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stub);

        var app = builder.Build();
        app.UsePiggyServiceDefaults();
        app.MapGet("/current", (ClaimsPrincipal user) => user.Identity!.Name)
            .RequireAuthorization(PiggyAuthorizationPolicies.User);
        app.MapGet("/server-only", (ClaimsPrincipal user) => user.Identity!.Name)
            .RequireAuthorization(PiggyAuthorizationPolicies.Server);

        await app.StartAsync();
        return new TestHostFixture(app, stub);
    }

    private sealed class TestHostFixture : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly StubUserInfoHandler _stub;

        public TestHostFixture(WebApplication app, StubUserInfoHandler stub)
        {
            _app = app;
            _stub = stub;
        }

        public IReadOnlyList<string> PresentedTokens => _stub.PresentedTokens;

        public async Task<HttpResponseMessage> GetAsync(string path, string? accessToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (accessToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            return await _app.GetTestClient().SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class StubUserInfoHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        private readonly List<string> _presentedTokens = [];

        public StubUserInfoHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        public IReadOnlyList<string> PresentedTokens => _presentedTokens;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(UserInfoUri, request.RequestUri!.ToString());
            _presentedTokens.Add(request.Headers.Authorization?.Parameter ?? string.Empty);
            return Task.FromResult(_responder(request));
        }
    }
}
