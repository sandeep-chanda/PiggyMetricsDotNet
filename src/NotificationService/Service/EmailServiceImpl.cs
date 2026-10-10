using System.Text;
using PiggyMetrics.NotificationService.Domain;

namespace PiggyMetrics.NotificationService.Service;

public sealed class EmailServiceImpl : EmailService
{
    private readonly IMailSender _mailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailServiceImpl> _logger;

    public EmailServiceImpl(IMailSender mailSender, IConfiguration configuration, ILogger<EmailServiceImpl> logger)
    {
        _mailSender = mailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public void Send(NotificationType type, Recipient recipient, string? attachment)
    {
        var subject = Property(NotificationTypeKeys.Subject(type))
            ?? throw new InvalidOperationException("subject missing");
        var text = JavaMessageFormat.Format(
            Property(NotificationTypeKeys.Text(type)) ?? throw new InvalidOperationException("text missing"),
            recipient.AccountName ?? string.Empty);

        var message = _mailSender.CreateMimeMessage();
        message.To = recipient.Email;
        message.Subject = subject;
        message.Body = text;

        if (!string.IsNullOrEmpty(attachment))
        {
            message.AttachmentFileName = Property(NotificationTypeKeys.Attachment(type));
            message.AttachmentBytes = Encoding.UTF8.GetBytes(attachment);
        }

        _mailSender.Send(message);
        _logger.LogInformation("{Type} email notification has been send to {Email}", type, recipient.Email);
    }

    private string? Property(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        return _configuration[key.Replace('.', ':')];
    }
}
