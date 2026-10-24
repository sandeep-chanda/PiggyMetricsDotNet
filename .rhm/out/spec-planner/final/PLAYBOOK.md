---
name: Walk 24 Oct migration P1 Shared skeleton
overview: "Greenfield net8.0 PiggyMetrics.sln with Shared bearer user-info validation,\
  \ MongoDB conventions, typed-client timeout/cache, health/JSON \u2014 verified by\
  \ dotnet test."
todos:
- id: step-001
  content: "STEP-001 \xB7 P1.T1 Create PiggyMetrics.sln with Shared and five service\
    \ projects"
  status: pending
- id: step-002
  content: "STEP-002 \xB7 P1.T2 Shared UserInfoBearerHandler validating via auth-service\
    \ user-info"
  status: pending
- id: step-003
  content: "STEP-003 \xB7 P1.T3 Shared MongoDB store conventions and document serializers"
  status: pending
- id: step-004
  content: "STEP-004 \xB7 P1.T4 Shared typed-client base with 10s timeout and client-credentials\
    \ cache"
  status: pending
- id: step-005
  content: "STEP-005 \xB7 P1.T5 Shared health checks and JSON options"
  status: pending
- id: step-006
  content: "STEP-006 \xB7 P1 Done-when verify full solution build and bearer tests"
  status: pending
isProject: false
---

# Walk 24 Oct migration — P1 Solution skeleton and Shared
Plan Mode package for **PiggyMetricsDotNet**. Target product tree starts empty; Java evidence from sibling `sandeep-chanda/piggymetricsJava` (HTTPS evidence only; no second clone). Execute STEPs literally. Commit messages name todo ids `P1.T1`…`P1.T5`.
## Decisions
- **D-001** net8.0 multi-project solution (Shared + five services + Shared.Tests).
- **D-002** `UserInfoBearerHandler` remote user-info validation (not local JWT).
- **D-003** MongoDB.Driver camelCase conventions + document `_id` semantics.
- **D-004** Typed client base: Polly **10000ms** + client_credentials token cache.
- **D-005** System.Text.Json enum names, ignore unknown, ISO-8601 dates; `/health`.
## Constraint
Gate floor: strongest acceptance is `dotnet test` (not `test-compile`). Snippets already declare every PackageReference coordinate the tests need. Do not lower acceptance. Do not ban csproj/Dockerfile updates later plans need for net8 runtime images.
## STEP-001 — P1.T1 Create PiggyMetrics.sln with Shared and five service projects
Depends on: (none).
Why: P1.T1 requires the .NET solution every later service plan builds on, mirrored from Java module list
Files:
- `global.json`
- `Directory.Build.props`
- `PiggyMetrics.sln`
- `Shared/Shared.csproj`
- `Gateway/Gateway.csproj`
- `AuthService/AuthService.csproj`
- `AccountService/AccountService.csproj`
- `StatisticsService/StatisticsService.csproj`
- `NotificationService/NotificationService.csproj`
- `Gateway/Program.cs`
- `AuthService/Program.cs`
- `AccountService/Program.cs`
- `StatisticsService/Program.cs`
- `NotificationService/Program.cs`
- `Shared.Tests/Shared.Tests.csproj`
Do not:
- Add Java config/registry/monitoring/turbine projects
- Use TFM other than net8.0
- Omit Shared.Tests project or test PackageReferences
- Lower acceptance to test-compile or skip tests
exact_commands:

~~~bash
dotnet new sln -n PiggyMetrics -o . --force
dotnet new classlib -n Shared -o Shared -f net8.0 --force
dotnet new web -n Gateway -o Gateway -f net8.0 --no-https --force
dotnet new web -n AuthService -o AuthService -f net8.0 --no-https --force
dotnet new web -n AccountService -o AccountService -f net8.0 --no-https --force
dotnet new web -n StatisticsService -o StatisticsService -f net8.0 --no-https --force
dotnet new web -n NotificationService -o NotificationService -f net8.0 --no-https --force
dotnet new xunit -n Shared.Tests -o Shared.Tests -f net8.0 --force
dotnet sln PiggyMetrics.sln add Shared/Shared.csproj Gateway/Gateway.csproj AuthService/AuthService.csproj AccountService/AccountService.csproj StatisticsService/StatisticsService.csproj NotificationService/NotificationService.csproj Shared.Tests/Shared.Tests.csproj
dotnet add Gateway/Gateway.csproj reference Shared/Shared.csproj
dotnet add AuthService/AuthService.csproj reference Shared/Shared.csproj
dotnet add AccountService/AccountService.csproj reference Shared/Shared.csproj
dotnet add StatisticsService/StatisticsService.csproj reference Shared/Shared.csproj
dotnet add NotificationService/NotificationService.csproj reference Shared/Shared.csproj
dotnet add Shared.Tests/Shared.Tests.csproj reference Shared/Shared.csproj
~~~
exact_snippet (`global.json`):

