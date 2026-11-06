using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using PiggyMetrics.NotificationService.Client;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Repository;
using PiggyMetrics.NotificationService.Service;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.NotificationService.Tests.Client;

public class AccountServiceClientTest
{
    [Fact]
    public void getAccount_uses_accounts_path_and_source_timeout()
    {
        var accounts = new RecordingHandler
        {
            Status = HttpStatusCode.OK,
            Body = "{\"name\":\"demo\"}"
        };
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<HttpClientFactoryOptions>("token", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = new RecordingHandler
                        {
                            Status = HttpStatusCode.OK,
                            Body = "{\"access_token\":\"issued\",\"expires_in\":3600}"
                        };
                    });
                });
                services.PostConfigure<HttpClientFactoryOptions>("account-service", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = accounts;
                    });
                });
            });
        });

        var http = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("account-service");
        Assert.Equal(TimeSpan.FromMilliseconds(10000), http.Timeout);
        Assert.Equal(EdgeHttpDefaults.Timeout, http.Timeout);
        Assert.Equal(new Uri("http://account-service:6000/"), http.BaseAddress);

        var client = factory.Services.GetRequiredService<AccountServiceClient>();
        var body = client.GetAccount("demo");

        Assert.Equal("{\"name\":\"demo\"}", body);
        Assert.Equal(HttpMethod.Get, accounts.Method);
        Assert.Equal("/accounts/demo", accounts.Path);
        Assert.Equal("Bearer", accounts.AuthorizationScheme);
    }

    [Fact]
    public void backup_failure_is_caught_and_logged_for_one_recipient()
    {
        var logs = new CaptureLogger();
        var accounts = new RecordingHandler { Status = HttpStatusCode.InternalServerError, Body = "down" };
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILogger<NotificationServiceImpl>>(logs);
                services.PostConfigure<HttpClientFactoryOptions>("token", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = new RecordingHandler
                        {
                            Status = HttpStatusCode.OK,
                            Body = "{\"access_token\":\"issued\",\"expires_in\":3600}"
                        };
                    });
                });
                services.PostConfigure<HttpClientFactoryOptions>("account-service", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = accounts;
                    });
                });
                services.AddSingleton<RecipientRepository>(new ReadyRecipientRepository());
            });
        });

        var client = factory.Services.GetRequiredService<AccountServiceClient>();
        var email = new NoopEmailService();
        var repository = new ReadyRecipientRepository();
        var recipientService = new RecipientServiceImpl(repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipientServiceImpl>.Instance);
        var notifications = new NotificationServiceImpl(client, recipientService, email, logs);

        notifications.SendBackupNotifications();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !logs.Messages.Any(message => message.Contains("an error during backup notification for")))
        {
            Thread.Sleep(20);
        }

        Assert.Equal("/accounts/demo", accounts.Path);
        Assert.Contains(logs.Messages, message => message.Contains("an error during backup notification for"));
        Assert.Empty(email.Sent);
    }

    private sealed class ReadyRecipientRepository : RecipientRepository
    {
        public Recipient? FindByAccountName(string name) => null;

        public void Save(Recipient recipient)
        {
        }

        public List<Recipient> FindReadyForBackup()
        {
            return new List<Recipient>
            {
                new()
                {
                    AccountName = "demo",
                    Email = "demo@piggymetrics.test"
                }
            };
        }

        public List<Recipient> FindReadyForRemind() => new();
    }

    private sealed class NoopEmailService : EmailService
    {
        public List<Recipient> Sent { get; } = new();

        public void Send(NotificationType type, Recipient recipient, string? attachment)
        {
            Sent.Add(recipient);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; }

        public string Body { get; init; } = string.Empty;

        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string? AuthorizationScheme { get; private set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json")
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }
    }

    private sealed class CaptureLogger : ILogger<NotificationServiceImpl>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
            {
                Messages.Add(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
