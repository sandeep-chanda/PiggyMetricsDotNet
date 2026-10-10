using PiggyMetrics.NotificationService.Client;
using PiggyMetrics.NotificationService.Domain;

namespace PiggyMetrics.NotificationService.Service;

public sealed class NotificationServiceImpl : NotificationService
{
    private readonly AccountServiceClient _client;
    private readonly RecipientService _recipientService;
    private readonly EmailService _emailService;
    private readonly ILogger<NotificationServiceImpl> _logger;

    public NotificationServiceImpl(
        AccountServiceClient client,
        RecipientService recipientService,
        EmailService emailService,
        ILogger<NotificationServiceImpl> logger)
    {
        _client = client;
        _recipientService = recipientService;
        _emailService = emailService;
        _logger = logger;
    }

    public void SendBackupNotifications()
    {
        const NotificationType type = NotificationType.BACKUP;
        var recipients = _recipientService.FindReadyToNotify(type);
        _logger.LogInformation("found {Count} recipients for backup notification", recipients.Count);
        foreach (var recipient in recipients)
        {
            var current = recipient;
            _ = Task.Run(() =>
            {
                try
                {
                    var attachment = _client.GetAccount(current.AccountName ?? string.Empty);
                    _emailService.Send(type, current, attachment);
                    _recipientService.MarkNotified(type, current);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "an error during backup notification for {Recipient}", current);
                }
            });
        }
    }

    public void SendRemindNotifications()
    {
        const NotificationType type = NotificationType.REMIND;
        var recipients = _recipientService.FindReadyToNotify(type);
        _logger.LogInformation("found {Count} recipients for remind notification", recipients.Count);
        foreach (var recipient in recipients)
        {
            var current = recipient;
            _ = Task.Run(() =>
            {
                try
                {
                    _emailService.Send(type, current, null);
                    _recipientService.MarkNotified(type, current);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "an error during remind notification for {Recipient}", current);
                }
            });
        }
    }
}
