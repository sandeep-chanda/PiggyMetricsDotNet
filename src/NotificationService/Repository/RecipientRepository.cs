using PiggyMetrics.NotificationService.Domain;

namespace PiggyMetrics.NotificationService.Repository;

public interface RecipientRepository
{
    Recipient? FindByAccountName(string name);

    void Save(Recipient recipient);

    List<Recipient> FindReadyForBackup();

    List<Recipient> FindReadyForRemind();
}
