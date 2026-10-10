using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.AuthService.Security;

public static class LocalAccessTokenDefaults
{
    public const string Scheme = "LocalBearer";
}

public sealed class LocalAccessTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly InMemoryTokenStore _tokenStore;

    public LocalAccessTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        InMemoryTokenStore tokenStore)
        : base(options, logger, encoder)
    {
        _tokenStore = tokenStore;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing bearer token"));
        }

        var accessToken = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(accessToken))
        {
            return Task.FromResult(AuthenticateResult.Fail("Empty bearer token"));
        }

        var issued = _tokenStore.FindByAccessToken(accessToken);
        if (issued is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid token"));
        }

        var name = string.IsNullOrEmpty(issued.Username) ? issued.ClientId : issued.Username!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new("client_id", issued.ClientId)
        };
        if (!string.IsNullOrEmpty(issued.Username))
        {
            claims.Add(new Claim("username", issued.Username));
        }

        foreach (var scope in issued.Scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
