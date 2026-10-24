using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Security;

public sealed class UserInfoAuthenticationHandlerTests
{
	private const string UserInfoUri = "http://auth-service:5000/uaa/users/current";

	private const string IssuedUserToken = "issued-user-token";
	private const string IssuedServerToken = "issued-server-token";
	private const string ForeignToken = "foreign-token";

	private const string UserPrincipalJson =
		"{\"authorities\":[],\"authenticated\":true,\"principal\":\"demo\",\"credentials\":\"\"," +
		"\"oauth2Request\":{\"clientId\":\"browser\",\"scope\":[\"ui\"]},\"clientOnly\":false,\"name\":\"demo\"}";

	private const string ServerPrincipalJson =
		"{\"authorities\":[],\"authenticated\":true,\"principal\":\"account-service\",\"credentials\":\"\"," +
		"\"oauth2Request\":{\"clientId\":\"account-service\",\"scope\":[\"server\"]},\"clientOnly\":true," +
		"\"name\":\"account-service\"}";

	[Fact]
	public async Task ShouldAcceptTokenIssuedByAuthorizationServer()
	{
		var result = await CallAsync("/user", IssuedUserToken);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("demo", result.Body);
	}

	[Fact]
	public async Task ShouldRefuseTokenTheAuthorizationServerDidNotIssue()
	{
		var result = await CallAsync("/user", ForeignToken);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	[Fact]
	public async Task ShouldRefuseRequestWithoutToken()
	{
		var result = await CallAsync("/user", token: null);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	[Fact]
	public async Task ShouldAllowServerPolicyForServerScope()
	{
		var result = await CallAsync("/server", IssuedServerToken);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("account-service", result.Body);
	}

	[Fact]
	public async Task ShouldRejectServerPolicyForUiScope()
	{
		var result = await CallAsync("/server", IssuedUserToken);

		Assert.Equal(HttpStatusCode.Forbidden, result.Status);
	}

	private static async Task<(HttpStatusCode Status, string Body)> CallAsync(string path, string? token)
	{
		using var host = await CreateHostAsync();
		var client = host.GetTestClient();

		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		if (token is not null)
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		using var response = await client.SendAsync(request);
		var body = await response.Content.ReadAsStringAsync();
		return (response.StatusCode, body);
	}

	private static async Task<IHost> CreateHostAsync()
	{
		var host = new HostBuilder()
			.ConfigureWebHost(webHost =>
			{
				webHost.UseTestServer();
				webHost.ConfigureServices(services =>
				{
					var configuration = new ConfigurationBuilder()
						.AddInMemoryCollection(new Dictionary<string, string?>
						{
							[PiggyMetricsAuth.UserInfoUriConfigurationKey] = UserInfoUri,
						})
						.Build();

					services.AddLogging();
					services.AddRouting();
					services.AddPiggyMetricsResourceServer(configuration);
					services
						.AddHttpClient(PiggyMetricsAuth.HttpClientName)
						.ConfigurePrimaryHttpMessageHandler(() => new AuthorizationServerStub());
				});
				webHost.Configure(app =>
				{
					app.UseRouting();
					app.UseAuthentication();
					app.UseAuthorization();
					app.UseEndpoints(endpoints =>
					{
						endpoints
							.MapGet("/user", (HttpContext context) => context.User.Identity!.Name)
							.RequireAuthorization(PiggyMetricsAuth.UserPolicy);
						endpoints
							.MapGet("/server", (HttpContext context) => context.User.Identity!.Name)
							.RequireAuthorization(PiggyMetricsAuth.ServerPolicy);
					});
				});
			})
			.Build();

		await host.StartAsync();
		return host;
	}

	// Answers the way the source's authorization server answers GET /uaa/users/current: the serialized
	// OAuth2Authentication for a token it issued, 401 for anything else.
	private sealed class AuthorizationServerStub : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var token = request.Headers.Authorization?.Parameter ?? string.Empty;
			var body = token switch
			{
				IssuedUserToken => UserPrincipalJson,
				IssuedServerToken => ServerPrincipalJson,
				_ => string.Empty,
			};

			if (body.Length == 0)
			{
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			});
		}
	}
}
