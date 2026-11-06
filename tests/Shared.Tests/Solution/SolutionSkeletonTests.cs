using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class SolutionSkeletonTests
{
    private static readonly string[] RuntimeServices =
    [
        "Gateway",
        "AuthService",
        "AccountService",
        "StatisticsService",
        "NotificationService"
    ];

    [Fact]
    public void Solution_lists_shared_and_each_runtime_service_on_net8()
    {
        var root = RepoRoot();
        var sln = File.ReadAllText(Path.Combine(root, "PiggyMetricsDotNet.sln"));
        Assert.Contains("\"Shared\"", sln);
        Assert.Contains("\"Shared.Tests\"", sln);
        foreach (var name in RuntimeServices)
        {
            Assert.Contains("\"" + name + "\"", sln);
        }

        var shared = File.ReadAllText(Path.Combine(root, "src", "Shared", "Shared.csproj"));
        Assert.Contains("<TargetFramework>net8.0</TargetFramework>", shared);

        var tests = File.ReadAllText(Path.Combine(root, "tests", "Shared.Tests", "Shared.Tests.csproj"));
        Assert.Contains("<TargetFramework>net8.0</TargetFramework>", tests);
        Assert.Contains("Shared.csproj", tests);

        foreach (var name in RuntimeServices)
        {
            var csproj = File.ReadAllText(Path.Combine(root, "src", name, name + ".csproj"));
            Assert.Contains("<TargetFramework>net8.0</TargetFramework>", csproj);
            Assert.Contains("Shared.csproj", csproj);
            var program = File.ReadAllText(Path.Combine(root, "src", name, "Program.cs"));
            Assert.Contains("AddPiggyMetricsSharedDefaults()", program);
            Assert.Contains("MapPiggyMetricsHealthChecks()", program);
        }
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
