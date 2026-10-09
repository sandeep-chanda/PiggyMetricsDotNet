using System.Net;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Security;

// P1.T2 - the bearer handler validates a token the way the source's resource servers do: it resolves the
// presented token at the authorization server's user-info endpoint and fails closed on anything else.
public sealed class UserInfoTokenResolutionTests
{
	private const string IssuedToken = "issued-user-token";

	private const string UserPrincipalJson =
		"{\"authorities\":[{\"authority\":\"ROLE_USER\"}],\"authenticated\":true,\"principal\":\"demo\"," +
		"\"oauth2Request\":{\"clientId\":\"browser\",\"scope\":[\"ui\"]},\"clientOnly\":false,\"name\":\"demo\"}";

	private static HttpResponseMessage Issued(string token)
	{
		return token == IssuedToken
			? ResourceServerHarness.Json(HttpStatusCode.OK, UserPrincipalJson)
			: new HttpResponseMessage(HttpStatusCode.Unauthorized);
	}

	[Fact]
	public async Task ShouldPresentTheCallersTokenToTheConfiguredUserInfoEndpoint()
	{
		var stub = new AuthorizationServerStub(Issued);
		await using var harness = await ResourceServerHarness.CreateAsync(stub);

		var result = await harness.CallAsync("/user", IssuedToken);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		var call = Assert.Single(stub.Calls);
		Assert.Equal(ResourceServerHarness.UserInfoUri, call.Uri);
		Assert.Equal("Bearer", call.Scheme);
		Assert.Equal(IssuedToken, call.Token);
	}

	[Fact]
	public async Task ShouldHonourTheUserInfoUriFromConfiguration()
	{
		const string configured = "http://auth-service:5000/uaa/users/current?probe=1";
		var stub = new AuthorizationServerStub(Issued);
		await using var harness = await ResourceServerHarness.CreateAsync(stub, configured);

		await harness.CallAsync("/user", IssuedToken);

		Assert.Equal(configured, Assert.Single(stub.Calls).Uri);
	}

	[Fact]
	public async Task ShouldFallBackToTheSourceUserInfoUriWhenConfigurationIsBlank()
	{
		var stub = new AuthorizationServerStub(Issued);
		await using var harness = await ResourceServerHarness.CreateAsync(stub, userInfoUri: "   ");

		await harness.CallAsync("/user", IssuedToken);

		Assert.Equal(PiggyMetricsAuth.DefaultUserInfoUri, Assert.Single(stub.Calls).Uri);
		Assert.Equal("http://auth-service:5000/uaa/users/current", PiggyMetricsAuth.DefaultUserInfoUri);
	}

	[Theory]
	[InlineData(HttpStatusCode.Unauthorized)]
	[InlineData(HttpStatusCode.Forbidden)]
	[InlineData(HttpStatusCode.NotFound)]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.BadGateway)]
	public async Task ShouldRefuseWhenTheAuthorizationServerAnswersAFailureStatus(HttpStatusCode status)
	{
		await using var harness = await ResourceServerHarness.CreateAsync(_ => new HttpResponseMessage(status));

		var result = await harness.CallAsync("/user", IssuedToken);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	[Fact]
	public async Task ShouldRefuseWhenTheAuthorizationServerIsUnreachable()
	{
		await using var harness = await ResourceServerHarness.CreateAsync(
			_ => throw new HttpRequestException("auth-service is down"));

		var result = await harness.CallAsync("/user", IssuedToken);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	[Fact]
	public async Task ShouldRefuseWhenTheResolvedDocumentCarriesAnErrorMember()
	{
		await using var harness = await ResourceServerHarness.CreateAsync(
			_ => ResourceServerHarness.Json(
				HttpStatusCode.OK,
				"{\"error\":\"invalid_token\",\"error_description\":\"Invalid access token\"}"));

		var result = await harness.CallAsync("/user", IssuedToken);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	[Theory]
	[InlineData("[]")]
	[InlineData("\"demo\"")]
	[InlineData("null")]
	[InlineData("{ not json")]
	[InlineData("")]
	public async Task ShouldRefuseWhenTheResolvedBodyIsNotAPrincipalDocument(string body)
	{
		await using var harness = await ResourceServerHarness.CreateAsync(
			_ => ResourceServerHarness.Json(HttpStatusCode.OK, body));

		var result = await harness.CallAsync("/user", IssuedToken);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	[Theory]
	[InlineData("Basic ZGVtbzpkZW1v")]
	[InlineData("Bearer")]
	[InlineData("Bearer ")]
	[InlineData("token-without-a-scheme")]
	public async Task ShouldNotResolveWhenNoBearerTokenIsPresented(string header)
	{
		var stub = new AuthorizationServerStub(Issued);
		await using var harness = await ResourceServerHarness.CreateAsync(stub);

		var result = await harness.CallWithRawHeaderAsync("/user", header);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
		Assert.Empty(stub.Calls);
	}

	[Fact]
	public async Task ShouldAcceptALowerCaseBearerPrefix()
	{
		await using var harness = await ResourceServerHarness.CreateAsync(Issued);

		var result = await harness.CallWithRawHeaderAsync("/user", "bearer " + IssuedToken);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("demo", result.Body);
	}

	[Fact]
	public async Task ShouldResolveEveryRequestRatherThanCacheTheUserInfoResponse()
	{
		var stub = new AuthorizationServerStub(Issued);
		await using var harness = await ResourceServerHarness.CreateAsync(stub);

		await harness.CallAsync("/user", IssuedToken);
		await harness.CallAsync("/user", IssuedToken);
		await harness.CallAsync("/user", IssuedToken);

		Assert.Equal(3, stub.Calls.Count);
	}

	[Fact]
	public async Task ShouldLeaveAnAnonymousEndpointReachableWithoutAToken()
	{
		var stub = new AuthorizationServerStub(Issued);
		await using var harness = await ResourceServerHarness.CreateAsync(stub);

		var result = await harness.CallAsync("/open", token: null);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("open", result.Body);
		Assert.Empty(stub.Calls);
	}
}
