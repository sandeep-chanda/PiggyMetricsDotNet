using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Security;

// The .NET counterpart of CustomUserInfoTokenServices: a presented bearer token is accepted only when the
// authorization server's user-info endpoint resolves it, and the resolved document supplies the principal
// name, the calling client id and the granted scopes.
public sealed class UserInfoAuthenticationHandler : AuthenticationHandler<UserInfoAuthenticationOptions>
{
	private const string BearerPrefix = "Bearer ";
	private const string FailureMessage = "Could not fetch user details";

	private readonly IHttpClientFactory _httpClientFactory;

	public UserInfoAuthenticationHandler(
		IOptionsMonitor<UserInfoAuthenticationOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder,
		IHttpClientFactory httpClientFactory)
		: base(options, logger, encoder)
	{
		_httpClientFactory = httpClientFactory;
	}

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var header = Request.Headers.Authorization.ToString();
		if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return AuthenticateResult.NoResult();
		}

		var token = header[BearerPrefix.Length..].Trim();
		if (token.Length == 0)
		{
			return AuthenticateResult.NoResult();
		}

		JsonElement document;
		try
		{
			var client = _httpClientFactory.CreateClient(Options.HttpClientName);
			using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInfoUri);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			using var response = await client.SendAsync(request, Context.RequestAborted);
			if (!response.IsSuccessStatusCode)
			{
				Logger.LogInformation("{Message}: status {Status}", FailureMessage, (int)response.StatusCode);
				return AuthenticateResult.Fail(FailureMessage);
			}

			await using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
			using var parsed = await JsonDocument.ParseAsync(stream, cancellationToken: Context.RequestAborted);
			document = parsed.RootElement.Clone();
		}
		catch (Exception exception)
		{
			Logger.LogInformation("{Message}: {Reason}", FailureMessage, exception.Message);
			return AuthenticateResult.Fail(FailureMessage);
		}

		if (document.ValueKind != JsonValueKind.Object || document.TryGetProperty("error", out _))
		{
			return AuthenticateResult.Fail(FailureMessage);
		}

		var principal = BuildPrincipal(document);
		return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
	}

	private static ClaimsPrincipal BuildPrincipal(JsonElement document)
	{
		var identity = new ClaimsIdentity(PiggyMetricsAuth.Scheme, ClaimTypes.Name, ClaimTypes.Role);
		identity.AddClaim(new Claim(ClaimTypes.Name, ReadPrincipalName(document)));

		if (document.TryGetProperty("oauth2Request", out var request) && request.ValueKind == JsonValueKind.Object)
		{
			if (request.TryGetProperty("clientId", out var clientId) && clientId.ValueKind == JsonValueKind.String)
			{
				identity.AddClaim(new Claim(PiggyMetricsAuth.ClientIdClaimType, clientId.GetString()!));
			}

			if (request.TryGetProperty("scope", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
			{
				foreach (var scope in scopes.EnumerateArray())
				{
					if (scope.ValueKind == JsonValueKind.String)
					{
						identity.AddClaim(new Claim(PiggyMetricsAuth.ScopeClaimType, scope.GetString()!));
					}
				}
			}
		}

		foreach (var authority in ReadAuthorities(document))
		{
			identity.AddClaim(new Claim(ClaimTypes.Role, authority));
		}

		return new ClaimsPrincipal(identity);
	}

	private static string ReadPrincipalName(JsonElement document)
	{
		foreach (var key in PiggyMetricsAuth.PrincipalKeys)
		{
			if (!document.TryGetProperty(key, out var value))
			{
				continue;
			}

			var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
		}

		return PiggyMetricsAuth.UnknownPrincipal;
	}

	private static List<string> ReadAuthorities(JsonElement document)
	{
		if (!document.TryGetProperty("authorities", out var value))
		{
			return new List<string> { PiggyMetricsAuth.DefaultAuthority };
		}

		var authorities = new List<string>();
		Collect(value, authorities);
		return authorities;
	}

	private static void Collect(JsonElement value, List<string> authorities)
	{
		switch (value.ValueKind)
		{
			case JsonValueKind.String:
				var text = value.GetString() ?? string.Empty;
				var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				authorities.AddRange(parts);
				break;
			case JsonValueKind.Array:
				foreach (var item in value.EnumerateArray())
				{
					Collect(item, authorities);
				}

				break;
			case JsonValueKind.Object:
				if (value.TryGetProperty("authority", out var authority) && authority.ValueKind == JsonValueKind.String)
				{
					authorities.Add(authority.GetString()!);
				}

				break;
		}
	}
}
