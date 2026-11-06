namespace PiggyMetrics.NotificationService.Service;

public interface NotificationService
{
    void SendBackupNotifications();

    void SendRemindNotifications();
}
