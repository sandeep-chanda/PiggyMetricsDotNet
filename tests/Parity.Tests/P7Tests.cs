using System.Net;
using System.Text.Json;
using Xunit;

namespace PiggyMetrics.Parity.Tests;

public sealed class ParityReportFixture : IAsyncLifetime
{
    public ParityReport Report { get; private set; } = new([]);

    public IReadOnlyList<string> SourceForwarded { get; private set; } = [];

    public IReadOnlyList<string> DestinationForwarded { get; private set; } = [];

    public IReadOnlyList<string> SourceRates { get; private set; } = [];

    public IReadOnlyList<string> DestinationRates { get; private set; } = [];

    public async Task InitializeAsync()
    {
        var run = await ParityRunner.RunAsync();
        Report = run.Report;
        SourceForwarded = run.SourceForwarded;
        DestinationForwarded = run.DestinationForwarded;
        SourceRates = run.SourceRates;
        DestinationRates = run.DestinationRates;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("p7-parity")]
public sealed class ParityCollection : ICollectionFixture<ParityReportFixture>;

[Collection("p7-parity")]
public class ReplayC1ToC12Tests
{
    private readonly ParityReportFixture _fixture;

    public ReplayC1ToC12Tests(ParityReportFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Replays_C1_through_C12_against_source_and_destination()
    {
        var rows = _fixture.Report.Rows;
        var contracts = rows.Select(row => row.Contract).Distinct().OrderBy(id => id).ToArray();
        Assert.Equal(["C1", "C10", "C11", "C12", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "C9"], contracts);
        var failures = rows.Where(row => !row.StatusMatch || !row.FieldMatch).Select(row => row.ToString()).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
        foreach (var contract in ContractCatalog.All)
        {
            var forwarded = contract.Method + " " + contract.Path;
            Assert.Contains(forwarded, _fixture.SourceForwarded);
            Assert.Contains(forwarded, _fixture.DestinationForwarded);
        }
    }
}

[Collection("p7-parity")]
public class IdenticalExternalStubTests
{
    private readonly ParityReportFixture _fixture;

    public IdenticalExternalStubTests(ParityReportFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task External_clients_return_the_same_status_and_body_on_both_sides()
    {
        foreach (var service in ExternalStubCatalog.Services)
        {
            await AssertSameAsync(service, ExternalStubCatalog.UserToken);
            await AssertSameAsync(service, ExternalStubCatalog.ServerToken);
            await AssertSameAsync(service, "not-a-token");
        }

        using var sourceRates = ExternalStubCatalog.Handler(ExternalStubCatalog.Rates);
        using var destinationRates = ExternalStubCatalog.Handler(ExternalStubCatalog.Rates);
        await AssertHandlerAsync(sourceRates, destinationRates, HttpMethod.Get, "https://api.exchangeratesapi.io/latest?base=USD");

        using var sourceToken = ExternalStubCatalog.Handler(ExternalStubCatalog.Token);
        using var destinationToken = ExternalStubCatalog.Handler(ExternalStubCatalog.Token);
        await AssertHandlerAsync(sourceToken, destinationToken, HttpMethod.Post, "http://auth-service:5000/uaa/oauth/token");

        using var sourceAuth = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        using var destinationAuth = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        await AssertHandlerAsync(sourceAuth, destinationAuth, HttpMethod.Post, "http://auth-service:5000/uaa/users");

        using var sourceStatistics = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        using var destinationStatistics = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        await AssertHandlerAsync(sourceStatistics, destinationStatistics, HttpMethod.Put, "http://statistics-service:7000/statistics/demo");
    }

    private static async Task AssertSameAsync(string service, string token)
    {
        using var source = ExternalStubCatalog.Handler(request => ExternalStubCatalog.UserInfo(service, request));
        using var destination = ExternalStubCatalog.Handler(request => ExternalStubCatalog.UserInfo(service, request));
        using var sourceClient = new HttpClient(source, disposeHandler: false);
        using var destinationClient = new HttpClient(destination, disposeHandler: false);
        using var sourceRequest = new HttpRequestMessage(HttpMethod.Get, "http://auth-service:5000/uaa/users/current");
        using var destinationRequest = new HttpRequestMessage(HttpMethod.Get, "http://auth-service:5000/uaa/users/current");
        sourceRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        destinationRequest.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        using var sourceResponse = await sourceClient.SendAsync(sourceRequest);
        using var destinationResponse = await destinationClient.SendAsync(destinationRequest);
        Assert.Equal(sourceResponse.StatusCode, destinationResponse.StatusCode);
        Assert.Equal(await sourceResponse.Content.ReadAsStringAsync(), await destinationResponse.Content.ReadAsStringAsync());
        var (status, _) = ExternalStubCatalog.UserInfoBody(service, token);
        Assert.Equal(status, sourceResponse.StatusCode);
    }

    private static async Task AssertHandlerAsync(HttpMessageHandler source, HttpMessageHandler destination, HttpMethod method, string url)
    {
        using var sourceClient = new HttpClient(source, disposeHandler: false);
        using var destinationClient = new HttpClient(destination, disposeHandler: false);
        using var sourceResponse = await sourceClient.SendAsync(new HttpRequestMessage(method, url));
        using var destinationResponse = await destinationClient.SendAsync(new HttpRequestMessage(method, url));
        Assert.Equal(sourceResponse.StatusCode, destinationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sourceResponse.StatusCode);
        Assert.Equal(await sourceResponse.Content.ReadAsStringAsync(), await destinationResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public void Rates_stub_is_the_same_call_on_both_sides()
    {
        Assert.Contains(ParityRunner.RatesCall, _fixture.SourceRates);
        Assert.Contains(ParityRunner.RatesCall, _fixture.DestinationRates);
    }
}

[Collection("p7-parity")]
public class AuthMatrixTests
{
    private readonly ParityReportFixture _fixture;

    public AuthMatrixTests(ParityReportFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Each_auth_row_matches_its_policy()
    {
        var failures = _fixture.Report.Rows.Where(row => !row.PolicyMatch).Select(row => row.ToString()).ToArray();
        Assert.True(failures.Length == 0, string.Join("\n", failures));
        Assert.All(_fixture.Report.Rows.GroupBy(row => row.Route), group =>
        {
            var kinds = group.Select(row => row.Auth).OrderBy(kind => kind).ToArray();
            Assert.Equal(["none", "server", "user"], kinds);
        });
    }
}

[Collection("p7-parity")]
public class ParityReportTests
{
    private readonly ParityReportFixture _fixture;

    public ParityReportTests(ParityReportFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Reports_parity_per_route()
    {
        var json = _fixture.Report.ToJson();
        File.WriteAllText(ParityRunner.ReportPath(), json + "\n");
        using var document = JsonDocument.Parse(json);
        var routes = document.RootElement.GetProperty("routes");
        Assert.True(routes.GetArrayLength() >= 12);
        foreach (var route in routes.EnumerateArray())
        {
            Assert.True(route.GetProperty("parity").GetBoolean(), route.GetProperty("route").GetString());
            Assert.Equal(3, route.GetProperty("rows").GetArrayLength());
        }
    }
}
