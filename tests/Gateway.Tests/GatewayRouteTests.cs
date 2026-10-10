using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using PiggyMetrics.Gateway.Proxy;
using Xunit;

namespace PiggyMetrics.Gateway.Tests;

public class GatewayRouteTests
{
    [Fact]
    public async Task each_prefix_reaches_its_service_without_stripping_and_unlisted_path_is_404()
    {
        var calls = new List<Forwarded>();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<HttpClientFactoryOptions>("zuul", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = new RecordingHandler(calls);
                    });
                });
            });
        });

        var client = factory.CreateClient();
        await AssertReaches(client, calls, HttpMethod.Get, "/uaa/users/current?x=1", null, "http://auth-service:5000/uaa/users/current?x=1");
        await AssertReaches(client, calls, HttpMethod.Get, "/uaa", null, "http://auth-service:5000/uaa");
        await AssertReaches(client, calls, HttpMethod.Get, "/accounts/demo", null, "http://account-service:6000/accounts/demo");
        await AssertReaches(client, calls, HttpMethod.Post, "/accounts/demo", "{\"name\":\"demo\"}", "http://account-service:6000/accounts/demo");
        await AssertReaches(client, calls, HttpMethod.Get, "/accounts", null, "http://account-service:6000/accounts");
        await AssertReaches(client, calls, HttpMethod.Get, "/statistics/demo", null, "http://statistics-service:7000/statistics/demo");
        await AssertReaches(client, calls, HttpMethod.Get, "/statistics", null, "http://statistics-service:7000/statistics");
        await AssertReaches(client, calls, HttpMethod.Get, "/notifications/demo", null, "http://notification-service:8000/notifications/demo");
        await AssertReaches(client, calls, HttpMethod.Get, "/notifications", null, "http://notification-service:8000/notifications");

        var forwarded = calls.Count;
        foreach (var path in new[] { "/not-a-route", "/uaa-extra", "/account", "/statisticsx", "/notification" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.Equal(forwarded, calls.Count);
    }

    [Fact]
    public void timeouts_and_port_4000_match_gateway_and_bootstrap()
    {
        using var factory = new WebApplicationFactory<Program>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal(20000, configuration.GetValue<int>("hystrix:command:default:execution:isolation:thread:timeoutInMilliseconds"));
        Assert.Equal(20000, configuration.GetValue<int>("ribbon:ReadTimeout"));
        Assert.Equal(20000, configuration.GetValue<int>("ribbon:ConnectTimeout"));
        Assert.Equal(20000, configuration.GetValue<int>("zuul:host:connect-timeout-millis"));
        Assert.Equal(20000, configuration.GetValue<int>("zuul:host:socket-timeout-millis"));
        Assert.Equal(4000, configuration.GetValue<int>("server:port"));
        Assert.Equal("http://0.0.0.0:4000", GatewayHost.ListenUrl(configuration));
        Assert.Equal(TimeSpan.FromMilliseconds(20000), GatewayTimeouts.Connect(configuration));
        Assert.Equal(TimeSpan.FromMilliseconds(20000), GatewayTimeouts.Command(configuration));

        var zuul = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("zuul");
        Assert.Equal(TimeSpan.FromMilliseconds(20000), zuul.Timeout);

        Assert.Equal("gateway", configuration["spring:application:name"]);
        Assert.Equal("http://config:8888", configuration["spring:cloud:config:uri"]);
        Assert.True(configuration.GetValue<bool>("spring:cloud:config:fail-fast"));
        Assert.Equal("${CONFIG_SERVICE_PASSWORD}", configuration["spring:cloud:config:password"]);
        Assert.Equal("user", configuration["spring:cloud:config:username"]);
    }

    private static async Task AssertReaches(
        HttpClient client,
        List<Forwarded> calls,
        HttpMethod method,
        string pathAndQuery,
        string? body,
        string expectedUri)
    {
        var start = calls.Count;
        var request = new HttpRequestMessage(method, pathAndQuery);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer issued-by-auth");
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var forwarded = Assert.Single(calls.Skip(start));
        Assert.Equal(method.Method, forwarded.Method);
        Assert.Equal(expectedUri, forwarded.Uri);
        Assert.Equal("Bearer issued-by-auth", forwarded.Authorization);
        Assert.Equal(body ?? "", forwarded.Body);
    }

    private sealed record Forwarded(string Method, string Uri, string Authorization, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<Forwarded> _calls;

        public RecordingHandler(List<Forwarded> calls)
        {
            _calls = calls;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            _calls.Add(new Forwarded(
                request.Method.Method,
                request.RequestUri?.AbsoluteUri ?? "",
                request.Headers.Authorization?.ToString() ?? "",
                body));
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
