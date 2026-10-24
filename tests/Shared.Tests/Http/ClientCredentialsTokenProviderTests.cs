using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Http;

// P1.T4 - the client-credentials token cache: one token per process, renewed once the lifetime the
// authorization server reported has elapsed, the way DefaultOAuth2ClientContext behaves in the source.
public sealed class ClientCredentialsTokenProviderTests
{
	[Fact]
	public void ShouldUseTheSourcesCallTimeout()
	{
		Assert.Equal(10000, ServiceClientOptions.SourceTimeoutMilliseconds);
		Assert.Equal(TimeSpan.FromMilliseconds(10000), new ServiceClientOptions().Timeout);
	}

	[Fact]
	public void ShouldDefaultToTheSourcesClientCredentialsGrant()
	{
		var options = new ServiceClientOptions();

		Assert.Equal("Security:OAuth2:Client", ServiceClientOptions.SectionName);
		Assert.Equal("client_credentials", options.GrantType);
		Assert.Equal("server", options.Scope);
		Assert.Equal("http://auth-service:5000/uaa/oauth/token", options.AccessTokenUri);
	}

	[Fact]
	public async Task ShouldRequestATokenOnceAndCacheItUntilExpiry()
	{
		var stub = new TokenEndpointStub(expiresInSeconds: 3600);
		using var provider = BuildProvider(stub);
		var tokens = provider.GetRequiredService<IClientCredentialsTokenProvider>();

		var first = await tokens.GetTokenAsync(CancellationToken.None);
		var second = await tokens.GetTokenAsync(CancellationToken.None);
		var third = await tokens.GetTokenAsync(CancellationToken.None);

		Assert.Equal("token-1", first);
		Assert.Equal("token-1", second);
		Assert.Equal("token-1", third);
		Assert.Equal(1, stub.Calls);
	}

	[Fact]
	public async Task ShouldRenewTheTokenOnceItHasExpired()
	{
		var stub = new TokenEndpointStub(expiresInSeconds: 0);
		using var provider = BuildProvider(stub);
		var tokens = provider.GetRequiredService<IClientCredentialsTokenProvider>();

		var first = await tokens.GetTokenAsync(CancellationToken.None);
		var second = await tokens.GetTokenAsync(CancellationToken.None);

		Assert.Equal("token-1", first);
		Assert.Equal("token-2", second);
		Assert.Equal(2, stub.Calls);
	}

	[Fact]
	public async Task ShouldRenewTheTokenWhenTheAuthorizationServerReportsNoLifetime()
	{
		var stub = new TokenEndpointStub(expiresInSeconds: null);
		using var provider = BuildProvider(stub);
		var tokens = provider.GetRequiredService<IClientCredentialsTokenProvider>();

		await tokens.GetTokenAsync(CancellationToken.None);
		await tokens.GetTokenAsync(CancellationToken.None);

		Assert.Equal(2, stub.Calls);
	}

	[Fact]
	public async Task ShouldRequestOneTokenForConcurrentCallers()
	{
		var stub = new TokenEndpointStub(expiresInSeconds: 3600, delay: TimeSpan.FromMilliseconds(50));
		using var provider = BuildProvider(stub);
		var tokens = provider.GetRequiredService<IClientCredentialsTokenProvider>();

		var issued = await Task.WhenAll(
			Enumerable.Range(0, 16).Select(_ => tokens.GetTokenAsync(CancellationToken.None)));

		Assert.All(issued, token => Assert.Equal("token-1", token));
		Assert.Equal(1, stub.Calls);
	}

	[Fact]
	public async Task ShouldAuthenticateAtTheTokenEndpointWithTheConfiguredClient()
	{
		var stub = new TokenEndpointStub(expiresInSeconds: 3600);
		using var provider = BuildProvider(stub);

		await provider.GetRequiredService<IClientCredentialsTokenProvider>()
			.GetTokenAsync(CancellationToken.None);

		Assert.Equal("http://auth-service:5000/uaa/oauth/token", stub.LastUri);
		Assert.Equal("Basic", stub.LastScheme);
		Assert.Equal(
			Convert.ToBase64String(Encoding.UTF8.GetBytes("account-service:secret")),
			stub.LastParameter);
		Assert.Contains("grant_type=client_credentials", stub.LastBody);
		Assert.Contains("scope=server", stub.LastBody);
	}

