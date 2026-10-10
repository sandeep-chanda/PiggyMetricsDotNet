extern alias AccountApp;
extern alias AuthApp;
extern alias NotificationApp;
extern alias StatisticsApp;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Account = AccountApp::PiggyMetrics.AccountService.Domain.Account;
using AccountRepository = AccountApp::PiggyMetrics.AccountService.Repository.AccountRepository;
using AuthUser = AuthApp::PiggyMetrics.AuthService.Domain.User;
using UserRepository = AuthApp::PiggyMetrics.AuthService.Repository.UserRepository;
using DataPoint = StatisticsApp::PiggyMetrics.StatisticsService.Domain.Timeseries.DataPoint;
using DataPointRepository = StatisticsApp::PiggyMetrics.StatisticsService.Repository.DataPointRepository;
using Recipient = NotificationApp::PiggyMetrics.NotificationService.Domain.Recipient;
using RecipientRepository = NotificationApp::PiggyMetrics.NotificationService.Repository.RecipientRepository;

namespace PiggyMetrics.Parity.Tests;

public sealed class DestinationSide : IAsyncDisposable
{
    private readonly WebApplicationFactory<AuthApp::Program>? _auth;
    private readonly WebApplicationFactory<AccountApp::Program>? _account;
    private readonly WebApplicationFactory<StatisticsApp::Program>? _statistics;
    private readonly WebApplicationFactory<NotificationApp::Program>? _notification;
    private readonly GatewayFront _gateway;
    private readonly HttpClient _direct;
    private readonly CatalogHandler? _rates;

    private DestinationSide(
        ServiceKind service,
        GatewayFront gateway,
        HttpClient direct,
        WebApplicationFactory<AuthApp::Program>? auth,
        WebApplicationFactory<AccountApp::Program>? account,
        WebApplicationFactory<StatisticsApp::Program>? statistics,
        WebApplicationFactory<NotificationApp::Program>? notification,
        string? serverAccess,
        string? userAccess,
        CatalogHandler? rates)
    {
        Service = service;
        _gateway = gateway;
        _direct = direct;
        Client = gateway.Client;
        _auth = auth;
        _account = account;
        _statistics = statistics;
        _notification = notification;
        ServerAccess = serverAccess;
        UserAccess = userAccess;
        _rates = rates;
    }

    public ServiceKind Service { get; }

    public HttpClient Client { get; }

    public string? ServerAccess { get; }

    public string? UserAccess { get; }

    public IReadOnlyList<string> Forwarded => _gateway.Forwarded;

    public IReadOnlyList<string> RatesCalls => _rates?.Calls ?? [];

