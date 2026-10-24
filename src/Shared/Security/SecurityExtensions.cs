using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PiggyMetrics.Shared.Security;

public static class SecurityExtensions
{
	// Registers the user-info bearer scheme plus the two authorization policies the source's controllers express:
	// user (an authenticated caller) and server (#oauth2.hasScope('server')).
	public static IServiceCollection AddPiggyMetricsResourceServer(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var userInfoUri = configuration[PiggyMetricsAuth.UserInfoUriConfigurationKey];

		services.AddHttpClient(PiggyMetricsAuth.HttpClientName);

		services
			.AddAuthentication(PiggyMetricsAuth.Scheme)
			.AddScheme<UserInfoAuthenticationOptions, UserInfoAuthenticationHandler>(
				PiggyMetricsAuth.Scheme,
				options =>
				{
					options.UserInfoUri = string.IsNullOrWhiteSpace(userInfoUri)
						? PiggyMetricsAuth.DefaultUserInfoUri
						: userInfoUri;
				});

		services
			.AddAuthorizationBuilder()
			.AddPolicy(PiggyMetricsAuth.UserPolicy, policy => policy
				.AddAuthenticationSchemes(PiggyMetricsAuth.Scheme)
				.RequireAuthenticatedUser())
			.AddPolicy(PiggyMetricsAuth.ServerPolicy, policy => policy
				.AddAuthenticationSchemes(PiggyMetricsAuth.Scheme)
				.RequireAuthenticatedUser()
				.RequireClaim(PiggyMetricsAuth.ScopeClaimType, PiggyMetricsAuth.ServerScope))
			.SetDefaultPolicy(new AuthorizationPolicyBuilder(PiggyMetricsAuth.Scheme)
				.RequireAuthenticatedUser()
				.Build());

		return services;
	}
}
