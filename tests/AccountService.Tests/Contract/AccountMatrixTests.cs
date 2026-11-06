using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.AccountService.Repository;
using Xunit;

namespace PiggyMetrics.AccountService.Tests.Contract;

public class AccountMatrixTests
{
    private const string UserToken = "user-token";
    private const string ServerToken = "server-token";

    [Fact]
    public async Task C4_server_or_demo_policy_statuses()
    {
        await using var host = await AccountDestination.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.GetAsync("/accounts/alice", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.GetAsync("/accounts/alice", UserToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/accounts/alice", ServerToken)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/accounts/demo", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/accounts/demo", UserToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/accounts/demo", ServerToken)).StatusCode);

        var serverBody = await host.GetAsync("/accounts/demo", ServerToken);
        using var document = JsonDocument.Parse(await serverBody.Content.ReadAsStringAsync());
        Assert.Equal("demo", document.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task C5_user_policy_returns_source_body_or_401()
    {
        await using var host = await AccountDestination.StartAsync();

        var missing = await host.GetAsync("/accounts/current", null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var response = await host.GetAsync("/accounts/current", UserToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var account = document.RootElement;
        Assert.Equal("demo", account.GetProperty("name").GetString());
        Assert.Equal("demo note", account.GetProperty("note").GetString());
        Assert.Equal(5900m, account.GetProperty("saving").GetProperty("amount").GetDecimal());
        Assert.Equal("USD", account.GetProperty("saving").GetProperty("currency").GetString());
        Assert.Equal(3.32m, account.GetProperty("saving").GetProperty("interest").GetDecimal());
        Assert.True(account.GetProperty("saving").GetProperty("deposit").GetBoolean());
        Assert.False(account.GetProperty("saving").GetProperty("capitalization").GetBoolean());
        Assert.Equal("Salary", account.GetProperty("incomes")[0].GetProperty("title").GetString());
        Assert.Equal(42000m, account.GetProperty("incomes")[0].GetProperty("amount").GetDecimal());
        Assert.Equal("Rent", account.GetProperty("expenses")[0].GetProperty("title").GetString());
        Assert.Equal(1300m, account.GetProperty("expenses")[0].GetProperty("amount").GetDecimal());

        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync("/accounts/current", ServerToken)).StatusCode);
    }

    [Fact]
    public async Task C6_user_policy_put_returns_source_body_or_401()
    {
        await using var host = await AccountDestination.StartAsync();
        const string body = """
            {
              "note": "updated",
              "saving": {"amount": 1500, "currency": "USD", "interest": 3.32, "deposit": true, "capitalization": false},
              "expenses": [{"title": "Grocery", "amount": 10, "currency": "USD", "period": "DAY", "icon": "meal"}],
              "incomes": [{"title": "Salary", "amount": 9100, "currency": "USD", "period": "MONTH", "icon": "wallet"}]
            }
            """;

        var missing = await host.PutAsync("/accounts/current", body, null);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        var response = await host.PutAsync("/accounts/current", body, UserToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());

        var server = await host.PutAsync("/accounts/current", body, ServerToken);
        Assert.Equal(HttpStatusCode.OK, server.StatusCode);
        Assert.Equal(string.Empty, await server.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task C7_anonymous_post_returns_source_status_and_body()
    {
        await using var host = await AccountDestination.StartAsync();
        const string body = """
            {"username":"newuser","password":"password"}
            """;

        var response = await host.PostAsync("/accounts", body, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var account = document.RootElement;
        Assert.Equal("newuser", account.GetProperty("name").GetString());
        Assert.Equal(0m, account.GetProperty("saving").GetProperty("amount").GetDecimal());
        Assert.Equal("USD", account.GetProperty("saving").GetProperty("currency").GetString());
        Assert.Equal(0m, account.GetProperty("saving").GetProperty("interest").GetDecimal());
        Assert.False(account.GetProperty("saving").GetProperty("deposit").GetBoolean());
        Assert.False(account.GetProperty("saving").GetProperty("capitalization").GetBoolean());
        Assert.False(string.IsNullOrEmpty(account.GetProperty("lastSeen").GetString()));

        var asUser = await host.PostAsync(
            "/accounts",
            """{"username":"fromuser","password":"password"}""",
            UserToken);
        Assert.Equal(HttpStatusCode.OK, asUser.StatusCode);
        using var userDocument = JsonDocument.Parse(await asUser.Content.ReadAsStringAsync());
        Assert.Equal("fromuser", userDocument.RootElement.GetProperty("name").GetString());

        var asServer = await host.PostAsync(
            "/accounts",
            """{"username":"fromserver","password":"password"}""",
            ServerToken);
        Assert.Equal(HttpStatusCode.OK, asServer.StatusCode);
        using var serverDocument = JsonDocument.Parse(await asServer.Content.ReadAsStringAsync());
        Assert.Equal("fromserver", serverDocument.RootElement.GetProperty("name").GetString());
    }

    private sealed class AccountDestination : IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        private AccountDestination(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        public static Task<AccountDestination> StartAsync()
        {
            var repository = new InMemoryAccountRepository();
            repository.Save(DemoAccount());
            repository.Save(new Account { Name = "alice", Note = "alice", Saving = EmptySaving() });
            repository.Save(new Account { Name = "account-service", Note = "service", Saving = EmptySaving() });

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
                    services.PostConfigure<HttpClientFactoryOptions>("auth-service", options =>
                    {
                        options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                        {
                            handlerBuilder.PrimaryHandler = new OkStubHandler();
                        });
                    });
                    services.PostConfigure<HttpClientFactoryOptions>("statistics-service", options =>
                    {
                        options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                        {
                            handlerBuilder.PrimaryHandler = new OkStubHandler();
                        });
                    });
                    services.RemoveAll<AccountRepository>();
                    services.AddSingleton<AccountRepository>(repository);
                });
            });
            return Task.FromResult(new AccountDestination(factory));
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

