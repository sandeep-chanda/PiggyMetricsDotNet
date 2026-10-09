using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PiggyMetrics.Shared;
using PiggyMetrics.Shared.Health;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

// P1.T1 and P1.T5 - the composition every service starts from: the shared JSON contract, the health endpoint
// and the configured context path.
public sealed class ServiceDefaultsTests
{
	public sealed record Payload(DateTime LastSeen, string AccountName);

	[Fact]
	public async Task ShouldServeHealthUnderTheConfiguredContextPath()
	{
		using var app = await CreateAppAsync("account-service", "/accounts");
		var client = app.GetTestClient();

		using var prefixed = await client.GetAsync("/accounts" + HealthExtensions.HealthPath);
		using var otherPrefix = await client.GetAsync("/uaa" + HealthExtensions.HealthPath);

		Assert.Equal(HttpStatusCode.OK, prefixed.StatusCode);
		Assert.Equal("{\"status\":\"UP\"}", await prefixed.Content.ReadAsStringAsync());
		Assert.Equal(HttpStatusCode.NotFound, otherPrefix.StatusCode);
	}

	// A parity note, not an endorsement: UsePathBase only strips the prefix when the request carries it, so a
	// .NET service also answers at the unprefixed path, where the source's server.servlet.context-path made the
	// prefix mandatory. Harmless in P1 - the gateway always calls the prefixed form - but a later plan that
	// moves routing onto these services should decide whether to close the gap.
	[Fact]
	public async Task ShouldAlsoAnswerUnprefixedBecauseUsePathBaseOnlyStripsAPresentPrefix()
	{
		using var app = await CreateAppAsync("account-service", "/accounts");

		using var response = await app.GetTestClient().GetAsync(HealthExtensions.HealthPath);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task ShouldServeHealthAtTheRootWhenNoContextPathIsConfigured()
	{
		using var app = await CreateAppAsync("gateway", contextPath: null);

		using var response = await app.GetTestClient().GetAsync(HealthExtensions.HealthPath);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	[Fact]
	public async Task ShouldWriteEndpointJsonWithTheSharedContract()
	{
		using var app = await CreateAppAsync("account-service", "/accounts");

		using var response = await app.GetTestClient().GetAsync("/accounts/payload");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal(
			"{\"lastSeen\":\"2018-06-01T12:00:00.000+0000\",\"accountName\":\"demo\"}",
			await response.Content.ReadAsStringAsync());
	}

	[Fact]
	public void ShouldConfigureTheSharedContractForControllersAndMinimalEndpoints()
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.AddPiggyMetricsDefaults("account-service");
		using var app = builder.Build();

		var minimal = app.Services
			.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
		var controllers = app.Services
			.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;

		Assert.Same(JsonNamingPolicy.CamelCase, minimal.PropertyNamingPolicy);
		Assert.False(minimal.PropertyNameCaseInsensitive);
		Assert.Same(JsonNamingPolicy.CamelCase, controllers.PropertyNamingPolicy);
		Assert.False(controllers.PropertyNameCaseInsensitive);
	}

	[Fact]
	public void ShouldRegisterTheServiceIdentity()
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.AddPiggyMetricsDefaults("statistics-service");
		using var app = builder.Build();

		Assert.Equal("statistics-service", app.Services.GetRequiredService<ServiceIdentity>().Name);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void ShouldRefuseABlankApplicationName(string name)
	{
		var builder = WebApplication.CreateSlimBuilder();

		Assert.Throws<ArgumentException>(() => builder.AddPiggyMetricsDefaults(name));
	}

	[Fact]
	public void ShouldPinTheContextPathConfigurationKey()
	{
		Assert.Equal("Server:Servlet:ContextPath", ServiceDefaults.ContextPathConfigurationKey);
	}

	private static async Task<WebApplication> CreateAppAsync(string name, string? contextPath)
	{
		var builder = WebApplication.CreateSlimBuilder();
		builder.WebHost.UseTestServer();
		if (contextPath is not null)
		{
			builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[ServiceDefaults.ContextPathConfigurationKey] = contextPath,
			});
		}

		builder.AddPiggyMetricsDefaults(name);

		var app = builder.Build();
		app.UsePiggyMetricsDefaults();
		app.MapGet("/payload", () => new Payload(new DateTime(2018, 6, 1, 12, 0, 0, DateTimeKind.Utc), "demo"));

		await app.StartAsync();
		return app;
	}
}
