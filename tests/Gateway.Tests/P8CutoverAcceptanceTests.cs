using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using PiggyMetrics.Gateway.Cutover;
using PiggyMetrics.Shared.Store;
using Xunit;

namespace PiggyMetrics.Gateway.Tests;

public class P8CutoverAcceptanceTests
{
    [Fact]
    public async Task switches_accounts_then_statistics_then_notifications_then_uaa_last()
    {
        var calls = new List<string>();
        var handler = new RecordingHandler(calls);
        await using var factory = CreateFactory(handler);
        var cutover = factory.Services.GetRequiredService<SourceGatewayCutover>();
        var client = factory.CreateClient();

        Assert.Equal(
            ["/accounts/**", "/statistics/**", "/notifications/**", "/uaa/**"],
            SourceGatewayCutover.SwitchOrder);
        Assert.Equal("/uaa/**", SourceGatewayCutover.SwitchOrder[^1]);
        Assert.Equal("auth-service", cutover.Route("/uaa/**").Name);
        Assert.False(cutover.TryDecide("/retired", out _));

        foreach (var route in Routes)
        {
            calls.Clear();
            Assert.Equal(handler.SourceBody, await SendAsync(client, route.Sample));
            Assert.Equal([route.Source + route.Sample], calls);
            Assert.Equal(CutoverMode.Source, cutover.Route(route.Path).Mode);
            Assert.Equal(route.Source, cutover.Route(route.Path).ActiveUnit);
            Assert.Equal(route.Source, cutover.Route(route.Path).SourceUnit);
        }

        handler.DestinationStatus = HttpStatusCode.ServiceUnavailable;
        handler.DestinationBody = "shadow-mismatch";
        foreach (var route in Routes)
        {
            cutover.BeginShadow(route.Path);
            calls.Clear();
            var body = await SendAsync(client, route.Sample);
            Assert.Equal(handler.SourceBody, body);
            Assert.Equal([route.Source + route.Sample, route.Destination + route.Sample], calls);
            Assert.Equal(CutoverMode.Shadow, cutover.Route(route.Path).Mode);
            Assert.Equal(route.Source, cutover.Route(route.Path).ActiveUnit);
            Assert.False(cutover.Route(route.Path).ParityHolds);
            var blocked = Assert.Throws<InvalidOperationException>(() => cutover.Switch(route.Path));
            Assert.Contains("parity", blocked.Message);
        }

        handler.DestinationStatus = HttpStatusCode.OK;
        handler.DestinationBody = handler.SourceBody;
        foreach (var route in Routes)
        {
            calls.Clear();
            Assert.Equal(handler.SourceBody, await SendAsync(client, route.Sample));
            Assert.Equal([route.Source + route.Sample, route.Destination + route.Sample], calls);
            Assert.True(cutover.Route(route.Path).ParityHolds);
            Assert.Equal(route.Source, cutover.Route(route.Path).ActiveUnit);
        }

        var authEarly = Assert.Throws<InvalidOperationException>(() => cutover.Switch("/uaa/**"));
        Assert.Contains("/accounts/**", authEarly.Message);
        Assert.Contains("auth route last", authEarly.Message);
        Assert.Equal(CutoverMode.Shadow, cutover.Route("/uaa/**").Mode);
        Assert.Equal(Routes[3].Source, cutover.Route("/uaa/**").ActiveUnit);

        var statisticsEarly = Assert.Throws<InvalidOperationException>(() => cutover.Switch("/statistics/**"));
        Assert.Contains("/accounts/**", statisticsEarly.Message);
        var notificationsEarly = Assert.Throws<InvalidOperationException>(() => cutover.Switch("/notifications/**"));
        Assert.Contains("/accounts/**", notificationsEarly.Message);

        foreach (var route in Routes)
        {
            cutover.Switch(route.Path);
            var snapshot = cutover.Route(route.Path);
            Assert.Equal(CutoverMode.Switched, snapshot.Mode);
            Assert.Equal(route.Destination, snapshot.DestinationUnit);
            Assert.Equal(snapshot.DestinationUnit, snapshot.ActiveUnit);

            handler.DestinationBody = "from-destination";
            calls.Clear();
            Assert.Equal("from-destination", await SendAsync(client, route.Sample));
            Assert.Equal([route.Destination + route.Sample], calls);
            Assert.DoesNotContain(calls, call => call.StartsWith(route.Source, StringComparison.Ordinal));
        }

        handler.SourceBody = "from-source";
        foreach (var route in Routes)
        {
            calls.Clear();
            Assert.Equal("from-destination", await SendAsync(client, route.Sample));
            Assert.Equal([route.Destination + route.Sample], calls);
        }
    }