        public Task<HttpResponseMessage> PostAsync(string path, string json, string? bearer)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path)
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

        private static Saving EmptySaving() => new()
        {
            Amount = 0m,
            Currency = Currency.USD,
            Interest = 0m,
            Deposit = false,
            Capitalization = false
        };

        private static Account DemoAccount() => new()
        {
            Name = "demo",
            Note = "demo note",
            LastSeen = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000),
            Saving = new Saving
            {
                Amount = 5900m,
                Capitalization = false,
                Currency = Currency.USD,
                Deposit = true,
                Interest = 3.32m
            },
            Expenses = new List<Item>
            {
                new() { Amount = 1300m, Currency = Currency.USD, Icon = "home", Period = TimePeriod.MONTH, Title = "Rent" },
                new() { Amount = 120m, Currency = Currency.USD, Icon = "utilities", Period = TimePeriod.MONTH, Title = "Utilities" },
                new() { Amount = 20m, Currency = Currency.USD, Icon = "meal", Period = TimePeriod.DAY, Title = "Meal" },
                new() { Amount = 240m, Currency = Currency.USD, Icon = "gas", Period = TimePeriod.MONTH, Title = "Gas" },
                new() { Amount = 3500m, Currency = Currency.EUR, Icon = "island", Period = TimePeriod.YEAR, Title = "Vacation" },
                new() { Amount = 30m, Currency = Currency.EUR, Icon = "phone", Period = TimePeriod.MONTH, Title = "Phone" },
                new() { Amount = 700m, Currency = Currency.USD, Icon = "sport", Period = TimePeriod.YEAR, Title = "Gym" }
            },
            Incomes = new List<Item>
            {
                new() { Amount = 42000m, Currency = Currency.USD, Icon = "wallet", Period = TimePeriod.YEAR, Title = "Salary" },
                new() { Amount = 500m, Currency = Currency.USD, Icon = "edu", Period = TimePeriod.MONTH, Title = "Scholarship" }
            }
        };
    }

    private sealed class InMemoryAccountRepository : AccountRepository
    {
        private readonly Dictionary<string, Account> _accounts = new(StringComparer.Ordinal);

        public Account? FindByName(string name)
        {
            return _accounts.TryGetValue(name, out var account) ? account : null;
        }

        public void Save(Account account)
        {
            _accounts[account.Name ?? string.Empty] = account;
        }
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
                ServerToken => "{\"name\":\"account-service\",\"oauth2Request\":{\"clientId\":\"account-service\",\"scope\":[\"server\"]}}",
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

    private sealed class OkStubHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return SendAsync(request, cancellationToken).GetAwaiter().GetResult();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            });
        }
    }
}
