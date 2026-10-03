using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Http;

namespace PiggyMetrics.Shared.Security;

/// <summary>Policy names covering the source's two security checks (decision D-006).</summary>
public static class PiggyAuthorizationPolicies
{
    /// <summary>anyRequest().authenticated()</summary>
    public const string User = "user";

    /// <summary>#oauth2.hasScope('server')</summary>
    public const string Server = "server";

    /// <summary>The scope value the source's three service clients carry.</summary>
    public const string ServerScope = "server";
}

/// <summary>
/// Succeeds for a caller carrying the server scope, and for an anonymous or user caller whose route
/// value matches the named resource - the shape of
/// #oauth2.hasScope('server') or #accountName.equals('demo').
/// </summary>
public sealed class ServerScopeOrNamedResourceRequirement : IAuthorizationRequirement
{
    public ServerScopeOrNamedResourceRequirement(string routeValueKey, string allowedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeValueKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedValue);

        RouteValueKey = routeValueKey;
        AllowedValue = allowedValue;
    }

    public string RouteValueKey { get; }

    public string AllowedValue { get; }
}

internal sealed class ServerScopeOrNamedResourceHandler
    : AuthorizationHandler<ServerScopeOrNamedResourceRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ServerScopeOrNamedResourceHandler(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ServerScopeOrNamedResourceRequirement requirement)
    {
        if (context.User.HasClaim(PiggyBearerDefaults.ScopeClaimType, PiggyAuthorizationPolicies.ServerScope))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var routeValue = _httpContextAccessor.HttpContext?.Request.RouteValues[requirement.RouteValueKey] as string;
        if (string.Equals(routeValue, requirement.AllowedValue, StringComparison.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class PiggySecurityExtensions
{
    /// <summary>
    /// Registers the Bearer scheme that calls the authorization server's user-info endpoint, and makes an
    /// authenticated principal the fallback requirement, as anyRequest().authenticated() does.
    /// </summary>
    public static IServiceCollection AddPiggyBearerAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpClient(
            PiggyBearerDefaults.HttpClientName,
            client => client.Timeout = PiggyHttpDefaults.Timeout);

        services
            .AddAuthentication(PiggyBearerDefaults.AuthenticationScheme)
            .AddScheme<PiggyBearerOptions, PiggyBearerHandler>(
                PiggyBearerDefaults.AuthenticationScheme,
                options => configuration.GetSection(PiggyBearerDefaults.ConfigurationSection).Bind(options));

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder(PiggyBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .Build());

        return services;
    }

    /// <summary>Registers the two named policies and the handler behind a named-resource exception.</summary>
    public static IServiceCollection AddPiggyAuthorization(
        this IServiceCollection services,
        Action<AuthorizationBuilder>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAuthorizationHandler, ServerScopeOrNamedResourceHandler>();

        var builder = services.AddAuthorizationBuilder()
            .AddPolicy(
                PiggyAuthorizationPolicies.User,
                policy => policy
                    .AddAuthenticationSchemes(PiggyBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser())
            .AddPolicy(
                PiggyAuthorizationPolicies.Server,
                policy => policy
                    .AddAuthenticationSchemes(PiggyBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(PiggyBearerDefaults.ScopeClaimType, PiggyAuthorizationPolicies.ServerScope));

        configure?.Invoke(builder);

        return services;
    }
}
