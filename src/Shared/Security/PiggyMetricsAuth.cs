using Microsoft.AspNetCore.Authentication;

namespace PiggyMetrics.Shared.Security;

// Names and constants shared by every resource server, taken from the source stack.
public static class PiggyMetricsAuth
{
	public const string Scheme = "Bearer";
	public const string HttpClientName = "piggymetrics-userinfo";

	public const string UserPolicy = "user";
	public const string ServerPolicy = "server";

	public const string ScopeClaimType = "scope";
	public const string ClientIdClaimType = "client_id";

	public const string UiScope = "ui";
	public const string ServerScope = "server";

	public const string DefaultAuthority = "ROLE_USER";
	public const string UnknownPrincipal = "unknown";
	public const string DefaultUserInfoUri = "http://auth-service:5000/uaa/users/current";
	public const string UserInfoUriConfigurationKey = "Security:OAuth2:Resource:UserInfoUri";

	// CustomUserInfoTokenServices.PRINCIPAL_KEYS, in the source's probe order.
	public static readonly string[] PrincipalKeys =
	{
		"user", "username", "userid", "user_id", "login", "id", "name",
	};
}

public sealed class UserInfoAuthenticationOptions : AuthenticationSchemeOptions
{
	public string UserInfoUri { get; set; } = PiggyMetricsAuth.DefaultUserInfoUri;

	public string HttpClientName { get; set; } = PiggyMetricsAuth.HttpClientName;
}
