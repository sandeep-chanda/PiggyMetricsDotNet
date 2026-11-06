using System.Text.Json;
using System.Text.RegularExpressions;
using PiggyMetrics.Shared.Hosting;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class P9RetirementAcceptanceTests
{
    private static readonly string[] CarriedProjects =
    [
        "AccountService",
        "AccountService.Tests",
        "AuthService",
        "AuthService.Tests",
        "Gateway",
        "Gateway.Tests",
        "NotificationService",
        "NotificationService.Tests",
        "Parity.Tests",
        "Shared",
        "Shared.Tests",
        "StatisticsService",
        "StatisticsService.Tests"
    ];

    private static readonly string[] CarriedHttpClients =
    [
        "account-service",
        "auth-service",
        "rates-client",
        "statistics-service",
        "token",
        "userinfo",
        "zuul"
    ];

    private static readonly (string RelativePath, int Port)[] CarriedListeners =
    [
        ("src/Gateway/appsettings.json", 4000),
        ("src/AuthService/appsettings.json", 5000),
        ("src/AccountService/appsettings.json", 6000),
        ("src/StatisticsService/appsettings.json", 7000),
        ("src/NotificationService/appsettings.json", 8000)
    ];

    [Fact]
    public void P9_T1_config_server_is_off_and_nothing_reads_it()
    {
        AssertRetired(RetiredInfrastructure.ConfigServer, "P9.T1", "config", "config-server", "spring-cloud-config-server", 8888, 8888);
        AssertUnread("http://config:8888");
        AssertUnread("EnableConfigServer");
        AssertUnread("spring:cloud:config");
        AssertUnread("spring.cloud.config");
        AssertUnread("CONFIG_SERVICE_PASSWORD");
        Assert.DoesNotContain(NamedHttpClients(), name => string.Equals(name, "config", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void P9_T2_monitoring_dashboard_is_off_and_nothing_reads_it()
    {
        AssertRetired(
            RetiredInfrastructure.MonitoringDashboard,
            "P9.T2",
            "monitoring",
            "dashboard",
            "spring-cloud-starter-netflix-hystrix-dashboard",
            8080,
            9000);
        AssertUnread("/hystrix");
        AssertUnread("hystrix-dashboard");
        AssertUnread("hystrix.stream");
        Assert.False(Directory.Exists(Path.Combine(RepoRoot(), "src", "monitoring")));
    }

    [Fact]
    public void P9_T3_registry_is_off_and_nothing_reads_it()
    {
        AssertRetired(
            RetiredInfrastructure.Registry,
            "P9.T3",
            "registry",
            "registry",
            "spring-cloud-starter-netflix-eureka-server",
            8761,
            8761);
        AssertUnread("eureka");
        AssertUnread("http://registry:8761");
        AssertUnread("EnableEurekaServer");
        Assert.DoesNotContain(NamedHttpClients(), name => string.Equals(name, "registry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void P9_T4_turbine_stream_service_is_off_and_nothing_reads_it()
    {
        AssertRetired(
            RetiredInfrastructure.TurbineStream,
            "P9.T4",
            "turbine-stream-service",
            "metrics-stream",
            "spring-cloud-starter-netflix-turbine-stream",
            8989,
            8989);
        AssertUnread("turbine");
        AssertUnread("metrics-stream");
        Assert.DoesNotContain(NamedHttpClients(), name => name.Contains("turbine", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void P9_T5_broker_and_metrics_stream_starters_are_not_carried()
    {
        var starters = RetiredInfrastructure.BrokerAndMetricsStreamStarters;
        Assert.Equal(
            ["spring-cloud-starter-bus-amqp", "spring-cloud-starter-stream-rabbit", "spring-cloud-netflix-hystrix-stream"],
            starters.Select(starter => starter.JavaArtifact).ToArray());
        Assert.Equal(["broker", "metrics-stream", "metrics-stream"], starters.Select(starter => starter.Kind).ToArray());
        Assert.All(starters, starter => Assert.Equal("P9.T5", starter.TodoId));
        Assert.Equal("not carried", RetiredInfrastructure.NotCarried);

        foreach (var starter in starters)
        {
            AssertUnread(starter.JavaArtifact);
            Assert.DoesNotContain(ProjectTexts(), text => text.Contains(starter.JavaArtifact, StringComparison.OrdinalIgnoreCase));
        }

        string[] notCarried =
        [
            "RabbitMQ.Client",
            "MassTransit",
            "Steeltoe",
            "EasyNetQ",
            "RawRabbit",
            "NServiceBus",
            "Confluent.Kafka",
            "RabbitMQ"
        ];
        foreach (var token in notCarried)
        {
            AssertUnread(token);
            Assert.DoesNotContain(ProjectTexts(), text => text.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void each_retired_module_is_off_and_nothing_reads_it()
    {
        Assert.Equal(CarriedProjects, SolutionProjectNames());
        Assert.Equal(RetiredInfrastructure.CarriedSrcProjects, SrcProjectDirectories());
        Assert.Equal(CarriedHttpClients, NamedHttpClients());

        foreach (var module in RetiredInfrastructure.Modules)
        {
            Assert.Equal(RetiredInfrastructure.Retire, module.Disposition);
            AssertModuleAbsent(module.Module);
            Assert.True(RetiredInfrastructure.IsRetiredPort(module.ListenPort));
            Assert.True(RetiredInfrastructure.IsRetiredPort(module.PublishedPort));
            Assert.DoesNotContain(ProjectTexts(), text => text.Contains(module.JavaArtifact, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var (relativePath, port) in CarriedListeners)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), relativePath)));
            var actual = doc.RootElement.GetProperty("server").GetProperty("port").GetInt32();
            Assert.Equal(port, actual);
            Assert.False(RetiredInfrastructure.IsRetiredPort(actual), relativePath);
        }

        foreach (var url in HttpUrlsInSource())
        {
            Assert.DoesNotContain("config:", url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("registry:", url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("monitoring", url, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("turbine", url, StringComparison.OrdinalIgnoreCase);
            var portMatch = Regex.Match(url, @":(\d+)");
            if (portMatch.Success)
            {
                Assert.False(RetiredInfrastructure.IsRetiredPort(int.Parse(portMatch.Groups[1].Value)), url);
            }
        }
    }

    private static void AssertRetired(
        RetiredModule module,
        string todoId,
        string name,
        string role,
        string javaArtifact,
        int listenPort,
        int publishedPort)
    {
        Assert.Equal(todoId, module.TodoId);
        Assert.Equal(name, module.Module);
        Assert.Equal(role, module.Role);
        Assert.Equal(RetiredInfrastructure.Retire, module.Disposition);
        Assert.Equal(javaArtifact, module.JavaArtifact);
        Assert.Equal(listenPort, module.ListenPort);
        Assert.Equal(publishedPort, module.PublishedPort);
        AssertModuleAbsent(name);
        Assert.DoesNotContain(ProjectTexts(), text => text.Contains(javaArtifact, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ProjectTexts(), text => text.Contains($"src/{name}/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(ProjectTexts(), text => text.Contains($"src\\{name}\\", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertModuleAbsent(string module)
    {
        var root = RepoRoot();
        Assert.False(Directory.Exists(Path.Combine(root, "src", module)), module);
        Assert.False(Directory.Exists(Path.Combine(root, module)), module);
        Assert.DoesNotContain(SolutionProjectNames(), name => string.Equals(name, module, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertUnread(string token)
    {
        foreach (var file in SourceFiles("*.cs"))
        {
            if (string.Equals(Path.GetFileName(file), "RetiredInfrastructure.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string[] SolutionProjectNames()
    {
        return File.ReadAllLines(Path.Combine(RepoRoot(), "PiggyMetricsDotNet.sln"))
            .Where(line => line.StartsWith("Project(", StringComparison.Ordinal) && line.Contains(".csproj\"", StringComparison.Ordinal))
            .Select(line =>
            {
                var start = line.IndexOf("= \"", StringComparison.Ordinal) + 3;
                var end = line.IndexOf('"', start);
                return line[start..end];
            })
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] SrcProjectDirectories()
    {
        return Directory.GetDirectories(Path.Combine(RepoRoot(), "src"))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
    }

    private static string[] NamedHttpClients()
    {
        return SourceFiles("*.cs")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "AddHttpClient\\(\"([^\"]+)\"").Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] HttpUrlsInSource()
    {
        return SourceFiles("*.cs")
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), "https?://[A-Za-z0-9._-]+(?::\\d+)?").Select(match => match.Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(url => url, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> ProjectTexts()
    {
        var root = RepoRoot();
        return Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.sln", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Select(File.ReadAllText);
    }

    private static IEnumerable<string> SourceFiles(string pattern)
    {
        return Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path));
    }

    private static bool IsBuildOutput(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PiggyMetricsDotNet.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("PiggyMetricsDotNet.sln not found from test output directory");
    }
}
