using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using PiggyMetrics.NotificationService.Client;
using PiggyMetrics.NotificationService.Config;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Repository;
using PiggyMetrics.NotificationService.Service;
using Testcontainers.MongoDb;
using Xunit;

namespace PiggyMetrics.NotificationService.Tests.Service;

public class NotificationScheduleTests : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0").Build();

    private IMongoDatabase _database = null!;
    private RecordingEmail _email = null!;
    private NotificationServiceImpl _notifications = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var client = new MongoClient(_container.GetConnectionString());
        _database = client.GetDatabase("piggymetrics");
        var repository = new MongoRecipientRepository(_database);
        var recipients = new RecipientServiceImpl(repository, NullLogger<RecipientServiceImpl>.Instance);
        _email = new RecordingEmail();
        _notifications = new NotificationServiceImpl(
            new FixedAccountClient("{\"name\":\"demo\"}"),
            recipients,
            _email,
            NullLogger<NotificationServiceImpl>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task sendBackupNotifications_fires_for_due_seed_on_cron_0_0_12_and_skips_not_due()
    {
        Assert.Equal(
            new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero),
            SpringCron.Next("0 0 12 * * *", new DateTimeOffset(2026, 10, 10, 11, 0, 0, TimeSpan.Zero)));

        _database.DropCollection("recipients");
        Save("due-backup", NotificationType.BACKUP, true, Frequency.MONTHLY, DateTimeOffset.UtcNow.AddDays(-40));
        Save("fresh-backup", NotificationType.BACKUP, true, Frequency.MONTHLY, DateTimeOffset.UtcNow.AddDays(-1));

        _notifications.SendBackupNotifications();

        await WaitUntil(() => _email.Sent.Any(item => item.Account == "due-backup"));

        Assert.Contains(_email.Sent, item => item.Type == NotificationType.BACKUP && item.Account == "due-backup" && item.Attachment == "{\"name\":\"demo\"}");
        Assert.DoesNotContain(_email.Sent, item => item.Account == "fresh-backup");
    }

    [Fact]
    public async Task sendRemindNotifications_fires_for_due_seed_on_cron_0_0_0_and_skips_not_due()
    {
        Assert.Equal(
            new DateTimeOffset(2026, 10, 11, 0, 0, 0, TimeSpan.Zero),
            SpringCron.Next("0 0 0 * * *", new DateTimeOffset(2026, 10, 10, 23, 0, 0, TimeSpan.Zero)));

        _database.DropCollection("recipients");
        Save("due-remind", NotificationType.REMIND, true, Frequency.WEEKLY, DateTimeOffset.UtcNow.AddDays(-8));
        Save("fresh-remind", NotificationType.REMIND, true, Frequency.WEEKLY, DateTimeOffset.UtcNow.AddDays(-1));

        _notifications.SendRemindNotifications();

        await WaitUntil(() => _email.Sent.Any(item => item.Account == "due-remind"));

        Assert.Contains(_email.Sent, item => item.Type == NotificationType.REMIND && item.Account == "due-remind" && item.Attachment == null);
        Assert.DoesNotContain(_email.Sent, item => item.Account == "fresh-remind");
    }

    private void Save(string accountName, NotificationType type, bool active, Frequency frequency, DateTimeOffset lastNotified)
    {
        var repository = new MongoRecipientRepository(_database);
        repository.Save(new Recipient
        {
            AccountName = accountName,
            Email = accountName + "@piggymetrics.test",
            ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
            {
                [type] = new()
                {
                    Active = active,
                    Frequency = frequency,
                    LastNotified = Milliseconds(lastNotified)
                }
            }
        });
    }

    private static DateTimeOffset Milliseconds(DateTimeOffset value)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }
    }

    private sealed class FixedAccountClient : AccountServiceClient
    {
        private readonly string _body;

        public FixedAccountClient(string body)
        {
            _body = body;
        }

        public string GetAccount(string accountName)
        {
            return _body;
        }
    }

    private sealed class RecordingEmail : EmailService
    {
        private readonly List<SentMail> _sent = new();

        public IReadOnlyList<SentMail> Sent
        {
            get
            {
                lock (_sent)
                {
                    return _sent.ToList();
                }
            }
        }

        public void Send(NotificationType type, Recipient recipient, string? attachment)
        {
            lock (_sent)
            {
                _sent.Add(new SentMail(type, recipient.AccountName, attachment));
            }
        }
    }

    private sealed record SentMail(NotificationType Type, string? Account, string? Attachment);
}