~~~json
{
  "sdk": {
  "version": "8.0.400",
  "rollForward": "latestFeature"
  }
}
~~~
exact_companion (multi-file bundle for STEP-001):

~~~csharp
FILE Directory.Build.props
<Project>
  <PropertyGroup>
  <LangVersion>12.0</LangVersion>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>

FILE Shared/Shared.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <RootNamespace>PiggyMetrics.Shared</RootNamespace>
  <AssemblyName>PiggyMetrics.Shared</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
  <PackageReference Include="MongoDB.Driver" Version="2.28.0" />
  <PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.8" />
  <PackageReference Include="Polly.Extensions.Http" Version="3.0.0" />
  </ItemGroup>
</Project>

FILE Gateway/Gateway.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <RootNamespace>PiggyMetrics.Gateway</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />
  </ItemGroup>
</Project>

FILE AuthService/AuthService.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <RootNamespace>PiggyMetrics.AuthService</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />
  </ItemGroup>
</Project>

FILE AccountService/AccountService.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <RootNamespace>PiggyMetrics.AccountService</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />
  </ItemGroup>
</Project>

FILE StatisticsService/StatisticsService.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <RootNamespace>PiggyMetrics.StatisticsService</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />
  </ItemGroup>
</Project>

FILE NotificationService/NotificationService.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <RootNamespace>PiggyMetrics.NotificationService</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />
  </ItemGroup>
</Project>

FILE Gateway/Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { service = app.Environment.ApplicationName }));
app.Run();

FILE AuthService/Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { service = app.Environment.ApplicationName }));
app.Run();

FILE AccountService/Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { service = app.Environment.ApplicationName }));
app.Run();

FILE StatisticsService/Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { service = app.Environment.ApplicationName }));
app.Run();

FILE NotificationService/Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { service = app.Environment.ApplicationName }));
app.Run();

FILE Shared.Tests/Shared.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <IsPackable>false</IsPackable>
  <RootNamespace>PiggyMetrics.Shared.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  <PackageReference Include="Microsoft.AspNetCore.TestHost" Version="8.0.8" />
  <PackageReference Include="Moq" Version="4.20.72" />
  </ItemGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />
  </ItemGroup>
</Project>
~~~
Verify:

~~~bash
dotnet build PiggyMetrics.sln
~~~
Acceptance:
- PiggyMetrics.sln lists Shared, Gateway, AuthService, AccountService, StatisticsService, NotificationService, Shared.Tests
- dotnet build PiggyMetrics.sln succeeds
## STEP-002 — P1.T2 Shared UserInfoBearerHandler validating via auth-service user-info
Depends on: STEP-001.
Why: P1.T2 and Done-when require Shared bearer to accept authorization-server-issued tokens and refuse others
Files:
- `Shared/Security/UserInfoBearerOptions.cs`
- `Shared/Security/UserInfoBearerHandler.cs`
- `Shared/Security/UserInfoBearerExtensions.cs`
- `Shared.Tests/Security/UserInfoBearerHandlerTests.cs`
Do not:
- Validate JWT signatures locally instead of user-info
- Change UserInfoEndpoint default away from http://auth-service:5000/uaa/users/current
- Skip refuse-path test
exact_commands:

