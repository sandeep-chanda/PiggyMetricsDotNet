using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Repository;
using Xunit;

namespace PiggyMetrics.NotificationService.Tests.Contract;

public class NotificationMatrixTests
{
    private const string UserToken = "user-token";
    private const string ServerToken = "server-token";

    [Fact]
    public async Task C11_user_policy_get_returns_source_body_or_401()
    {
        await using var host = await NotificationDestination.StartAsync();

        var missing = await host.SendAsync(HttpMethod.Get, "/notifications/recipients/current", null, null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var response = await host.SendAsync(HttpMethod.Get, "/notifications/recipients/current", null, UserToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal("demo", body.GetProperty("accountName").GetString());
        Assert.Equal("demo@piggymetrics.test", body.GetProperty("email").GetString());
        Assert.False(body.GetProperty("scheduledNotifications").GetProperty("BACKUP").GetProperty("active").GetBoolean());
        Assert.Equal("MONTHLY", body.GetProperty("scheduledNotifications").GetProperty("BACKUP").GetProperty("frequency").GetString());
        Assert.True(body.GetProperty("scheduledNotifications").GetProperty("REMIND").GetProperty("active").GetBoolean());
        Assert.Equal("WEEKLY", body.GetProperty("scheduledNotifications").GetProperty("REMIND").GetProperty("frequency").GetString());

        Assert.Equal(
            HttpStatusCode.OK,
            (await host.SendAsync(HttpMethod.Get, "/notifications/recipients/current", null, ServerToken)).StatusCode);
    }

    [Fact]
    public async Task C12_user_policy_put_returns_source_body_or_401()
    {
        await using var host = await NotificationDestination.StartAsync();
        const string body = """
            {
              "email": "saved@piggymetrics.test",
              "scheduledNotifications": {
                "BACKUP": {"active": true, "frequency": "QUARTERLY", "lastNotified": null},
                "REMIND": {"active": false, "frequency": "MONTHLY", "lastNotified": null}
              }
            }
            """;

        var missing = await host.SendAsync(HttpMethod.Put, "/notifications/recipients/current", body, null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var response = await host.SendAsync(HttpMethod.Put, "/notifications/recipients/current", body, UserToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var saved = document.RootElement;
        Assert.Equal("demo", saved.GetProperty("accountName").GetString());
        Assert.Equal("saved@piggymetrics.test", saved.GetProperty("email").GetString());
        Assert.True(saved.GetProperty("scheduledNotifications").GetProperty("BACKUP").GetProperty("active").GetBoolean());
        Assert.Equal("QUARTERLY", saved.GetProperty("scheduledNotifications").GetProperty("BACKUP").GetProperty("frequency").GetString());
        Assert.False(string.IsNullOrEmpty(saved.GetProperty("scheduledNotifications").GetProperty("BACKUP").GetProperty("lastNotified").GetString()));
        Assert.False(saved.GetProperty("scheduledNotifications").GetProperty("REMIND").GetProperty("active").GetBoolean());
        Assert.Equal("MONTHLY", saved.GetProperty("scheduledNotifications").GetProperty("REMIND").GetProperty("frequency").GetString());
        Assert.False(string.IsNullOrEmpty(saved.GetProperty("scheduledNotifications").GetProperty("REMIND").GetProperty("lastNotified").GetString()));

        var server = await host.SendAsync(HttpMethod.Put, "/notifications/recipients/current", body, ServerToken);
        Assert.Equal(HttpStatusCode.OK, server.StatusCode);
        using var serverDocument = JsonDocument.Parse(await server.Content.ReadAsStringAsync());
        Assert.Equal("notification-service", serverDocument.RootElement.GetProperty("accountName").GetString());
    }

    private sealed class NotificationDestination : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        private NotificationDestination(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        public static Task<NotificationDestination> StartAsync()
        {
            var repository = new InMemoryRecipientRepository();
            repository.Save(new Recipient
            {
                AccountName = "demo",
                Email = "demo@piggymetrics.test",
                ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
                {
                    [NotificationType.BACKUP] = new()
                    {
                        Active = false,
                        Frequency = Frequency.MONTHLY,
                        LastNotified = DateTimeOffset.UnixEpoch
                    },
                    [NotificationType.REMIND] = new()
                    {
                        Active = true,
                        Frequency = Frequency.WEEKLY,
                        LastNotified = DateTimeOffset.UnixEpoch
                    }
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
                    services.PostConfigure<HttpClientFactoryOptions>("token", options =>
                    {
                        options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                        {
                            handlerBuilder.PrimaryHandler = new TokenStubHandler();
                        });
                    });
                    services.RemoveAll<RecipientRepository>();
                    services.AddSingleton<RecipientRepository>(repository);
                });
            });
            return Task.FromResult(new NotificationDestination(factory));
        }

        public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? json, string? bearer)
        {
            var request = new HttpRequestMessage(method, path);
            if (json is not null)
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

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

    private sealed class InMemoryRecipientRepository : RecipientRepository
    {
        private readonly Dictionary<string, Recipient> _recipients = new(StringComparer.Ordinal);

        public Recipient? FindByAccountName(string name)
        {
            return _recipients.TryGetValue(name, out var recipient) ? recipient : null;
        }

        public void Save(Recipient recipient)
        {
            _recipients[recipient.AccountName ?? string.Empty] = recipient;
        }

        public List<Recipient> FindReadyForBackup() => new();

        public List<Recipient> FindReadyForRemind() => new();
    }

    private sealed class UserInfoStubHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return SendAsync(request, cancellationToken).GetAwaiter().GetResult();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = request.Headers.Authorization?.Parameter ?? string.Empty;
            var json = token switch
            {
                UserToken => "{\"username\":\"demo\",\"oauth2Request\":{\"clientId\":\"browser\",\"scope\":[\"ui\"]}}",
                ServerToken => "{\"name\":\"notification-service\",\"oauth2Request\":{\"clientId\":\"notification-service\",\"scope\":[\"server\"]}}",
                _ => "{\"error\":\"invalid_token\"}"
            };
            var status = token is UserToken or ServerToken ? HttpStatusCode.OK : HttpStatusCode.Unauthorized;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class TokenStubHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return SendAsync(request, cancellationToken).GetAwaiter().GetResult();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"issued\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
            });
        }
    }
}
