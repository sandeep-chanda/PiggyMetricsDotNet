using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class EdgeHttpClientTests
{
    [Fact]
    public void Timeout_is_source_hystrix_10000_milliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(10000), EdgeHttpDefaults.Timeout);
    }

    [Fact]
    public void Client_credentials_defaults_match_auth_service_token_endpoint()
    {
        var options = new ClientCredentialsOptions();
        Assert.Equal("http://auth-service:5000/uaa/oauth/token", options.AccessTokenUri);
        Assert.Equal("client_credentials", options.GrantType);
        Assert.Equal("server", options.Scope);
    }

    [Fact]
    public void Named_clients_keep_distinct_edges_and_the_shared_timeout()
    {
        var services = new ServiceCollection();
        services.AddTypedEdgeHttpClient("statistics", new Uri("http://statistics-service:7000/"));
        services.AddTypedEdgeHttpClient("accounts", new Uri("http://account-service:6000/"));
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var statistics = factory.CreateClient("statistics");
        var accounts = factory.CreateClient("accounts");

        Assert.Equal(new Uri("http://statistics-service:7000/"), statistics.BaseAddress);
        Assert.Equal(new Uri("http://account-service:6000/"), accounts.BaseAddress);
        Assert.Equal(TimeSpan.FromMilliseconds(10000), statistics.Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(10000), accounts.Timeout);
    }

    [Fact]
    public async Task Token_cache_posts_client_credentials_and_reuses_unexpired_token()
    {
        var handler = new RecordingHandler();
        var cache = CacheAround(handler);
        var options = new ClientCredentialsOptions { ClientId = "account-service", ClientSecret = "secret" };

        var first = await cache.GetAccessTokenAsync(options, CancellationToken.None);
        var second = await cache.GetAccessTokenAsync(options, CancellationToken.None);

        Assert.Equal("tok-1", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(new Uri("http://auth-service:5000/uaa/oauth/token"), handler.Uri);
        Assert.Equal("grant_type=client_credentials&scope=server", handler.Body);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes("account-service:secret"));
        Assert.Equal("Basic " + basic, handler.Authorization);
    }

    [Fact]
    public async Task Token_cache_refreshes_when_expiry_is_inside_thirty_seconds()
    {
        var handler = new RecordingHandler
        {
            ResponseBody = "{\"access_token\":\"tok-short\",\"expires_in\":10}"
        };
        var cache = CacheAround(handler);
        var options = new ClientCredentialsOptions { ClientId = "notification-service", ClientSecret = "secret" };

        var first = await cache.GetAccessTokenAsync(options, CancellationToken.None);
        var second = await cache.GetAccessTokenAsync(options, CancellationToken.None);

        Assert.Equal("tok-short", first);
        Assert.Equal(first, second);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Token_cache_throws_when_access_token_is_null()
    {
        var handler = new RecordingHandler
        {
            ResponseBody = "{\"access_token\":null,\"expires_in\":3600}"
        };
        var cache = CacheAround(handler);
        var options = new ClientCredentialsOptions { ClientId = "statistics-service", ClientSecret = "secret" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetAccessTokenAsync(options, CancellationToken.None));
        Assert.Equal("access_token missing", ex.Message);
    }

    [Fact]
    public async Task Typed_client_sends_cached_bearer_with_edge_timeout()
    {
        var tokenHandler = new RecordingHandler();
        var outbound = new AuthCaptureHandler();
        var http = new HttpClient(outbound, disposeHandler: false);
        var edge = new TypedEdgeHttpClient(
            http,
            CacheAround(tokenHandler),
            Options.Create(new ClientCredentialsOptions { ClientId = "account-service", ClientSecret = "secret" }));

        Assert.Equal(TimeSpan.FromMilliseconds(10000), edge.Http.Timeout);

        using var response = await edge.SendWithClientCredentialsAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://statistics-service:7000/statistics/demo"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("Bearer", outbound.Authorization?.Scheme);
        Assert.Equal("tok-1", outbound.Authorization?.Parameter);
        Assert.Equal(1, tokenHandler.Calls);
    }

    private static ClientCredentialsTokenCache CacheAround(RecordingHandler handler)
    {
        var mock = new Mock<IHttpClientFactory>();
        mock.Setup(f => f.CreateClient("token")).Returns(() => new HttpClient(handler, disposeHandler: false));
        return new ClientCredentialsTokenCache(mock.Object);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Calls;
        public string? Authorization;
        public string? Body;
        public Uri? Uri;
        public string ResponseBody = "{\"access_token\":\"tok-1\",\"expires_in\":3600}";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Uri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class AuthCaptureHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }
}
