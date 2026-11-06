namespace PiggyMetrics.Parity.Tests;

public enum AuthKind
{
    None,
    User,
    Server
}

public enum ServiceKind
{
    Auth,
    Account,
    Statistics,
    Notification
}

public enum FieldMode
{
    Exact,
    Timestamp,
    StringSet,
    EmptyBody,
    JsonNull,
    EmptyArray
}

public sealed record FieldExpect(string? Path, FieldMode Mode, string? Anchor = null);

public sealed record ContractCase
{
    public required string Id { get; init; }
    public required ServiceKind Service { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required IReadOnlyDictionary<AuthKind, int> Policy { get; init; }
    public Func<AuthKind, string?>? Json { get; init; }
    public IReadOnlyDictionary<string, string>? Form { get; init; }
    public required Func<AuthKind, IReadOnlyList<FieldExpect>> Fields { get; init; }

    public string Route => Method + " " + Path;
}

public static class ContractCatalog
{
    public const string AccountPutBody = """
        {
          "note": "updated",
          "saving": {"amount": 1500, "currency": "USD", "interest": 3.32, "deposit": true, "capitalization": false},
          "expenses": [{"title": "Grocery", "amount": 10, "currency": "USD", "period": "DAY", "icon": "meal"}],
          "incomes": [{"title": "Salary", "amount": 9100, "currency": "USD", "period": "MONTH", "icon": "wallet"}]
        }
        """;

    public const string StatisticsPutBody = """
        {
          "saving": {"amount": 1500, "currency": "USD", "interest": 3.32, "deposit": true, "capitalization": false},
          "expenses": [{"title": "Grocery", "amount": 10, "currency": "USD", "period": "DAY"}],
          "incomes": [{"title": "Salary", "amount": 9100, "currency": "USD", "period": "MONTH"}]
        }
        """;

    public const string RecipientPutBody = """
        {
          "email": "saved@piggymetrics.test",
          "scheduledNotifications": {
            "BACKUP": {"active": true, "frequency": "QUARTERLY", "lastNotified": null},
            "REMIND": {"active": false, "frequency": "MONTHLY", "lastNotified": null}
          }
        }
        """;

    private static readonly IReadOnlyDictionary<AuthKind, int> UserPolicy = new Dictionary<AuthKind, int>
    {
        [AuthKind.None] = 401,
        [AuthKind.User] = 200,
        [AuthKind.Server] = 200
    };

    private static readonly IReadOnlyDictionary<AuthKind, int> ServerPolicy = new Dictionary<AuthKind, int>
    {
        [AuthKind.None] = 401,
        [AuthKind.User] = 403,
        [AuthKind.Server] = 200
    };

    private static readonly IReadOnlyDictionary<AuthKind, int> AnonymousPolicy = new Dictionary<AuthKind, int>
    {
        [AuthKind.None] = 200,
        [AuthKind.User] = 200,
        [AuthKind.Server] = 200
    };

    private static readonly IReadOnlyDictionary<AuthKind, int> ClientCredentialsPolicy = new Dictionary<AuthKind, int>
    {
        [AuthKind.None] = 401,
        [AuthKind.User] = 401,
        [AuthKind.Server] = 401
    };

    private static readonly IReadOnlyDictionary<AuthKind, int> ServerOrDemoClosed = new Dictionary<AuthKind, int>
    {
        [AuthKind.None] = 401,
        [AuthKind.User] = 403,
        [AuthKind.Server] = 200
    };

    private static readonly IReadOnlyDictionary<AuthKind, int> ServerOrDemoOpen = new Dictionary<AuthKind, int>
    {
        [AuthKind.None] = 200,
        [AuthKind.User] = 200,
        [AuthKind.Server] = 200
    };

    public static IReadOnlyList<ContractCase> All { get; } = Build();

