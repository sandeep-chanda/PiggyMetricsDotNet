namespace PiggyMetrics.Gateway.Proxy;

public static class GatewayHost
{
    public const string EntryAssemblyName = "PiggyMetrics.Gateway";

    public static string ListenUrl(IConfiguration configuration)
    {
        var port = configuration.GetValue("server:port", 4000);
        return "http://0.0.0.0:" + port;
    }
}

public static class GatewayTimeouts
{
    public static TimeSpan Connect(IConfiguration configuration)
    {
        var milliseconds = Positive(
            configuration,
            20000,
            "zuul:host:connect-timeout-millis",
            "ribbon:ConnectTimeout");
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    public static TimeSpan Command(IConfiguration configuration)
    {
        var milliseconds = Positive(
            configuration,
            20000,
            "hystrix:command:default:execution:isolation:thread:timeoutInMilliseconds",
            "zuul:host:socket-timeout-millis",
            "ribbon:ReadTimeout");
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static int Positive(IConfiguration configuration, int fallback, params string[] keys)
    {
        var values = new List<int>();
        foreach (var key in keys)
        {
            var value = configuration.GetValue(key, 0);
            if (value > 0)
            {
                values.Add(value);
            }
        }

        return values.Count == 0 ? fallback : values.Min();
    }
}
