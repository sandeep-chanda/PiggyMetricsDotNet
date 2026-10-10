using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PiggyMetrics.AccountService.Controller;
using PiggyMetrics.Shared.Hosting;
using Xunit;
using AccountServiceApi = PiggyMetrics.AccountService.Service.AccountService;

namespace PiggyMetrics.AccountService.Tests.Controller;

public class AccountControllerTest
{
    private readonly Mock<AccountServiceApi> _accountService = new();

    [Fact]
    public async Task shouldGetAccountByName()
    {
        _accountService.Setup(service => service.FindByName("test"))
            .Returns(new PiggyMetrics.AccountService.Domain.Account { Name = "test" });

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.GetAsync("/test");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("test", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task shouldGetCurrentAccount()
    {
        _accountService.Setup(service => service.FindByName("test"))
            .Returns(new PiggyMetrics.AccountService.Domain.Account { Name = "test" });

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.GetAsync("/current");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("test", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task shouldSaveCurrentAccount()
    {
        const string json = """
            {
              "name": "test",
              "note": "test note",
              "lastSeen": "2020-01-01T00:00:00.000Z",
              "saving": {"amount": 1500, "currency": "USD", "interest": 3.32, "deposit": true, "capitalization": false},
              "expenses": [{"title": "Grocery", "amount": 10, "currency": "USD", "period": "DAY", "icon": "meal"}],
              "incomes": [{"title": "Salary", "amount": 9100, "currency": "USD", "period": "MONTH", "icon": "wallet"}]
            }
            """;

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.PutAsync("/current", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task shouldFailOnValidationTryingToSaveCurrentAccount()
    {
        const string json = """
            {"name":"test"}
            """;

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.PutAsync("/current", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task shouldRegisterNewAccount()
    {
        const string json = """
            {"username":"test","password":"password"}
            """;

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.PostAsync("/", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task shouldFailOnValidationTryingToRegisterNewAccount()
    {
        const string json = """
            {"username":"t"}
            """;

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.PostAsync("/", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private TestServer CreateServer()
    {
        var accountService = _accountService;
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(accountService.Object);
                services.AddAuthentication();
                services.AddAuthorization(options =>
                {
                    options.AddPolicy("user", policy => policy.RequireAuthenticatedUser());
                    options.AddPolicy("server", policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "server"));
                });
                services.AddControllers(options =>
                    {
                        options.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
                    })
                    .AddApplicationPart(typeof(AccountController).Assembly)
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
                app.UseRouting();
                app.UseAuthentication();
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
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });
        return new TestServer(builder);
    }
}
