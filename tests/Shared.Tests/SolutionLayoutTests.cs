using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

// P1.T1 - one project per runtime service plus Shared, every version centrally pinned, and the ports and
// context paths the source's config server handed out.
public sealed class SolutionLayoutTests
{
	private static readonly string Root = FindRepositoryRoot();

	public static TheoryData<string, string, int, string?> Services => new()
	{
		{ "Gateway", "PiggyMetrics.Gateway", 4000, null },
		{ "AuthService", "PiggyMetrics.AuthService", 5000, "/uaa" },
		{ "AccountService", "PiggyMetrics.AccountService", 6000, "/accounts" },
		{ "StatisticsService", "PiggyMetrics.StatisticsService", 7000, "/statistics" },
		{ "NotificationService", "PiggyMetrics.NotificationService", 8000, "/notifications" },
	};

	[Fact]
	public void ShouldListTheSevenProjectsOfTheSolution()
	{
		var solution = Read("PiggyMetrics.sln");

		foreach (var project in new[]
		{
			@"src\Shared\Shared.csproj",
			@"src\Gateway\Gateway.csproj",
			@"src\AuthService\AuthService.csproj",
			@"src\AccountService\AccountService.csproj",
			@"src\StatisticsService\StatisticsService.csproj",
			@"src\NotificationService\NotificationService.csproj",
			@"tests\Shared.Tests\Shared.Tests.csproj",
		})
		{
			Assert.Contains(project, solution);
		}
	}

	[Theory]
	[MemberData(nameof(Services))]
	public void ShouldBuildEachServiceOnTheSharedLibrary(
		string service,
		string rootNamespace,
		int port,
		string? contextPath)
	{
		var project = XDocument.Parse(Read($"src/{service}/{service}.csproj"));

		Assert.Equal("Microsoft.NET.Sdk.Web", project.Root!.Attribute("Sdk")!.Value);
		Assert.Equal(rootNamespace, project.Descendants("RootNamespace").Single().Value);
		Assert.Equal(service, project.Descendants("AssemblyName").Single().Value);
		Assert.Equal(
			"../Shared/Shared.csproj",
			project.Descendants("ProjectReference").Single().Attribute("Include")!.Value);
		Assert.NotEqual(0, port);
		Assert.True(contextPath is null || contextPath.StartsWith('/'));
	}

	[Theory]
	[MemberData(nameof(Services))]
	public void ShouldListenOnTheSourcesPortAndContextPath(
		string service,
		string rootNamespace,
		int port,
		string? contextPath)
	{
		using var settings = JsonDocument.Parse(Read($"src/{service}/appsettings.json"));
		var root = settings.RootElement;

		var url = root.GetProperty("Kestrel").GetProperty("Endpoints").GetProperty("Http").GetProperty("Url");
		Assert.Equal($"http://0.0.0.0:{port}", url.GetString());
		Assert.NotNull(rootNamespace);

		if (contextPath is null)
		{
			Assert.False(root.TryGetProperty("Server", out _));
		}
		else
		{
			Assert.Equal(
				contextPath,
				root.GetProperty("Server").GetProperty("Servlet").GetProperty("ContextPath").GetString());
		}
	}

	[Fact]
	public void ShouldDeclareSharedAsALibraryOnTheStoreDriver()
	{
		var project = XDocument.Parse(Read("src/Shared/Shared.csproj"));

		Assert.Equal("Microsoft.NET.Sdk", project.Root!.Attribute("Sdk")!.Value);
		Assert.Equal("PiggyMetrics.Shared", project.Descendants("RootNamespace").Single().Value);
		Assert.Equal(
			"Microsoft.AspNetCore.App",
			project.Descendants("FrameworkReference").Single().Attribute("Include")!.Value);
		Assert.Equal(
			"MongoDB.Driver",
			project.Descendants("PackageReference").Single().Attribute("Include")!.Value);
	}

