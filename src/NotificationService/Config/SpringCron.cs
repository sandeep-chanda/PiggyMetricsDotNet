namespace PiggyMetrics.NotificationService.Config;

public static class SpringCron
{
    public static TimeSpan DelayUntilNext(string expression, DateTimeOffset now)
    {
        var delay = Next(expression, now) - now;
        return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    public static DateTimeOffset Next(string expression, DateTimeOffset now)
    {
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 6)
        {
            throw new ArgumentException("Spring cron requires 6 fields: " + expression);
        }

        var cursor = now.ToUniversalTime();
        cursor = new DateTimeOffset(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, cursor.Minute, cursor.Second, TimeSpan.Zero)
            .AddSeconds(1);
        for (var i = 0; i < 366 * 24 * 60; i++)
        {
            if (!Field(parts[4], cursor.Month) || !DayMatches(parts, cursor))
            {
                cursor = new DateTimeOffset(cursor.Year, cursor.Month, cursor.Day, 0, 0, 0, TimeSpan.Zero).AddDays(1);
                continue;
            }

            if (!Field(parts[2], cursor.Hour))
            {
                cursor = new DateTimeOffset(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0, TimeSpan.Zero).AddHours(1);
                continue;
            }

            if (!Field(parts[1], cursor.Minute))
            {
                cursor = new DateTimeOffset(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, cursor.Minute, 0, TimeSpan.Zero).AddMinutes(1);
                continue;
            }

            if (!Field(parts[0], cursor.Second))
            {
                cursor = cursor.AddSeconds(1);
                continue;
            }

            return cursor;
        }

        throw new InvalidOperationException("No cron match for " + expression);
    }

    private static bool DayMatches(string[] parts, DateTimeOffset instant)
    {
        var dayOfMonthStar = parts[3] == "*" || parts[3] == "?";
        var dayOfWeekStar = parts[5] == "*" || parts[5] == "?";
        var dayOfMonth = Field(parts[3], instant.Day);
        var dayOfWeek = Field(parts[5], (int)instant.DayOfWeek) || (parts[5] == "7" && instant.DayOfWeek == DayOfWeek.Sunday);
        if (dayOfMonthStar && dayOfWeekStar)
        {
            return true;
        }

        if (dayOfMonthStar)
        {
            return dayOfWeek;
        }

        if (dayOfWeekStar)
        {
            return dayOfMonth;
        }

        return dayOfMonth || dayOfWeek;
    }

    private static bool Field(string expression, int value)
    {
        if (expression == "*" || expression == "?")
        {
            return true;
        }

        foreach (var part in expression.Split(','))
        {
            var stepSplit = part.Split('/');
            var range = stepSplit[0];
            var step = stepSplit.Length > 1 ? int.Parse(stepSplit[1]) : 1;
            int start;
            int end;
            if (range == "*")
            {
                start = value;
                end = value;
            }
            else if (range.Contains('-'))
            {
                var bounds = range.Split('-');
                start = int.Parse(bounds[0]);
                end = int.Parse(bounds[1]);
            }
            else
            {
                start = int.Parse(range);
                end = start;
            }

            if (value >= start && value <= end && (value - start) % step == 0)
            {
                return true;
            }
        }

        return false;
    }
}
