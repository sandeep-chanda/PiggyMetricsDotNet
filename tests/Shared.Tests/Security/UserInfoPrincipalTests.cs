using System.Net;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Security;

// P1.T2 - the principal the handler builds out of the user-info document, the way
// CustomUserInfoTokenServices builds one: a principal name, the calling client id, the granted scopes and the
// granted authorities.
public sealed class UserInfoPrincipalTests
{
	private const string Token = "issued-token";

	private static async Task<string> ResolveAsync(string document)
	{
		await using var harness = await ResourceServerHarness.CreateAsync(
			_ => ResourceServerHarness.Json(HttpStatusCode.OK, document));

		var result = await harness.CallAsync("/claims", Token);
		Assert.Equal(HttpStatusCode.OK, result.Status);
		return result.Body;
	}

	[Fact]
	public async Task ShouldEmitTheClientIdAndEveryScopeOfTheResolvedRequest()
	{
		var described = await ResolveAsync(
			"{\"name\":\"demo\",\"oauth2Request\":{\"clientId\":\"account-service\"," +
			"\"scope\":[\"server\",\"ui\"]}}");

		Assert.Equal("demo|account-service|server,ui|ROLE_USER", described);
	}

	[Fact]
	public async Task ShouldEmitNoClientIdOrScopeWhenTheDocumentCarriesNoOauth2Request()
	{
		var described = await ResolveAsync("{\"name\":\"demo\"}");

		Assert.Equal("demo|||ROLE_USER", described);
	}

	[Theory]
	[InlineData("{\"user\":\"from-user\",\"name\":\"from-name\"}", "from-user")]
	[InlineData("{\"username\":\"from-username\",\"name\":\"from-name\"}", "from-username")]
	[InlineData("{\"userid\":\"from-userid\",\"name\":\"from-name\"}", "from-userid")]
	[InlineData("{\"user_id\":\"from-user-id\",\"name\":\"from-name\"}", "from-user-id")]
	[InlineData("{\"login\":\"from-login\",\"name\":\"from-name\"}", "from-login")]
	[InlineData("{\"id\":\"from-id\",\"name\":\"from-name\"}", "from-id")]
	[InlineData("{\"name\":\"from-name\"}", "from-name")]
	public async Task ShouldReadThePrincipalNameInTheSourcesProbeOrder(string document, string expected)
	{
		var described = await ResolveAsync(document);

		Assert.Equal(expected, described.Split('|')[0]);
	}

	[Fact]
	public void ShouldProbeThePrincipalKeysTheSourceProbes()
	{
		Assert.Equal(
			new[] { "user", "username", "userid", "user_id", "login", "id", "name" },
			PiggyMetricsAuth.PrincipalKeys);
	}

	[Fact]
	public async Task ShouldFallBackToUnknownWhenNoPrincipalKeyIsPresent()
	{
		var described = await ResolveAsync("{\"authenticated\":true}");

		Assert.Equal(PiggyMetricsAuth.UnknownPrincipal, described.Split('|')[0]);
		Assert.Equal("unknown", PiggyMetricsAuth.UnknownPrincipal);
	}

	[Fact]
	public async Task ShouldReadAuthoritiesGivenAsGrantedAuthorityObjects()
	{
		var described = await ResolveAsync(
			"{\"name\":\"demo\",\"authorities\":[{\"authority\":\"ROLE_USER\"},{\"authority\":\"ROLE_ADMIN\"}]}");

		Assert.Equal("ROLE_USER,ROLE_ADMIN", described.Split('|')[3]);
	}

	[Fact]
	public async Task ShouldReadAuthoritiesGivenAsACommaSeparatedString()
	{
		var described = await ResolveAsync("{\"name\":\"demo\",\"authorities\":\"ROLE_USER, ROLE_ADMIN\"}");

		Assert.Equal("ROLE_USER,ROLE_ADMIN", described.Split('|')[3]);
	}

	[Fact]
	public async Task ShouldGrantTheDefaultAuthorityWhenTheDocumentCarriesNone()
	{
		var described = await ResolveAsync("{\"name\":\"demo\"}");

		Assert.Equal(PiggyMetricsAuth.DefaultAuthority, described.Split('|')[3]);
		Assert.Equal("ROLE_USER", PiggyMetricsAuth.DefaultAuthority);
	}

	[Fact]
	public async Task ShouldGrantNoAuthorityWhenTheDocumentCarriesAnEmptyAuthorityList()
	{
		var described = await ResolveAsync("{\"name\":\"demo\",\"authorities\":[]}");

		Assert.Equal(string.Empty, described.Split('|')[3]);
	}

	[Fact]
	public async Task ShouldAuthenticateUnderTheBearerScheme()
	{
		await using var harness = await ResourceServerHarness.CreateAsync(
			_ => ResourceServerHarness.Json(HttpStatusCode.OK, "{\"name\":\"demo\"}"));

		var result = await harness.CallAsync("/default", Token);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("demo", result.Body);
		Assert.Equal("Bearer", PiggyMetricsAuth.Scheme);
	}
}
