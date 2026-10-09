using System.Text;
using PiggyMetrics.AuthService.Domain;
using PiggyMetrics.AuthService.Security;
using PiggyMetrics.AuthService.Service.Security;

namespace PiggyMetrics.AuthService.Config;

public static class OAuthTokenEndpoint
{
    public static WebApplication MapOAuthTokenEndpoint(this WebApplication app)
    {
        app.MapPost("/oauth/token", IssueToken);
        return app;
    }

    private static async Task<IResult> IssueToken(
        HttpContext context,
        OAuth2AuthorizationConfig config,
        MongoUserDetailsService users,
        InMemoryTokenStore store)
    {
        IFormCollection form;
        try
        {
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch (InvalidOperationException)
        {
            return OAuthError(StatusCodes.Status400BadRequest, "invalid_request", "form body required");
        }

        string clientId;
        string clientSecret;
        try
        {
            (clientId, clientSecret) = ReadClient(context.Request, form);
        }
        catch (FormatException)
        {
            return OAuthError(StatusCodes.Status401Unauthorized, "invalid_client", "Bad client credentials");
        }

        if (string.IsNullOrEmpty(clientId))
        {
            return OAuthError(StatusCodes.Status401Unauthorized, "invalid_client", "Bad client credentials");
        }

        var client = config.Find(clientId);
        if (client is null || !SecretMatches(client, clientSecret))
        {
            return OAuthError(StatusCodes.Status401Unauthorized, "invalid_client", "Bad client credentials");
        }

        var grantType = form["grant_type"].ToString();
        if (string.IsNullOrEmpty(grantType))
        {
            return OAuthError(StatusCodes.Status400BadRequest, "invalid_request", "grant_type required");
        }

        if (!client.GrantTypes.Contains(grantType, StringComparer.Ordinal))
        {
            return OAuthError(StatusCodes.Status401Unauthorized, "invalid_client", "Unauthorized grant type: " + grantType);
        }

        if (!TryResolveScopes(form["scope"].ToString(), client, out var scopes, out var scopeError))
        {
            return scopeError!;
        }

        if (grantType == "client_credentials")
        {
            var token = store.Issue(client.ClientId, scopes, username: null, grantType);
            return TokenResponse(token);
        }

        if (grantType == "password")
        {
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return OAuthError(StatusCodes.Status400BadRequest, "invalid_grant", "Bad credentials");
            }

            User user;
            try
            {
                user = users.LoadUserByUsername(username);
            }
            catch (UsernameNotFoundException)
            {
                return OAuthError(StatusCodes.Status400BadRequest, "invalid_grant", "Bad credentials");
            }

            if (string.IsNullOrEmpty(user.Password) || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                return OAuthError(StatusCodes.Status400BadRequest, "invalid_grant", "Bad credentials");
            }

            var token = store.Issue(client.ClientId, scopes, username, grantType);
            return TokenResponse(token);
        }

        if (grantType == "refresh_token")
        {
            var refreshToken = form["refresh_token"].ToString();
            var token = store.Refresh(refreshToken, client.ClientId);
            if (token is null)
            {
                return OAuthError(StatusCodes.Status400BadRequest, "invalid_grant", "Invalid refresh token");
            }

            return TokenResponse(token);
        }

        return OAuthError(StatusCodes.Status400BadRequest, "unsupported_grant_type", grantType);
    }

    private static bool TryResolveScopes(string requested, OAuthClient client, out string[] scopes, out IResult? error)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            scopes = client.Scopes;
            error = null;
            return true;
        }

        scopes = requested.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var scope in scopes)
        {
            if (!client.Scopes.Contains(scope, StringComparer.Ordinal))
            {
                error = OAuthError(StatusCodes.Status400BadRequest, "invalid_scope", scope);
                return false;
            }
        }

        error = null;
        return true;
    }

    private static (string clientId, string clientSecret) ReadClient(HttpRequest request, IFormCollection form)
    {
        if (request.Headers.TryGetValue("Authorization", out var values))
        {
            var header = values.ToString();
            if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var encoded = header["Basic ".Length..].Trim();
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                var separator = decoded.IndexOf(':');
                if (separator < 0)
                {
                    return (decoded, string.Empty);
                }

                return (decoded[..separator], decoded[(separator + 1)..]);
            }
        }

        return (form["client_id"].ToString(), form["client_secret"].ToString());
    }

    private static bool SecretMatches(OAuthClient client, string? presented)
    {
        if (string.IsNullOrEmpty(client.Secret))
        {
            return string.IsNullOrEmpty(presented);
        }

        return string.Equals(client.Secret, presented, StringComparison.Ordinal);
    }

    private static IResult TokenResponse(IssuedToken token)
    {
        var expiresIn = (int)Math.Max(1, (token.ExpiresUtc - DateTimeOffset.UtcNow).TotalSeconds);
        var body = new Dictionary<string, object?>
        {
            ["access_token"] = token.AccessToken,
            ["token_type"] = "bearer",
            ["expires_in"] = expiresIn,
            ["scope"] = string.Join(' ', token.Scopes),
            ["refresh_token"] = token.RefreshToken
        };
        return Results.Json(body);
    }

    private static IResult OAuthError(int status, string error, string description)
    {
        return Results.Json(new Dictionary<string, string>
        {
            ["error"] = error,
            ["error_description"] = description
        }, statusCode: status);
    }
}
