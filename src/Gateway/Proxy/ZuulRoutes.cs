namespace PiggyMetrics.Gateway.Proxy;

public sealed record ZuulRouteDefinition(
    string Name,
    string Path,
    string? Url,
    string? ServiceId,
    bool StripPrefix,
    IReadOnlySet<string> SensitiveHeaders);

public static class GatewayServiceLocations
{
    public const string AuthService = "http://auth-service:5000";
    public const string AccountService = "http://account-service:6000";
    public const string StatisticsService = "http://statistics-service:7000";
    public const string NotificationService = "http://notification-service:8000";
}

public static class ZuulRoutes
{
    public static IReadOnlyList<ZuulRouteDefinition> Load(IConfiguration configuration)
    {
        var routes = new List<ZuulRouteDefinition>();
        foreach (var section in configuration.GetSection("zuul:routes").GetChildren())
        {
            var path = section["path"];
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            routes.Add(new ZuulRouteDefinition(
                section.Key,
                path,
                section["url"],
                section["serviceId"],
                ParseBool(section["stripPrefix"]),
                ParseSensitiveHeaders(section["sensitiveHeaders"])));
        }

        return routes;
    }

    public static bool Matches(string pattern, PathString requestPath)
    {
        var path = requestPath.Value ?? "/";
        if (pattern.EndsWith("/**", StringComparison.Ordinal))
        {
            var prefix = pattern[..^3];
            if (prefix.Length == 0)
            {
                return true;
            }

            return path.Equals(prefix, StringComparison.Ordinal)
                || path.StartsWith(prefix + "/", StringComparison.Ordinal);
        }

        return path.Equals(pattern, StringComparison.Ordinal);
    }

    public static string ResolveBase(IConfiguration configuration, ZuulRouteDefinition route)
    {
        if (!string.IsNullOrWhiteSpace(route.Url))
        {
            return route.Url;
        }

        var serviceId = string.IsNullOrWhiteSpace(route.ServiceId) ? route.Name : route.ServiceId;
        var configured = configuration[$"zuul:serviceUrls:{serviceId}"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return serviceId switch
        {
            "auth-service" => GatewayServiceLocations.AuthService,
            "account-service" => GatewayServiceLocations.AccountService,
            "statistics-service" => GatewayServiceLocations.StatisticsService,
            "notification-service" => GatewayServiceLocations.NotificationService,
            _ => throw new InvalidOperationException("No destination for " + serviceId)
        };
    }

    public static string ForwardPath(string requestPath, ZuulRouteDefinition route)
    {
        if (!route.StripPrefix)
        {
            return requestPath;
        }

        if (!route.Path.EndsWith("/**", StringComparison.Ordinal))
        {
            return "/";
        }

        var prefix = route.Path[..^3];
        if (requestPath.Equals(prefix, StringComparison.Ordinal))
        {
            return "/";
        }

        if (requestPath.StartsWith(prefix + "/", StringComparison.Ordinal))
        {
            return requestPath[prefix.Length..];
        }

        return requestPath;
    }

    private static bool ParseBool(string? value)
    {
        return bool.TryParse(value, out var parsed) && parsed;
    }

    private static IReadOnlySet<string> ParseSensitiveHeaders(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class ZuulRouteTable
{
    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<ZuulRouteDefinition> _routes;

    public ZuulRouteTable(IConfiguration configuration)
    {
        _configuration = configuration;
        _routes = ZuulRoutes.Load(configuration);
    }

    public IReadOnlyList<ZuulRouteDefinition> Routes => _routes;

    public bool TryMatch(PathString path, out ZuulRouteDefinition route, out string baseUrl)
    {
        foreach (var candidate in _routes)
        {
            if (!ZuulRoutes.Matches(candidate.Path, path))
            {
                continue;
            }

            route = candidate;
            baseUrl = ZuulRoutes.ResolveBase(_configuration, candidate);
            return true;
        }

        route = null!;
        baseUrl = string.Empty;
        return false;
    }
}
