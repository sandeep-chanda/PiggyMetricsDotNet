using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PiggyMetrics.Shared.Http;

public sealed class ClientCredentialsOptions
{
    public string AccessTokenUri { get; set; } = "http://auth-service:5000/uaa/oauth/token";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string GrantType { get; set; } = "client_credentials";
    public string Scope { get; set; } = "server";
}

public sealed class ClientCredentialsTokenCache
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public ClientCredentialsTokenCache(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetAccessTokenAsync(ClientCredentialsOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var key = options.ClientId + "|" + options.Scope + "|" + options.AccessTokenUri;
        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresUtc > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return entry.AccessToken;
        }

        var client = _httpClientFactory.CreateClient("token");
        using var request = new HttpRequestMessage(HttpMethod.Post, options.AccessTokenUri);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = options.GrantType,
            ["scope"] = options.Scope
        });
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(options.ClientId + ":" + options.ClientSecret));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var token = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token missing");
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds)
            ? seconds
            : 3600;
        _cache[key] = new CacheEntry(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
        return token;
    }

    private sealed record CacheEntry(string AccessToken, DateTimeOffset ExpiresUtc);
}
