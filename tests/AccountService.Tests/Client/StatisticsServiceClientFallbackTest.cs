using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using PiggyMetrics.AccountService.Client;
using PiggyMetrics.AccountService.Domain;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.AccountService.Tests.Client;

public class StatisticsServiceClientFallbackTest
{
    private readonly CaptureLoggerProvider _logs = new();

    [Fact]
    public void testUpdateStatisticsWithFailFallback()
    {
        _logs.Messages.Clear();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["security:oauth2:client:accessTokenUri"] = "http://127.0.0.1:9/uaa/oauth/token",
                    ["clients:statistics-service"] = "http://127.0.0.1:9/"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(_logs);
            });
        });

        var statisticsServiceClient = factory.Services.GetRequiredService<StatisticsServiceClient>();
        statisticsServiceClient.UpdateStatistics("test", new Account());

        Assert.Contains(_logs.Messages, message => message.Contains("Error during update statistics for account: test"));
    }

    [Fact]
    public void updateStatistics_puts_statistics_path_and_falls_back_on_failure()
    {
        var statistics = new RecordingHandler { Status = HttpStatusCode.InternalServerError };
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(_logs);
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
                services.PostConfigure<HttpClientFactoryOptions>("statistics-service", options =>
                {
                    options.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.PrimaryHandler = statistics;
                    });
                });
            });
        });

        var http = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("statistics-service");
        Assert.Equal(TimeSpan.FromMilliseconds(10000), http.Timeout);
        Assert.Equal(EdgeHttpDefaults.Timeout, http.Timeout);
        Assert.Equal(new Uri("http://statistics-service:7000/"), http.BaseAddress);

        _logs.Messages.Clear();
        var statisticsServiceClient = factory.Services.GetRequiredService<StatisticsServiceClient>();
        statisticsServiceClient.UpdateStatistics("test", new Account());

        Assert.Equal(HttpMethod.Put, statistics.Method);
        Assert.Equal("/statistics/test", statistics.Path);
        Assert.Contains(_logs.Messages, message => message.Contains("Error during update statistics for account: test"));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; }

        public string Body { get; init; } = string.Empty;

        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, System.Text.Encoding.UTF8, "application/json")
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger : ILogger
        {
            private readonly List<string> _messages;

            public CaptureLogger(List<string> messages)
            {
                _messages = messages;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (_messages)
                {
                    _messages.Add(formatter(state, exception));
                }
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
