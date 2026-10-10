namespace PiggyMetrics.NotificationService.Domain;

public enum NotificationType
{
    BACKUP,
    REMIND
}

public static class NotificationTypeKeys
{
    public static string Subject(NotificationType type)
    {
        return type switch
        {
            NotificationType.BACKUP => "backup.email.subject",
            NotificationType.REMIND => "remind.email.subject",
            _ => throw new ArgumentException()
        };
    }

    public static string Text(NotificationType type)
    {
        return type switch
        {
            NotificationType.BACKUP => "backup.email.text",
            NotificationType.REMIND => "remind.email.text",
            _ => throw new ArgumentException()
        };
    }

    public static string? Attachment(NotificationType type)
    {
        return type switch
        {
            NotificationType.BACKUP => "backup.email.attachment",
            NotificationType.REMIND => null,
            _ => throw new ArgumentException()
        };
    }
}
