namespace PiggyMetrics.Shared.Hosting;

public sealed record RetiredModule(
    string TodoId,
    string Module,
    string Role,
    string Disposition,
    string JavaArtifact,
    int ListenPort,
    int PublishedPort);

public sealed record StarterNotCarried(
    string TodoId,
    string JavaArtifact,
    string Kind);

public static class RetiredInfrastructure
{
    public const string Retire = "retire";
    public const string NotCarried = "not carried";

    public static readonly RetiredModule ConfigServer = new(
        "P9.T1",
        "config",
        "config-server",
        Retire,
        "spring-cloud-config-server",
        8888,
        8888);

    public static readonly RetiredModule MonitoringDashboard = new(
        "P9.T2",
        "monitoring",
        "dashboard",
        Retire,
        "spring-cloud-starter-netflix-hystrix-dashboard",
        8080,
        9000);

    public static readonly RetiredModule Registry = new(
        "P9.T3",
        "registry",
        "registry",
        Retire,
        "spring-cloud-starter-netflix-eureka-server",
        8761,
        8761);

    public static readonly RetiredModule TurbineStream = new(
        "P9.T4",
        "turbine-stream-service",
        "metrics-stream",
        Retire,
        "spring-cloud-starter-netflix-turbine-stream",
        8989,
        8989);

    public static readonly RetiredModule[] Modules =
    [
        ConfigServer,
        MonitoringDashboard,
        Registry,
        TurbineStream
    ];

    public static readonly StarterNotCarried[] BrokerAndMetricsStreamStarters =
    [
        new("P9.T5", "spring-cloud-starter-bus-amqp", "broker"),
        new("P9.T5", "spring-cloud-starter-stream-rabbit", "metrics-stream"),
        new("P9.T5", "spring-cloud-netflix-hystrix-stream", "metrics-stream")
    ];

    public static readonly string[] CarriedSrcProjects =
    [
        "AccountService",
        "AuthService",
        "Gateway",
        "NotificationService",
        "Shared",
        "StatisticsService"
    ];

    public static bool IsRetiredPort(int port)
    {
        foreach (var module in Modules)
        {
            if (module.ListenPort == port || module.PublishedPort == port)
            {
                return true;
            }
        }

        return false;
    }
}