    private static List<ContractCase> Build()
    {
        return
        [
            new ContractCase
            {
                Id = "C1",
                Service = ServiceKind.Auth,
                Method = "GET",
                Path = "/uaa/users/current",
                Policy = UserPolicy,
                Fields = auth => auth switch
                {
                    AuthKind.User =>
                    [
                        Exact("name", "demo"),
                        Exact("username", "demo"),
                        Exact("authenticated", "true"),
                        Exact("clientOnly", "false"),
                        Exact("oauth2Request.clientId", "browser"),
                        new FieldExpect("oauth2Request.scope", FieldMode.StringSet, "ui")
                    ],
                    AuthKind.Server =>
                    [
                        Exact("name", "account-service"),
                        Exact("authenticated", "true"),
                        Exact("clientOnly", "true"),
                        Exact("oauth2Request.clientId", "account-service"),
                        new FieldExpect("oauth2Request.scope", FieldMode.StringSet, "server")
                    ],
                    _ => []
                }
            },
            new ContractCase
            {
                Id = "C2",
                Service = ServiceKind.Auth,
                Method = "POST",
                Path = "/uaa/users",
                Policy = ServerPolicy,
                Json = auth => UserJson(auth switch
                {
                    AuthKind.None => "c2none",
                    AuthKind.User => "c2user",
                    _ => "c2serv"
                }),
                Fields = auth => auth == AuthKind.Server ? [new FieldExpect(null, FieldMode.EmptyBody)] : []
            },
            new ContractCase
            {
                Id = "C3",
                Service = ServiceKind.Auth,
                Method = "POST",
                Path = "/uaa/oauth/token",
                Policy = ClientCredentialsPolicy,
                Form = new Dictionary<string, string> { ["grant_type"] = "client_credentials" },
                Fields = _ => [Exact("error", "invalid_client")]
            },
            new ContractCase
            {
                Id = "C4",
                Service = ServiceKind.Account,
                Method = "GET",
                Path = "/accounts/alice",
                Policy = ServerOrDemoClosed,
                Fields = auth => auth == AuthKind.Server
                    ? [Exact("name", "alice"), Exact("note", "alice"), Exact("saving.amount", "0"), Exact("saving.currency", "USD")]
                    : []
            },
            new ContractCase
            {
                Id = "C4",
                Service = ServiceKind.Account,
                Method = "GET",
                Path = "/accounts/demo",
                Policy = ServerOrDemoOpen,
                Fields = _ => DemoAccountFields()
            },
            new ContractCase
            {
                Id = "C5",
                Service = ServiceKind.Account,
                Method = "GET",
                Path = "/accounts/current",
                Policy = UserPolicy,
                Fields = auth => auth switch
                {
                    AuthKind.User => DemoAccountFields(),
                    AuthKind.Server =>
                    [
                        Exact("name", "account-service"),
                        Exact("note", "service"),
                        Exact("saving.amount", "0"),
                        Exact("saving.currency", "USD"),
                        Exact("saving.interest", "0"),
                        Exact("saving.deposit", "false"),
                        Exact("saving.capitalization", "false")
                    ],
                    _ => []
                }
            },
            new ContractCase
            {
                Id = "C6",
                Service = ServiceKind.Account,
                Method = "PUT",
                Path = "/accounts/current",
                Policy = UserPolicy,
                Json = _ => AccountPutBody,
                Fields = auth => auth == AuthKind.None ? [] : [new FieldExpect(null, FieldMode.EmptyBody)]
            },
            new ContractCase
            {
                Id = "C7",
                Service = ServiceKind.Account,
                Method = "POST",
                Path = "/accounts",
                Policy = AnonymousPolicy,
                Json = auth => UserJson(auth switch
                {
                    AuthKind.None => "c7none",
                    AuthKind.User => "c7user",
                    _ => "c7serv"
                }),
                Fields = auth =>
                {
                    var name = auth switch
                    {
                        AuthKind.None => "c7none",
                        AuthKind.User => "c7user",
                        _ => "c7serv"
                    };
                    return
                    [
                        Exact("name", name),
                        Exact("saving.amount", "0"),
                        Exact("saving.currency", "USD"),
                        Exact("saving.interest", "0"),
                        Exact("saving.deposit", "false"),
                        Exact("saving.capitalization", "false"),
                        new FieldExpect("lastSeen", FieldMode.Timestamp)
                    ];
                }
            },
            new ContractCase
            {
                Id = "C8",
                Service = ServiceKind.Statistics,
                Method = "GET",
                Path = "/statistics/current",
                Policy = UserPolicy,
                Fields = auth => auth switch
                {
                    AuthKind.User => DemoDataPointFields(),
                    AuthKind.Server => [new FieldExpect(null, FieldMode.EmptyArray)],
                    _ => []
                }
            },
            new ContractCase
            {
                Id = "C9",
                Service = ServiceKind.Statistics,
                Method = "GET",
                Path = "/statistics/alice",
                Policy = ServerOrDemoClosed,
                Fields = auth => auth == AuthKind.Server ? [new FieldExpect(null, FieldMode.EmptyArray)] : []
            },
            new ContractCase
            {
                Id = "C9",
                Service = ServiceKind.Statistics,
                Method = "GET",
                Path = "/statistics/demo",
                Policy = ServerOrDemoOpen,
                Fields = _ => DemoDataPointFields()
            },
            new ContractCase
            {
                Id = "C10",
                Service = ServiceKind.Statistics,
                Method = "PUT",
                Path = "/statistics/alice",
                Policy = ServerPolicy,
                Json = _ => StatisticsPutBody,
                Fields = auth => auth == AuthKind.Server ? [new FieldExpect(null, FieldMode.EmptyBody)] : []
            },
            new ContractCase
            {
                Id = "C11",
                Service = ServiceKind.Notification,
                Method = "GET",
                Path = "/notifications/recipients/current",
                Policy = UserPolicy,
                Fields = auth => auth switch
                {
                    AuthKind.User => DemoRecipientFields(),
                    AuthKind.Server => [new FieldExpect(null, FieldMode.JsonNull)],
                    _ => []
                }
            },
            new ContractCase
            {
                Id = "C12",
                Service = ServiceKind.Notification,
                Method = "PUT",
                Path = "/notifications/recipients/current",
                Policy = UserPolicy,
                Json = _ => RecipientPutBody,
                Fields = auth => auth switch
                {
                    AuthKind.User => SavedRecipientFields("demo"),
                    AuthKind.Server => SavedRecipientFields("notification-service"),
                    _ => []
                }
            }
        ];
    }

