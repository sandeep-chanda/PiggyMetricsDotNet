using PiggyMetrics.NotificationService.Domain;

namespace PiggyMetrics.NotificationService.Service;

public interface EmailService
{
    void Send(NotificationType type, Recipient recipient, string? attachment);
}

public interface IMailSender
{
    OutboundMail CreateMimeMessage();

    void Send(OutboundMail message);
}

public sealed class OutboundMail
{
    public string? To { get; set; }

    public string? Subject { get; set; }

    public string? Body { get; set; }

    public string? AttachmentFileName { get; set; }

    public byte[]? AttachmentBytes { get; set; }
}
