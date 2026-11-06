using PiggyMetrics.NotificationService.Domain;

namespace PiggyMetrics.NotificationService.Repository.Converter;

public static class FrequencyReaderConverter
{
    public static Frequency Convert(int days)
    {
        return FrequencyDays.WithDays(days);
    }
}
