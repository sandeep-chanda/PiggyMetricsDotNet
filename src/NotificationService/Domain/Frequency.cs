namespace PiggyMetrics.NotificationService.Domain;

public enum Frequency
{
    WEEKLY = 7,
    MONTHLY = 30,
    QUARTERLY = 90
}

public static class FrequencyDays
{
    public static Frequency WithDays(int days)
    {
        foreach (var frequency in Enum.GetValues<Frequency>())
        {
            if ((int)frequency == days)
            {
                return frequency;
            }
        }

        throw new ArgumentException();
    }
}
