using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using PiggyMetrics.StatisticsService.Domain;
using PiggyMetrics.StatisticsService.Domain.Timeseries;
using PiggyMetrics.StatisticsService.Repository;
using PiggyMetrics.StatisticsService.Service;
using Xunit;

namespace PiggyMetrics.StatisticsService.Tests.Contract;

public class StatisticsMatrixTests
{
    private const string UserToken = "user-token";
    private const string ServerToken = "server-token";

    [Fact]
    public async Task C8_user_policy_returns_source_body_or_401()
    {
        await using var host = await StatisticsDestination.StartAsync();

        var missing = await host.GetAsync("/statistics/current", null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var response = await host.GetAsync("/statistics/current", UserToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var point = document.RootElement[0];
        Assert.Equal("demo", point.GetProperty("id").GetProperty("account").GetString());
        Assert.Equal("salary", point.GetProperty("incomes")[0].GetProperty("title").GetString());
        Assert.Equal(1m, point.GetProperty("incomes")[0].GetProperty("amount").GetDecimal());
        Assert.Equal("grocery", point.GetProperty("expenses")[0].GetProperty("title").GetString());
        Assert.Equal(2m, point.GetProperty("expenses")[0].GetProperty("amount").GetDecimal());
        Assert.Equal(3m, point.GetProperty("statistics").GetProperty("SAVING_AMOUNT").GetDecimal());
        Assert.Equal(1m, point.GetProperty("rates").GetProperty("USD").GetDecimal());
    }

    [Fact]
    public async Task C9_server_or_demo_policy_statuses()
    {
        await using var host = await StatisticsDestination.StartAsync();

        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/statistics/alice", ServerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.GetAsync("/statistics/alice", UserToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/statistics/demo", null)).StatusCode);
    }

    [Fact]
    public async Task C10_server_policy_statuses()
    {
        await using var host = await StatisticsDestination.StartAsync();
        const string body = """
            {
              "saving": {"amount": 1500, "currency": "USD", "interest": 3.32, "deposit": true, "capitalization": false},
              "expenses": [{"title": "Grocery", "amount": 10, "currency": "USD", "period": "DAY"}],
              "incomes": [{"title": "Salary", "amount": 9100, "currency": "USD", "period": "MONTH"}]
            }
            """;

        Assert.Equal(HttpStatusCode.OK, (await host.PutAsync("/statistics/alice", body, ServerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.PutAsync("/statistics/alice", body, UserToken)).StatusCode);
    }

    private sealed class StatisticsDestination : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        private StatisticsDestination(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        public static Task<StatisticsDestination> StartAsync()
        {
            var repository = new InMemoryDataPointRepository();
            repository.Save(new DataPoint
            {
                Id = new DataPointId("demo", DateTime.UnixEpoch),
                Incomes = new HashSet<ItemMetric> { new("salary", 1m) },
                Expenses = new HashSet<ItemMetric> { new("grocery", 2m) },
                Statistics = new Dictionary<StatisticMetric, decimal>
                {
                    [StatisticMetric.SAVING_AMOUNT] = 3m
                },
                Rates = new Dictionary<Currency, decimal>
                {
                    [Currency.USD] = 1m
                }
            });

            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.PostConfigure<HttpClientFactoryOptions>("userinfo", options =>
                    {
                        options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                        {
                            handlerBuilder.PrimaryHandler = new UserInfoStubHandler();
                        });
                    });
                    services.RemoveAll<DataPointRepository>();
                    services.AddSingleton<DataPointRepository>(repository);
                    services.RemoveAll<ExchangeRatesService>();
                    services.AddSingleton<ExchangeRatesService, StubExchangeRatesService>();
                });
            });
            return Task.FromResult(new StatisticsDestination(factory));
        }

        public Task<HttpResponseMessage> GetAsync(string path, string? bearer)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (!string.IsNullOrEmpty(bearer))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            }

            return _client.SendAsync(request);
        }

        public Task<HttpResponseMessage> PutAsync(string path, string json, string? bearer)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, path)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrEmpty(bearer))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            }

            return _client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
        }
    }

    private sealed class InMemoryDataPointRepository : DataPointRepository
    {
        private readonly List<DataPoint> _points = new();

        public List<DataPoint> FindByIdAccount(string account)
        {
            return _points.Where(point => point.Id!.Account == account).ToList();
        }

        public DataPoint Save(DataPoint dataPoint)
        {
            _points.RemoveAll(point => point.Id!.Account == dataPoint.Id!.Account && point.Id.Date == dataPoint.Id.Date);
            _points.Add(dataPoint);
            return dataPoint;
        }
    }

    private sealed class StubExchangeRatesService : ExchangeRatesService
    {
        public Dictionary<Currency, decimal> GetCurrentRates()
        {
            return new Dictionary<Currency, decimal>
            {
                [Currency.USD] = 1m,
                [Currency.EUR] = 0.8m,
                [Currency.RUB] = 80m
            };
        }

        public decimal Convert(Currency from, Currency to, decimal? amount)
        {
            var rates = GetCurrentRates();
            var ratio = Math.Round(rates[to] / rates[from], 4, MidpointRounding.AwayFromZero);
            return amount!.Value * ratio;
        }
    }

    private sealed class UserInfoStubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = request.Headers.Authorization?.Parameter ?? string.Empty;
            var json = token switch
            {
                UserToken => "{\"username\":\"demo\",\"oauth2Request\":{\"clientId\":\"browser\",\"scope\":[\"ui\"]}}",
                ServerToken => "{\"name\":\"statistics-service\",\"oauth2Request\":{\"clientId\":\"statistics-service\",\"scope\":[\"server\"]}}",
                _ => "{\"error\":\"invalid_token\"}"
            };
            var status = token is UserToken or ServerToken ? HttpStatusCode.OK : HttpStatusCode.Unauthorized;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
