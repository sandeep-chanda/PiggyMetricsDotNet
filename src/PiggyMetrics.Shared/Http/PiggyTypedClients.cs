using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PiggyMetrics.Shared.Json;

namespace PiggyMetrics.Shared.Http;

/// <summary>The budget and names every source edge runs under (decision D-007).</summary>
public static class PiggyHttpDefaults
{
    /// <summary>hystrix.command.default.execution.isolation.thread.timeoutInMilliseconds: 10000</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(10_000);

    public const string TokenHttpClientName = "piggy-client-credentials";
    public const string ServerScope = "server";
    public const string BearerScheme = "Bearer";
}

/// <summary>One service's security.oauth2.client.* values.</summary>
public sealed class ClientCredentialsOptions
{
    public const string SectionName = "ClientCredentials";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AccessTokenUri { get; set; } = "http://auth-service:5000/uaa/oauth/token";
    public string Scope { get; set; } = PiggyHttpDefaults.ServerScope;
}

/// <summary>
/// Holds one client-credentials token and reuses it until its expiry has passed, which is what
/// OAuth2RestTemplate does through OAuth2AccessToken.isExpired(). There is no refresh-ahead margin.
/// </summary>
public sealed class PiggyClientCredentialsTokenCache
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ClientCredentialsOptions> _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public PiggyClientCredentialsTokenCache(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ClientCredentialsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        _httpClientFactory = httpClientFactory;
        _options = options;
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (TryReadCachedToken(out var cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryReadCachedToken(out cached))
            {
                return cached;
            }

            var issued = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            _accessToken = issued.AccessToken;
            _expiresAt = issued.ExpiresAt;
            return issued.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryReadCachedToken(out string token)
    {
        var cached = _accessToken;
        if (cached is not null && DateTimeOffset.UtcNow < _expiresAt)
        {
            token = cached;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private async Task<(string AccessToken, DateTimeOffset ExpiresAt)> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var client = _httpClientFactory.CreateClient(PiggyHttpDefaults.TokenHttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, options.AccessTokenUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = options.Scope,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}")));

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var issued = await response.Content
            .ReadFromJsonAsync<AccessTokenResponse>(PiggyJson.Default, cancellationToken)
            .ConfigureAwait(false);

        return issued is null || string.IsNullOrEmpty(issued.AccessToken)
            ? throw new InvalidOperationException($"The authorization server at {options.AccessTokenUri} returned no access token.")
            : (issued.AccessToken, DateTimeOffset.UtcNow.AddSeconds(issued.ExpiresIn));
    }

    internal sealed class AccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }
}

/// <summary>Attaches the cached client-credentials token, as OAuth2FeignRequestInterceptor does.</summary>
public sealed class PiggyClientCredentialsHandler : DelegatingHandler
{
    private readonly PiggyClientCredentialsTokenCache _tokens;

    public PiggyClientCredentialsHandler(PiggyClientCredentialsTokenCache tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        _tokens = tokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(PiggyHttpDefaults.BearerScheme, token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Base of one edge: the shared JSON shape and the source's degrade-on-failure behaviour.</summary>
public abstract class PiggyTypedClient
{
    protected PiggyTypedClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        HttpClient = httpClient;
    }

    protected HttpClient HttpClient { get; }

    protected static JsonSerializerOptions JsonOptions => PiggyJson.Default;

    /// <summary>
    /// Returns the fallback value when the callee fails or exceeds the 10000 ms budget, which is what a
    /// Hystrix fallback implementation gives a Feign client.
    /// </summary>
    protected static async Task<TResult> ExecuteWithFallbackAsync<TResult>(
        Func<CancellationToken, Task<TResult>> call,
        Func<TResult> fallback,
        ILogger logger,
        string edge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            return await call(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OperationCanceledException or TimeoutException or JsonException)
        {
            logger.LogError(exception, "Error during call to {Edge}", edge);
            return fallback();
        }
    }
}

public static class PiggyHttpExtensions
{
    /// <summary>Registers the token cache and the handler that signs every service-to-service call.</summary>
    public static IServiceCollection AddPiggyClientCredentials(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ClientCredentialsOptions>().Bind(configuration.GetSection(ClientCredentialsOptions.SectionName));
        services.AddHttpClient(PiggyHttpDefaults.TokenHttpClientName, client => client.Timeout = PiggyHttpDefaults.Timeout);
        services.AddSingleton<PiggyClientCredentialsTokenCache>();
        services.AddTransient<PiggyClientCredentialsHandler>();

        return services;
    }

    /// <summary>One HTTP client per service-to-service edge: the source's timeout and the service token.</summary>
    public static IHttpClientBuilder AddPiggyTypedClient<TClient, TImplementation>(
        this IServiceCollection services,
        Uri baseAddress)
        where TClient : class
        where TImplementation : class, TClient
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        return services
            .AddHttpClient<TClient, TImplementation>(client =>
            {
                client.BaseAddress = baseAddress;
                client.Timeout = PiggyHttpDefaults.Timeout;
            })
            .AddHttpMessageHandler<PiggyClientCredentialsHandler>();
    }

    /// <summary>One HTTP client per third-party edge: the source's timeout, no token.</summary>
    public static IHttpClientBuilder AddPiggyAnonymousTypedClient<TClient, TImplementation>(
        this IServiceCollection services,
        Uri baseAddress)
        where TClient : class
        where TImplementation : class, TClient
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        return services.AddHttpClient<TClient, TImplementation>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = PiggyHttpDefaults.Timeout;
        });
    }
}
