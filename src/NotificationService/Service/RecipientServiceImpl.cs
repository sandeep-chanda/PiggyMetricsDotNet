using PiggyMetrics.NotificationService.Domain;
using PiggyMetrics.NotificationService.Repository;

namespace PiggyMetrics.NotificationService.Service;

public sealed class RecipientServiceImpl : RecipientService
{
    private readonly RecipientRepository _repository;
    private readonly ILogger<RecipientServiceImpl> _logger;

    public RecipientServiceImpl(RecipientRepository repository, ILogger<RecipientServiceImpl> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Recipient? FindByAccountName(string accountName)
    {
        if (string.IsNullOrEmpty(accountName))
        {
            throw new ArgumentException("this String argument must have length; it must not be null or empty");
        }

        return _repository.FindByAccountName(accountName);
    }

    public Recipient Save(string accountName, Recipient recipient)
    {
        recipient.AccountName = accountName;
        foreach (var settings in recipient.ScheduledNotifications!.Values)
        {
            if (settings.LastNotified is null)
            {
                settings.LastNotified = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }
        }

        _repository.Save(recipient);
        _logger.LogInformation("recipient {Recipient} settings has been updated", recipient);
        return recipient;
    }

    public List<Recipient> FindReadyToNotify(NotificationType type)
    {
        return type switch
        {
            NotificationType.BACKUP => _repository.FindReadyForBackup(),
            NotificationType.REMIND => _repository.FindReadyForRemind(),
            _ => throw new ArgumentException()
        };
    }

    public void MarkNotified(NotificationType type, Recipient recipient)
    {
        recipient.ScheduledNotifications![type].LastNotified =
            DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _repository.Save(recipient);
    }
}
