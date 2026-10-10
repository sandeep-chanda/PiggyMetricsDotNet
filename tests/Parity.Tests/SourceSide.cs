extern alias AccountApp;
extern alias NotificationApp;
extern alias StatisticsApp;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Account = AccountApp::PiggyMetrics.AccountService.Domain.Account;
using DataPoint = StatisticsApp::PiggyMetrics.StatisticsService.Domain.Timeseries.DataPoint;
using Recipient = NotificationApp::PiggyMetrics.NotificationService.Domain.Recipient;

namespace PiggyMetrics.Parity.Tests;

public sealed class SourceSide : IAsyncDisposable
{
    private static readonly string[] PrincipalKeys = ["user", "username", "userid", "user_id", "login", "id", "name"];

    private WebApplication? _app;
    private readonly HttpClient _tokenClient;
    private readonly HttpClient _authClient;
    private readonly HttpClient _statisticsClient;
    private readonly HttpClient _ratesClient;
    private readonly Dictionary<string, Account> _accounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<DataPoint>> _points = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Recipient> _recipients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _users = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AuthSession> _tokens = new(StringComparer.Ordinal);

    private SourceSide(HttpClient tokenClient, HttpClient authClient, HttpClient statisticsClient, HttpClient ratesClient)
    {
        _tokenClient = tokenClient;
        _authClient = authClient;
        _statisticsClient = statisticsClient;
        _ratesClient = ratesClient;
        ResetSeed();
    }

    public HttpClient Client { get; private set; } = null!;

    public string? ServerAccess { get; private set; }

    public string? UserAccess { get; private set; }

    public static async Task<SourceSide> StartAsync()
    {
        var token = ExternalStubCatalog.Handler(ExternalStubCatalog.Token);
        var auth = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        var statistics = ExternalStubCatalog.Handler(ExternalStubCatalog.EmptyOk);
        var rates = ExternalStubCatalog.Handler(ExternalStubCatalog.Rates);
        var side = new SourceSide(
            new HttpClient(token, disposeHandler: false),
            new HttpClient(auth, disposeHandler: false),
            new HttpClient(statistics, disposeHandler: false),
            new HttpClient(rates, disposeHandler: false));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        side.Map(app);
        await app.StartAsync();
        side._app = app;
        side.Client = app.GetTestClient();
        return side;
    }

    public void ResetSeed()
    {
        _accounts.Clear();
        _points.Clear();
        _recipients.Clear();
        _users.Clear();
        _tokens.Clear();
        ServerAccess = null;
        UserAccess = null;

        var demo = SeedData.DemoAccount();
        _accounts[demo.Name!] = demo;
        _accounts["alice"] = SeedData.NamedAccount("alice", "alice");
        _accounts["account-service"] = SeedData.NamedAccount("account-service", "service");
        _points["demo"] = [SeedData.DemoDataPoint()];
        var recipient = SeedData.DemoRecipient();
        _recipients[recipient.AccountName!] = recipient;
        _users["demo"] = "secret";
    }

