using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PiggyMetrics.AccountService.Client;
using PiggyMetrics.AccountService.Domain;
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
