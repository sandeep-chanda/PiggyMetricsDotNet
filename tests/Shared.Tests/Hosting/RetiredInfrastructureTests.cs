using PiggyMetrics.Shared.Hosting;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class RetiredInfrastructureTests
{
    [Fact]
    public void P9_T1_config_server_is_retired()
    {
        var module = RetiredInfrastructure.ConfigServer;
        Assert.Equal("P9.T1", module.TodoId);
        Assert.Equal("config", module.Module);
        Assert.Equal("config-server", module.Role);
        Assert.Equal(RetiredInfrastructure.Retire, module.Disposition);
        Assert.Equal("spring-cloud-config-server", module.JavaArtifact);
        Assert.Equal(8888, module.ListenPort);
        Assert.Equal(8888, module.PublishedPort);
        AssertRetired(module);
        Assert.DoesNotContain(
            SourceCs(),
            text => text.Contains("http://config:8888", StringComparison.Ordinal)
                || text.Contains("EnableConfigServer", StringComparison.Ordinal));
    }

    [Fact]
    public void P9_T2_monitoring_dashboard_is_retired()
    {
        var module = RetiredInfrastructure.MonitoringDashboard;
        Assert.Equal("P9.T2", module.TodoId);
        Assert.Equal("monitoring", module.Module);
        Assert.Equal("dashboard", module.Role);
        Assert.Equal(RetiredInfrastructure.Retire, module.Disposition);
        Assert.Equal("spring-cloud-starter-netflix-hystrix-dashboard", module.JavaArtifact);
        Assert.Equal(8080, module.ListenPort);
        Assert.Equal(9000, module.PublishedPort);
        AssertRetired(module);
        Assert.DoesNotContain(SourceText(), text => text.Contains("/hystrix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void P9_T3_registry_is_retired()
    {
        var module = RetiredInfrastructure.Registry;
        Assert.Equal("P9.T3", module.TodoId);
        Assert.Equal("registry", module.Module);
        Assert.Equal("registry", module.Role);
        Assert.Equal(RetiredInfrastructure.Retire, module.Disposition);
        Assert.Equal("spring-cloud-starter-netflix-eureka-server", module.JavaArtifact);
        Assert.Equal(8761, module.ListenPort);
        AssertRetired(module);
        Assert.DoesNotContain(
            SourceText(),
            text => text.Contains("eureka", StringComparison.OrdinalIgnoreCase)
                || text.Contains("http://registry:8761", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void P9_T4_turbine_stream_service_is_retired()
    {
        var module = RetiredInfrastructure.TurbineStream;
        Assert.Equal("P9.T4", module.TodoId);
        Assert.Equal("turbine-stream-service", module.Module);
        Assert.Equal("metrics-stream", module.Role);
        Assert.Equal(RetiredInfrastructure.Retire, module.Disposition);
        Assert.Equal("spring-cloud-starter-netflix-turbine-stream", module.JavaArtifact);
        Assert.Equal(8989, module.ListenPort);
        Assert.Equal(8989, module.PublishedPort);
        AssertRetired(module);
    }

    [Fact]
    public void P9_T5_broker_and_metrics_stream_starters_are_not_carried()
    {
        var starters = RetiredInfrastructure.BrokerAndMetricsStreamStarters;
        Assert.Equal(3, starters.Length);
        Assert.Contains(starters, starter => starter.JavaArtifact == "spring-cloud-starter-bus-amqp" && starter.Kind == "broker");
        Assert.Contains(starters, starter => starter.JavaArtifact == "spring-cloud-starter-stream-rabbit" && starter.Kind == "metrics-stream");
        Assert.Contains(starters, starter => starter.JavaArtifact == "spring-cloud-netflix-hystrix-stream" && starter.Kind == "metrics-stream");
        Assert.All(starters, starter => Assert.Equal("P9.T5", starter.TodoId));
        Assert.Equal("not carried", RetiredInfrastructure.NotCarried);

        var banned =
            starters.Select(starter => starter.JavaArtifact)
            .Concat(
            [
                "RabbitMQ.Client",
                "MassTransit",
                "Steeltoe",
                "EasyNetQ",
                "RawRabbit",
                "NServiceBus",
                "Confluent.Kafka",
                "rabbitmq"
            ]);

        foreach (var token in banned)
        {
            Assert.DoesNotContain(
                ProjectFiles(),
                text => text.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Carried_sources_are_only_the_runtime_services_and_shared()
    {
        var root = RepoRoot();
        var src = Directory.GetDirectories(Path.Combine(root, "src"))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(RetiredInfrastructure.CarriedSrcProjects, src);

        var sln = File.ReadAllText(Path.Combine(root, "PiggyMetricsDotNet.sln"));
        foreach (var module in RetiredInfrastructure.Modules)
        {
            if (module.Module is "monitoring" or "registry" or "turbine-stream-service")
            {
                Assert.DoesNotContain(module.Module, sln, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var port in new[] { 8888, 8080, 9000, 8761, 8989 })
        {
            Assert.True(RetiredInfrastructure.IsRetiredPort(port));
        }
    }

    private static void AssertRetired(RetiredModule module)
    {
        var root = RepoRoot();
        Assert.False(Directory.Exists(Path.Combine(root, "src", module.Module)), module.Module);
        Assert.False(Directory.Exists(Path.Combine(root, module.Module)), module.Module);
        Assert.Equal(RetiredInfrastructure.Retire, module.Disposition);
        Assert.True(RetiredInfrastructure.IsRetiredPort(module.ListenPort));
        Assert.True(RetiredInfrastructure.IsRetiredPort(module.PublishedPort));

        foreach (var text in ProjectFiles())
        {
            Assert.DoesNotContain(module.JavaArtifact, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"src\\{module.Module}\\", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain($"src/{module.Module}/", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> SourceCs()
    {
        return ReadUnder("src", "*.cs");
    }

    private static IEnumerable<string> SourceText()
    {
        return ReadUnder("src", "*.*");
    }

    private static IEnumerable<string> ProjectFiles()
    {
        var root = RepoRoot();
        return Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.sln", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "src"), "*.json", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path))
            .Select(File.ReadAllText);
    }

    private static IEnumerable<string> ReadUnder(string relative, string pattern)
    {
        var root = Path.Combine(RepoRoot(), relative);
        return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path) && !path.EndsWith($"{Path.DirectorySeparatorChar}RetiredInfrastructure.cs", StringComparison.Ordinal))
            .Select(File.ReadAllText);
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
