using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using PiggyMetrics.Shared.Health;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Health;

// P1.T5 - the actuator health contract the source's probes read: GET /actuator/health answers 200
// {"status":"UP"} and 503 {"status":"DOWN"}, with no detail body and no token.
public sealed class HealthExtensionsTests
{
	[Fact]
	public async Task ShouldAnswerUpWith200WhenEveryCheckIsHealthy()
	{
		using var host = await CreateHostAsync(HealthStatus.Healthy);

		using var response = await host.GetTestClient().GetAsync(HealthExtensions.HealthPath);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("{\"status\":\"UP\"}", await response.Content.ReadAsStringAsync());
		Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
	}

	[Fact]
	public async Task ShouldAnswerDownWith503WhenACheckIsUnhealthy()
	{
		using var host = await CreateHostAsync(HealthStatus.Unhealthy);

		using var response = await host.GetTestClient().GetAsync(HealthExtensions.HealthPath);

		Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
		Assert.Equal("{\"status\":\"DOWN\"}", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task ShouldAnswerUpWith200WhenACheckIsDegraded()
	{
		using var host = await CreateHostAsync(HealthStatus.Degraded);

		using var response = await host.GetTestClient().GetAsync(HealthExtensions.HealthPath);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("{\"status\":\"UP\"}", await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public async Task ShouldCarryNoCheckNamesDurationsOrExceptionDetail()
	{
		using var host = await CreateHostAsync(HealthStatus.Unhealthy, checkName: "store");

		using var response = await host.GetTestClient().GetAsync(HealthExtensions.HealthPath);
		var body = await response.Content.ReadAsStringAsync();

		Assert.DoesNotContain("store", body);
		Assert.DoesNotContain("duration", body, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task ShouldAnswerHealthWithoutATokenBehindAnAuthenticatedFallbackPolicy()
	{
		using var host = await CreateSecuredHostAsync();

		using var health = await host.GetTestClient().GetAsync(HealthExtensions.HealthPath);
		using var guarded = await host.GetTestClient().GetAsync("/guarded");

		Assert.Equal(HttpStatusCode.OK, health.StatusCode);
		Assert.Equal("{\"status\":\"UP\"}", await health.Content.ReadAsStringAsync());
		Assert.Equal(HttpStatusCode.Unauthorized, guarded.StatusCode);
	}

	[Theory]
	[InlineData("/actuator/info")]
	[InlineData("/actuator/env")]
	[InlineData("/actuator/metrics")]
	[InlineData("/actuator")]
	public async Task ShouldExposeNoOtherActuatorEndpoint(string path)
	{
		using var host = await CreateHostAsync(HealthStatus.Healthy);

		using var response = await host.GetTestClient().GetAsync(path);

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public void ShouldPinTheSourcesHealthPathAndBodies()
	{
		Assert.Equal("/actuator/health", HealthExtensions.HealthPath);
		Assert.Equal("{\"status\":\"UP\"}", HealthExtensions.UpBody);
		Assert.Equal("{\"status\":\"DOWN\"}", HealthExtensions.DownBody);
	}

	private static async Task<IHost> CreateHostAsync(HealthStatus status, string checkName = "probe")
	{
		var host = new HostBuilder()
			.ConfigureWebHost(webHost =>
			{
				webHost.UseTestServer();
				webHost.ConfigureServices(services =>
				{
					services.AddLogging();
					services.AddRouting();
					services.AddPiggyMetricsHealthChecks();
					services
						.AddHealthChecks()
						.AddCheck(checkName, () => new HealthCheckResult(status, "detail for " + checkName));
				});
				webHost.Configure(app =>
				{
					app.UseRouting();
					app.UseEndpoints(endpoints => endpoints.MapPiggyMetricsHealthChecks());
				});
			})
			.Build();

		await host.StartAsync();
		return host;
	}

	private static async Task<IHost> CreateSecuredHostAsync()
	{
		var host = new HostBuilder()
			.ConfigureWebHost(webHost =>
			{
				webHost.UseTestServer();
				webHost.ConfigureServices(services =>
				{
					services.AddLogging();
					services.AddRouting();
					services.AddPiggyMetricsResourceServer(
						new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
					services
						.AddAuthorizationBuilder()
						.SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
								PiggyMetricsAuth.Scheme)
							.RequireAuthenticatedUser()
							.Build());
					services.AddPiggyMetricsHealthChecks();
				});
				webHost.Configure(app =>
				{
					app.UseRouting();
					app.UseAuthentication();
					app.UseAuthorization();
					app.UseEndpoints(endpoints =>
					{
						endpoints.MapPiggyMetricsHealthChecks();
						endpoints.MapGet("/guarded", () => "guarded").RequireAuthorization();
					});
				});
			})
			.Build();

		await host.StartAsync();
		return host;
	}
}
