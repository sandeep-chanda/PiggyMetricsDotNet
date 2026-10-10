using MongoDB.Driver;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Repository;
using Testcontainers.MongoDb;
using Xunit;

namespace PiggyMetrics.NotificationService.Tests.Repository;

public class RecipientRepositoryTest : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:7.0").Build();

    private IMongoDatabase _database = null!;
    private MongoRecipientRepository _repository = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var client = new MongoClient(_container.GetConnectionString());
        _database = client.GetDatabase("piggymetrics");
        _repository = new MongoRecipientRepository(_database);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    [Fact]
    public void shouldFindByAccountName()
    {
        Reset();
        var remind = new NotificationSettings
        {
            Active = true,
            Frequency = Frequency.WEEKLY,
            LastNotified = DateTimeOffset.UnixEpoch
        };
        var backup = new NotificationSettings
        {
            Active = false,
            Frequency = Frequency.MONTHLY,
            LastNotified = Milliseconds(DateTimeOffset.UtcNow)
        };
        var recipient = new Recipient
        {
            AccountName = "test",
            Email = "test@test.com",
            ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
            {
                [NotificationType.BACKUP] = backup,
                [NotificationType.REMIND] = remind
            }
        };

        _repository.Save(recipient);

        var found = _repository.FindByAccountName(recipient.AccountName!);
        Assert.Equal(recipient.AccountName, found!.AccountName);
        Assert.Equal(recipient.Email, found.Email);

        Assert.Equal(
            recipient.ScheduledNotifications[NotificationType.BACKUP].Active,
            found.ScheduledNotifications![NotificationType.BACKUP].Active);
        Assert.Equal(
            recipient.ScheduledNotifications[NotificationType.BACKUP].Frequency,
            found.ScheduledNotifications[NotificationType.BACKUP].Frequency);
        Assert.Equal(
            recipient.ScheduledNotifications[NotificationType.BACKUP].LastNotified,
            found.ScheduledNotifications[NotificationType.BACKUP].LastNotified);

        Assert.Equal(
            recipient.ScheduledNotifications[NotificationType.REMIND].Active,
            found.ScheduledNotifications[NotificationType.REMIND].Active);
        Assert.Equal(
            recipient.ScheduledNotifications[NotificationType.REMIND].Frequency,
            found.ScheduledNotifications[NotificationType.REMIND].Frequency);
        Assert.Equal(
            recipient.ScheduledNotifications[NotificationType.REMIND].LastNotified,
            found.ScheduledNotifications[NotificationType.REMIND].LastNotified);
    }

    [Fact]
    public void shouldFindReadyForRemindWhenFrequencyIsWeeklyAndLastNotifiedWas8DaysAgo()
    {
        Reset();
        var remind = new NotificationSettings
        {
            Active = true,
            Frequency = Frequency.WEEKLY,
            LastNotified = Milliseconds(DateTimeOffset.UtcNow.AddDays(-8))
        };
        var recipient = new Recipient
        {
            AccountName = "test",
            Email = "test@test.com",
            ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
            {
                [NotificationType.REMIND] = remind
            }
        };

        _repository.Save(recipient);

        var found = _repository.FindReadyForRemind();
        Assert.NotEmpty(found);
    }

    [Fact]
    public void shouldNotFindReadyForRemindWhenFrequencyIsWeeklyAndLastNotifiedWasYesterday()
    {
        Reset();
        var remind = new NotificationSettings
        {
            Active = true,
            Frequency = Frequency.WEEKLY,
            LastNotified = Milliseconds(DateTimeOffset.UtcNow.AddDays(-1))
        };
        var recipient = new Recipient
        {
            AccountName = "test",
            Email = "test@test.com",
            ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
            {
                [NotificationType.REMIND] = remind
            }
        };

        _repository.Save(recipient);

        var found = _repository.FindReadyForRemind();
        Assert.Empty(found);
    }

    [Fact]
    public void shouldNotFindReadyForRemindWhenNotificationIsNotActive()
    {
        Reset();
        var remind = new NotificationSettings
        {
            Active = false,
            Frequency = Frequency.WEEKLY,
            LastNotified = Milliseconds(DateTimeOffset.UtcNow.AddDays(-30))
        };
        var recipient = new Recipient
        {
            AccountName = "test",
            Email = "test@test.com",
            ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
            {
                [NotificationType.REMIND] = remind
            }
        };

        _repository.Save(recipient);

        var found = _repository.FindReadyForRemind();
        Assert.Empty(found);
    }

    [Fact]
    public void shouldNotFindReadyForBackupWhenFrequencyIsQuaterly()
    {
        Reset();
        var remind = new NotificationSettings
        {
            Active = true,
            Frequency = Frequency.QUARTERLY,
            LastNotified = Milliseconds(DateTimeOffset.UtcNow.AddDays(-91))
        };
        var recipient = new Recipient
        {
            AccountName = "test",
            Email = "test@test.com",
            ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
            {
                [NotificationType.BACKUP] = remind
            }
        };

        _repository.Save(recipient);

        var found = _repository.FindReadyForBackup();
        Assert.NotEmpty(found);
    }

    private void Reset()
    {
        _database.DropCollection("recipients");
    }

    private static DateTimeOffset Milliseconds(DateTimeOffset value)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(value.ToUnixTimeMilliseconds());
    }
}
