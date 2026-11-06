namespace PiggyMetrics.AuthService.Config;

public sealed class OAuthClient
{
    public required string ClientId { get; init; }

    public string? Secret { get; init; }

    public required string[] GrantTypes { get; init; }

    public required string[] Scopes { get; init; }
}

public sealed class OAuth2AuthorizationConfig
{
    public OAuth2AuthorizationConfig(IConfiguration configuration)
    {
        Clients = new[]
        {
            new OAuthClient
            {
                ClientId = "browser",
                Secret = null,
                GrantTypes = ["refresh_token", "password"],
                Scopes = ["ui"]
            },
            new OAuthClient
            {
                ClientId = "account-service",
                Secret = ResolveSecret(configuration, "ACCOUNT_SERVICE_PASSWORD"),
                GrantTypes = ["client_credentials", "refresh_token"],
                Scopes = ["server"]
            },
            new OAuthClient
            {
                ClientId = "statistics-service",
                Secret = ResolveSecret(configuration, "STATISTICS_SERVICE_PASSWORD"),
                GrantTypes = ["client_credentials", "refresh_token"],
                Scopes = ["server"]
            },
            new OAuthClient
            {
                ClientId = "notification-service",
                Secret = ResolveSecret(configuration, "NOTIFICATION_SERVICE_PASSWORD"),
                GrantTypes = ["client_credentials", "refresh_token"],
                Scopes = ["server"]
            }
        };
    }

    public IReadOnlyList<OAuthClient> Clients { get; }

    public OAuthClient? Find(string clientId)
    {
        foreach (var client in Clients)
        {
            if (string.Equals(client.ClientId, clientId, StringComparison.Ordinal))
            {
                return client;
            }
        }

        return null;
    }

    public static string? ResolveSecret(IConfiguration configuration, string name)
    {
        var value = AuthSettings.ResolvePlaceholder(configuration[name]);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        return Environment.GetEnvironmentVariable(name);
    }
}
