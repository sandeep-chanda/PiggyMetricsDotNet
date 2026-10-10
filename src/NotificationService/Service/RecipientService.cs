using PiggyMetrics.NotificationService.Domain;

namespace PiggyMetrics.NotificationService.Service;

public interface RecipientService
{
    Recipient? FindByAccountName(string accountName);

    List<Recipient> FindReadyToNotify(NotificationType type);

    Recipient Save(string accountName, Recipient recipient);

    void MarkNotified(NotificationType type, Recipient recipient);
}
