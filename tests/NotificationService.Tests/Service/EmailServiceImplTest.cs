using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Service;
using Xunit;

namespace PiggyMetrics.NotificationService.Tests.Service;

public class EmailServiceImplTest
{
    private readonly Mock<IMailSender> _mailSender = new();

    public EmailServiceImplTest()
    {
        _mailSender.Setup(sender => sender.CreateMimeMessage()).Returns(() => new OutboundMail());
    }

    [Fact]
    public void shouldSendBackupEmail()
    {
        const string subject = "subject";
        const string text = "text";
        const string attachment = "attachment.json";
        var recipient = new Recipient
        {
            AccountName = "test",
            Email = "test@test.com"
        };
        OutboundMail? sent = null;
        _mailSender.Setup(sender => sender.Send(It.IsAny<OutboundMail>()))
            .Callback<OutboundMail>(message => sent = message);

        var emailService = CreateService(new Dictionary<string, string?>
        {
            [NotificationTypeKeys.Subject(NotificationType.BACKUP).Replace('.', ':')] = subject,
            [NotificationTypeKeys.Text(NotificationType.BACKUP).Replace('.', ':')] = text,
            [NotificationTypeKeys.Attachment(NotificationType.BACKUP)!.Replace('.', ':')] = attachment
        });

        emailService.Send(NotificationType.BACKUP, recipient, "{\"name\":\"test\"");

        _mailSender.Verify(sender => sender.Send(It.IsAny<OutboundMail>()), Times.Once);
        Assert.NotNull(sent);
        Assert.Equal(subject, sent!.Subject);
    }

    [Fact]
    public void shouldSendRemindEmail()
    {
        const string subject = "subject";
        const string text = "text";
        var recipient = new Recipient
        {
            AccountName = "test",
            Email = "test@test.com"
        };
        OutboundMail? sent = null;
        _mailSender.Setup(sender => sender.Send(It.IsAny<OutboundMail>()))
            .Callback<OutboundMail>(message => sent = message);

        var emailService = CreateService(new Dictionary<string, string?>
        {
            [NotificationTypeKeys.Subject(NotificationType.REMIND).Replace('.', ':')] = subject,
            [NotificationTypeKeys.Text(NotificationType.REMIND).Replace('.', ':')] = text
        });

        emailService.Send(NotificationType.REMIND, recipient, null);

        _mailSender.Verify(sender => sender.Send(It.IsAny<OutboundMail>()), Times.Once);
        Assert.NotNull(sent);
        Assert.Equal(subject, sent!.Subject);
    }

    private EmailServiceImpl CreateService(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new EmailServiceImpl(_mailSender.Object, configuration, NullLogger<EmailServiceImpl>.Instance);
    }
}
