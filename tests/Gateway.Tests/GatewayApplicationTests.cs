using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PiggyMetrics.Gateway.Proxy;
using Xunit;

namespace PiggyMetrics.Gateway.Tests;

public class GatewayApplicationTests
{
    [Fact]
    public void contextLoads()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
    }

    [Fact]
    public void fire()
    {
    }

    [Fact]
    public void settings_match_gateway_yml_and_bootstrap()
    {
        using var factory = new WebApplicationFactory<Program>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        Assert.Equal("gateway", configuration["spring:application:name"]);
        Assert.Equal("http://config:8888", configuration["spring:cloud:config:uri"]);
        Assert.True(bool.Parse(configuration["spring:cloud:config:fail-fast"]!));
        Assert.Equal("${CONFIG_SERVICE_PASSWORD}", configuration["spring:cloud:config:password"]);
        Assert.Equal("user", configuration["spring:cloud:config:username"]);

        Assert.Equal("20000", configuration["hystrix:command:default:execution:isolation:thread:timeoutInMilliseconds"]);
        Assert.Equal("20000", configuration["ribbon:ReadTimeout"]);
        Assert.Equal("20000", configuration["ribbon:ConnectTimeout"]);
        Assert.Equal("20000", configuration["zuul:host:connect-timeout-millis"]);
        Assert.Equal("20000", configuration["zuul:host:socket-timeout-millis"]);
        Assert.Equal("*", configuration["zuul:ignoredServices"]);
        Assert.Equal("4000", configuration["server:port"]);
        Assert.Equal("http://0.0.0.0:4000", GatewayHost.ListenUrl(configuration));
        Assert.Equal(TimeSpan.FromMilliseconds(20000), GatewayTimeouts.Connect(configuration));
        Assert.Equal(TimeSpan.FromMilliseconds(20000), GatewayTimeouts.Command(configuration));

        AssertRoute(configuration, "auth-service", "/uaa/**", "http://auth-service:5000", null);
        AssertRoute(configuration, "account-service", "/accounts/**", null, "account-service");
        AssertRoute(configuration, "statistics-service", "/statistics/**", null, "statistics-service");
        AssertRoute(configuration, "notification-service", "/notifications/**", null, "notification-service");

        Assert.Equal(GatewayServiceLocations.AccountService, ZuulRoutes.ResolveBase(configuration, Route(configuration, "account-service")));
        Assert.Equal(GatewayServiceLocations.StatisticsService, ZuulRoutes.ResolveBase(configuration, Route(configuration, "statistics-service")));
        Assert.Equal(GatewayServiceLocations.NotificationService, ZuulRoutes.ResolveBase(configuration, Route(configuration, "notification-service")));
        Assert.Equal("http://auth-service:5000", ZuulRoutes.ResolveBase(configuration, Route(configuration, "auth-service")));
    }

    [Fact]
    public async Task routes_keep_prefix_and_forward_authorization()
    {
        await using var downstream = await Downstream.StartAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["zuul:routes:auth-service:url"] = downstream.BaseUrl,
                    ["zuul:routes:account-service:url"] = downstream.BaseUrl,
                    ["zuul:routes:statistics-service:url"] = downstream.BaseUrl,
                    ["zuul:routes:notification-service:url"] = downstream.BaseUrl
                });
            });
        });

        var client = factory.CreateClient();
        await AssertForwarded(client, HttpMethod.Get, "/uaa/users/current?x=1", null);
        await AssertForwarded(client, HttpMethod.Post, "/accounts/demo", "{\"name\":\"demo\"}");
        await AssertForwarded(client, HttpMethod.Get, "/statistics/demo", null);
        await AssertForwarded(client, HttpMethod.Get, "/notifications/demo", null);

        async Task AssertForwarded(HttpClient http, HttpMethod method, string pathAndQuery, string? body)
        {
            var request = new HttpRequestMessage(method, pathAndQuery);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer issued-by-auth");
            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var response = await http.SendAsync(request);
            var payload = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var path = pathAndQuery.Split('?')[0];
            var query = pathAndQuery.Contains('?') ? "?" + pathAndQuery.Split('?')[1] : "";
            Assert.Equal(method.Method + " " + path + query + " auth=Bearer issued-by-auth body=" + (body ?? ""), payload);
        }
    }

    [Fact]
    public async Task static_ui_is_served_unchanged()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var env = factory.Services.GetRequiredService<IWebHostEnvironment>();
        var client = factory.CreateClient();

        var root = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(env.WebRootPath, "index.html")), await root.Content.ReadAsByteArrayAsync());

        var files = Directory.EnumerateFiles(env.WebRootPath, "*", SearchOption.AllDirectories).ToArray();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(env.WebRootPath, file).Replace('\\', '/');
            await AssertSameFile(client, env, "/" + relative, relative);
        }
    }

    private static async Task AssertSameFile(HttpClient client, IWebHostEnvironment env, string url, string relativePath)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = await File.ReadAllBytesAsync(Path.Combine(env.WebRootPath, relativePath));
        Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync());
    }

    private static void AssertRoute(IConfiguration configuration, string name, string path, string? url, string? serviceId)
    {
        Assert.Equal(path, configuration[$"zuul:routes:{name}:path"]);
        Assert.Equal(url, configuration[$"zuul:routes:{name}:url"]);
        Assert.Equal(serviceId, configuration[$"zuul:routes:{name}:serviceId"]);
        Assert.False(bool.Parse(configuration[$"zuul:routes:{name}:stripPrefix"]!));
        Assert.Equal("", configuration[$"zuul:routes:{name}:sensitiveHeaders"]);
        var route = Route(configuration, name);
        Assert.False(route.StripPrefix);
        Assert.Equal(path, route.Path);
        Assert.Empty(route.SensitiveHeaders);
    }

    private static ZuulRouteDefinition Route(IConfiguration configuration, string name)
    {
        return Assert.Single(ZuulRoutes.Load(configuration), route => route.Name == name);
    }

    private sealed class Downstream : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly string _contentRoot;

        private Downstream(WebApplication app, string contentRoot, string baseUrl)
        {
            _app = app;
            _contentRoot = contentRoot;
            BaseUrl = baseUrl;
        }

        public string BaseUrl { get; }

        public static async Task<Downstream> StartAsync()
        {
            var port = FreePort();
            var contentRoot = Directory.CreateTempSubdirectory("gateway-downstream").FullName;
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = contentRoot,
                WebRootPath = contentRoot,
                Args = []
            });
            var baseUrl = "http://127.0.0.1:" + port;
            builder.WebHost.UseUrls(baseUrl);
            var app = builder.Build();
            app.Run(async context =>
            {
                string body = "";
                if (context.Request.ContentLength is > 0)
                {
                    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
                    body = await reader.ReadToEndAsync();
                }

                context.Response.ContentType = "text/plain; charset=utf-8";
                var payload = context.Request.Method
                    + " "
                    + context.Request.Path
                    + context.Request.QueryString
                    + " auth="
                    + context.Request.Headers.Authorization
                    + " body="
                    + body;
                await context.Response.WriteAsync(payload);
            });
            await app.StartAsync();
            return new Downstream(app, contentRoot, baseUrl);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
            Directory.Delete(_contentRoot, recursive: true);
        }

        private static int FreePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
