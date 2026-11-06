using PiggyMetrics.Gateway.Proxy;
using PiggyMetrics.Shared.Store;

namespace PiggyMetrics.Gateway.Cutover;

public enum CutoverMode
{
    Source,
    Shadow,
    Switched
}

public readonly record struct CutoverDecision(
    string Path,
    CutoverMode Mode,
    string ActiveUnit,
    string SourceUnit,
    string DestinationUnit);

public sealed record CutoverRouteSnapshot(
    string Path,
    string Name,
    CutoverMode Mode,
    string SourceUnit,
    string DestinationUnit,
    string ActiveUnit,
    bool ParityHolds);

public sealed record SharedStore(
    string RoutePath,
    string Host,
    int Port,
    string Database,
    string Collection)
{
    public string SourceStore => Host + ":" + Port + "/" + Database + "/" + Collection;

    public string DestinationStore => Host + ":" + Port + "/" + Database + "/" + Collection;
}

public static class SharedDataStores
{
    public const bool RollbackRequired = false;

    public const string RollbackReason = "both ends read and write the same stores";

    public static IReadOnlyList<SharedStore> All { get; } =
    [
        new("/accounts/**", "account-mongodb", 27017, "piggymetrics", MongoCollectionNames.Accounts),
        new("/statistics/**", "statistics-mongodb", 27017, "piggymetrics", MongoCollectionNames.Datapoints),
        new("/notifications/**", "notification-mongodb", 27017, "piggymetrics", MongoCollectionNames.Recipients),
        new("/uaa/**", "auth-mongodb", 27017, "piggymetrics", MongoCollectionNames.Users)
    ];
}

public sealed class SourceGatewayCutover
{
    public static readonly string[] SwitchOrder =
    [
        "/accounts/**",
        "/statistics/**",
        "/notifications/**",
        "/uaa/**"
    ];

    private readonly object _gate = new();
    private readonly List<CutoverRoute> _routes;

    public SourceGatewayCutover(IConfiguration configuration)
    {
        _routes = SwitchOrder.Select(path => CutoverRoute.Create(configuration, path)).ToList();
    }

    public bool TryDecide(string path, out CutoverDecision decision)
    {
        lock (_gate)
        {
            var route = _routes.FirstOrDefault(candidate => candidate.Path == path);
            if (route is null)
            {
                decision = default;
                return false;
            }

            decision = route.Decision();
            return true;
        }
    }

    public CutoverRouteSnapshot Route(string path)
    {
        lock (_gate)
        {
            return Find(path).Snapshot();
        }
    }

    public IReadOnlyList<CutoverRouteSnapshot> Routes
    {
        get
        {
            lock (_gate)
            {
                return _routes.Select(route => route.Snapshot()).ToArray();
            }
        }
    }

    public void BeginShadow(string path)
    {
        lock (_gate)
        {
            var route = Find(path);
            if (route.Mode == CutoverMode.Switched)
            {
                throw new InvalidOperationException("Roll " + path + " back to the source unit before shadowing it");
            }

            route.Mode = CutoverMode.Shadow;
            route.ParityHolds = false;
        }
    }

    public void RecordShadowResult(
        string path,
        int sourceStatus,
        string sourceBody,
        int destinationStatus,
        string destinationBody)
    {
        lock (_gate)
        {
            var route = Find(path);
            if (route.Mode != CutoverMode.Shadow)
            {
                return;
            }

            route.ParityHolds = sourceStatus == destinationStatus
                && string.Equals(sourceBody, destinationBody, StringComparison.Ordinal);
        }
    }

    public void Switch(string path)
    {
        lock (_gate)
        {
            var route = Find(path);
            var index = _routes.FindIndex(candidate => candidate.Path == path);
            if (route.Mode != CutoverMode.Shadow || !route.ParityHolds)
            {
                throw new InvalidOperationException("Shadow " + path + " until parity holds");
            }

            for (var i = 0; i < index; i++)
            {
                if (_routes[i].Mode != CutoverMode.Switched)
                {
                    throw new InvalidOperationException(
                        "Switch " + _routes[i].Path + " before " + path + "; the auth route last");
                }
            }

            route.Mode = CutoverMode.Switched;
        }
    }

    public void RollBack(string path)
    {
        lock (_gate)
        {
            var route = Find(path);
            route.Mode = CutoverMode.Source;
            route.ParityHolds = false;
        }
    }

    private CutoverRoute Find(string path)
    {
        var route = _routes.FirstOrDefault(candidate => candidate.Path == path);
        if (route is null)
        {
            throw new InvalidOperationException("Unknown gateway route " + path);
        }

        return route;
    }

    private sealed class CutoverRoute
    {
        public required string Path { get; init; }
        public required string Name { get; init; }
        public required string SourceUnit { get; init; }
        public required string DestinationUnit { get; init; }
        public CutoverMode Mode { get; set; }
        public bool ParityHolds { get; set; }

        public string ActiveUnit => Mode == CutoverMode.Switched ? DestinationUnit : SourceUnit;

        public static CutoverRoute Create(IConfiguration configuration, string path)
        {
            var name = path switch
            {
                "/accounts/**" => "account-service",
                "/statistics/**" => "statistics-service",
                "/notifications/**" => "notification-service",
                "/uaa/**" => "auth-service",
                _ => throw new InvalidOperationException("Unknown gateway route " + path)
            };

            var definition = new ZuulRouteDefinition(
                name,
                path,
                configuration[$"zuul:routes:{name}:url"],
                configuration[$"zuul:routes:{name}:serviceId"] ?? name,
                false,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var resolved = ZuulRoutes.ResolveBase(configuration, definition);
            var source = Configured(configuration, name, "sourceUnit", resolved);
            return new CutoverRoute
            {
                Path = path,
                Name = name,
                SourceUnit = source,
                DestinationUnit = Configured(configuration, name, "destinationUnit", source)
            };
        }

        public CutoverDecision Decision()
        {
            return new CutoverDecision(Path, Mode, ActiveUnit, SourceUnit, DestinationUnit);
        }

        public CutoverRouteSnapshot Snapshot()
        {
            return new CutoverRouteSnapshot(Path, Name, Mode, SourceUnit, DestinationUnit, ActiveUnit, ParityHolds);
        }

        private static string Configured(IConfiguration configuration, string name, string key, string fallback)
        {
            var value = configuration[$"cutover:routes:{name}:{key}"];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