    [Fact]
    public async Task shadow_until_parity_holds_and_rollback_points_at_the_source_unit()
    {
        var calls = new List<string>();
        var handler = new RecordingHandler(calls);
        await using var factory = CreateFactory(handler);
        var cutover = factory.Services.GetRequiredService<SourceGatewayCutover>();
        var client = factory.CreateClient();

        cutover.RecordShadowResult("/accounts/**", 200, handler.SourceBody, 200, handler.SourceBody);
        Assert.False(cutover.Route("/accounts/**").ParityHolds);
        var notShadowed = Assert.Throws<InvalidOperationException>(() => cutover.Switch("/accounts/**"));
        Assert.Contains("parity", notShadowed.Message);

        handler.FailDestination = true;
        cutover.BeginShadow("/accounts/**");
        calls.Clear();
        Assert.Equal(handler.SourceBody, await SendAsync(client, "/accounts/demo"));
        Assert.Equal(
            ["http://account-service:6000/accounts/demo", "http://account-destination:6000/accounts/demo"],
            calls);
        Assert.False(cutover.Route("/accounts/**").ParityHolds);
        Assert.Equal(CutoverMode.Shadow, cutover.Route("/accounts/**").Mode);
        Assert.Equal("http://account-service:6000", cutover.Route("/accounts/**").ActiveUnit);

        handler.FailDestination = false;
        handler.DestinationBody = handler.SourceBody;
        calls.Clear();
        Assert.Equal(handler.SourceBody, await SendAsync(client, "/accounts/demo"));
        Assert.True(cutover.Route("/accounts/**").ParityHolds);
        cutover.Switch("/accounts/**");

        handler.DestinationBody = "from-destination";
        calls.Clear();
        Assert.Equal("from-destination", await SendAsync(client, "/accounts/demo"));
        Assert.Equal(["http://account-destination:6000/accounts/demo"], calls);

        var whileSwitched = Assert.Throws<InvalidOperationException>(() => cutover.BeginShadow("/accounts/**"));
        Assert.Contains("source unit", whileSwitched.Message);

        cutover.RollBack("/accounts/**");
        var rolled = cutover.Route("/accounts/**");
        Assert.Equal(CutoverMode.Source, rolled.Mode);
        Assert.False(rolled.ParityHolds);
        Assert.Equal(rolled.SourceUnit, rolled.ActiveUnit);
        Assert.Equal("http://account-service:6000", rolled.ActiveUnit);

        handler.SourceBody = "from-source";
        calls.Clear();
        Assert.Equal("from-source", await SendAsync(client, "/accounts/current"));
        Assert.Equal(["http://account-service:6000/accounts/current"], calls);
    }

    [Fact]
    public void data_needs_no_rollback_because_both_ends_read_and_write_the_same_stores()
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
        var cutover = new SourceGatewayCutover(new ConfigurationBuilder().AddInMemoryCollection(Destinations()).Build());
        foreach (var path in SourceGatewayCutover.SwitchOrder)
        {
            cutover.BeginShadow(path);
            cutover.RecordShadowResult(path, 200, "ok", 200, "ok");
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
    public void retired_modules_are_off_and_nothing_in_the_solution_reads_them()
    {
        var root = FindRoot();
        foreach (var retired in new[] { "config", "monitoring", "registry", "turbine-stream-service" })
        {
            Assert.False(Directory.Exists(Path.Combine(root, "src", retired)), retired);
        }

        var sln = File.ReadAllText(Path.Combine(root, "PiggyMetricsDotNet.sln"));
        foreach (var retired in new[] { "turbine-stream-service", "monitoring", "registry" })
        {
            Assert.DoesNotContain(retired, sln, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(RecordingHandler handler)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
    }

    private static Dictionary<string, string?> Destinations()
    {
        return new Dictionary<string, string?>
        {
            ["cutover:routes:account-service:destinationUnit"] = "http://account-destination:6000",
            ["cutover:routes:statistics-service:destinationUnit"] = "http://statistics-destination:7000",
            ["cutover:routes:notification-service:destinationUnit"] = "http://notification-destination:8000",
            ["cutover:routes:auth-service:destinationUnit"] = "http://auth-destination:5000"
        };
    }

    private static void AssertStore(string path, string host, string collection)
    {
        var store = Assert.Single(SharedDataStores.All, candidate => candidate.RoutePath == path);
        Assert.Equal(host, store.Host);
        Assert.Equal(27017, store.Port);
        Assert.Equal("piggymetrics", store.Database);
        Assert.Equal(collection, store.Collection);
        Assert.Equal(store.SourceStore, store.DestinationStore);
        Assert.Equal(host + ":27017/piggymetrics/" + collection, store.SourceStore);
    }

    private static async Task<string> SendAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PiggyMetricsDotNet.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Solution root not found");
    }

    private static readonly RouteCase[] Routes =
    [
        new("/accounts/**", "/accounts/demo", "http://account-service:6000", "http://account-destination:6000"),
        new("/statistics/**", "/statistics/demo", "http://statistics-service:7000", "http://statistics-destination:7000"),
        new("/notifications/**", "/notifications/demo", "http://notification-service:8000", "http://notification-destination:8000"),
        new("/uaa/**", "/uaa/users/current", "http://auth-service:5000", "http://auth-destination:5000")
    ];

    private sealed record RouteCase(string Path, string Sample, string Source, string Destination);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<string> _calls;

        public RecordingHandler(List<string> calls)
        {
            _calls = calls;
        }

        public string SourceBody { get; set; } = "from-source";

        public string DestinationBody { get; set; } = "from-destination";

        public HttpStatusCode DestinationStatus { get; set; } = HttpStatusCode.OK;

        public bool FailDestination { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.AbsoluteUri ?? "";
            _calls.Add(uri);
            var destination = uri.Contains("destination", StringComparison.Ordinal);
            if (destination && FailDestination)
            {
                throw new HttpRequestException("destination down");
            }

            var status = destination ? DestinationStatus : HttpStatusCode.OK;
            var text = destination ? DestinationBody : SourceBody;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(text)
            });
        }
    }
}
