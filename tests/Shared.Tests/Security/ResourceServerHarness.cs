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

namespace PiggyMetrics.Shared.Tests.Security;

// Records what the resource server asked the authorization server, and answers with whatever the test pinned
// for the presented token.
internal sealed class AuthorizationServerStub : HttpMessageHandler
{
	private readonly Func<string, HttpResponseMessage> _responder;

	public AuthorizationServerStub(Func<string, HttpResponseMessage> responder)
	{
		_responder = responder;
	}

	public List<(string Uri, string? Scheme, string? Token)> Calls { get; } = new();

	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		var authorization = request.Headers.Authorization;
		lock (Calls)
		{
			Calls.Add((request.RequestUri!.ToString(), authorization?.Scheme, authorization?.Parameter));
		}

		return Task.FromResult(_responder(authorization?.Parameter ?? string.Empty));
	}
}

// A resource server wired the way every PiggyMetrics service wires one - AddPiggyMetricsResourceServer plus the
// authentication and authorization middleware - with the authorization server replaced by a stub.
internal sealed class ResourceServerHarness : IAsyncDisposable
{
	public const string UserInfoUri = "http://auth-service:5000/uaa/users/current";

	private readonly IHost _host;

	private ResourceServerHarness(IHost host, AuthorizationServerStub stub)
	{
		_host = host;
		Stub = stub;
	}

	public AuthorizationServerStub Stub { get; }

	public HttpClient Client => _host.GetTestClient();

	public static Task<ResourceServerHarness> CreateAsync(
		Func<string, HttpResponseMessage> responder,
		string userInfoUri = UserInfoUri)
	{
		return CreateAsync(new AuthorizationServerStub(responder), userInfoUri);
	}

	public static async Task<ResourceServerHarness> CreateAsync(
		AuthorizationServerStub stub,
		string userInfoUri = UserInfoUri)
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
							[PiggyMetricsAuth.UserInfoUriConfigurationKey] = userInfoUri,
						})
						.Build();

					services.AddLogging();
					services.AddRouting();
					services.AddPiggyMetricsResourceServer(configuration);
					services
						.AddHttpClient(PiggyMetricsAuth.HttpClientName)
						.ConfigurePrimaryHttpMessageHandler(() => stub);
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
						endpoints
							.MapGet("/claims", (HttpContext context) => Describe(context))
							.RequireAuthorization(PiggyMetricsAuth.UserPolicy);
						endpoints
							.MapGet("/default", (HttpContext context) => context.User.Identity!.Name)
							.RequireAuthorization();
						endpoints.MapGet("/open", () => "open");
					});
				});
			})
			.Build();

		await host.StartAsync();
		return new ResourceServerHarness(host, stub);
	}

	// name|client_id|scope,scope|role,role
	private static string Describe(HttpContext context)
	{
		var user = context.User;
		var clientId = user.FindFirst(PiggyMetricsAuth.ClientIdClaimType)?.Value ?? string.Empty;
		var scopes = string.Join(',', user.FindAll(PiggyMetricsAuth.ScopeClaimType).Select(claim => claim.Value));
		var roles = string.Join(
			',',
			user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(claim => claim.Value));

		return string.Join('|', user.Identity!.Name, clientId, scopes, roles);
	}

	public async Task<(HttpStatusCode Status, string Body)> CallAsync(string path, string? token)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		if (token is not null)
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		using var response = await Client.SendAsync(request);
		return (response.StatusCode, await response.Content.ReadAsStringAsync());
	}

	public async Task<(HttpStatusCode Status, string Body)> CallWithRawHeaderAsync(string path, string header)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		request.Headers.TryAddWithoutValidation("Authorization", header);

		using var response = await Client.SendAsync(request);
		return (response.StatusCode, await response.Content.ReadAsStringAsync());
	}

	public static HttpResponseMessage Json(HttpStatusCode status, string body)
	{
		return new HttpResponseMessage(status)
		{
			Content = new StringContent(body, Encoding.UTF8, "application/json"),
		};
	}

	public async ValueTask DisposeAsync()
	{
		await _host.StopAsync();
		_host.Dispose();
	}
}
