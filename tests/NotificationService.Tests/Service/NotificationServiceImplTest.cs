using Microsoft.Extensions.Logging;
using Moq;
using PiggyMetrics.NotificationService.Client;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Service;
using Xunit;

namespace PiggyMetrics.NotificationService.Tests.Service;

public class NotificationServiceImplTest
{
    private readonly Mock<RecipientService> _recipientService = new();
    private readonly Mock<AccountServiceClient> _client = new();
    private readonly Mock<EmailService> _emailService = new();
    private readonly CaptureLogger _logger = new();
    private readonly NotificationServiceImpl _notificationService;

    public NotificationServiceImplTest()
    {
        _notificationService = new NotificationServiceImpl(
            _client.Object,
            _recipientService.Object,
            _emailService.Object,
            _logger);
    }

    [Fact]
    public async Task shouldSendBackupNotificationsEvenWhenErrorsOccursForSomeRecipients()
    {
        const string attachment = "json";
        var withError = new Recipient
        {
            AccountName = "with-error"
        };
        var withNoError = new Recipient
        {
            AccountName = "with-no-error"
        };

        _client.Setup(client => client.GetAccount(withError.AccountName!)).Throws(new InvalidOperationException());
        _client.Setup(client => client.GetAccount(withNoError.AccountName!)).Returns(attachment);
        _recipientService.Setup(service => service.FindReadyToNotify(NotificationType.BACKUP))
            .Returns(new List<Recipient> { withNoError, withError });

        _notificationService.SendBackupNotifications();

        await WaitUntil(() =>
        {
            _emailService.Verify(service => service.Send(NotificationType.BACKUP, withNoError, attachment), Times.Once);
            _recipientService.Verify(service => service.MarkNotified(NotificationType.BACKUP, withNoError), Times.Once);
        });

        _recipientService.Verify(service => service.MarkNotified(NotificationType.BACKUP, withError), Times.Never);
        Assert.Contains(_logger.Messages, message => message.Contains("an error during backup notification for"));
    }

    [Fact]
    public async Task shouldSendRemindNotificationsEvenWhenErrorsOccursForSomeRecipients()
    {
        var withError = new Recipient
        {
            AccountName = "with-error"
        };
        var withNoError = new Recipient
        {
            AccountName = "with-no-error"
        };

        _recipientService.Setup(service => service.FindReadyToNotify(NotificationType.REMIND))
            .Returns(new List<Recipient> { withNoError, withError });
        _emailService.Setup(service => service.Send(NotificationType.REMIND, withError, null))
            .Throws(new InvalidOperationException());

        _notificationService.SendRemindNotifications();

        await WaitUntil(() =>
        {
            _emailService.Verify(service => service.Send(NotificationType.REMIND, withNoError, null), Times.Once);
            _recipientService.Verify(service => service.MarkNotified(NotificationType.REMIND, withNoError), Times.Once);
        });

        _recipientService.Verify(service => service.MarkNotified(NotificationType.REMIND, withError), Times.Never);
        Assert.Contains(_logger.Messages, message => message.Contains("an error during remind notification for"));
    }

    private static async Task WaitUntil(Action assertion)
    {
        Exception? last = null;
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception ex)
            {
                last = ex;
                await Task.Delay(20);
            }
        }

        if (last is not null)
        {
            throw last;
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
