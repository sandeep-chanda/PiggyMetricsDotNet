using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Security;

public sealed class UserInfoBearerOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "Bearer";
    public string UserInfoEndpointUrl { get; set; } = "http://auth-service:5000/uaa/users/current";
    public string ClientId { get; set; } = string.Empty;
}

public sealed class UserInfoBearerAuthenticationHandler : AuthenticationHandler<UserInfoBearerOptions>
{
    private static readonly string[] PrincipalKeys = ["user", "username", "userid", "user_id", "login", "id", "name"];
    private readonly IHttpClientFactory _httpClientFactory;

    public UserInfoBearerAuthenticationHandler(
        IOptionsMonitor<UserInfoBearerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHttpClientFactory httpClientFactory)
        : base(options, logger, encoder)
    {
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var header = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Missing bearer token");
        }

        var accessToken = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(accessToken))
        {
            return AuthenticateResult.Fail("Empty bearer token");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("userinfo");
            using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInfoEndpointUrl);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + accessToken);
            using var response = await client.SendAsync(request, Context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                return AuthenticateResult.Fail("Invalid token");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: Context.RequestAborted);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out _))
            {
                return AuthenticateResult.Fail("Invalid token");
            }

            var principalName = ResolvePrincipal(root);
            var clientId = Options.ClientId;
            var scopes = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("oauth2Request", out var oauth2Request) && oauth2Request.ValueKind == JsonValueKind.Object)
            {
                if (oauth2Request.TryGetProperty("clientId", out var cid) && cid.ValueKind == JsonValueKind.String)
                {
                    clientId = cid.GetString() ?? clientId;
                }
                if (oauth2Request.TryGetProperty("scope", out var scopeEl))
                {
                    if (scopeEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in scopeEl.EnumerateArray())
                        {
                            if (s.ValueKind == JsonValueKind.String && s.GetString() is { } v)
                            {
                                scopes.Add(v);
                            }
                        }
                    }
                    else if (scopeEl.ValueKind == JsonValueKind.String && scopeEl.GetString() is { } one)
                    {
                        foreach (var part in one.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            scopes.Add(part);
                        }
                    }
                }
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, principalName),
                new("client_id", clientId)
            };
            foreach (var scope in scopes)
            {
                claims.Add(new Claim("scope", scope));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogInformation(ex, "Could not fetch user details");
            return AuthenticateResult.Fail("Invalid token");
        }
    }

    private static string ResolvePrincipal(JsonElement map)
    {
        foreach (var key in PrincipalKeys)
        {
            if (map.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String && el.GetString() is { Length: > 0 } v)
            {
                return v;
            }
        }
        return "unknown";
    }
}

public static class UserInfoBearerExtensions
{
    public static AuthenticationBuilder AddUserInfoBearer(
        this AuthenticationBuilder builder,
        Action<UserInfoBearerOptions>? configure = null)
    {
        return builder.AddScheme<UserInfoBearerOptions, UserInfoBearerAuthenticationHandler>(
            UserInfoBearerOptions.DefaultScheme,
            configure);
    }
}
