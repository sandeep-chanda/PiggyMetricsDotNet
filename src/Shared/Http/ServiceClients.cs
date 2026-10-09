using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Http;

// The source's security.oauth2.client.* block, plus the call timeout the source enforces through
// hystrix.command.default.execution.isolation.thread.timeoutInMilliseconds.
public sealed class ServiceClientOptions
{
	public const string SectionName = "Security:OAuth2:Client";
	public const int SourceTimeoutMilliseconds = 10000;

	public string ClientId { get; set; } = string.Empty;

	public string ClientSecret { get; set; } = string.Empty;

	public string AccessTokenUri { get; set; } = "http://auth-service:5000/uaa/oauth/token";

	public string GrantType { get; set; } = "client_credentials";

	public string Scope { get; set; } = "server";

	public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(SourceTimeoutMilliseconds);
}

public interface IClientCredentialsTokenProvider
{
	Task<string> GetTokenAsync(CancellationToken cancellationToken);
}

// Caches one client-credentials access token per process and renews it once the lifetime the authorization
// server reported has elapsed, which is what DefaultOAuth2ClientContext plus
// DefaultOAuth2AccessToken.isExpired() do in the source.
public sealed class ClientCredentialsTokenProvider : IClientCredentialsTokenProvider
{
	public const string TokenHttpClientName = "piggymetrics-token";

	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ServiceClientOptions _options;
	private readonly SemaphoreSlim _gate = new(1, 1);

	private string _token = string.Empty;
	private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

	public ClientCredentialsTokenProvider(IHttpClientFactory httpClientFactory, IOptions<ServiceClientOptions> options)
	{
		_httpClientFactory = httpClientFactory;
		_options = options.Value;
	}

	public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
	{
		if (IsCachedTokenUsable())
		{
			return _token;
		}

		await _gate.WaitAsync(cancellationToken);
		try
		{
			if (IsCachedTokenUsable())
			{
				return _token;
			}

			var issued = await RequestTokenAsync(cancellationToken);
			_token = issued.AccessToken;
			_expiresAt = issued.ExpiresAt;
			return _token;
		}
		finally
		{
			_gate.Release();
		}
	}

	private bool IsCachedTokenUsable()
	{
		return _token.Length > 0 && DateTimeOffset.UtcNow < _expiresAt;
	}

	private async Task<(string AccessToken, DateTimeOffset ExpiresAt)> RequestTokenAsync(CancellationToken cancellationToken)
	{
		var client = _httpClientFactory.CreateClient(TokenHttpClientName);
		using var request = new HttpRequestMessage(HttpMethod.Post, _options.AccessTokenUri);

		var credentials = Convert.ToBase64String(
			Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
		request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
		request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["grant_type"] = _options.GrantType,
			["scope"] = _options.Scope,
		});

		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var accessToken = document.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
		var lifetime = document.RootElement.TryGetProperty("expires_in", out var expiresIn)
			&& expiresIn.ValueKind == JsonValueKind.Number
				? TimeSpan.FromSeconds(expiresIn.GetInt64())
				: TimeSpan.Zero;

		return (accessToken, DateTimeOffset.UtcNow.Add(lifetime));
	}
}

// Attaches the cached client-credentials token to every outgoing call, the way OAuth2FeignRequestInterceptor
// does for the source's service-to-service edges.
public sealed class ClientCredentialsHandler : DelegatingHandler
{
	private readonly IClientCredentialsTokenProvider _tokenProvider;

	public ClientCredentialsHandler(IClientCredentialsTokenProvider tokenProvider)
	{
		_tokenProvider = tokenProvider;
	}

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		var token = await _tokenProvider.GetTokenAsync(cancellationToken);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return await base.SendAsync(request, cancellationToken);
	}
}
