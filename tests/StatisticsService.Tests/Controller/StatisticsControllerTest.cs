using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PiggyMetrics.Shared.Hosting;
using PiggyMetrics.StatisticsService.Controller;
using PiggyMetrics.StatisticsService.Domain;
using PiggyMetrics.StatisticsService.Domain.Timeseries;
using StatisticsServiceApi = PiggyMetrics.StatisticsService.Service.StatisticsService;
using Xunit;

namespace PiggyMetrics.StatisticsService.Tests.Controller;

public class StatisticsControllerTest
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Mock<StatisticsServiceApi> _statisticsService = new();

    public StatisticsControllerTest()
    {
        Json.Converters.Add(new JsonStringEnumConverter());
        Json.Converters.Add(new IsoDateTimeOffsetConverter());
    }

    [Fact]
    public async Task shouldGetStatisticsByAccountName()
    {
        var dataPoint = new DataPoint
        {
            Id = new DataPointId("test", DateTime.UtcNow)
        };
        _statisticsService.Setup(service => service.FindByAccountName(dataPoint.Id.Account))
            .Returns(new List<DataPoint> { dataPoint });

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.GetAsync("/test");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(dataPoint.Id.Account, document.RootElement[0].GetProperty("id").GetProperty("account").GetString());
    }

    [Fact]
    public async Task shouldGetCurrentAccountStatistics()
    {
        var dataPoint = new DataPoint
        {
            Id = new DataPointId("test", DateTime.UtcNow)
        };
        _statisticsService.Setup(service => service.FindByAccountName(dataPoint.Id.Account))
            .Returns(new List<DataPoint> { dataPoint });

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.GetAsync("/current");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(dataPoint.Id.Account, document.RootElement[0].GetProperty("id").GetProperty("account").GetString());
    }

    [Fact]
    public async Task shouldSaveAccountStatistics()
    {
        var json = """
            {
              "saving": {"amount": 1500, "currency": "USD", "interest": 3.32, "deposit": true, "capitalization": false},
              "expenses": [{"title": "Grocery", "amount": 10, "currency": "USD", "period": "DAY"}],
              "incomes": [{"title": "Salary", "amount": 9100, "currency": "USD", "period": "MONTH"}]
            }
            """;

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.PutAsync("/test", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _statisticsService.Verify(service => service.Save(It.IsAny<string>(), It.IsAny<Account>()), Times.Once);
    }

    private TestServer CreateServer()
    {
        var statisticsService = _statisticsService;
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(statisticsService.Object);
                services.AddAuthentication();
                services.AddAuthorization(options =>
                {
                    options.AddPolicy("user", policy => policy.RequireAuthenticatedUser());
                    options.AddPolicy("server", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "server"));
                });
                services.AddControllers()
                    .AddApplicationPart(typeof(StatisticsController).Assembly)
                    .AddJsonOptions(options =>
                    {
                        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                        options.JsonSerializerOptions.Converters.Add(new IsoDateTimeOffsetConverter());
                    });
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.Use(async (context, next) =>
                {
                    var identity = new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.Name, "test"),
                            new Claim("scope", "server")
                        },
                        "Test");
                    context.User = new ClaimsPrincipal(identity);
                    await next();
                });
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        return new TestServer(builder);
    }
}