	[Fact]
	public void ShouldDeclareTheTargetFrameworkOnlyOnce()
	{
		var frameworks = XDocument.Parse(Read("Directory.Build.props"))
			.Descendants("TargetFramework")
			.Select(element => element.Value)
			.ToArray();

		Assert.Equal(new[] { "net10.0" }, frameworks);

		foreach (var project in ProjectFiles())
		{
			Assert.Empty(XDocument.Load(project).Descendants("TargetFramework"));
			Assert.Empty(XDocument.Load(project).Descendants("TargetFrameworks"));
		}
	}

	[Fact]
	public void ShouldPinEveryPackageVersionCentrally()
	{
		var packages = XDocument.Parse(Read("Directory.Packages.props"));

		Assert.Equal(
			"true",
			packages.Descendants("ManagePackageVersionsCentrally").Single().Value);
		Assert.All(
			packages.Descendants("PackageVersion"),
			element => Assert.False(string.IsNullOrWhiteSpace(element.Attribute("Version")?.Value)));

		foreach (var project in ProjectFiles())
		{
			Assert.All(
				XDocument.Load(project).Descendants("PackageReference"),
				element => Assert.Null(element.Attribute("Version")));
		}
	}

	[Fact]
	public void ShouldPinTheSdkWithARollForward()
	{
		using var global = JsonDocument.Parse(Read("global.json"));
		var sdk = global.RootElement.GetProperty("sdk");

		Assert.Equal("10.0.100", sdk.GetProperty("version").GetString());
		Assert.Equal("latestMajor", sdk.GetProperty("rollForward").GetString());
		Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());
	}

	[Theory]
	[InlineData("AccountService")]
	[InlineData("StatisticsService")]
	[InlineData("NotificationService")]
	public void ShouldStandUpTheResourceServerOnEveryGuardedService(string service)
	{
		var program = Read($"src/{service}/Program.cs");

		Assert.Contains("AddPiggyMetricsDefaults", program);
		Assert.Contains("AddPiggyMetricsResourceServer", program);
		Assert.Contains("UsePiggyMetricsDefaults", program);
	}

	[Fact]
	public void ShouldLeaveTheAuthorizationServerWithoutAResourceServer()
	{
		var program = Read("src/AuthService/Program.cs");

		Assert.Contains("AddPiggyMetricsDefaults(\"auth-service\")", program);
		Assert.DoesNotContain("AddPiggyMetricsResourceServer", program);
	}

	[Fact]
	public void ShouldLeaveTheGatewayWithHealthOnly()
	{
		var program = Read("src/Gateway/Program.cs");

		Assert.Contains("AddPiggyMetricsHealthChecks", program);
		Assert.Contains("MapPiggyMetricsHealthChecks", program);
		Assert.DoesNotContain("AddPiggyMetricsResourceServer", program);
		Assert.DoesNotContain("MapControllers", program);
	}

	[Theory]
	[InlineData("Eureka")]
	[InlineData("Hystrix")]
	[InlineData("Turbine")]
	[InlineData("RabbitMQ")]
	[InlineData("Steeltoe")]
	[InlineData("Newtonsoft.Json")]
	[InlineData("JwtBearer")]
	public void ShouldCarryOverNoInfrastructureThePlanDropped(string package)
	{
		foreach (var project in ProjectFiles())
		{
			Assert.DoesNotContain(
				XDocument.Load(project).Descendants("PackageReference"),
				element => element.Attribute("Include")!.Value.Contains(
					package,
					StringComparison.OrdinalIgnoreCase));
		}

		Assert.DoesNotContain(package, Read("Directory.Packages.props"), StringComparison.OrdinalIgnoreCase);
	}

	private static IEnumerable<string> ProjectFiles()
	{
		return Directory
			.EnumerateFiles(Path.Combine(Root, "src"), "*.csproj", SearchOption.AllDirectories)
			.Concat(Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.csproj", SearchOption.AllDirectories))
			.OrderBy(path => path, StringComparer.Ordinal);
	}

	private static string Read(string relativePath)
	{
		return File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PiggyMetrics.sln")))
		{
			directory = directory.Parent;
		}

		Assert.NotNull(directory);
		return directory!.FullName;
	}
}
