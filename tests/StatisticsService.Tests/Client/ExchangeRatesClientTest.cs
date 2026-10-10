using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PiggyMetrics.StatisticsService.Client;
using PiggyMetrics.StatisticsService.Domain;
using Xunit;

namespace PiggyMetrics.StatisticsService.Tests.Client;

public class ExchangeRatesClientTest : IClassFixture<ExchangeRatesClientTest.RatesStub>
{
    private readonly RatesStub _stub;

    public ExchangeRatesClientTest(RatesStub stub)
    {
        _stub = stub;
    }

    [Fact]
    public void shouldRetrieveExchangeRates()
    {
        using var factory = CreateFactory();
        var client = factory.Services.GetRequiredService<ExchangeRatesClient>();
        var container = client.GetRates(CurrencyCodes.GetBase());

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), container.Date);
        Assert.Equal(CurrencyCodes.GetBase(), container.Base);
        Assert.NotNull(container.Rates);
        Assert.True(container.Rates.ContainsKey(Currency.USD.ToString()));
        Assert.True(container.Rates.ContainsKey(Currency.EUR.ToString()));
        Assert.True(container.Rates.ContainsKey(Currency.RUB.ToString()));
    }

    [Fact]
    public void shouldRetrieveExchangeRatesForSpecifiedCurrency()
    {
        var requestedCurrency = Currency.EUR;
        using var factory = CreateFactory();
        var client = factory.Services.GetRequiredService<ExchangeRatesClient>();

        var container = client.GetRates(CurrencyCodes.GetBase());

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), container.Date);
        Assert.Equal(CurrencyCodes.GetBase(), container.Base);
        Assert.NotNull(container.Rates);
        Assert.True(container.Rates.ContainsKey(requestedCurrency.ToString()));
    }

    [Fact]
    public void shouldFallBackWhenRatesCallFails()
    {
        var fallback = new ExchangeRatesClientFallback().GetRates(Currency.EUR);
        Assert.Equal(CurrencyCodes.GetBase(), fallback.Base);
        Assert.NotNull(fallback.Rates);
        Assert.Empty(fallback.Rates);

        using var factory = CreateFactory("http://127.0.0.1:1/");
        var client = factory.Services.GetRequiredService<ExchangeRatesClient>();
        var container = client.GetRates(Currency.EUR);
        Assert.Equal(CurrencyCodes.GetBase(), container.Base);
        Assert.NotNull(container.Rates);
        Assert.Empty(container.Rates);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        return CreateFactory(_stub.Url + "/");
    }

    private static WebApplicationFactory<Program> CreateFactory(string ratesUrl)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["rates:url"] = ratesUrl
                });
            });
        });
    }

    public sealed class RatesStub : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public RatesStub()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            _app = builder.Build();
            _app.MapGet("/latest", () => Results.Text(
                "{\"base\":\"USD\",\"date\":\"2016-01-01\",\"rates\":{\"USD\":1,\"EUR\":0.92,\"RUB\":75}}",
                "application/json",
                Encoding.UTF8,
                (int)HttpStatusCode.OK));
            _app.Start();
            Url = _app.Urls.First().TrimEnd('/');
        }

        public string Url { get; }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }
}