	[Fact]
	public async Task ShouldFailWhenTheAuthorizationServerRefusesTheClient()
	{
		var stub = new TokenEndpointStub(expiresInSeconds: 3600) { Status = HttpStatusCode.Unauthorized };
		using var provider = BuildProvider(stub);
		var tokens = provider.GetRequiredService<IClientCredentialsTokenProvider>();

		await Assert.ThrowsAsync<HttpRequestException>(() => tokens.GetTokenAsync(CancellationToken.None));
	}

	[Fact]
	public async Task ShouldAttachTheCachedTokenToEveryOutgoingCall()
	{
		var tokenStub = new TokenEndpointStub(expiresInSeconds: 3600);
		var edgeStub = new EdgeStub();
		using var provider = BuildProvider(tokenStub, services => services
			.AddPiggyMetricsServiceClient<EdgeTestClient>("edge", new Uri("http://account-service:6000"))
			.ConfigurePrimaryHttpMessageHandler(() => edgeStub));

		var client = provider.GetRequiredService<EdgeTestClient>();
		await client.Http.GetAsync("/accounts/demo");
		await client.Http.GetAsync("/accounts/demo");

		Assert.Equal(new[] { "Bearer token-1", "Bearer token-1" }, edgeStub.Authorizations);
		Assert.Equal(1, tokenStub.Calls);
	}

	internal static ServiceProvider BuildProvider(
		TokenEndpointStub stub,
		Action<IServiceCollection>? configure = null)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Security:OAuth2:Client:ClientId"] = "account-service",
				["Security:OAuth2:Client:ClientSecret"] = "secret",
			})
			.Build();

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddPiggyMetricsClientCredentials(configuration);
		services
			.AddHttpClient(ClientCredentialsTokenProvider.TokenHttpClientName)
			.ConfigurePrimaryHttpMessageHandler(() => stub);
		configure?.Invoke(services);
		return services.BuildServiceProvider();
	}
}

internal sealed class EdgeTestClient
{
	public EdgeTestClient(HttpClient http)
	{
		Http = http;
	}

	public HttpClient Http { get; }
}

internal sealed class EdgeStub : HttpMessageHandler
{
	public List<string?> Authorizations { get; } = new();

	public List<Uri?> Uris { get; } = new();

	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		Authorizations.Add(request.Headers.Authorization?.ToString());
		Uris.Add(request.RequestUri);
		return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
	}
}

// Answers the way the source's /uaa/oauth/token answers a client-credentials grant.
internal sealed class TokenEndpointStub : HttpMessageHandler
{
	private readonly int? _expiresInSeconds;
	private readonly TimeSpan _delay;

	private int _issued;

	public TokenEndpointStub(int? expiresInSeconds, TimeSpan delay = default)
	{
		_expiresInSeconds = expiresInSeconds;
		_delay = delay;
	}

	public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

	public int Calls => _issued;

	public string? LastUri { get; private set; }

	public string? LastScheme { get; private set; }

	public string? LastParameter { get; private set; }

	public string LastBody { get; private set; } = string.Empty;

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		LastUri = request.RequestUri!.ToString();
		LastScheme = request.Headers.Authorization?.Scheme;
		LastParameter = request.Headers.Authorization?.Parameter;
		LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

		if (_delay > TimeSpan.Zero)
		{
			await Task.Delay(_delay, cancellationToken);
		}

		if (Status != HttpStatusCode.OK)
		{
			return new HttpResponseMessage(Status);
		}

		var number = Interlocked.Increment(ref _issued);
		var lifetime = _expiresInSeconds is null
			? string.Empty
			: ",\"expires_in\":" + _expiresInSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

		return new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(
				"{\"access_token\":\"token-" + number + "\",\"token_type\":\"bearer\"" + lifetime + ",\"scope\":\"server\"}",
				Encoding.UTF8,
				"application/json"),
		};
	}
}
