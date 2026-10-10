using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using PiggyMetrics.Gateway.Cutover;
using PiggyMetrics.Gateway.Proxy;
using PiggyMetrics.Shared.Store;
using Xunit;

namespace PiggyMetrics.Gateway.Tests;

public class SourceGatewayCutoverTests
{
    [Fact]
    public void switch_order_is_accounts_then_statistics_then_notifications_then_uaa_last()
    {
        var cutover = CreateCutover();
        Assert.Equal(
            ["/accounts/**", "/statistics/**", "/notifications/**", "/uaa/**"],
            SourceGatewayCutover.SwitchOrder);
        Assert.Equal("/uaa/**", SourceGatewayCutover.SwitchOrder[^1]);
        Assert.Equal("auth-service", cutover.Route("/uaa/**").Name);

        Assert.Equal(GatewayServiceLocations.AccountService, cutover.Route("/accounts/**").SourceUnit);
        Assert.Equal(GatewayServiceLocations.StatisticsService, cutover.Route("/statistics/**").SourceUnit);
        Assert.Equal(GatewayServiceLocations.NotificationService, cutover.Route("/notifications/**").SourceUnit);
        Assert.Equal(GatewayServiceLocations.AuthService, cutover.Route("/uaa/**").SourceUnit);

        foreach (var path in SourceGatewayCutover.SwitchOrder)
        {
            AcceptShadow(cutover, path);
        }

        var early = Assert.Throws<InvalidOperationException>(() => cutover.Switch("/uaa/**"));
        Assert.Contains("auth route last", early.Message);
        Assert.Equal(CutoverMode.Shadow, cutover.Route("/uaa/**").Mode);
        Assert.Equal(GatewayServiceLocations.AuthService, cutover.Route("/uaa/**").ActiveUnit);

        var skipped = Assert.Throws<InvalidOperationException>(() => cutover.Switch("/statistics/**"));
        Assert.Contains("/accounts/**", skipped.Message);
        Assert.Equal(CutoverMode.Shadow, cutover.Route("/statistics/**").Mode);
        Assert.Equal(GatewayServiceLocations.StatisticsService, cutover.Route("/statistics/**").ActiveUnit);

        foreach (var path in SourceGatewayCutover.SwitchOrder)
        {
            cutover.Switch(path);
            var route = cutover.Route(path);
            Assert.Equal(CutoverMode.Switched, route.Mode);
            Assert.Equal(Destination(path), route.ActiveUnit);
            Assert.Equal(route.DestinationUnit, route.ActiveUnit);
        }

        Assert.Equal(
            SourceGatewayCutover.SwitchOrder,
            cutover.Routes.Select(route => route.Path).ToArray());
    }

    [Fact]
    public void shadow_blocks_switch_until_parity_holds_and_rollback_points_at_source_unit()
    {
        var cutover = CreateCutover();

        var notShadowed = Assert.Throws<InvalidOperationException>(() => cutover.Switch("/accounts/**"));
        Assert.Contains("parity", notShadowed.Message);
        Assert.Equal(GatewayServiceLocations.AccountService, cutover.Route("/accounts/**").ActiveUnit);

        cutover.BeginShadow("/accounts/**");
        cutover.RecordShadowResult("/accounts/**", 200, "source", 200, "destination");
        Assert.False(cutover.Route("/accounts/**").ParityHolds);
        var mismatch = Assert.Throws<InvalidOperationException>(() => cutover.Switch("/accounts/**"));
        Assert.Contains("parity", mismatch.Message);
        Assert.Equal(CutoverMode.Shadow, cutover.Route("/accounts/**").Mode);
        Assert.Equal(GatewayServiceLocations.AccountService, cutover.Route("/accounts/**").ActiveUnit);

        cutover.RecordShadowResult("/accounts/**", 200, "same", 200, "same");
        Assert.True(cutover.Route("/accounts/**").ParityHolds);
        cutover.Switch("/accounts/**");
        Assert.Equal(Destination("/accounts/**"), cutover.Route("/accounts/**").ActiveUnit);

        cutover.RollBack("/accounts/**");
        var rolled = cutover.Route("/accounts/**");
        Assert.Equal(CutoverMode.Source, rolled.Mode);
        Assert.Equal(rolled.SourceUnit, rolled.ActiveUnit);
        Assert.Equal(GatewayServiceLocations.AccountService, rolled.ActiveUnit);
        Assert.False(rolled.ParityHolds);

        AcceptShadow(cutover, "/accounts/**");
        cutover.Switch("/accounts/**");
        var whileSwitched = Assert.Throws<InvalidOperationException>(() => cutover.BeginShadow("/accounts/**"));
        Assert.Contains("source unit", whileSwitched.Message);
    }

