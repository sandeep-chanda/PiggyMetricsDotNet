using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Repository;
using PiggyMetrics.NotificationService.Service;
using Xunit;

namespace PiggyMetrics.NotificationService.Tests.Service;

public class RecipientServiceImplTest
{
    private readonly Mock<RecipientRepository> _repository = new();
    private readonly RecipientServiceImpl _recipientService;

    public RecipientServiceImplTest()
    {
        _recipientService = new RecipientServiceImpl(_repository.Object, NullLogger<RecipientServiceImpl>.Instance);
    }

    [Fact]
    public void shouldFindByAccountName()
    {
        var recipient = new Recipient
        {
            AccountName = "test"
        };
        _repository.Setup(repository => repository.FindByAccountName(recipient.AccountName!)).Returns(recipient);

        var found = _recipientService.FindByAccountName(recipient.AccountName!);

        Assert.Equal(recipient, found);
    }

    [Fact]
    public void shouldFailToFindRecipientWhenAccountNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => _recipientService.FindByAccountName(""));
    }

    [Fact]
    public void shouldSaveRecipient()
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
            LastNotified = DateTimeOffset.UtcNow
        };
        var recipient = new Recipient
        {
            Email = "test@test.com",
            ScheduledNotifications = new Dictionary<NotificationType, NotificationSettings>
            {
                [NotificationType.BACKUP] = backup,
                [NotificationType.REMIND] = remind
            }
        };

        var saved = _recipientService.Save("test", recipient);

        _repository.Verify(repository => repository.Save(recipient), Times.Once);
        Assert.NotNull(saved.ScheduledNotifications![NotificationType.REMIND].LastNotified);
        Assert.Equal("test", saved.AccountName);
    }

    [Fact]
    public void shouldFindReadyToNotifyWhenNotificationTypeIsBackup()
    {
        var recipients = new List<Recipient> { new() };
        _repository.Setup(repository => repository.FindReadyForBackup()).Returns(recipients);

        var found = _recipientService.FindReadyToNotify(NotificationType.BACKUP);

        Assert.Equal(recipients, found);
    }

    [Fact]
    public void shouldFindReadyToNotifyWhenNotificationTypeIsRemind()
    {
        var recipients = new List<Recipient> { new() };
        _repository.Setup(repository => repository.FindReadyForRemind()).Returns(recipients);

        var found = _recipientService.FindReadyToNotify(NotificationType.REMIND);

        Assert.Equal(recipients, found);
    }

    [Fact]
    public void shouldMarkAsNotified()
    {
        var remind = new NotificationSettings
        {
            Active = true,
            Frequency = Frequency.WEEKLY,
            LastNotified = null
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

        _recipientService.MarkNotified(NotificationType.REMIND, recipient);

        Assert.NotNull(recipient.ScheduledNotifications[NotificationType.REMIND].LastNotified);
        _repository.Verify(repository => repository.Save(recipient), Times.Once);
    }
}
