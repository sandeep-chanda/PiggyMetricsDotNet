using PiggyMetrics.NotificationService.Domain;

namespace PiggyMetrics.NotificationService.Repository.Converter;

public static class FrequencyWriterConverter
{
    public static int Convert(Frequency frequency)
    {
        return (int)frequency;
    }
}
