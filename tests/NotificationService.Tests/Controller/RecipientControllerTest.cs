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
using PiggyMetrics.NotificationService.Controller;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.Shared.Hosting;
using Xunit;
using RecipientServiceApi = PiggyMetrics.NotificationService.Service.RecipientService;

namespace PiggyMetrics.NotificationService.Tests.Controller;

public class RecipientControllerTest
{
    private readonly Mock<RecipientServiceApi> _recipientService = new();

    [Fact]
    public async Task shouldSaveCurrentRecipientSettings()
    {
        var recipient = StubRecipient();
        var json = JsonSerializer.Serialize(recipient, JsonOptions());

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.PutAsync("/recipients/current", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task shouldGetCurrentRecipientSettings()
    {
        var recipient = StubRecipient();
        _recipientService.Setup(service => service.FindByAccountName(recipient.AccountName!)).Returns(recipient);

        using var server = CreateServer();
        var client = server.CreateClient();
        var response = await client.GetAsync("/recipients/current");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(recipient.AccountName, document.RootElement.GetProperty("accountName").GetString());
    }

    private static Recipient StubRecipient()
    {
        var remind = new NotificationSettings
        {
            Active = true,
            Frequency = Frequency.WEEKLY,
            LastNotified = null
        };
        var backup = new NotificationSettings
        {
            Active = false,
            Frequency = Frequency.MONTHLY,
            LastNotified = null
        };
        return new Recipient
        {
            AccountName = "test",
            Email = "test@test.com",
            ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
            {
                [NotificationType.BACKUP] = backup,
                [NotificationType.REMIND] = remind
            }
        };
    }

    private static JsonSerializerOptions JsonOptions()
    {
        var options = PiggyMetricsJsonOptions.Create();
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private TestServer CreateServer()
    {
        var recipientService = _recipientService;
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(recipientService.Object);
                services.AddAuthentication();
                services.AddAuthorization(options =>
                {
                    options.AddPolicy("user", policy => policy.RequireAuthenticatedUser());
                });
                services.AddControllers(options =>
                    {
                        options.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
                    })
                    .AddApplicationPart(typeof(RecipientController).Assembly)
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
                            new Claim(ClaimTypes.Name, "test")
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