    private static string UserJson(string username) =>
        "{\"username\":\"" + username + "\",\"password\":\"password\"}";

    private static FieldExpect Exact(string path, string anchor) => new(path, FieldMode.Exact, anchor);

    private static IReadOnlyList<FieldExpect> DemoAccountFields() =>
    [
        Exact("name", "demo"),
        Exact("note", "demo note"),
        new FieldExpect("lastSeen", FieldMode.Exact),
        Exact("saving.amount", "5900"),
        Exact("saving.currency", "USD"),
        Exact("saving.interest", "3.32"),
        Exact("saving.deposit", "true"),
        Exact("saving.capitalization", "false"),
        Exact("incomes[0].title", "Salary"),
        Exact("incomes[0].amount", "42000"),
        Exact("expenses[0].title", "Rent"),
        Exact("expenses[0].amount", "1300")
    ];

    private static IReadOnlyList<FieldExpect> DemoDataPointFields() =>
    [
        Exact("[0].id.account", "demo"),
        Exact("[0].incomes[0].title", "salary"),
        Exact("[0].incomes[0].amount", "1"),
        Exact("[0].expenses[0].title", "grocery"),
        Exact("[0].expenses[0].amount", "2"),
        Exact("[0].statistics.SAVING_AMOUNT", "3"),
        Exact("[0].rates.USD", "1")
    ];

    private static IReadOnlyList<FieldExpect> DemoRecipientFields() =>
    [
        Exact("accountName", "demo"),
        Exact("email", "demo@piggymetrics.test"),
        Exact("scheduledNotifications.BACKUP.active", "false"),
        Exact("scheduledNotifications.BACKUP.frequency", "MONTHLY"),
        Exact("scheduledNotifications.REMIND.active", "true"),
        Exact("scheduledNotifications.REMIND.frequency", "WEEKLY")
    ];

    private static IReadOnlyList<FieldExpect> SavedRecipientFields(string accountName) =>
    [
        Exact("accountName", accountName),
        Exact("email", "saved@piggymetrics.test"),
        Exact("scheduledNotifications.BACKUP.active", "true"),
        Exact("scheduledNotifications.BACKUP.frequency", "QUARTERLY"),
        new FieldExpect("scheduledNotifications.BACKUP.lastNotified", FieldMode.Timestamp),
        Exact("scheduledNotifications.REMIND.active", "false"),
        Exact("scheduledNotifications.REMIND.frequency", "MONTHLY"),
        new FieldExpect("scheduledNotifications.REMIND.lastNotified", FieldMode.Timestamp)
    ];
}
