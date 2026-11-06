using System.Net;
using System.Net.Mail;

namespace PiggyMetrics.NotificationService.Service;

public sealed class SmtpMailSender : IMailSender
{
    private readonly IConfiguration _configuration;

    public SmtpMailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public OutboundMail CreateMimeMessage()
    {
        return new OutboundMail();
    }

    public void Send(OutboundMail message)
    {
        var host = _configuration["spring:mail:host"] ?? "smtp.gmail.com";
        var port = int.Parse(_configuration["spring:mail:port"] ?? "465");
        var username = _configuration["spring:mail:username"] ?? "dev-user";
        var password = _configuration["spring:mail:password"] ?? "dev-password";
        var ssl = bool.TryParse(_configuration["spring:mail:properties:mail:smtp:ssl:enable"], out var enabled)
            ? enabled
            : true;

        using var mail = new MailMessage(username, message.To ?? string.Empty, message.Subject ?? string.Empty, message.Body ?? string.Empty);
        if (message.AttachmentBytes is { Length: > 0 })
        {
            mail.Attachments.Add(new Attachment(
                new MemoryStream(message.AttachmentBytes),
                message.AttachmentFileName ?? "backup.json"));
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = ssl,
            Credentials = new NetworkCredential(username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        client.Send(mail);
    }
}
