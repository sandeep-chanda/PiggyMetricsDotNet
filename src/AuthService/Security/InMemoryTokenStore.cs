using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace PiggyMetrics.AuthService.Security;

public sealed class IssuedToken
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required string ClientId { get; init; }

    public required string[] Scopes { get; init; }

    public string? Username { get; init; }

    public required string GrantType { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }

    public required DateTimeOffset RefreshExpiresUtc { get; init; }
}

public sealed class InMemoryTokenStore
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromSeconds(43199);

    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    private readonly ConcurrentDictionary<string, IssuedToken> _byAccess = new();
    private readonly ConcurrentDictionary<string, IssuedToken> _byRefresh = new();

    public IssuedToken Issue(string clientId, string[] scopes, string? username, string grantType)
    {
        var token = new IssuedToken
        {
            AccessToken = NewToken(),
            RefreshToken = NewToken(),
            ClientId = clientId,
            Scopes = scopes,
            Username = username,
            GrantType = grantType,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(AccessTokenLifetime),
            RefreshExpiresUtc = DateTimeOffset.UtcNow.Add(RefreshTokenLifetime)
        };
        _byAccess[token.AccessToken] = token;
        _byRefresh[token.RefreshToken] = token;
        return token;
    }

    public IssuedToken? FindByAccessToken(string accessToken)
    {
        if (!_byAccess.TryGetValue(accessToken, out var token))
        {
            return null;
        }

        if (token.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return token;
    }

    public IssuedToken? Refresh(string refreshToken, string clientId)
    {
        if (!_byRefresh.TryGetValue(refreshToken, out var existing))
        {
            return null;
        }

        if (!string.Equals(existing.ClientId, clientId, StringComparison.Ordinal))
        {
            return null;
        }

        if (existing.RefreshExpiresUtc <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        var token = new IssuedToken
        {
            AccessToken = NewToken(),
            RefreshToken = existing.RefreshToken,
            ClientId = existing.ClientId,
            Scopes = existing.Scopes,
            Username = existing.Username,
            GrantType = "refresh_token",
            ExpiresUtc = DateTimeOffset.UtcNow.Add(AccessTokenLifetime),
            RefreshExpiresUtc = existing.RefreshExpiresUtc
        };
        _byAccess[token.AccessToken] = token;
        _byRefresh[token.RefreshToken] = token;
        return token;
    }

    private static string NewToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    }
}