    public async Task IssueSeedTokensAsync()
    {
        ServerAccess = await IssueAsync("account-service", "account-secret", new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "server"
        });
        UserAccess = await IssueAsync("browser", string.Empty, new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = "demo",
            ["password"] = "secret",
            ["scope"] = "ui"
        });
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        _tokenClient.Dispose();
        _authClient.Dispose();
        _statisticsClient.Dispose();
        _ratesClient.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/uaa/users/current", CurrentUser);
        endpoints.MapPost("/uaa/users", CreateUser);
        endpoints.MapPost("/uaa/oauth/token", Token);
        endpoints.MapGet("/accounts/current", CurrentAccount);
        endpoints.MapPut("/accounts/current", SaveAccount);
        endpoints.MapGet("/accounts/{name}", AccountByName);
        endpoints.MapPost("/accounts", CreateAccount);
        endpoints.MapGet("/statistics/current", CurrentStatistics);
        endpoints.MapGet("/statistics/{accountName}", StatisticsByName);
        endpoints.MapPut("/statistics/{accountName}", SaveStatistics);
        endpoints.MapGet("/notifications/recipients/current", CurrentRecipient);
        endpoints.MapPut("/notifications/recipients/current", SaveRecipient);
    }

    private async Task CurrentUser(HttpContext context)
    {
        var session = AuthSessionFrom(context.Request);
        if (session is null)
        {
            await Write(context, StatusCodes.Status401Unauthorized, null);
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["name"] = session.Name,
            ["authenticated"] = true,
            ["clientOnly"] = string.IsNullOrEmpty(session.Username),
            ["oauth2Request"] = new Dictionary<string, object?>
            {
                ["clientId"] = session.ClientId,
                ["scope"] = session.Scopes
            }
        };
        if (!string.IsNullOrEmpty(session.Username))
        {
            payload["username"] = session.Username;
        }

        await Write(context, StatusCodes.Status200OK, JsonSerializer.Serialize(payload));
    }

    private async Task CreateUser(HttpContext context)
    {
        var session = AuthSessionFrom(context.Request);
        if (session is null)
        {
            await Write(context, StatusCodes.Status401Unauthorized, null);
            return;
        }

        if (!session.Scopes.Contains("server"))
        {
            await Write(context, StatusCodes.Status403Forbidden, null);
            return;
        }

        using var document = await JsonDocument.ParseAsync(context.Request.Body);
        var username = document.RootElement.GetProperty("username").GetString() ?? string.Empty;
        var password = document.RootElement.GetProperty("password").GetString() ?? string.Empty;
        _users[username] = password;
        await Write(context, StatusCodes.Status200OK, null);
    }

    private async Task Token(HttpContext context)
    {
        var form = await context.Request.ReadFormAsync();
        if (!TryReadBasic(context.Request, out var clientId, out var secret))
        {
            await Write(context, StatusCodes.Status401Unauthorized,
                "{\"error\":\"invalid_client\",\"error_description\":\"Bad client credentials\"}");
            return;
        }

        if (!ClientAllows(clientId, secret, form["grant_type"].ToString(), out var scopes, out var username))
        {
            var status = form["grant_type"].ToString() == "password" ? StatusCodes.Status400BadRequest : StatusCodes.Status401Unauthorized;
            var error = status == StatusCodes.Status400BadRequest ? "invalid_grant" : "invalid_client";
            await Write(context, status, "{\"error\":\"" + error + "\",\"error_description\":\"rejected\"}");
            return;
        }

        if (form["grant_type"].ToString() == "password")
        {
            username = form["username"].ToString();
            if (!_users.TryGetValue(username, out var password) || password != form["password"].ToString())
            {
                await Write(context, StatusCodes.Status400BadRequest, "{\"error\":\"invalid_grant\",\"error_description\":\"Bad credentials\"}");
                return;
            }
        }

        var access = Guid.NewGuid().ToString("N");
        _tokens[access] = new AuthSession(access, clientId, username, scopes);
        await Write(context, StatusCodes.Status200OK,
            "{\"access_token\":\"" + access + "\",\"token_type\":\"bearer\",\"expires_in\":3600,\"scope\":\"" + string.Join(' ', scopes) + "\",\"refresh_token\":\"" + Guid.NewGuid().ToString("N") + "\"}");
    }

    private Task AccountByName(HttpContext context)
    {
        var name = context.Request.RouteValues["name"]?.ToString() ?? string.Empty;
        var principal = ResourcePrincipalFrom(context.Request, "account-service");
        var denied = Authorize(demoName: name, principal, requireServer: true);
        if (denied is not null)
        {
            return Write(context, denied.Value, null);
        }

        return _accounts.TryGetValue(name, out var account)
            ? Write(context, StatusCodes.Status200OK, SeedData.Write(account))
            : Write(context, StatusCodes.Status200OK, "null");
    }

    private Task CurrentAccount(HttpContext context)
    {
        var principal = ResourcePrincipalFrom(context.Request, "account-service");
        if (principal is null)
        {
            return Write(context, StatusCodes.Status401Unauthorized, null);
        }

        return _accounts.TryGetValue(principal.Name, out var account)
            ? Write(context, StatusCodes.Status200OK, SeedData.Write(account))
            : Write(context, StatusCodes.Status200OK, "null");
    }

    private async Task SaveAccount(HttpContext context)
    {
        var principal = ResourcePrincipalFrom(context.Request, "account-service");
        if (principal is null)
        {
            await Write(context, StatusCodes.Status401Unauthorized, null);
            return;
        }

        using var document = await JsonDocument.ParseAsync(context.Request.Body);
        if (_accounts.TryGetValue(principal.Name, out var account))
        {
            account.Note = document.RootElement.TryGetProperty("note", out var note) ? note.GetString() : account.Note;
            account.LastSeen = DateTimeOffset.UtcNow;
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, "http://statistics-service:7000/statistics/" + Uri.EscapeDataString(principal.Name))
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        using var response = await _statisticsClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        await Write(context, StatusCodes.Status200OK, null);
    }

    private async Task CreateAccount(HttpContext context)
    {
        using var document = await JsonDocument.ParseAsync(context.Request.Body);
        var username = document.RootElement.GetProperty("username").GetString() ?? string.Empty;
        using (var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "http://auth-service:5000/uaa/oauth/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = "server"
            })
        })
        using (var tokenResponse = await _tokenClient.SendAsync(tokenRequest))
        {
            tokenResponse.EnsureSuccessStatusCode();
        }

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "http://auth-service:5000/uaa/users")
        {
            Content = new StringContent(document.RootElement.GetRawText(), Encoding.UTF8, "application/json")
        };
        using var createResponse = await _authClient.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        var created = SeedData.CreatedAccount(username);
        _accounts[username] = created;
        await Write(context, StatusCodes.Status200OK, SeedData.Write(created));
    }

    private Task CurrentStatistics(HttpContext context)
    {
        var principal = ResourcePrincipalFrom(context.Request, "statistics-service");
        if (principal is null)
        {
            return Write(context, StatusCodes.Status401Unauthorized, null);
        }

        return Write(context, StatusCodes.Status200OK, PointsJson(principal.Name));
    }

    private Task StatisticsByName(HttpContext context)
    {
        var name = context.Request.RouteValues["accountName"]?.ToString() ?? string.Empty;
        var principal = ResourcePrincipalFrom(context.Request, "statistics-service");
        var denied = Authorize(name, principal, requireServer: true);
        if (denied is not null)
        {
            return Write(context, denied.Value, null);
        }

        return Write(context, StatusCodes.Status200OK, PointsJson(name));
    }

    private async Task SaveStatistics(HttpContext context)
    {
        var principal = ResourcePrincipalFrom(context.Request, "statistics-service");
        if (principal is null)
        {
            await Write(context, StatusCodes.Status401Unauthorized, null);
            return;
        }

        if (!principal.Scopes.Contains("server"))
        {
            await Write(context, StatusCodes.Status403Forbidden, null);
            return;
        }

        using var response = await _ratesClient.GetAsync("https://api.exchangeratesapi.io/latest?base=USD");
        response.EnsureSuccessStatusCode();
        await Write(context, StatusCodes.Status200OK, null);
    }

    private Task CurrentRecipient(HttpContext context)
    {
        var principal = ResourcePrincipalFrom(context.Request, "notification-service");
        if (principal is null)
        {
            return Write(context, StatusCodes.Status401Unauthorized, null);
        }

        if (!_recipients.TryGetValue(principal.Name, out var recipient))
        {
            return Write(context, StatusCodes.Status200OK, "null");
        }

        return Write(context, StatusCodes.Status200OK, SeedData.Write(recipient));
    }

    private async Task SaveRecipient(HttpContext context)
    {
        var principal = ResourcePrincipalFrom(context.Request, "notification-service");
        if (principal is null)
        {
            await Write(context, StatusCodes.Status401Unauthorized, null);
            return;
        }

        using var reader = new StreamReader(context.Request.Body);
        var json = await reader.ReadToEndAsync();
        var recipient = JsonSerializer.Deserialize<Recipient>(json, SeedData.Json)
            ?? throw new InvalidOperationException("recipient missing");
        recipient.AccountName = principal.Name;
        if (recipient.ScheduledNotifications is not null)
        {
            foreach (var settings in recipient.ScheduledNotifications.Values)
            {
                if (settings.LastNotified is null)
                {
                    settings.LastNotified = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                }
            }
        }

        _recipients[principal.Name] = recipient;
        await Write(context, StatusCodes.Status200OK, SeedData.Write(recipient));
    }

    private string PointsJson(string account)
    {
        if (!_points.TryGetValue(account, out var points))
        {
            return "[]";
        }

        return SeedData.Write(points);
    }

    private async Task<string> IssueAsync(string clientId, string secret, Dictionary<string, string> form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/uaa/oauth/token")
        {
            Content = new FormUrlEncodedContent(form)
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basic);
        using var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException("source token failed " + (int)response.StatusCode + " " + body);
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("source access_token missing");
    }

    private AuthSession? AuthSessionFrom(HttpRequest request)
    {
        var token = Bearer(request);
        if (token is null)
        {
            return null;
        }

        return _tokens.TryGetValue(token, out var session) ? session : null;
    }

    private static ResourcePrincipal? ResourcePrincipalFrom(HttpRequest request, string serviceName)
    {
        var token = Bearer(request);
        if (token is null)
        {
            return null;
        }

        var (status, body) = ExternalStubCatalog.UserInfoBody(serviceName, token);
        if (status != HttpStatusCode.OK)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out _))
        {
            return null;
        }

        var clientId = serviceName;
        var scopes = new List<string>();
        if (root.TryGetProperty("oauth2Request", out var oauth) && oauth.ValueKind == JsonValueKind.Object)
        {
            if (oauth.TryGetProperty("clientId", out var client) && client.GetString() is { } id)
            {
                clientId = id;
            }

            if (oauth.TryGetProperty("scope", out var scopeElement) && scopeElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var scope in scopeElement.EnumerateArray())
                {
                    if (scope.GetString() is { } value)
                    {
                        scopes.Add(value);
                    }
                }
            }
        }

        return new ResourcePrincipal(ResolvePrincipal(root), clientId, scopes);
    }

    private static int? Authorize(string demoName, ResourcePrincipal? principal, bool requireServer)
    {
        if (string.Equals(demoName, "demo", StringComparison.Ordinal))
        {
            return null;
        }

        if (principal is null)
        {
            return StatusCodes.Status401Unauthorized;
        }

        if (requireServer && !principal.Scopes.Contains("server"))
        {
            return StatusCodes.Status403Forbidden;
        }

        return null;
    }

    private static string ResolvePrincipal(JsonElement map)
    {
        foreach (var key in PrincipalKeys)
        {
            if (map.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.String && element.GetString() is { Length: > 0 } value)
            {
                return value;
            }
        }

        return "unknown";
    }

    private static string? Bearer(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Authorization", out var values))
        {
            return null;
        }

        var header = values.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(token) ? null : token;
    }

    private static bool TryReadBasic(HttpRequest request, out string clientId, out string secret)
    {
        clientId = string.Empty;
        secret = string.Empty;
        if (!request.Headers.TryGetValue("Authorization", out var values))
        {
            return false;
        }

        var header = values.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
        var separator = decoded.IndexOf(':');
        if (separator < 0)
        {
            clientId = decoded;
            return true;
        }

        clientId = decoded[..separator];
        secret = decoded[(separator + 1)..];
        return true;
    }

    private bool ClientAllows(string clientId, string secret, string grantType, out string[] scopes, out string? username)
    {
        scopes = [];
        username = null;
        if (clientId == "browser" && secret.Length == 0 && grantType == "password")
        {
            scopes = ["ui"];
            return true;
        }

        if (clientId == "account-service" && secret == "account-secret" && grantType == "client_credentials")
        {
            scopes = ["server"];
            return true;
        }

        return false;
    }

    private static Task Write(HttpContext context, int status, string? body)
    {
        context.Response.StatusCode = status;
        if (string.IsNullOrEmpty(body))
        {
            return Task.CompletedTask;
        }

        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(body);
    }

    private sealed record AuthSession(string Access, string ClientId, string? Username, string[] Scopes)
    {
        public string Name => string.IsNullOrEmpty(Username) ? ClientId : Username!;
    }

    private sealed record ResourcePrincipal(string Name, string ClientId, List<string> Scopes);
}
