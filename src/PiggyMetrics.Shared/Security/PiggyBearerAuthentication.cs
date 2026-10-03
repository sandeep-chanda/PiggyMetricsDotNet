using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using PiggyMetrics.Shared.Json;

namespace PiggyMetrics.Shared.Security;

/// <summary>Names and constants of the source's resource-server token validation (decision D-004).</summary>
public static class PiggyBearerDefaults
{
    public const string AuthenticationScheme = "Bearer";
    public const string Realm = "oauth2-resource";
    public const string HttpClientName = "piggy-userinfo";
    public const string ConfigurationSection = "Auth";
    public const string ScopeClaimType = "scope";
    public const string ClientIdClaimType = "client_id";
    public const string DefaultAuthority = "ROLE_USER";
    public const string UnknownPrincipal = "unknown";

    /// <summary>CustomUserInfoTokenServices.PRINCIPAL_KEYS, in the source's order.</summary>
    public static readonly string[] PrincipalKeys = ["user", "username", "userid", "user_id", "login", "id", "name"];
}

public sealed class PiggyBearerOptions : AuthenticationSchemeOptions
{
    public string UserInfoUri { get; set; } = "http://auth-service:5000/uaa/users/current";
    public string ClientId { get; set; } = string.Empty;
}

/// <summary>
/// Port of CustomUserInfoTokenServices: the caller's token is presented to the authorization server's
/// user-info endpoint, and the returned OAuth2Authentication map becomes the ClaimsPrincipal.
/// </summary>
public sealed class PiggyBearerHandler : AuthenticationHandler<PiggyBearerOptions>
{
    private const string BearerPrefix = "Bearer ";

    private readonly IHttpClientFactory _httpClientFactory;
    private string? _rejectedToken;

    public PiggyBearerHandler(
        IOptionsMonitor<PiggyBearerOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHttpClientFactory httpClientFactory)
        : base(options, logger, encoder) => _httpClientFactory = httpClientFactory;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken(Request.Headers.Authorization);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var map = await LoadUserInfoAsync(token, Context.RequestAborted).ConfigureAwait(false);
        if (map is null || map.ContainsKey("error"))
        {
            _rejectedToken = token;
            return AuthenticateResult.Fail("The authorization server did not recognise the presented token.");
        }

        var identity = new ClaimsIdentity(PiggyBearerDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.Name, ReadPrincipal(map)));
        foreach (var authority in ReadAuthorities(map))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, authority));
        }

        var (clientId, scopes) = ReadOAuth2Request(map);
        if (!string.IsNullOrEmpty(clientId))
        {
            identity.AddClaim(new Claim(PiggyBearerDefaults.ClientIdClaimType, clientId));
        }

        foreach (var scope in scopes)
        {
            identity.AddClaim(new Claim(PiggyBearerDefaults.ScopeClaimType, scope));
        }

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        _rejectedToken is null
            ? PiggyOAuthErrors.WriteUnauthorizedAsync(Response)
            : PiggyOAuthErrors.WriteInvalidTokenAsync(Response, _rejectedToken);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        PiggyOAuthErrors.WriteAccessDeniedAsync(Response);

    private static string? ReadToken(StringValues header)
    {
        var value = header.Count == 0 ? null : header[0];
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = value[BearerPrefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    private async Task<Dictionary<string, JsonElement>?> LoadUserInfoAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(PiggyBearerDefaults.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInfoUri);
            request.Headers.Authorization = new AuthenticationHeaderValue(PiggyBearerDefaults.AuthenticationScheme, token);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var payload = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer
                .DeserializeAsync<Dictionary<string, JsonElement>>(payload, PiggyJson.Default, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            // The source's getMap catches every exception and answers with the error map, which is a 401.
            Logger.LogInformation(exception, "Could not fetch user details from {UserInfoUri}", Options.UserInfoUri);
            return null;
        }
    }

    private static string ReadPrincipal(Dictionary<string, JsonElement> map)
    {
        foreach (var key in PiggyBearerDefaults.PrincipalKeys)
        {
            if (!map.TryGetValue(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            var principal = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
            if (!string.IsNullOrEmpty(principal))
            {
                return principal;
            }
        }

        return PiggyBearerDefaults.UnknownPrincipal;
    }

    // FixedAuthoritiesExtractor: the authorities entry when present, otherwise the constant ROLE_USER.
    private static IReadOnlyList<string> ReadAuthorities(Dictionary<string, JsonElement> map)
    {
        if (!map.TryGetValue("authorities", out var authorities) || authorities.ValueKind != JsonValueKind.Array)
        {
            return [PiggyBearerDefaults.DefaultAuthority];
        }

        var names = new List<string>();
        foreach (var entry in authorities.EnumerateArray())
        {
            var name = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("authority", out var nested)
                ? nested.GetString()
                : entry.ValueKind == JsonValueKind.String
                    ? entry.GetString()
                    : entry.ToString();

            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static (string? ClientId, IReadOnlyList<string> Scopes) ReadOAuth2Request(Dictionary<string, JsonElement> map)
    {
        if (!map.TryGetValue("oauth2Request", out var request) || request.ValueKind != JsonValueKind.Object)
        {
            return (null, []);
        }

        var clientId = request.TryGetProperty("clientId", out var clientIdElement) &&
                       clientIdElement.ValueKind == JsonValueKind.String
            ? clientIdElement.GetString()
            : null;

        var scopes = new List<string>();
        if (request.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in scope.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String && entry.GetString() is { Length: > 0 } value)
                {
                    scopes.Add(value);
                }
            }
        }

        return (clientId, scopes);
    }
}