    public static async Task<DestinationSide> StartAsync(ServiceKind service)
    {
        return service switch
        {
            ServiceKind.Auth => await StartAuthAsync(),
            ServiceKind.Account => StartAccount(),
            ServiceKind.Statistics => StartStatistics(),
            ServiceKind.Notification => StartNotification(),
            _ => throw new ArgumentOutOfRangeException(nameof(service))
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _gateway.DisposeAsync();
        _direct.Dispose();
        if (_auth is not null)
        {
            await _auth.DisposeAsync();
        }

        if (_account is not null)
        {
            await _account.DisposeAsync();
        }

        if (_statistics is not null)
        {
            await _statistics.DisposeAsync();
        }

        if (_notification is not null)
        {
            await _notification.DisposeAsync();
        }
    }

    private static async Task<DestinationSide> StartAuthAsync()
    {
        var factory = new WebApplicationFactory<AuthApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ACCOUNT_SERVICE_PASSWORD"] = "account-secret",
                    ["STATISTICS_SERVICE_PASSWORD"] = "statistics-secret",
                    ["NOTIFICATION_SERVICE_PASSWORD"] = "notification-secret"
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<UserRepository>();
                services.AddSingleton<UserRepository, InMemoryUserRepository>();
            });
        });

        var direct = factory.CreateClient();
        var gateway = GatewayFront.Start(direct);
        var client = gateway.Client;
        var server = await IssueAsync(client, "account-service", "account-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "server"
        });
        var create = new HttpRequestMessage(HttpMethod.Post, "/uaa/users")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new AuthUser { Username = "demo", Password = "secret" }),
                Encoding.UTF8,
                "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer " + server);
        var created = await client.SendAsync(create);
        if (created.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("destination user seed returned " + (int)created.StatusCode);
        }

        var user = await IssueAsync(client, "browser", string.Empty, new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "demo",
            ["password"] = "secret",
            ["scope"] = "ui"
        });
        return new DestinationSide(ServiceKind.Auth, gateway, direct, factory, null, null, null, server, user, null);
    }

    private static DestinationSide StartAccount()
    {
        var repository = new InMemoryAccountRepository();
        repository.Save(SeedData.DemoAccount());
        repository.Save(SeedData.NamedAccount("alice", "alice"));
        repository.Save(SeedData.NamedAccount("account-service", "service"));
        var userInfo = ExternalStubCatalog.Handler(request => ExternalStubCatalog.UserInfo("account-service", request));
        var token = ExternalStubCatalog.Handler(ExternalStubCatalog.Token);
        var auth = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        var statistics = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        var factory = new WebApplicationFactory<AccountApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                Stub(services, "userinfo", userInfo);
                Stub(services, "token", token);
                Stub(services, "auth-service", auth);
                Stub(services, "statistics-service", statistics);
                services.RemoveAll<AccountRepository>();
                services.AddSingleton<AccountRepository>(repository);
            });
        });
        var direct = factory.CreateClient();
        var gateway = GatewayFront.Start(direct);
        return new DestinationSide(ServiceKind.Account, gateway, direct, null, factory, null, null, null, null, null);
    }

    private static DestinationSide StartStatistics()
    {
        var repository = new InMemoryDataPointRepository();
        repository.Save(SeedData.DemoDataPoint());
        var userInfo = ExternalStubCatalog.Handler(request => ExternalStubCatalog.UserInfo("statistics-service", request));
        var rates = ExternalStubCatalog.Handler(ExternalStubCatalog.Rates);
        var factory = new WebApplicationFactory<StatisticsApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                Stub(services, "userinfo", userInfo);
                Stub(services, "rates-client", rates);
                services.RemoveAll<DataPointRepository>();
                services.AddSingleton<DataPointRepository>(repository);
            });
        });
        var direct = factory.CreateClient();
        var gateway = GatewayFront.Start(direct);
        return new DestinationSide(ServiceKind.Statistics, gateway, direct, null, null, factory, null, null, null, rates);
    }

    private static DestinationSide StartNotification()
    {
        var repository = new InMemoryRecipientRepository();
        repository.Save(SeedData.DemoRecipient());
        var userInfo = ExternalStubCatalog.Handler(request => ExternalStubCatalog.UserInfo("notification-service", request));
        var token = ExternalStubCatalog.Handler(ExternalStubCatalog.Token);
        var account = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        var factory = new WebApplicationFactory<NotificationApp::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                Stub(services, "userinfo", userInfo);
                Stub(services, "token", token);
                Stub(services, "account-service", account);
                services.RemoveAll<RecipientRepository>();
                services.AddSingleton<RecipientRepository>(repository);
            });
        });
        var direct = factory.CreateClient();
        var gateway = GatewayFront.Start(direct);
        return new DestinationSide(ServiceKind.Notification, gateway, direct, null, null, null, factory, null, null, null);
    }

    private static void Stub(IServiceCollection services, string name, HttpMessageHandler handler)
    {
        services.PostConfigure<HttpClientFactoryOptions>(name, options =>
        {
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler);
        });
    }

    private static async Task<string> IssueAsync(HttpClient client, string clientId, string clientSecret, Dictionary<string, string> form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/uaa/oauth/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        using var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("destination token returned " + (int)response.StatusCode + " " + payload);
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("destination access_token missing");
    }

    private sealed class InMemoryUserRepository : UserRepository
    {
        private readonly Dictionary<string, AuthUser> _users = new(StringComparer.Ordinal);

        public AuthUser? FindById(string id) => _users.TryGetValue(id, out var user) ? user : null;

        public void Save(AuthUser user) => _users[user.Username ?? string.Empty] = user;
    }

    private sealed class InMemoryAccountRepository : AccountRepository
    {
        private readonly Dictionary<string, Account> _accounts = new(StringComparer.Ordinal);

        public Account? FindByName(string name) => _accounts.TryGetValue(name, out var account) ? account : null;

        public void Save(Account account) => _accounts[account.Name ?? string.Empty] = account;
    }

    private sealed class InMemoryDataPointRepository : DataPointRepository
    {
        private readonly List<DataPoint> _points = new();

        public List<DataPoint> FindByIdAccount(string account) =>
            _points.Where(point => point.Id!.Account == account).ToList();

        public DataPoint Save(DataPoint dataPoint)
        {
            _points.RemoveAll(point => point.Id!.Account == dataPoint.Id!.Account && point.Id.Date == dataPoint.Id.Date);
            _points.Add(dataPoint);
            return dataPoint;
        }
    }

    private sealed class InMemoryRecipientRepository : RecipientRepository
    {
        private readonly Dictionary<string, Recipient> _recipients = new(StringComparer.Ordinal);

        public Recipient? FindByAccountName(string name) => _recipients.TryGetValue(name, out var recipient) ? recipient : null;

        public void Save(Recipient recipient) => _recipients[recipient.AccountName ?? string.Empty] = recipient;

        public List<Recipient> FindReadyForBackup() => new();

        public List<Recipient> FindReadyForRemind() => new();
    }
}
