using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Security;

// P1.T2 - AddPiggyMetricsResourceServer registers the bearer scheme and the two policies the source's
// controllers express: user (an authenticated caller) and server (#oauth2.hasScope('server')).
public sealed class SecurityExtensionsTests
{
	private const string UserToken = "issued-user-token";
	private const string ServerToken = "issued-server-token";

	private const string UserPrincipalJson =
		"{\"name\":\"demo\",\"oauth2Request\":{\"clientId\":\"browser\",\"scope\":[\"ui\"]}}";

	private const string ServerPrincipalJson =
		"{\"name\":\"account-service\",\"oauth2Request\":{\"clientId\":\"account-service\"," +
		"\"scope\":[\"server\"]}}";

	private static HttpResponseMessage Issued(string token)
	{
		return token switch
		{
			UserToken => ResourceServerHarness.Json(HttpStatusCode.OK, UserPrincipalJson),
			ServerToken => ResourceServerHarness.Json(HttpStatusCode.OK, ServerPrincipalJson),
			_ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
		};
	}

	private static ServiceProvider BuildProvider()
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
		services.AddPiggyMetricsResourceServer(new ConfigurationBuilder().Build());
		return services.BuildServiceProvider();
	}

	[Fact]
	public void ShouldNameThePoliciesUserAndServer()
	{
		Assert.Equal("user", PiggyMetricsAuth.UserPolicy);
		Assert.Equal("server", PiggyMetricsAuth.ServerPolicy);
		Assert.Equal("scope", PiggyMetricsAuth.ScopeClaimType);
		Assert.Equal("server", PiggyMetricsAuth.ServerScope);
		Assert.Equal("ui", PiggyMetricsAuth.UiScope);
	}

	[Fact]
	public async Task ShouldRegisterTheUserPolicyAsAnAuthenticatedCaller()
	{
		using var provider = BuildProvider();
		var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

		var policy = await policies.GetPolicyAsync(PiggyMetricsAuth.UserPolicy);

		Assert.NotNull(policy);
		Assert.Equal(new[] { PiggyMetricsAuth.Scheme }, policy!.AuthenticationSchemes);
		Assert.Single(policy.Requirements);
		Assert.Single(policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>());
	}

	[Fact]
	public async Task ShouldRegisterTheServerPolicyAsAnAuthenticatedCallerWithTheServerScope()
	{
		using var provider = BuildProvider();
		var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

		var policy = await policies.GetPolicyAsync(PiggyMetricsAuth.ServerPolicy);

		Assert.NotNull(policy);
		Assert.Equal(new[] { PiggyMetricsAuth.Scheme }, policy!.AuthenticationSchemes);
		Assert.Single(policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>());
		var claim = Assert.Single(policy.Requirements.OfType<ClaimsAuthorizationRequirement>());
		Assert.Equal(PiggyMetricsAuth.ScopeClaimType, claim.ClaimType);
		Assert.Equal(new[] { PiggyMetricsAuth.ServerScope }, claim.AllowedValues);
	}

	[Fact]
	public async Task ShouldDefaultToRequiringAnAuthenticatedCaller()
	{
		using var provider = BuildProvider();
		var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();

		var policy = await policies.GetDefaultPolicyAsync();

		Assert.Single(policy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>());
		Assert.Equal(new[] { PiggyMetricsAuth.Scheme }, policy.AuthenticationSchemes);
	}

	[Fact]
	public async Task ShouldRegisterBearerAsTheDefaultAuthenticationScheme()
	{
		using var provider = BuildProvider();
		var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

		var scheme = await schemes.GetSchemeAsync(PiggyMetricsAuth.Scheme);
		var defaultScheme = await schemes.GetDefaultAuthenticateSchemeAsync();

		Assert.NotNull(scheme);
		Assert.Equal(typeof(UserInfoAuthenticationHandler), scheme!.HandlerType);
		Assert.Equal(PiggyMetricsAuth.Scheme, defaultScheme!.Name);
	}

	[Fact]
	public void ShouldRegisterTheUserInfoHttpClient()
	{
		using var provider = BuildProvider();

		var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(
			PiggyMetricsAuth.HttpClientName);

		Assert.NotNull(client);
		Assert.Equal("piggymetrics-userinfo", PiggyMetricsAuth.HttpClientName);
	}

	[Fact]
	public async Task ShouldAllowTheUserPolicyForAUiScopedCaller()
	{
		await using var harness = await ResourceServerHarness.CreateAsync(Issued);

		var result = await harness.CallAsync("/user", UserToken);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("demo", result.Body);
	}

	[Fact]
	public async Task ShouldAllowTheUserPolicyForAServerScopedCaller()
	{
		await using var harness = await ResourceServerHarness.CreateAsync(Issued);

		var result = await harness.CallAsync("/user", ServerToken);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("account-service", result.Body);
	}

	[Fact]
	public async Task ShouldRefuseTheServerPolicyWithoutAToken()
	{
		await using var harness = await ResourceServerHarness.CreateAsync(Issued);

		var result = await harness.CallAsync("/server", token: null);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	private sealed class NullLoggerProvider : ILoggerProvider
	{
		public static readonly NullLoggerProvider Instance = new();

		public ILogger CreateLogger(string categoryName) => Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

		public void Dispose()
		{
		}
	}
}