    [Fact]
    public void data_needs_no_rollback_because_both_ends_use_the_same_stores()
    {
        Assert.False(SharedDataStores.RollbackRequired);
        Assert.Equal("both ends read and write the same stores", SharedDataStores.RollbackReason);
        Assert.Equal(
            SourceGatewayCutover.SwitchOrder,
            SharedDataStores.All.Select(store => store.RoutePath).ToArray());

        AssertStore("/accounts/**", "account-mongodb", MongoCollectionNames.Accounts);
        AssertStore("/statistics/**", "statistics-mongodb", MongoCollectionNames.Datapoints);
        AssertStore("/notifications/**", "notification-mongodb", MongoCollectionNames.Recipients);
        AssertStore("/uaa/**", "auth-mongodb", MongoCollectionNames.Users);

        var before = SharedDataStores.All.Select(store => store.SourceStore).ToArray();
        var cutover = CreateCutover();
        foreach (var path in SourceGatewayCutover.SwitchOrder)
        {
            AcceptShadow(cutover, path);
            cutover.Switch(path);
        }

        foreach (var path in SourceGatewayCutover.SwitchOrder)
        {
            cutover.RollBack(path);
            Assert.Equal(cutover.Route(path).SourceUnit, cutover.Route(path).ActiveUnit);
        }

        Assert.Equal(before, SharedDataStores.All.Select(store => store.DestinationStore).ToArray());
        Assert.All(SharedDataStores.All, store => Assert.Equal(store.SourceStore, store.DestinationStore));
    }

    [Fact]
    public async Task gateway_shadows_then_switches_and_rolls_back_to_the_source_unit()
    {
        var calls = new List<Forwarded>();
        var handler = new RecordingHandler(calls);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(Destinations());
            });
            builder.ConfigureTestServices(services =>
            {
                services.Configure<HttpClientFactoryOptions>("zuul", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = handler;
                    });
                });
            });
        });

        var cutover = factory.Services.GetRequiredService<SourceGatewayCutover>();
        var client = factory.CreateClient();

        var sourceOnly = await SendAsync(client, "/accounts/demo");
        Assert.Equal("source", sourceOnly);
        Assert.Equal(["http://account-service:6000/accounts/demo"], calls.Select(call => call.Uri).ToArray());

        calls.Clear();
        handler.Match = false;
        cutover.BeginShadow("/accounts/**");
        var shadowed = await SendAsync(client, "/accounts/demo");
        Assert.Equal("source", shadowed);
        Assert.Equal(
            [
                "http://account-service:6000/accounts/demo",
                "http://account-destination:6000/accounts/demo"
            ],
            calls.Select(call => call.Uri).ToArray());
        Assert.All(calls, call => Assert.Equal("{\"name\":\"demo\"}", call.Body));
        Assert.False(cutover.Route("/accounts/**").ParityHolds);
        Assert.Throws<InvalidOperationException>(() => cutover.Switch("/accounts/**"));

        calls.Clear();
        handler.Match = true;
        await SendAsync(client, "/accounts/demo");
        Assert.True(cutover.Route("/accounts/**").ParityHolds);
        cutover.Switch("/accounts/**");

        calls.Clear();
        await SendAsync(client, "/accounts/demo");
        Assert.Equal(["http://account-destination:6000/accounts/demo"], calls.Select(call => call.Uri).ToArray());

        cutover.RollBack("/accounts/**");
        calls.Clear();
        await SendAsync(client, "/accounts/current");
        Assert.Equal(["http://account-service:6000/accounts/current"], calls.Select(call => call.Uri).ToArray());
        Assert.Equal(CutoverMode.Source, cutover.Route("/accounts/**").Mode);
        Assert.Equal(cutover.Route("/accounts/**").SourceUnit, cutover.Route("/accounts/**").ActiveUnit);
    }

    private static SourceGatewayCutover CreateCutover()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Destinations()).Build();
        return new SourceGatewayCutover(configuration);
    }

    private static Dictionary<string, string?> Destinations()
    {
        return new Dictionary<string, string?>
        {
            ["cutover:routes:account-service:destinationUnit"] = Destination("/accounts/**"),
            ["cutover:routes:statistics-service:destinationUnit"] = Destination("/statistics/**"),
            ["cutover:routes:notification-service:destinationUnit"] = Destination("/notifications/**"),
            ["cutover:routes:auth-service:destinationUnit"] = Destination("/uaa/**")
        };
    }

    private static string Destination(string path)
    {
        return path switch
        {
            "/accounts/**" => "http://account-destination:6000",
            "/statistics/**" => "http://statistics-destination:7000",
            "/notifications/**" => "http://notification-destination:8000",
            "/uaa/**" => "http://auth-destination:5000",
            _ => throw new ArgumentException(path)
        };
    }

    private static void AcceptShadow(SourceGatewayCutover cutover, string path)
    {
        cutover.BeginShadow(path);
        cutover.RecordShadowResult(path, 200, "ok", 200, "ok");
        Assert.True(cutover.Route(path).ParityHolds);
    }

    private static void AssertStore(string path, string host, string collection)
    {
        var store = Assert.Single(SharedDataStores.All, candidate => candidate.RoutePath == path);
        Assert.Equal(host, store.Host);
        Assert.Equal(27017, store.Port);
        Assert.Equal("piggymetrics", store.Database);
        Assert.Equal(collection, store.Collection);
        Assert.Equal(store.SourceStore, store.DestinationStore);
    }

    private static async Task<string> SendAsync(HttpClient client, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer issued-by-auth");
        request.Content = new StringContent("{\"name\":\"demo\"}", Encoding.UTF8, "application/json");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private sealed record Forwarded(string Uri, string Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<Forwarded> _calls;

        public RecordingHandler(List<Forwarded> calls)
        {
            _calls = calls;
        }

        public bool Match { get; set; } = true;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.AbsoluteUri ?? "";
            var body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            _calls.Add(new Forwarded(uri, body));
            var text = Match || !uri.Contains("destination", StringComparison.Ordinal) ? "source" : "dest";
            if (Match)
            {
                text = "source";
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(text, Encoding.UTF8, "text/plain")
            };
        }
    }
}
