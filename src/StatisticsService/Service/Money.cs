namespace PiggyMetrics.StatisticsService.Service;

internal static class Money
{
    public static decimal Divide(decimal amount, decimal divisor, int scale)
    {
        return Math.Round(amount / divisor, scale, MidpointRounding.AwayFromZero);
    }
}