~~~bash
dotnet test Shared.Tests/Shared.Tests.csproj --filter FullyQualifiedName~UserInfoBearerHandlerTests
~~~
exact_snippet (`Shared/Security/UserInfoBearerOptions.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Security;

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class UserInfoBearerHandler : AuthenticationHandler<UserInfoBearerOptions>
{
  private static readonly string[] PrincipalKeys =
  {
  "user", "username", "userid", "user_id", "login", "id", "name"
  };

  private readonly IHttpClientFactory _httpClientFactory;

  public UserInfoBearerHandler(
  IOptionsMonitor<UserInfoBearerOptions> options,
  ILoggerFactory logger,
  System.Text.Encodings.Web.UrlEncoder encoder,
  IHttpClientFactory httpClientFactory)
  : base(options, logger, encoder)
  {
  _httpClientFactory = httpClientFactory;
  }

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
  if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
  {
  return AuthenticateResult.NoResult();
  }

  var header = headerValues.ToString();
  if (string.IsNullOrWhiteSpace(header) ||
  !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
  {
  return AuthenticateResult.NoResult();
  }

  var accessToken = header["Bearer ".Length..].Trim();
  if (string.IsNullOrEmpty(accessToken))
  {
  return AuthenticateResult.Fail("Missing bearer token");
  }

  try
  {
  var client = _httpClientFactory.CreateClient("userinfo");
  using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInfoEndpoint);
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
  using var response = await client.SendAsync(request, Context.RequestAborted);
  var body = await response.Content.ReadAsStringAsync(Context.RequestAborted);
  using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
  var root = doc.RootElement;
  if (!response.IsSuccessStatusCode || root.TryGetProperty("error", out _))
  {
  return AuthenticateResult.Fail("Token rejected by authorization server");
  }

  var principalName = "unknown";
  foreach (var key in PrincipalKeys)
  {
  if (root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
  {
  principalName = p.GetString() ?? "unknown";
  break;
  }
  }

  var claims = new List<Claim> { new Claim(ClaimTypes.Name, principalName) };
  if (root.TryGetProperty("oauth2Request", out var oauth2) &&
  oauth2.ValueKind == JsonValueKind.Object)
  {
  if (oauth2.TryGetProperty("clientId", out var clientId) &&
  clientId.ValueKind == JsonValueKind.String)
  {
  claims.Add(new Claim("client_id", clientId.GetString() ?? string.Empty));
  }
  if (oauth2.TryGetProperty("scope", out var scopeEl))
  {
  if (scopeEl.ValueKind == JsonValueKind.Array)
  {
  foreach (var s in scopeEl.EnumerateArray())
  {
  if (s.ValueKind == JsonValueKind.String)
  {
  claims.Add(new Claim("scope", s.GetString() ?? string.Empty));
  }
  }
  }
  else if (scopeEl.ValueKind == JsonValueKind.String)
  {
  foreach (var s in (scopeEl.GetString() ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
  {
  claims.Add(new Claim("scope", s));
  }
  }
  }
  }

  var identity = new ClaimsIdentity(claims, Scheme.Name);
  var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
  return AuthenticateResult.Success(ticket);
  }
  catch (Exception ex)
  {
  Logger.LogInformation(ex, "Could not fetch user details from {Endpoint}", Options.UserInfoEndpoint);
  return AuthenticateResult.Fail("Could not fetch user details");
  }
  }
}
~~~
exact_companion (multi-file bundle for STEP-002):

~~~csharp
FILE Shared/Security/UserInfoBearerOptions.cs
namespace PiggyMetrics.Shared.Security;

public sealed class UserInfoBearerOptions
{
  public const string DefaultScheme = "Bearer";
  public string UserInfoEndpoint { get; set; } = "http://auth-service:5000/uaa/users/current";
  public string ChallengeScheme { get; set; } = DefaultScheme;
}

FILE Shared/Security/UserInfoBearerExtensions.cs
namespace PiggyMetrics.Shared.Security;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

public static class UserInfoBearerExtensions
{
  public static AuthenticationBuilder AddUserInfoBearer(
  this AuthenticationBuilder builder,
  Action<UserInfoBearerOptions>? configure = null)
  {
  builder.Services.AddHttpClient("userinfo");
  return builder.AddScheme<UserInfoBearerOptions, UserInfoBearerHandler>(
  UserInfoBearerOptions.DefaultScheme,
  configure);
  }
}

FILE Shared.Tests/Security/UserInfoBearerHandlerTests.cs
namespace PiggyMetrics.Shared.Tests.Security;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Security;
using Xunit;

public sealed class UserInfoBearerHandlerTests
{
  [Fact]
  public async Task Accepts_token_authorization_server_issued()
  {
  var userInfo = JsonSerializer.Serialize(new
  {
  name = "demo",
  oauth2Request = new { clientId = "browser", scope = new[] { "ui" } }
  });
  using var host = CreateHost(_ => new HttpResponseMessage(HttpStatusCode.OK)
  {
  Content = new StringContent(userInfo, Encoding.UTF8, "application/json")
  });
  var client = host.GetTestClient();
  client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "issued-token");
  var response = await client.GetAsync("/secure");
  Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  Assert.Equal("demo", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task Refuses_token_authorization_server_did_not_issue()
  {
  using var host = CreateHost(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
  {
  Content = new StringContent("{"error":"invalid_token"}", Encoding.UTF8, "application/json")
  });
  var client = host.GetTestClient();
  client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "forged-token");
  var response = await client.GetAsync("/secure");
  Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  private static TestServer CreateHost(Func<HttpRequestMessage, HttpResponseMessage> userInfo)
  {
  var builder = new WebHostBuilder()
  .ConfigureServices(services =>
  {
  services.AddRouting();
  services.AddAuthentication(UserInfoBearerOptions.DefaultScheme)
  .AddUserInfoBearer(o => o.UserInfoEndpoint = "http://auth-service:5000/uaa/users/current");
  services.AddAuthorization();
  services.AddHttpClient("userinfo")
  .ConfigurePrimaryHttpMessageHandler(() => new StubHandler(userInfo));
  })
  .Configure(app =>
  {
  app.UseRouting();
  app.UseAuthentication();
  app.UseAuthorization();
  app.UseEndpoints(endpoints =>
  {
  endpoints.MapGet("/secure", async (HttpContext ctx) =>
  {
  var result = await ctx.AuthenticateAsync(UserInfoBearerOptions.DefaultScheme);
  if (!result.Succeeded)
  {
  ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
  return;
  }
  await ctx.Response.WriteAsync(result.Principal!.Identity!.Name ?? string.Empty);
  });
  });
  });
  return new TestServer(builder);
  }

  private sealed class StubHandler : HttpMessageHandler
  {
  private readonly Func<HttpRequestMessage, HttpResponseMessage> _impl;
  public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> impl) => _impl = impl;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
  => Task.FromResult(_impl(request));
  }
}
~~~
Verify:

~~~bash
dotnet test Shared.Tests/Shared.Tests.csproj --filter FullyQualifiedName~UserInfoBearerHandlerTests
~~~
Acceptance:
- dotnet test --filter UserInfoBearerHandlerTests succeeds
- Accept test returns 200 with principal name demo for issued token stub
- Refuse test returns 401 for forged token stub
## STEP-003 — P1.T3 Shared MongoDB store conventions and document serializers
Depends on: STEP-001.
Why: P1.T3 requires Shared store conventions for document field names and ids on MongoDB.Driver
Files:
- `Shared/Store/PiggyMongoConventions.cs`
- `Shared/Store/AccountDocument.cs`
- `Shared/Store/DataPointDocument.cs`
- `Shared/Store/UserDocument.cs`
Do not:
- Rename BSON fields to PascalCase in storage
- Use collection names other than accounts, datapoints, users
- Introduce EF Core Mongo provider
exact_commands:

~~~bash
dotnet build Shared/Shared.csproj
~~~
exact_snippet (`Shared/Store/PiggyMongoConventions.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Store;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;

public static class PiggyMongoConventions
{
  public const string RegistryName = "piggymetrics";
  public const string AccountsCollection = "accounts";
  public const string DatapointsCollection = "datapoints";
  public const string UsersCollection = "users";
  public const string DatabaseName = "piggymetrics";

  private static int _registered;

  public static void Register()
  {
  if (Interlocked.Exchange(ref _registered, 1) == 1)
  {
  return;
  }

  var pack = new ConventionPack
  {
  new CamelCaseElementNameConvention(),
  new IgnoreExtraElementsConvention(true),
  new EnumRepresentationConvention(BsonType.String),
  };
  ConventionRegistry.Register(RegistryName, pack, _ => true);
  }
}
~~~
exact_companion (multi-file bundle for STEP-003):

~~~csharp
FILE Shared/Store/AccountDocument.cs
namespace PiggyMetrics.Shared.Store;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class AccountDocument
{
  [BsonId]
  public string Name { get; set; } = string.Empty;

  [BsonElement("lastSeen")]
  public DateTime? LastSeen { get; set; }

  [BsonElement("incomes")]
  public List<ItemDocument> Incomes { get; set; } = new();

  [BsonElement("expenses")]
  public List<ItemDocument> Expenses { get; set; } = new();

  [BsonElement("saving")]
  public SavingDocument Saving { get; set; } = new();

  [BsonElement("note")]
  public string? Note { get; set; }
}

public sealed class ItemDocument
{
  [BsonElement("title")]
  public string Title { get; set; } = string.Empty;

  [BsonElement("amount")]
  public decimal Amount { get; set; }

  [BsonElement("currency")]
  public string Currency { get; set; } = "USD";

  [BsonElement("period")]
  public string Period { get; set; } = "MONTH";

  [BsonElement("icon")]
  public string Icon { get; set; } = string.Empty;
}

public sealed class SavingDocument
{
  [BsonElement("amount")]
  public decimal Amount { get; set; }

  [BsonElement("currency")]
  public string Currency { get; set; } = "USD";

  [BsonElement("interest")]
  public decimal Interest { get; set; }

  [BsonElement("deposit")]
  public bool Deposit { get; set; }

  [BsonElement("capitalization")]
  public bool Capitalization { get; set; }
}

FILE Shared/Store/DataPointDocument.cs
namespace PiggyMetrics.Shared.Store;

using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class DataPointDocument
{
  [BsonId]
  public DataPointIdDocument Id { get; set; } = new();

  [BsonElement("incomes")]
  public HashSet<ItemMetricDocument> Incomes { get; set; } = new();

  [BsonElement("expenses")]
  public HashSet<ItemMetricDocument> Expenses { get; set; } = new();

  [BsonElement("statistics")]
  public Dictionary<string, decimal> Statistics { get; set; } = new();

  [BsonElement("rates")]
  public Dictionary<string, decimal> Rates { get; set; } = new();
}

public sealed class DataPointIdDocument
{
  [BsonElement("account")]
  public string Account { get; set; } = string.Empty;

  [BsonElement("date")]
  public DateTime Date { get; set; }
}

public sealed class ItemMetricDocument
{
  [BsonElement("title")]
  public string Title { get; set; } = string.Empty;

  [BsonElement("amount")]
  public decimal Amount { get; set; }
}

FILE Shared/Store/UserDocument.cs
namespace PiggyMetrics.Shared.Store;

using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public sealed class UserDocument
{
  // Java User @Id username => Mongo _id value is the username (field name username in domain)
  [BsonId]
  public string Username { get; set; } = string.Empty;

  [BsonElement("password")]
  public string Password { get; set; } = string.Empty;
}
~~~
Verify:

~~~bash
dotnet build Shared/Shared.csproj
~~~
Acceptance:
- PiggyMongoConventions.AccountsCollection == accounts
- AccountDocument.Name is BsonId
- DataPointDocument.Id is DataPointIdDocument with account+date elements
- UserDocument uses username as BsonId
## STEP-004 — P1.T4 Shared typed-client base with 10s timeout and client-credentials cache
Depends on: STEP-001.
Why: P1.T4 requires typed-client base with source timeout and client-credentials token cache
Files:
- `Shared/Http/TypedClientBase.cs`
- `Shared/Http/TypedClientServiceCollectionExtensions.cs`
Do not:
- Change default timeout away from 10000
- Use password grant inside the typed-client base
- Omit token cache
exact_commands:

~~~bash
dotnet build Shared/Shared.csproj
~~~
exact_snippet (`Shared/Http/TypedClientBase.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Http;

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public sealed class ClientCredentialsOptions
{
  public string AccessTokenUri { get; set; } = "http://auth-service:5000/uaa/oauth/token";
  public string ClientId { get; set; } = string.Empty;
  public string ClientSecret { get; set; } = string.Empty;
  public string Scope { get; set; } = "server";
  public int TimeoutMilliseconds { get; set; } = 10000;
}

public interface IClientCredentialsTokenCache
{
  Task<string> GetTokenAsync(ClientCredentialsOptions options, CancellationToken cancellationToken);
}

public sealed class ClientCredentialsTokenCache : IClientCredentialsTokenCache
{
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Expires)> _cache = new();

  public ClientCredentialsTokenCache(IHttpClientFactory httpClientFactory)
  {
  _httpClientFactory = httpClientFactory;
  }

  public async Task<string> GetTokenAsync(ClientCredentialsOptions options, CancellationToken cancellationToken)
  {
  var key = options.ClientId + "|" + options.Scope;
  if (_cache.TryGetValue(key, out var hit) && hit.Expires > DateTimeOffset.UtcNow.AddSeconds(30))
  {
  return hit.Token;
  }

  var client = _httpClientFactory.CreateClient("client-credentials");
  using var content = new FormUrlEncodedContent(new Dictionary<string, string>
  {
  ["grant_type"] = "client_credentials",
  ["scope"] = options.Scope,
  });
  using var request = new HttpRequestMessage(HttpMethod.Post, options.AccessTokenUri) { Content = content };
  var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(options.ClientId + ":" + options.ClientSecret));
  request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
  using var response = await client.SendAsync(request, cancellationToken);
  response.EnsureSuccessStatusCode();
  await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
  using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
  var token = doc.RootElement.GetProperty("access_token").GetString()
  ?? throw new InvalidOperationException("access_token missing");
  var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
  _cache[key] = (token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
  return token;
  }
}

public abstract class TypedClientBase
{
  protected TypedClientBase(HttpClient httpClient, IClientCredentialsTokenCache tokenCache, ClientCredentialsOptions options)
  {
  Http = httpClient;
  TokenCache = tokenCache;
  Options = options;
  Http.Timeout = Timeout.InfiniteTimeSpan;
  }

  protected HttpClient Http { get; }
  protected IClientCredentialsTokenCache TokenCache { get; }
  protected ClientCredentialsOptions Options { get; }

  protected async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(
  HttpMethod method,
  string uri,
  CancellationToken cancellationToken)
  {
  var token = await TokenCache.GetTokenAsync(Options, cancellationToken);
  var request = new HttpRequestMessage(method, uri);
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
  return request;
  }
}
~~~
exact_companion (multi-file bundle for STEP-004):

~~~csharp
namespace PiggyMetrics.Shared.Http;

using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

public static class TypedClientServiceCollectionExtensions
{
  public static IHttpClientBuilder AddPiggyTypedClient<TClient>(
  this IServiceCollection services,
  string name,
  ClientCredentialsOptions options,
  Action<IHttpClientBuilder>? configure = null)
  where TClient : class
  {
  services.AddSingleton<IClientCredentialsTokenCache, ClientCredentialsTokenCache>();
  services.AddHttpClient("client-credentials")
  .AddPolicyHandler(HttpPolicyExtensions
  .HandleTransientHttpError()
  .OrResult(r => false)
  .WaitAndRetryAsync(0, _ => TimeSpan.Zero)
  .WrapAsync(Policy.TimeoutAsync<HttpResponseMessage>(
  TimeSpan.FromMilliseconds(options.TimeoutMilliseconds))));

  var builder = services.AddHttpClient<TClient>(name, client =>
  {
  client.Timeout = Timeout.InfiniteTimeSpan;
  })
  .AddPolicyHandler(Policy.TimeoutAsync<HttpResponseMessage>(
  TimeSpan.FromMilliseconds(options.TimeoutMilliseconds)));
  configure?.Invoke(builder);
  return builder;
  }
}
~~~
Verify:

~~~bash
dotnet build Shared/Shared.csproj
~~~
Acceptance:
- ClientCredentialsOptions.TimeoutMilliseconds default is 10000
- AddPiggyTypedClient applies Polly timeout of options.TimeoutMilliseconds
- Token cache returns cached token when not within 30s of expiry
## STEP-005 — P1.T5 Shared health checks and JSON options
Depends on: STEP-001.
Why: P1.T5 requires Shared health checks and JSON options matching source enum/date/unknown-field behaviour
Files:
- `Shared/Serialization/PiggyJsonOptions.cs`
- `Shared/Serialization/PiggyMvcJsonExtensions.cs`
- `Shared/Health/PiggyHealthExtensions.cs`
Do not:
- Enable integer enum reading
- Use Newtonsoft.Json
- Map health to a path other than /health
exact_commands:

~~~bash
dotnet build Shared/Shared.csproj
~~~
exact_snippet (`Shared/Serialization/PiggyJsonOptions.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class PiggyJsonOptions
{
  public static JsonSerializerOptions Create()
  {
  var options = new JsonSerializerOptions
  {
  PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  PropertyNameCaseInsensitive = true,
  DefaultIgnoreCondition = JsonIgnoreCondition.Never,
  ReadCommentHandling = JsonCommentHandling.Skip,
  AllowTrailingCommas = false,
  WriteIndented = false,
  };
  options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
  options.Converters.Add(new Iso8601DateTimeConverter());
  return options;
  }

  private sealed class Iso8601DateTimeConverter : JsonConverter<DateTime>
  {
  private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffK";

  public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
  if (reader.TokenType == JsonTokenType.String)
  {
  var s = reader.GetString();
  if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
  {
  return dt;
  }
  }
  return reader.GetDateTime();
  }

  public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
  {
  writer.WriteStringValue(value.ToString(Format));
  }
  }
}
~~~
exact_companion (multi-file bundle for STEP-005):

~~~csharp
FILE Shared/Serialization/PiggyMvcJsonExtensions.cs
namespace PiggyMetrics.Shared.Serialization;

using Microsoft.Extensions.DependencyInjection;

public static class PiggyMvcJsonExtensions
{
  public static IMvcBuilder AddPiggyJsonOptions(this IMvcBuilder mvc)
  {
  return mvc.AddJsonOptions(o =>
  {
  var shared = PiggyJsonOptions.Create();
  o.JsonSerializerOptions.PropertyNamingPolicy = shared.PropertyNamingPolicy;
  o.JsonSerializerOptions.PropertyNameCaseInsensitive = shared.PropertyNameCaseInsensitive;
  o.JsonSerializerOptions.DefaultIgnoreCondition = shared.DefaultIgnoreCondition;
  o.JsonSerializerOptions.Converters.Clear();
  foreach (var c in shared.Converters)
  {
  o.JsonSerializerOptions.Converters.Add(c);
  }
  });
  }
}

FILE Shared/Health/PiggyHealthExtensions.cs
namespace PiggyMetrics.Shared.Health;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

public static class PiggyHealthExtensions
{
  public static IServiceCollection AddPiggyHealthChecks(this IServiceCollection services)
  {
  services.AddHealthChecks();
  return services;
  }

  public static WebApplication MapPiggyHealthChecks(this WebApplication app)
  {
  app.MapHealthChecks("/health");
  return app;
  }
}
~~~
Verify:

~~~bash
dotnet build Shared/Shared.csproj
~~~
Acceptance:
- PiggyJsonOptions.Create uses JsonStringEnumConverter
- MapPiggyHealthChecks maps /health
- No Newtonsoft PackageReference added
## STEP-006 — P1 Done-when verify full solution build and bearer tests
Depends on: STEP-002, STEP-003, STEP-004, STEP-005.
Why: Spec Done-when and gate floor require full build plus tests, not compile-only
Files:
- `PiggyMetrics.sln`
Do not:
- Use dotnet build alone as final acceptance
- Use test-compile or -DskipTests equivalents
- Mark P1 done with failing Shared.Tests
exact_commands:

~~~bash
dotnet restore PiggyMetrics.sln
dotnet build PiggyMetrics.sln --no-restore
dotnet test PiggyMetrics.sln --no-build
~~~
Verify:

~~~bash
dotnet restore PiggyMetrics.sln
dotnet build PiggyMetrics.sln --no-restore
dotnet test PiggyMetrics.sln --no-build
~~~
Acceptance:
- dotnet build PiggyMetrics.sln exits 0
- dotnet test PiggyMetrics.sln exits 0
- Accept and refuse bearer tests executed
## Out of scope
- P2+ Auth/Account/Statistics/Notification endpoint behaviour (separate plans).
- Cloning `piggymetricsJava` into this workspace.
- Reintroducing Spring Cloud Config, Eureka, or Zuul.
