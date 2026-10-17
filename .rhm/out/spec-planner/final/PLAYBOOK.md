---
name: Migration Java to .NET v18
overview: Create the net10.0 PiggyMetrics solution, Shared bearer and store primitives, AuthService contracts C1-C3, and StatisticsService contracts C8-C10 with the ported test names.
todos:
- id: step-001
  content: STEP-001 Create the net10.0 solution and shared project skeleton
  status: pending
- id: step-002
  content: STEP-002 Add shared JSON options and actuator health
  status: pending
- id: step-003
  content: STEP-003 Add the shared bearer handler that calls auth-service
  status: pending
- id: step-004
  content: STEP-004 Add shared Mongo conventions for document ids and field names
  status: pending
- id: step-005
  content: STEP-005 Add the typed client base and client-credentials token cache
  status: pending
- id: step-006
  content: STEP-006 Add buildable Gateway, AccountService, and NotificationService hosts
  status: pending
- id: step-007
  content: STEP-007 Add AuthService user document, repository, and user services
  status: pending
- id: step-008
  content: STEP-008 Add AuthService settings, token endpoint, and user routes
  status: pending
- id: step-009
  content: STEP-009 Port the five AuthService test classes
  status: pending
- id: step-010
  content: STEP-010 Add StatisticsService documents, repository, rates client, and routes
  status: pending
- id: step-011
  content: STEP-011 Port the six StatisticsService test classes
  status: pending
isProject: false
---

# Migration Java to .NET v18

This plan builds the empty PiggyMetricsDotNet tree into the Shared library, the five service hosts, AuthService contracts C1-C3, and StatisticsService contracts C8-C10. Split every `// FILE: path` region into that path. Keep the C# bodies as written.

## Decisions

- D-001 pins net10.0 because .NET 10 is the active LTS.
- D-004 validates bearers by calling http://auth-service:5000/uaa/users/current.
- D-005 stores Mongo fields with the Java names.
- D-006 sets connect timeout 10000 ms and read timeout 60000 ms.
- D-007 leaves Gateway, AccountService, and NotificationService as health-only hosts on ports 4000, 6000, and 8000.
- D-009 embeds shared yml values in appsettings and does not call the config server during boot.
- D-011 returns name and scope from GET /uaa/users/current.

## Constraint

The target checkout has no project files. Do not modify README.md, AGENTS.md, .harnessmatters, or the agent pack. Do not add account, notification, or gateway business routes in this package.

## Concrete changes

### STEP-001 Create the net10.0 solution and shared project skeleton

Depends on: none.

Files:

- `PiggyMetrics.sln`
- `global.json`
- `Directory.Build.props`
- `src/Shared/Shared.csproj`

Create PiggyMetrics.sln at the repository root with Shared, Gateway, AuthService, AccountService, StatisticsService, NotificationService, Shared.Tests, AuthService.Tests, and StatisticsService.Tests. Pin net10.0 in Directory.Build.props and the SDK floor in global.json. Shared is a class library with FrameworkReference Microsoft.AspNetCore.App and MongoDB.Driver 2.30.0.

exact_snippet (bundle):

~~~csharp
// FILE: PiggyMetrics.sln
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Shared", "src\Shared\Shared.csproj", "{A1B2C3D4-E5F6-4711-8111-111111111111}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Gateway", "src\Gateway\Gateway.csproj", "{A1B2C3D4-E5F6-4711-8111-222222222222}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "AuthService", "src\AuthService\AuthService.csproj", "{A1B2C3D4-E5F6-4711-8111-333333333333}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "AccountService", "src\AccountService\AccountService.csproj", "{A1B2C3D4-E5F6-4711-8111-444444444444}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "StatisticsService", "src\StatisticsService\StatisticsService.csproj", "{A1B2C3D4-E5F6-4711-8111-555555555555}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NotificationService", "src\NotificationService\NotificationService.csproj", "{A1B2C3D4-E5F6-4711-8111-666666666666}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Shared.Tests", "tests\Shared.Tests\Shared.Tests.csproj", "{A1B2C3D4-E5F6-4711-8111-777777777777}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "AuthService.Tests", "tests\AuthService.Tests\AuthService.Tests.csproj", "{A1B2C3D4-E5F6-4711-8111-888888888888}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "StatisticsService.Tests", "tests\StatisticsService.Tests\StatisticsService.Tests.csproj", "{A1B2C3D4-E5F6-4711-8111-999999999999}"
EndProject
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
  Debug|Any CPU = Debug|Any CPU
  Release|Any CPU = Release|Any CPU
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
  {A1B2C3D4-E5F6-4711-8111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-111111111111}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-111111111111}.Release|Any CPU.Build.0 = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-222222222222}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-222222222222}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-222222222222}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-222222222222}.Release|Any CPU.Build.0 = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-333333333333}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-333333333333}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-333333333333}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-333333333333}.Release|Any CPU.Build.0 = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-444444444444}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-444444444444}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-444444444444}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-444444444444}.Release|Any CPU.Build.0 = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-555555555555}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-555555555555}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-555555555555}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-555555555555}.Release|Any CPU.Build.0 = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-666666666666}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-666666666666}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-666666666666}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-666666666666}.Release|Any CPU.Build.0 = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-777777777777}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-777777777777}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-777777777777}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-777777777777}.Release|Any CPU.Build.0 = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-888888888888}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-888888888888}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-888888888888}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-888888888888}.Release|Any CPU.Build.0 = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-999999999999}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-999999999999}.Debug|Any CPU.Build.0 = Debug|Any CPU
  {A1B2C3D4-E5F6-4711-8111-999999999999}.Release|Any CPU.ActiveCfg = Release|Any CPU
  {A1B2C3D4-E5F6-4711-8111-999999999999}.Release|Any CPU.Build.0 = Release|Any CPU
  EndGlobalSection
EndGlobal

// FILE: global.json
{
  "sdk": {
  "version": "10.0.100",
  "rollForward": "latestMajor",
  "allowPrerelease": false
  }
}

// FILE: Directory.Build.props
<Project>
  <PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>

// FILE: src/Shared/Shared.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
  <RootNamespace>PiggyMetrics.Shared</RootNamespace>
  <AssemblyName>PiggyMetrics.Shared</AssemblyName>
  <Description>Shared bearer handler, store conventions, typed clients, health, and JSON options.</Description>
  </PropertyGroup>
  <ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
  <PackageReference Include="MongoDB.Driver" Version="2.30.0" />
  </ItemGroup>
</Project>
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- PiggyMetrics.sln lists nine projects.
- Directory.Build.props sets TargetFramework net10.0.
### STEP-002 Add shared JSON options and actuator health

Depends on: STEP-001.

Files:

- `src/Shared/Json/PiggyJson.cs`
- `src/Shared/Health/PiggyHealthExtensions.cs`

Add PiggyJson with camelCase names, string enums, skipped unknown members, and Jackson 2.9 date text yyyy-MM-ddTHH:mm:ss.fff plus a numeric zone without a colon. Map GET /actuator/health to the body {"status":"UP"}.

exact_snippet (bundle):

~~~csharp
// FILE: src/Shared/Json/PiggyJson.cs
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiggyMetrics.Shared.Json;

public static class PiggyJson
{
  public static JsonSerializerOptions Options { get; } = Create();

  public static JsonSerializerOptions Create()
  {
  var options = new JsonSerializerOptions
  {
  PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  PropertyNameCaseInsensitive = true,
  DefaultIgnoreCondition = JsonIgnoreCondition.Never,
  UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
  NumberHandling = JsonNumberHandling.AllowReadingFromString
  };
  options.Converters.Add(new JsonStringEnumConverter(null, allowIntegerValues: false));
  options.Converters.Add(new JacksonDateTimeConverter());
  options.Converters.Add(new JacksonDateOnlyConverter());
  return options;
  }

  public static string FormatJacksonDate(DateTimeOffset value)
  {
  var utc = value.ToUniversalTime();
  var stamp = utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture);
  var zone = utc.ToString("zzz", CultureInfo.InvariantCulture).Replace(":", "");
  return stamp + zone;
  }
}

public sealed class JacksonDateTimeConverter : JsonConverter<DateTime>
{
  public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
  var text = reader.GetString() ?? throw new JsonException("date is missing");
  if (text.Length > 5 && (text[^5] == '+' || text[^5] == '-') && text[^3] != ':')
  {
  text = text[..^2] + ":" + text[^2..];
  }
  return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
  }

  public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
  {
  var offset = value.Kind == DateTimeKind.Unspecified
  ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local))
  : new DateTimeOffset(value);
  writer.WriteStringValue(PiggyJson.FormatJacksonDate(offset));
  }
}

public sealed class JacksonDateOnlyConverter : JsonConverter<DateOnly>
{
  public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
  var text = reader.GetString() ?? throw new JsonException("date is missing");
  return DateOnly.Parse(text, CultureInfo.InvariantCulture);
  }

  public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
  {
  writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
  }
}

// FILE: src/Shared/Health/PiggyHealthExtensions.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace PiggyMetrics.Shared.Health;

public static class PiggyHealthExtensions
{
  public const string Path = "/actuator/health";
  public const string Body = "{\"status\":\"UP\"}";

  public static IServiceCollection AddPiggyHealth(this IServiceCollection services)
  {
  services.AddHealthChecks();
  return services;
  }

  public static WebApplication MapPiggyHealth(this WebApplication app)
  {
  app.MapGet(Path, () => Results.Text(Body, "application/json"));
  return app;
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- GET /actuator/health returns 200 and the body {"status":"UP"}.
- Enum values serialize as their names.
### STEP-003 Add the shared bearer handler that calls auth-service

Depends on: STEP-002.

Files:

- `src/Shared/Auth/AuthServiceBearerHandler.cs`
- `tests/Shared.Tests/Shared.Tests.csproj`
- `tests/Shared.Tests/BearerHandlerTests.cs`

Add AuthServiceBearerHandler. It forwards the inbound Authorization header to Piggy:Auth:UserInfoUrl, default http://auth-service:5000/uaa/users/current. HTTP 200 with a name claim succeeds. Any other status refuses the token. Register policies user and server.

exact_snippet (bundle):

~~~csharp
// FILE: src/Shared/Auth/AuthServiceBearerHandler.cs
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Auth;

public sealed class AuthServiceBearerHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
  public const string SchemeName = "PiggyBearer";
  private readonly IHttpClientFactory _clients;

  public AuthServiceBearerHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder,
  IHttpClientFactory clients)
  : base(options, logger, encoder)
  {
  _clients = clients;
  }

  protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
  {
  var header = Request.Headers.Authorization.ToString();
  if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
  {
  return AuthenticateResult.NoResult();
  }

  var userInfoUrl = Context.RequestServices
  .GetRequiredService<IOptions<PiggyAuthOptions>>().Value.UserInfoUrl;
  var client = _clients.CreateClient("auth-userinfo");
  var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
  request.Headers.TryAddWithoutValidation("Authorization", header);
  HttpResponseMessage response;
  try
  {
  response = await client.SendAsync(request, Context.RequestAborted);
  }
  catch (HttpRequestException ex)
  {
  return AuthenticateResult.Fail(ex.Message);
  }

  if (!response.IsSuccessStatusCode)
  {
  return AuthenticateResult.Fail("token refused");
  }

  await using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
  using var document = await JsonDocument.ParseAsync(stream, cancellationToken: Context.RequestAborted);
  if (!document.RootElement.TryGetProperty("name", out var nameNode))
  {
  return AuthenticateResult.Fail("token refused");
  }
  var name = nameNode.GetString();
  if (string.IsNullOrEmpty(name))
  {
  return AuthenticateResult.Fail("token refused");
  }
  var scope = document.RootElement.TryGetProperty("scope", out var scopeNode)
  ? scopeNode.GetString() ?? ""
  : "";
  var claims = new List<Claim>
  {
  new(ClaimTypes.Name, name),
  new("scope", scope)
  };
  var identity = new ClaimsIdentity(claims, SchemeName);
  var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
  return AuthenticateResult.Success(ticket);
  }
}

public sealed class PiggyAuthOptions
{
  public string UserInfoUrl { get; set; } = "http://auth-service:5000/uaa/users/current";
  public string TokenUrl { get; set; } = "http://auth-service:5000/uaa/oauth/token";
}

public static class PiggyAuthenticationExtensions
{
  public static IServiceCollection AddPiggyBearer(this IServiceCollection services, IConfiguration configuration)
  {
  services.Configure<PiggyAuthOptions>(configuration.GetSection("Piggy:Auth"));
  services.AddHttpClient("auth-userinfo");
  services.AddAuthentication(AuthServiceBearerHandler.SchemeName)
  .AddScheme<AuthenticationSchemeOptions, AuthServiceBearerHandler>(AuthServiceBearerHandler.SchemeName, null);
  services.AddAuthorization(options =>
  {
  options.AddPolicy("user", policy => policy.RequireAuthenticatedUser());
  options.AddPolicy("server", policy => policy.RequireAssertion(context =>
  {
  var scope = context.User.FindFirst("scope")?.Value ?? "";
  return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("server");
  }));
  });
  return services;
  }
}

// FILE: tests/Shared.Tests/Shared.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
  <RootNamespace>PiggyMetrics.Shared.Tests</RootNamespace>
  <AssemblyName>PiggyMetrics.Shared.Tests</AssemblyName>
  <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />
  <PackageReference Include="xunit" Version="2.9.3" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.13" />
  <PackageReference Include="Mongo2Go" Version="3.1.3" />
  </ItemGroup>
  <ItemGroup>
  <ProjectReference Include="..\..\src\Shared\Shared.csproj" />
  </ItemGroup>
</Project>

// FILE: tests/Shared.Tests/BearerHandlerTests.cs
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Auth;

namespace PiggyMetrics.Shared.Tests;

public class BearerHandlerTests
{
  [Fact]
  public async Task accepts_a_token_the_authorization_server_issued()
  {
  await using var auth = await AuthStub.Start(HttpStatusCode.OK, "{\"name\":\"demo\",\"scope\":\"ui\"}");
  await using var app = await ResourceStub.Start(auth.Url);
  using var client = new HttpClient();
  var request = new HttpRequestMessage(HttpMethod.Get, app.Url + "/probe");
  request.Headers.TryAddWithoutValidation("Authorization", "Bearer issued-token");
  var response = await client.SendAsync(request);
  Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  Assert.Equal("demo", await response.Content.ReadAsStringAsync());
  }

  [Fact]
  public async Task refuses_a_token_the_authorization_server_did_not_issue()
  {
  await using var auth = await AuthStub.Start(HttpStatusCode.Unauthorized, "{\"error\":\"invalid_token\"}");
  await using var app = await ResourceStub.Start(auth.Url);
  using var client = new HttpClient();
  var request = new HttpRequestMessage(HttpMethod.Get, app.Url + "/probe");
  request.Headers.TryAddWithoutValidation("Authorization", "Bearer foreign-token");
  var response = await client.SendAsync(request);
  Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  private sealed class AuthStub : IAsyncDisposable
  {
  private readonly WebApplication _app;
  public string Url { get; }

  private AuthStub(WebApplication app, string url)
  {
  _app = app;
  Url = url;
  }

  public static async Task<AuthStub> Start(HttpStatusCode status, string body)
  {
  var builder = WebApplication.CreateBuilder();
  builder.WebHost.UseUrls("http://127.0.0.1:0");
  var app = builder.Build();
  app.MapGet("/uaa/users/current", () => Results.Text(body, "application/json", Encoding.UTF8, (int)status));
  await app.StartAsync();
  var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
  return new AuthStub(app, address);
  }

  public async ValueTask DisposeAsync() => await _app.StopAsync();
  }

  private sealed class ResourceStub : IAsyncDisposable
  {
  private readonly WebApplication _app;
  public string Url { get; }

  private ResourceStub(WebApplication app, string url)
  {
  _app = app;
  Url = url;
  }

  public static async Task<ResourceStub> Start(string authUrl)
  {
  var builder = WebApplication.CreateBuilder();
  builder.WebHost.UseUrls("http://127.0.0.1:0");
  builder.Configuration["Piggy:Auth:UserInfoUrl"] = authUrl + "/uaa/users/current";
  builder.Services.AddPiggyBearer(builder.Configuration);
  var app = builder.Build();
  app.UseAuthentication();
  app.UseAuthorization();
  app.MapGet("/probe", (HttpContext context) => Results.Text(context.User.Identity!.Name!)).RequireAuthorization("user");
  await app.StartAsync();
  var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
  return new ResourceStub(app, address);
  }

  public async ValueTask DisposeAsync() => await _app.StopAsync();
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- accepts_a_token_the_authorization_server_issued passes.
- refuses_a_token_the_authorization_server_did_not_issue passes.
### STEP-004 Add shared Mongo conventions for document ids and field names

Depends on: STEP-001.

Files:

- `src/Shared/Mongo/PiggyMongo.cs`

Register a convention pack that stores enums as strings and ignores extra elements. Build the Mongo client from host, port, username, and password with authSource=admin. Callers still place _id and field names on each document type.

exact_snippet (bundle):

~~~csharp
// FILE: src/Shared/Mongo/PiggyMongo.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace PiggyMetrics.Shared.Mongo;

public static class PiggyMongo
{
  private static int _registered;

  public static void RegisterConventions()
  {
  if (Interlocked.Exchange(ref _registered, 1) == 1)
  {
  return;
  }
  var pack = new ConventionPack
  {
  new EnumRepresentationConvention(BsonType.String),
  new IgnoreExtraElementsConvention(true)
  };
  ConventionRegistry.Register("piggymetrics-store", pack, _ => true);
  }

  public static MongoClient CreateClient(string host, int port, string username, string password)
  {
  RegisterConventions();
  var identity = Uri.EscapeDataString(username);
  var secret = Uri.EscapeDataString(password);
  var url = $"mongodb://{identity}:{secret}@{host}:{port}/?authSource=admin";
  return new MongoClient(url);
  }

  public static IMongoDatabase Database(MongoClient client, string name)
  {
  return client.GetDatabase(name);
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- PiggyMongo.RegisterConventions runs once.
- The client URL contains authSource=admin.
### STEP-005 Add the typed client base and client-credentials token cache

Depends on: STEP-002.

Files:

- `src/Shared/Http/TypedClientBase.cs`

Add TypedClientBase.AddEdgeClient with connect timeout 10000 ms and read timeout 60000 ms, matching Feign Request.Options defaults because statistics-service.yml sets no ribbon timeout. ClientCredentialsTokenCache posts grant_type=client_credentials to the token URL and reuses access_token until expires_in elapses.

exact_snippet (bundle):

~~~csharp
// FILE: src/Shared/Http/TypedClientBase.cs
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Json;

namespace PiggyMetrics.Shared.Http;

public sealed class ClientCredentialsTokenCache
{
  private readonly IHttpClientFactory _clients;
  private readonly SemaphoreSlim _gate = new(1, 1);
  private string? _accessToken;
  private DateTimeOffset _expiresAt;

  public ClientCredentialsTokenCache(IHttpClientFactory clients)
  {
  _clients = clients;
  }

  public async Task<string> GetAsync(ClientCredentialsRequest request, CancellationToken cancellationToken)
  {
  if (_accessToken != null && DateTimeOffset.UtcNow < _expiresAt)
  {
  return _accessToken;
  }
  await _gate.WaitAsync(cancellationToken);
  try
  {
  if (_accessToken != null && DateTimeOffset.UtcNow < _expiresAt)
  {
  return _accessToken;
  }
  var client = _clients.CreateClient(request.HttpClientName);
  using var message = new HttpRequestMessage(HttpMethod.Post, request.TokenUrl);
  message.Content = new FormUrlEncodedContent(new Dictionary<string, string>
  {
  ["grant_type"] = "client_credentials",
  ["scope"] = request.Scope
  });
  var raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.ClientId + ":" + request.ClientSecret));
  message.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
  using var response = await client.SendAsync(message, cancellationToken);
  response.EnsureSuccessStatusCode();
  await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
  var payload = await JsonSerializer.DeserializeAsync<TokenPayload>(stream, PiggyJson.Options, cancellationToken)
  ?? throw new InvalidOperationException("token response was empty");
  _accessToken = payload.AccessToken;
  _expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn);
  return _accessToken;
  }
  finally
  {
  _gate.Release();
  }
  }
}

public sealed class ClientCredentialsRequest
{
  public string HttpClientName { get; init; } = "auth-token";
  public string TokenUrl { get; init; } = "http://auth-service:5000/uaa/oauth/token";
  public string ClientId { get; init; } = "";
  public string ClientSecret { get; init; } = "";
  public string Scope { get; init; } = "server";
}

public sealed class TokenPayload
{
  [JsonPropertyName("access_token")]
  public string AccessToken { get; set; } = "";

  [JsonPropertyName("token_type")]
  public string TokenType { get; set; } = "";

  [JsonPropertyName("expires_in")]
  public int ExpiresIn { get; set; }

  [JsonPropertyName("refresh_token")]
  public string? RefreshToken { get; set; }

  [JsonPropertyName("scope")]
  public string Scope { get; set; } = "";
}

public static class TypedClientBase
{
  public const int SourceConnectTimeoutMilliseconds = 10000;
  public const int SourceReadTimeoutMilliseconds = 60000;

  public static IHttpClientBuilder AddEdgeClient(IServiceCollection services, string name)
  {
  return services.AddHttpClient(name, client =>
  {
  client.Timeout = TimeSpan.FromMilliseconds(SourceReadTimeoutMilliseconds);
  })
  .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
  {
  ConnectTimeout = TimeSpan.FromMilliseconds(SourceConnectTimeoutMilliseconds)
  });
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- Named client rates-client uses a 60000 ms timeout.
- A second GetAsync before expiry does not post again.
### STEP-006 Add buildable Gateway, AccountService, and NotificationService hosts

Depends on: STEP-002.

Files:

- `src/Gateway/Gateway.csproj`
- `src/Gateway/Program.cs`
- `src/Gateway/appsettings.json`
- `src/AccountService/AccountService.csproj`
- `src/AccountService/Program.cs`
- `src/AccountService/appsettings.json`
- `src/NotificationService/NotificationService.csproj`
- `src/NotificationService/Program.cs`
- `src/NotificationService/appsettings.json`

Add three web projects that reference Shared, map /actuator/health, and listen on the source ports: Gateway 4000 with an empty path base, AccountService 6000 with path base /accounts, NotificationService 8000 with path base /notifications. Do not add their business routes.

exact_snippet (bundle):

~~~csharp
// FILE: src/Gateway/Gateway.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <RootNamespace>Gateway</RootNamespace>
  <AssemblyName>Gateway</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />

  </ItemGroup>
</Project>

// FILE: src/Gateway/Program.cs
using PiggyMetrics.Shared.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyHealth();
var app = builder.Build();
var pathBase = builder.Configuration["Piggy:PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
  app.UsePathBase(pathBase);
}
app.MapPiggyHealth();
app.Run();

// FILE: src/Gateway/appsettings.json
{
  "Kestrel": {
  "Endpoints": {
  "Http": {
  "Url": "http://0.0.0.0:4000"
  }
  }
  },
  "Piggy": {
  "PathBase": "",
  "ApplicationName": "gateway"
  }
}

// FILE: src/AccountService/AccountService.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <RootNamespace>AccountService</RootNamespace>
  <AssemblyName>AccountService</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />

  </ItemGroup>
</Project>

// FILE: src/AccountService/Program.cs
using PiggyMetrics.Shared.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyHealth();
var app = builder.Build();
var pathBase = builder.Configuration["Piggy:PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
  app.UsePathBase(pathBase);
}
app.MapPiggyHealth();
app.Run();

// FILE: src/AccountService/appsettings.json
{
  "Kestrel": {
  "Endpoints": {
  "Http": {
  "Url": "http://0.0.0.0:6000"
  }
  }
  },
  "Piggy": {
  "PathBase": "/accounts",
  "ApplicationName": "account-service",
  "Mongo": {
  "Host": "account-mongodb",
  "Port": 27017,
  "Username": "user",
  "Database": "piggymetrics"
  }
  }
}

// FILE: src/NotificationService/NotificationService.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <RootNamespace>NotificationService</RootNamespace>
  <AssemblyName>NotificationService</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />

  </ItemGroup>
</Project>

// FILE: src/NotificationService/Program.cs
using PiggyMetrics.Shared.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyHealth();
var app = builder.Build();
var pathBase = builder.Configuration["Piggy:PathBase"];
if (!string.IsNullOrEmpty(pathBase))
{
  app.UsePathBase(pathBase);
}
app.MapPiggyHealth();
app.Run();

// FILE: src/NotificationService/appsettings.json
{
  "Kestrel": {
  "Endpoints": {
  "Http": {
  "Url": "http://0.0.0.0:8000"
  }
  }
  },
  "Piggy": {
  "PathBase": "/notifications",
  "ApplicationName": "notification-service",
  "Mongo": {
  "Host": "notification-mongodb",
  "Port": 27017,
  "Username": "user",
  "Database": "piggymetrics"
  }
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- Gateway listens on port 4000.
- AccountService path base is /accounts.
- NotificationService path base is /notifications.
### STEP-007 Add AuthService user document, repository, and user services

Depends on: STEP-004.

Files:

- `src/AuthService/AuthService.csproj`
- `src/AuthService/Domain/User.cs`
- `src/AuthService/Data/UserRepository.cs`
- `src/AuthService/Security/MongoUserDetailsService.cs`
- `src/AuthService/Services/UserService.cs`

Add the users collection document with _id equal to username and a password field. UserRepository saves and finds by that id on database piggymetrics. UserService rejects an existing username with ArgumentException user already exists and stores a BCrypt work-factor 10 hash. MongoUserDetailsService throws UsernameNotFoundException when the id is absent.

exact_snippet (bundle):

~~~csharp
// FILE: src/AuthService/AuthService.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <RootNamespace>AuthService</RootNamespace>
  <AssemblyName>AuthService</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />
  <PackageReference Include="BCrypt.Net-Next" Version="4.2.1" />

  </ItemGroup>
</Project>

// FILE: src/AuthService/Domain/User.cs
using MongoDB.Bson.Serialization.Attributes;

namespace AuthService.Domain;

public sealed class User
{
  [BsonId]
  [BsonElement("_id")]
  public string Username { get; set; } = "";

  [BsonElement("password")]
  public string Password { get; set; } = "";
}

// FILE: src/AuthService/Data/UserRepository.cs
using AuthService.Domain;
using MongoDB.Driver;
using PiggyMetrics.Shared.Mongo;

namespace AuthService.Data;

public interface IUserRepository
{
  User? FindById(string username);
  void Save(User user);
}

public sealed class UserRepository : IUserRepository
{
  public const string CollectionName = "users";
  private readonly IMongoCollection<User> _users;

  public UserRepository(IMongoDatabase database)
  {
  PiggyMongo.RegisterConventions();
  _users = database.GetCollection<User>(CollectionName);
  }

  public User? FindById(string username)
  {
  return _users.Find(user => user.Username == username).FirstOrDefault();
  }

  public void Save(User user)
  {
  _users.ReplaceOne(
  existing => existing.Username == user.Username,
  user,
  new ReplaceOptions { IsUpsert = true });
  }
}

// FILE: src/AuthService/Security/MongoUserDetailsService.cs
using AuthService.Data;
using AuthService.Domain;

namespace AuthService.Security;

public sealed class UsernameNotFoundException : Exception
{
  public UsernameNotFoundException(string username) : base(username)
  {
  }
}

public sealed class MongoUserDetailsService
{
  private readonly IUserRepository _repository;

  public MongoUserDetailsService(IUserRepository repository)
  {
  _repository = repository;
  }

  public User LoadUserByUsername(string username)
  {
  return _repository.FindById(username) ?? throw new UsernameNotFoundException(username);
  }
}

// FILE: src/AuthService/Services/UserService.cs
using AuthService.Data;
using AuthService.Domain;

namespace AuthService.Services;

public interface IUserService
{
  void Create(User user);
}

public sealed class UserService : IUserService
{
  private readonly IUserRepository _repository;

  public UserService(IUserRepository repository)
  {
  _repository = repository;
  }

  public void Create(User user)
  {
  var existing = _repository.FindById(user.Username);
  if (existing != null)
  {
  throw new ArgumentException("user already exists: " + existing.Username);
  }
  user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password, workFactor: 10);
  _repository.Save(user);
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- shouldSaveAndFindUserByName reads the same username and password.
- shouldFailWhenUserAlreadyExists throws ArgumentException with message user already exists: name.
### STEP-008 Add AuthService settings, token endpoint, and user routes

Depends on: STEP-007, STEP-003.

Files:

- `src/AuthService/Program.cs`
- `src/AuthService/appsettings.json`
- `src/AuthService/Controllers/UserController.cs`
- `src/AuthService/OAuth/AuthorizationServer.cs`

Host AuthService on port 5000 with path base /uaa. Persist users in auth-mongodb database piggymetrics. Register four clients: browser with grants refresh_token and password and scope ui; account-service, statistics-service, and notification-service with grants client_credentials and refresh_token, scope server, and secrets from ACCOUNT_SERVICE_PASSWORD, STATISTICS_SERVICE_PASSWORD, and NOTIFICATION_SERVICE_PASSWORD. POST /oauth/token issues an opaque bearer token stored in memory for 43200 seconds. GET /users/current requires policy user and returns name plus scope. POST /users requires policy server.

exact_snippet (bundle):

~~~csharp
// FILE: src/AuthService/Program.cs
using AuthService.Data;
using AuthService.OAuth;
using AuthService.Security;
using AuthService.Services;
using MongoDB.Driver;
using PiggyMetrics.Shared.Health;
using PiggyMetrics.Shared.Json;
using PiggyMetrics.Shared.Mongo;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
  options.SerializerOptions.PropertyNamingPolicy = PiggyJson.Options.PropertyNamingPolicy;
  options.SerializerOptions.PropertyNameCaseInsensitive = true;
  options.SerializerOptions.DefaultIgnoreCondition = PiggyJson.Options.DefaultIgnoreCondition;
  options.SerializerOptions.UnmappedMemberHandling = PiggyJson.Options.UnmappedMemberHandling;
  options.SerializerOptions.NumberHandling = PiggyJson.Options.NumberHandling;
  foreach (var converter in PiggyJson.Options.Converters)
  {
  options.SerializerOptions.Converters.Add(converter);
  }
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
  options.JsonSerializerOptions.PropertyNamingPolicy = PiggyJson.Options.PropertyNamingPolicy;
  options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
  options.JsonSerializerOptions.DefaultIgnoreCondition = PiggyJson.Options.DefaultIgnoreCondition;
  options.JsonSerializerOptions.UnmappedMemberHandling = PiggyJson.Options.UnmappedMemberHandling;
  foreach (var converter in PiggyJson.Options.Converters)
  {
  options.JsonSerializerOptions.Converters.Add(converter);
  }
});
builder.Services.AddPiggyHealth();
builder.Services.AddSingleton<InMemoryTokenStore>();
builder.Services.AddSingleton(sp =>
{
  var configuration = sp.GetRequiredService<IConfiguration>();
  var connectionString = configuration["Piggy:Mongo:ConnectionString"];
  if (!string.IsNullOrEmpty(connectionString))
  {
  PiggyMongo.RegisterConventions();
  return new MongoClient(connectionString);
  }
  return PiggyMongo.CreateClient(
  configuration["Piggy:Mongo:Host"] ?? "auth-mongodb",
  int.Parse(configuration["Piggy:Mongo:Port"] ?? "27017"),
  configuration["Piggy:Mongo:Username"] ?? "user",
  configuration["MONGODB_PASSWORD"] ?? configuration["Piggy:Mongo:Password"] ?? "");
});
builder.Services.AddSingleton(sp =>
{
  var configuration = sp.GetRequiredService<IConfiguration>();
  return PiggyMongo.Database(sp.GetRequiredService<MongoClient>(), configuration["Piggy:Mongo:Database"] ?? "piggymetrics");
});
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<MongoUserDetailsService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddAuthentication(LocalTokenHandler.SchemeName)
  .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, LocalTokenHandler>(LocalTokenHandler.SchemeName, null);
builder.Services.AddAuthorization(options =>
{
  options.AddPolicy("user", policy => policy.RequireAuthenticatedUser());
  options.AddPolicy("server", policy => policy.RequireAssertion(context =>
  {
  var scope = context.User.FindFirst("scope")?.Value ?? "";
  return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("server");
  }));
});

var app = builder.Build();
app.UsePathBase("/uaa");
app.UseAuthentication();
app.UseAuthorization();
app.MapPiggyHealth();
app.MapControllers();
var clients = AuthorizationServer.ClientsFrom(app.Configuration);
AuthorizationServer.Map(
  app,
  clients,
  app.Services.GetRequiredService<InMemoryTokenStore>(),
  app.Services.GetRequiredService<MongoUserDetailsService>());
app.Run();

public partial class Program { }

// FILE: src/AuthService/appsettings.json
{
  "Kestrel": {
  "Endpoints": {
  "Http": {
  "Url": "http://0.0.0.0:5000"
  }
  }
  },
  "Piggy": {
  "PathBase": "/uaa",
  "ApplicationName": "auth-service",
  "Config": {
  "Uri": "http://config:8888",
  "FailFast": true,
  "Username": "user"
  },
  "Mongo": {
  "Host": "auth-mongodb",
  "Port": 27017,
  "Username": "user",
  "Database": "piggymetrics",
  "Collection": "users"
  }
  }
}

// FILE: src/AuthService/Controllers/UserController.cs
using AuthService.Domain;
using AuthService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[ApiController]
[Route("users")]
public sealed class UserController : ControllerBase
{
  private readonly IUserService _users;

  public UserController(IUserService users)
  {
  _users = users;
  }

  [Authorize(Policy = "user")]
  [HttpGet("current")]
  public IActionResult GetUser()
  {
  var name = User.Identity?.Name ?? "";
  var scope = User.FindFirst("scope")?.Value ?? "";
  return Ok(new CurrentPrincipal(name, scope));
  }

  [Authorize(Policy = "server")]
  [HttpPost]
  public IActionResult CreateUser([FromBody] User? user)
  {
  if (user == null)
  {
  return BadRequest();
  }
  _users.Create(user);
  return Ok();
  }
}

public sealed record CurrentPrincipal(string Name, string Scope);

// FILE: src/AuthService/OAuth/AuthorizationServer.cs
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using AuthService.Domain;
using AuthService.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AuthService.OAuth;

public sealed class OAuthClient
{
  public string Id { get; init; } = "";
  public string Secret { get; init; } = "";
  public string[] Grants { get; init; } = Array.Empty<string>();
  public string[] Scopes { get; init; } = Array.Empty<string>();
}

public sealed class IssuedToken
{
  public string AccessToken { get; init; } = "";
  public string RefreshToken { get; init; } = "";
  public string TokenType { get; init; } = "bearer";
  public int ExpiresIn { get; init; } = 43200;
  public string Scope { get; init; } = "";
  public string ClientId { get; init; } = "";
  public string Name { get; init; } = "";
  public DateTimeOffset ExpiresAt { get; init; }
}

public sealed class InMemoryTokenStore
{
  private readonly Dictionary<string, IssuedToken> _byAccess = new();
  private readonly Dictionary<string, IssuedToken> _byRefresh = new();

  public IssuedToken Issue(string clientId, string name, string scope, string? refreshToken = null)
  {
  var issued = new IssuedToken
  {
  AccessToken = Guid.NewGuid().ToString("N"),
  RefreshToken = refreshToken ?? Guid.NewGuid().ToString("N"),
  TokenType = "bearer",
  ExpiresIn = 43200,
  Scope = scope,
  ClientId = clientId,
  Name = name,
  ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(43200)
  };
  lock (_byAccess)
  {
  _byAccess[issued.AccessToken] = issued;
  _byRefresh[issued.RefreshToken] = issued;
  }
  return issued;
  }

  public IssuedToken? FindByAccess(string accessToken)
  {
  lock (_byAccess)
  {
  return _byAccess.TryGetValue(accessToken, out var issued) ? issued : null;
  }
  }

  public IssuedToken? FindByRefresh(string refreshToken)
  {
  lock (_byAccess)
  {
  return _byRefresh.TryGetValue(refreshToken, out var issued) ? issued : null;
  }
  }
}

public sealed class TokenResponse
{
  [JsonPropertyName("access_token")]
  public string AccessToken { get; init; } = "";

  [JsonPropertyName("token_type")]
  public string TokenType { get; init; } = "bearer";

  [JsonPropertyName("refresh_token")]
  public string RefreshToken { get; init; } = "";

  [JsonPropertyName("expires_in")]
  public int ExpiresIn { get; init; }

  [JsonPropertyName("scope")]
  public string Scope { get; init; } = "";
}

public sealed class LocalTokenHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
  public const string SchemeName = "LocalToken";
  private readonly InMemoryTokenStore _store;

  public LocalTokenHandler(
  IOptionsMonitor<AuthenticationSchemeOptions> options,
  ILoggerFactory logger,
  UrlEncoder encoder,
  InMemoryTokenStore store)
  : base(options, logger, encoder)
  {
  _store = store;
  }

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
  var header = Request.Headers.Authorization.ToString();
  if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
  {
  return Task.FromResult(AuthenticateResult.NoResult());
  }
  var raw = header["Bearer ".Length..].Trim();
  var issued = _store.FindByAccess(raw);
  if (issued == null || issued.ExpiresAt <= DateTimeOffset.UtcNow)
  {
  return Task.FromResult(AuthenticateResult.Fail("token refused"));
  }
  var claims = new List<Claim>
  {
  new(ClaimTypes.Name, issued.Name),
  new("scope", issued.Scope)
  };
  var identity = new ClaimsIdentity(claims, SchemeName);
  var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
  return Task.FromResult(AuthenticateResult.Success(ticket));
  }
}

public static class AuthorizationServer
{
  public static IReadOnlyList<OAuthClient> ClientsFrom(IConfiguration configuration)
  {
  return new[]
  {
  new OAuthClient
  {
  Id = "browser",
  Secret = "",
  Grants = new[] { "refresh_token", "password" },
  Scopes = new[] { "ui" }
  },
  new OAuthClient
  {
  Id = "account-service",
  Secret = configuration["ACCOUNT_SERVICE_PASSWORD"] ?? "",
  Grants = new[] { "client_credentials", "refresh_token" },
  Scopes = new[] { "server" }
  },
  new OAuthClient
  {
  Id = "statistics-service",
  Secret = configuration["STATISTICS_SERVICE_PASSWORD"] ?? "",
  Grants = new[] { "client_credentials", "refresh_token" },
  Scopes = new[] { "server" }
  },
  new OAuthClient
  {
  Id = "notification-service",
  Secret = configuration["NOTIFICATION_SERVICE_PASSWORD"] ?? "",
  Grants = new[] { "client_credentials", "refresh_token" },
  Scopes = new[] { "server" }
  }
  };
  }

  public static void Map(WebApplication app, IReadOnlyList<OAuthClient> clients, InMemoryTokenStore store, MongoUserDetailsService users)
  {
  app.MapPost("/oauth/token", async (HttpRequest request) =>
  {
  var form = await request.ReadFormAsync();
  var grant = form["grant_type"].ToString();
  var (client, error) = ResolveClient(request, form, clients);
  if (client == null)
  {
  return Results.Json(new { error = "invalid_client" }, statusCode: error);
  }
  if (!client.Grants.Contains(grant))
  {
  return Results.Json(new { error = "unsupported_grant_type" }, statusCode: 400);
  }
  if (grant == "client_credentials")
  {
  var issued = store.Issue(client.Id, client.Id, "server");
  return Results.Json(ToResponse(issued));
  }
  if (grant == "password")
  {
  var username = form["username"].ToString();
  var password = form["password"].ToString();
  User user;
  try
  {
  user = users.LoadUserByUsername(username);
  }
  catch (UsernameNotFoundException)
  {
  return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
  }
  if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
  {
  return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
  }
  var issued = store.Issue(client.Id, user.Username, "ui");
  return Results.Json(ToResponse(issued));
  }
  if (grant == "refresh_token")
  {
  var current = store.FindByRefresh(form["refresh_token"].ToString());
  if (current == null || current.ClientId != client.Id)
  {
  return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
  }
  var issued = store.Issue(client.Id, current.Name, current.Scope, current.RefreshToken);
  return Results.Json(ToResponse(issued));
  }
  return Results.Json(new { error = "unsupported_grant_type" }, statusCode: 400);
  });
  }

  private static TokenResponse ToResponse(IssuedToken issued)
  {
  return new TokenResponse
  {
  AccessToken = issued.AccessToken,
  TokenType = issued.TokenType,
  RefreshToken = issued.RefreshToken,
  ExpiresIn = issued.ExpiresIn,
  Scope = issued.Scope
  };
  }

  private static (OAuthClient? Client, int Status) ResolveClient(HttpRequest request, IFormCollection form, IReadOnlyList<OAuthClient> clients)
  {
  string? id = null;
  string secret = "";
  var header = request.Headers.Authorization.ToString();
  if (header.StartsWith("Basic ", StringComparison.Ordinal))
  {
  var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
  var split = decoded.Split(':', 2);
  id = split[0];
  secret = split.Length > 1 ? split[1] : "";
  }
  else
  {
  id = form["client_id"].ToString();
  secret = form["client_secret"].ToString();
  }
  var client = clients.FirstOrDefault(item => item.Id == id);
  if (client == null)
  {
  return (null, 401);
  }
  if (client.Secret.Length > 0 && client.Secret != secret)
  {
  return (null, 401);
  }
  return (client, 200);
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- C1 without a token returns 401 and with a user token returns name.
- C2 with a server token returns 200 and with a user token returns 403.
- C3 client_credentials, password, and refresh_token each yield a token accepted by GET /uaa/users/current.
### STEP-009 Port the five AuthService test classes

Depends on: STEP-008.

Files:

- `tests/AuthService.Tests/AuthService.Tests.csproj`
- `tests/AuthService.Tests/AuthTests.cs`

Add AuthService.Tests with the Java method names contextLoads, shouldCreateNewUser, shouldFailWhenUserIsNotValid, shouldReturnCurrentUser, shouldSaveAndFindUserByName, shouldCreateUser, shouldFailWhenUserAlreadyExists, shouldLoadByUsernameWhenUserExists, and shouldFailToLoadByUsernameWhenUserNotExists. Add contract replay tests for C1, C2, and C3 against WebApplicationFactory and Mongo2Go.

exact_snippet (bundle):

~~~csharp
// FILE: tests/AuthService.Tests/AuthService.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
  <RootNamespace>AuthService.Tests</RootNamespace>
  <AssemblyName>AuthService.Tests</AssemblyName>
  <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />
  <PackageReference Include="xunit" Version="2.9.3" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.13" />
  <PackageReference Include="Mongo2Go" Version="3.1.3" />
  </ItemGroup>
  <ItemGroup>
  <ProjectReference Include="..\..\src\AuthService\AuthService.csproj" />
  </ItemGroup>
</Project>

// FILE: tests/AuthService.Tests/AuthTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AuthService.Controllers;
using AuthService.Data;
using AuthService.Domain;
using AuthService.Security;
using AuthService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Mongo2Go;
using MongoDB.Driver;
using PiggyMetrics.Shared.Json;
using PiggyMetrics.Shared.Mongo;

namespace AuthService.Tests;

public sealed class AuthApplicationFactory : WebApplicationFactory<Program>
{
  private readonly MongoDbRunner _runner = MongoDbRunner.Start();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
  builder.UseSetting("Piggy:Mongo:ConnectionString", _runner.ConnectionString);
  builder.UseSetting("Piggy:Mongo:Database", "piggymetrics");
  builder.UseSetting("ACCOUNT_SERVICE_PASSWORD", "account-secret");
  builder.UseSetting("STATISTICS_SERVICE_PASSWORD", "statistics-secret");
  builder.UseSetting("NOTIFICATION_SERVICE_PASSWORD", "notification-secret");
  }

  protected override void Dispose(bool disposing)
  {
  base.Dispose(disposing);
  _runner.Dispose();
  }
}

public class AuthServiceApplicationTests : IClassFixture<AuthApplicationFactory>
{
  private readonly AuthApplicationFactory _factory;

  public AuthServiceApplicationTests(AuthApplicationFactory factory)
  {
  _factory = factory;
  }

  [Fact]
  public void contextLoads()
  {
  using var client = _factory.CreateClient();
  Assert.NotNull(client);
  }
}

public class UserControllerTest
{
  private sealed class FakeUsers : IUserService
  {
  public void Create(User user)
  {
  }
  }

  [Fact]
  public void shouldCreateNewUser()
  {
  var controller = new UserController(new FakeUsers());
  var result = controller.CreateUser(new User { Username = "test", Password = "password" });
  Assert.IsType<OkResult>(result);
  }

  [Fact]
  public void shouldFailWhenUserIsNotValid()
  {
  var controller = new UserController(new FakeUsers());
  var result = controller.CreateUser(null);
  Assert.IsType<BadRequestResult>(result);
  }

  [Fact]
  public void shouldReturnCurrentUser()
  {
  var controller = new UserController(new FakeUsers());
  controller.ControllerContext = new ControllerContext
  {
  HttpContext = new DefaultHttpContext
  {
  User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test") }, "test"))
  }
  };
  var result = Assert.IsType<OkObjectResult>(controller.GetUser());
  var json = JsonSerializer.Serialize(result.Value, PiggyJson.Options);
  using var document = JsonDocument.Parse(json);
  Assert.Equal("test", document.RootElement.GetProperty("name").GetString());
  }
}

public sealed class MemoryUserRepository : IUserRepository
{
  public User? Stored { get; private set; }
  public int Saves { get; private set; }

  public User? FindById(string username)
  {
  return Stored != null && Stored.Username == username ? Stored : null;
  }

  public void Save(User user)
  {
  Saves += 1;
  Stored = user;
  }
}

public class UserServiceTest
{
  [Fact]
  public void shouldCreateUser()
  {
  var repository = new MemoryUserRepository();
  var service = new UserService(repository);
  var user = new User { Username = "name", Password = "password" };
  service.Create(user);
  Assert.Equal(1, repository.Saves);
  Assert.Same(user, repository.Stored);
  }

  [Fact]
  public void shouldFailWhenUserAlreadyExists()
  {
  var repository = new MemoryUserRepository();
  repository.Save(new User { Username = "name", Password = "kept" });
  var service = new UserService(repository);
  var user = new User { Username = "name", Password = "password" };
  var error = Assert.Throws<ArgumentException>(() => service.Create(user));
  Assert.Equal("user already exists: name", error.Message);
  }
}

public class MongoUserDetailsServiceTest
{
  [Fact]
  public void shouldLoadByUsernameWhenUserExists()
  {
  var repository = new MemoryUserRepository();
  var user = new User { Username = "name", Password = "password" };
  repository.Save(user);
  var service = new MongoUserDetailsService(repository);
  var loaded = service.LoadUserByUsername("name");
  Assert.Same(user, loaded);
  }

  [Fact]
  public void shouldFailToLoadByUsernameWhenUserNotExists()
  {
  var service = new MongoUserDetailsService(new MemoryUserRepository());
  Assert.Throws<UsernameNotFoundException>(() => service.LoadUserByUsername("name"));
  }
}

public class UserRepositoryTest : IDisposable
{
  private readonly MongoDbRunner _runner = MongoDbRunner.Start();

  [Fact]
  public void shouldSaveAndFindUserByName()
  {
  PiggyMongo.RegisterConventions();
  var database = new MongoClient(_runner.ConnectionString).GetDatabase("piggymetrics");
  var repository = new UserRepository(database);
  var user = new User { Username = "name", Password = "password" };
  repository.Save(user);
  var found = repository.FindById(user.Username);
  Assert.NotNull(found);
  Assert.Equal(user.Username, found!.Username);
  Assert.Equal(user.Password, found.Password);
  }

  public void Dispose()
  {
  _runner.Dispose();
  }
}

public class ContractReplayTests : IClassFixture<AuthApplicationFactory>
{
  private readonly HttpClient _client;

  public ContractReplayTests(AuthApplicationFactory factory)
  {
  _client = factory.CreateClient();
  }

  [Fact]
  public async Task C1_without_token_is_401_and_with_user_token_returns_name()
  {
  var anonymous = await _client.GetAsync("/uaa/users/current");
  Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

  await CreateUserAsync("ada", "secret");
  var token = await PasswordTokenAsync("ada", "secret");
  var request = new HttpRequestMessage(HttpMethod.Get, "/uaa/users/current");
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
  var response = await _client.SendAsync(request);
  Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  Assert.Equal("ada", document.RootElement.GetProperty("name").GetString());
  Assert.Equal("ui", document.RootElement.GetProperty("scope").GetString());
  }

  [Fact]
  public async Task C2_server_token_is_200_and_user_token_is_403()
  {
  var server = await ClientTokenAsync("account-service", "account-secret");
  var create = new HttpRequestMessage(HttpMethod.Post, "/uaa/users");
  create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.AccessToken);
  create.Content = JsonContent.Create(new { username = "grace", password = "secret" });
  var created = await _client.SendAsync(create);
  Assert.Equal(HttpStatusCode.OK, created.StatusCode);

  var user = await PasswordTokenAsync("grace", "secret");
  var denied = new HttpRequestMessage(HttpMethod.Post, "/uaa/users");
  denied.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
  denied.Content = JsonContent.Create(new { username = "other", password = "secret" });
  var response = await _client.SendAsync(denied);
  Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task C3_each_grant_issues_a_token_the_resource_check_accepts()
  {
  var clientToken = await ClientTokenAsync("statistics-service", "statistics-secret");
  Assert.Equal("bearer", clientToken.TokenType);
  Assert.Equal("server", clientToken.Scope);
  await AssertCurrentAccepts(clientToken.AccessToken);

  await CreateUserAsync("nia", "secret");
  var password = await PasswordTokenAsync("nia", "secret");
  Assert.Equal("ui", password.Scope);
  await AssertCurrentAccepts(password.AccessToken);

  var refresh = await RefreshAsync("browser", password.RefreshToken);
  Assert.False(string.IsNullOrEmpty(refresh.AccessToken));
  await AssertCurrentAccepts(refresh.AccessToken);
  }

  private async Task AssertCurrentAccepts(string accessToken)
  {
  var request = new HttpRequestMessage(HttpMethod.Get, "/uaa/users/current");
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
  var response = await _client.SendAsync(request);
  Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  private async Task CreateUserAsync(string username, string password)
  {
  var server = await ClientTokenAsync("notification-service", "notification-secret");
  var request = new HttpRequestMessage(HttpMethod.Post, "/uaa/users");
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", server.AccessToken);
  request.Content = JsonContent.Create(new { username, password });
  var response = await _client.SendAsync(request);
  response.EnsureSuccessStatusCode();
  }

  private async Task<TokenWire> ClientTokenAsync(string clientId, string secret)
  {
  var request = new HttpRequestMessage(HttpMethod.Post, "/uaa/oauth/token");
  var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret));
  request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
  request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
  {
  ["grant_type"] = "client_credentials",
  ["scope"] = "server"
  });
  var response = await _client.SendAsync(request);
  response.EnsureSuccessStatusCode();
  return (await response.Content.ReadFromJsonAsync<TokenWire>())!;
  }

  private async Task<TokenWire> PasswordTokenAsync(string username, string password)
  {
  var response = await _client.PostAsync("/uaa/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
  {
  ["grant_type"] = "password",
  ["username"] = username,
  ["password"] = password,
  ["client_id"] = "browser"
  }));
  response.EnsureSuccessStatusCode();
  return (await response.Content.ReadFromJsonAsync<TokenWire>())!;
  }

  private async Task<TokenWire> RefreshAsync(string clientId, string refreshToken)
  {
  var response = await _client.PostAsync("/uaa/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
  {
  ["grant_type"] = "refresh_token",
  ["refresh_token"] = refreshToken,
  ["client_id"] = clientId
  }));
  response.EnsureSuccessStatusCode();
  return (await response.Content.ReadFromJsonAsync<TokenWire>())!;
  }

  private sealed class TokenWire
  {
  public string access_token { get; set; } = "";
  public string token_type { get; set; } = "";
  public string refresh_token { get; set; } = "";
  public string scope { get; set; } = "";
  public string AccessToken => access_token;
  public string TokenType => token_type;
  public string RefreshToken => refresh_token;
  public string Scope => scope;
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- The nine ported tests pass.
- C1, C2, and C3 pass.
### STEP-010 Add StatisticsService documents, repository, rates client, and routes

Depends on: STEP-004, STEP-005, STEP-003.

Files:

- `src/StatisticsService/StatisticsService.csproj`
- `src/StatisticsService/Program.cs`
- `src/StatisticsService/appsettings.json`
- `src/StatisticsService/Domain/StatisticsDomain.cs`
- `src/StatisticsService/Data/DataPointRepository.cs`
- `src/StatisticsService/Clients/ExchangeRatesClient.cs`
- `src/StatisticsService/Services/StatisticsServices.cs`
- `src/StatisticsService/Controllers/StatisticsController.cs`

Host StatisticsService on port 7000 with path base /statistics and store datapoints in statistics-mongodb database piggymetrics. Keep field names incomes, expenses, saving, statistics, rates, and compound id account plus date. GET /current requires policy user. GET /{accountName} allows scope server or the literal account name demo. PUT /{accountName} requires scope server. The rates client calls GET {Piggy:Rates:Url}/latest and on failure returns base USD with an empty rates map. Normalize items with the Java double ratios and scale 4 half-up division.

exact_snippet (bundle):

~~~csharp
// FILE: src/StatisticsService/StatisticsService.csproj
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
  <RootNamespace>StatisticsService</RootNamespace>
  <AssemblyName>StatisticsService</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
  <ProjectReference Include="..\Shared\Shared.csproj" />

  </ItemGroup>
</Project>

// FILE: src/StatisticsService/Program.cs
using MongoDB.Driver;
using PiggyMetrics.Shared.Auth;
using PiggyMetrics.Shared.Health;
using PiggyMetrics.Shared.Json;
using PiggyMetrics.Shared.Mongo;
using StatisticsService.Clients;
using StatisticsService.Data;
using StatisticsService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(options =>
{
  options.JsonSerializerOptions.PropertyNamingPolicy = PiggyJson.Options.PropertyNamingPolicy;
  options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
  options.JsonSerializerOptions.DefaultIgnoreCondition = PiggyJson.Options.DefaultIgnoreCondition;
  options.JsonSerializerOptions.UnmappedMemberHandling = PiggyJson.Options.UnmappedMemberHandling;
  options.JsonSerializerOptions.NumberHandling = PiggyJson.Options.NumberHandling;
  foreach (var converter in PiggyJson.Options.Converters)
  {
  options.JsonSerializerOptions.Converters.Add(converter);
  }
});
builder.Services.AddPiggyHealth();
builder.Services.AddPiggyBearer(builder.Configuration);
builder.Services.AddSingleton(sp =>
{
  var configuration = sp.GetRequiredService<IConfiguration>();
  var connectionString = configuration["Piggy:Mongo:ConnectionString"];
  if (!string.IsNullOrEmpty(connectionString))
  {
  PiggyMongo.RegisterConventions();
  return new MongoClient(connectionString);
  }
  return PiggyMongo.CreateClient(
  configuration["Piggy:Mongo:Host"] ?? "statistics-mongodb",
  int.Parse(configuration["Piggy:Mongo:Port"] ?? "27017"),
  configuration["Piggy:Mongo:Username"] ?? "user",
  configuration["MONGODB_PASSWORD"] ?? configuration["Piggy:Mongo:Password"] ?? "");
});
builder.Services.AddSingleton(sp =>
{
  var configuration = sp.GetRequiredService<IConfiguration>();
  return PiggyMongo.Database(sp.GetRequiredService<MongoClient>(), configuration["Piggy:Mongo:Database"] ?? "piggymetrics");
});
builder.Services.AddSingleton<IDataPointRepository, DataPointRepository>();
RatesClientRegistration.AddRatesClient(builder.Services);
builder.Services.AddSingleton<IExchangeRatesService, ExchangeRatesService>();
builder.Services.AddSingleton<IStatisticsService, StatisticsApplicationService>();

var app = builder.Build();
app.UsePathBase("/statistics");
app.UseAuthentication();
app.UseAuthorization();
app.MapPiggyHealth();
app.MapControllers();
app.Run();

public partial class Program { }

// FILE: src/StatisticsService/appsettings.json
{
  "Kestrel": {
  "Endpoints": {
  "Http": {
  "Url": "http://0.0.0.0:7000"
  }
  }
  },
  "Piggy": {
  "PathBase": "/statistics",
  "ApplicationName": "statistics-service",
  "Config": {
  "Uri": "http://config:8888",
  "FailFast": true,
  "Username": "user"
  },
  "Auth": {
  "UserInfoUrl": "http://auth-service:5000/uaa/users/current",
  "TokenUrl": "http://auth-service:5000/uaa/oauth/token"
  },
  "Mongo": {
  "Host": "statistics-mongodb",
  "Port": 27017,
  "Username": "user",
  "Database": "piggymetrics",
  "Collection": "datapoints"
  },
  "Rates": {
  "Url": "https://api.exchangeratesapi.io"
  },
  "Client": {
  "ClientId": "statistics-service",
  "Scope": "server",
  "GrantType": "client_credentials"
  }
  }
}

// FILE: src/StatisticsService/Domain/StatisticsDomain.cs
using System.Globalization;
using MongoDB.Bson.Serialization.Attributes;

namespace StatisticsService.Domain;

public enum Currency
{
  USD,
  EUR,
  RUB
}

public static class CurrencyBase
{
  public static Currency GetBase() => Currency.USD;
}

public enum TimePeriod
{
  YEAR,
  QUARTER,
  MONTH,
  DAY,
  HOUR
}

public static class TimePeriodRatio
{
  public static decimal GetBaseRatio(TimePeriod period)
  {
  return period switch
  {
  TimePeriod.YEAR => Decimal.Parse("365.24250000000000682121026329696178436279296875", CultureInfo.InvariantCulture),
  TimePeriod.QUARTER => Decimal.Parse("91.310599999999993769961292855441570281982421875", CultureInfo.InvariantCulture),
  TimePeriod.MONTH => Decimal.Parse("30.436800000000001631406121305190026760101318359375", CultureInfo.InvariantCulture),
  TimePeriod.DAY => 1m,
  TimePeriod.HOUR => Decimal.Parse("0.0415999999999999980904163976447307504713535308837890625", CultureInfo.InvariantCulture),
  _ => throw new ArgumentOutOfRangeException(nameof(period))
  };
  }

  public static TimePeriod GetBase() => TimePeriod.DAY;
}

public sealed class Item
{
  public string Title { get; set; } = "";
  public decimal Amount { get; set; }
  public Currency Currency { get; set; }
  public TimePeriod Period { get; set; }
}

public sealed class Saving
{
  public decimal Amount { get; set; }
  public Currency Currency { get; set; }
  public decimal Interest { get; set; }
  public bool Deposit { get; set; }
  public bool Capitalization { get; set; }
}

public sealed class Account
{
  public List<Item> Incomes { get; set; } = new();
  public List<Item> Expenses { get; set; } = new();
  public Saving Saving { get; set; } = new();
}

public sealed class ExchangeRatesContainer
{
  public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Now);
  public Currency Base { get; set; } = CurrencyBase.GetBase();
  public Dictionary<string, decimal> Rates { get; set; } = new();
}

public enum StatisticMetric
{
  INCOMES_AMOUNT,
  EXPENSES_AMOUNT,
  SAVING_AMOUNT
}

public sealed class ItemMetric
{
  public ItemMetric()
  {
  }

  public ItemMetric(string title, decimal amount)
  {
  Title = title;
  Amount = amount;
  }

  public string Title { get; set; } = "";
  public decimal Amount { get; set; }

  public override bool Equals(object? obj)
  {
  return obj is ItemMetric other && string.Equals(Title, other.Title, StringComparison.OrdinalIgnoreCase);
  }

  public override int GetHashCode()
  {
  return Title.GetHashCode(StringComparison.Ordinal);
  }
}

public sealed class DataPointId
{
  public DataPointId()
  {
  }

  public DataPointId(string account, DateTime date)
  {
  Account = account;
  Date = date;
  }

  [BsonElement("account")]
  public string Account { get; set; } = "";

  [BsonElement("date")]
  public DateTime Date { get; set; }
}

public sealed class DataPoint
{
  [BsonId]
  public DataPointId Id { get; set; } = new();

  [BsonElement("incomes")]
  public HashSet<ItemMetric> Incomes { get; set; } = new();

  [BsonElement("expenses")]
  public HashSet<ItemMetric> Expenses { get; set; } = new();

  [BsonElement("statistics")]
  public Dictionary<StatisticMetric, decimal> Statistics { get; set; } = new();

  [BsonElement("rates")]
  public Dictionary<Currency, decimal> Rates { get; set; } = new();
}

// FILE: src/StatisticsService/Data/DataPointRepository.cs
using MongoDB.Driver;
using PiggyMetrics.Shared.Mongo;
using StatisticsService.Domain;

namespace StatisticsService.Data;

public interface IDataPointRepository
{
  void Save(DataPoint point);
  List<DataPoint> FindByIdAccount(string account);
}

public sealed class DataPointRepository : IDataPointRepository
{
  public const string CollectionName = "datapoints";
  private readonly IMongoCollection<DataPoint> _points;

  public DataPointRepository(IMongoDatabase database)
  {
  PiggyMongo.RegisterConventions();
  _points = database.GetCollection<DataPoint>(CollectionName);
  }

  public void Save(DataPoint point)
  {
  point.Id.Date = DateTime.SpecifyKind(point.Id.Date, DateTimeKind.Utc);
  _points.ReplaceOne(
  existing => existing.Id.Account == point.Id.Account && existing.Id.Date == point.Id.Date,
  point,
  new ReplaceOptions { IsUpsert = true });
  }

  public List<DataPoint> FindByIdAccount(string account)
  {
  return _points.Find(point => point.Id.Account == account).ToList();
  }
}

// FILE: src/StatisticsService/Clients/ExchangeRatesClient.cs
using System.Text.Json;
using PiggyMetrics.Shared.Http;
using PiggyMetrics.Shared.Json;
using StatisticsService.Domain;

namespace StatisticsService.Clients;

public interface IExchangeRatesClient
{
  ExchangeRatesContainer GetRates(Currency baseCurrency);
}

public sealed class ExchangeRatesClient : IExchangeRatesClient
{
  private readonly IHttpClientFactory _clients;
  private readonly string _ratesUrl;

  public ExchangeRatesClient(IHttpClientFactory clients, IConfiguration configuration)
  {
  _clients = clients;
  _ratesUrl = configuration["Piggy:Rates:Url"] ?? "https://api.exchangeratesapi.io";
  }

  public ExchangeRatesContainer GetRates(Currency baseCurrency)
  {
  try
  {
  var client = _clients.CreateClient("rates-client");
  var url = _ratesUrl.TrimEnd('/') + "/latest?base=" + baseCurrency;
  using var response = client.Send(new HttpRequestMessage(HttpMethod.Get, url));
  if (!response.IsSuccessStatusCode)
  {
  return ExchangeRatesClientFallback.Create();
  }
  using var stream = response.Content.ReadAsStream();
  var payload = JsonSerializer.Deserialize<RatesWire>(stream, PiggyJson.Options);
  return new ExchangeRatesContainer
  {
  Date = DateOnly.FromDateTime(DateTime.Now),
  Base = payload?.Base ?? CurrencyBase.GetBase(),
  Rates = payload?.Rates ?? new Dictionary<string, decimal>()
  };
  }
  catch (Exception)
  {
  return ExchangeRatesClientFallback.Create();
  }
  }
}

public static class ExchangeRatesClientFallback
{
  public static ExchangeRatesContainer Create()
  {
  return new ExchangeRatesContainer
  {
  Date = DateOnly.FromDateTime(DateTime.Now),
  Base = CurrencyBase.GetBase(),
  Rates = new Dictionary<string, decimal>()
  };
  }
}

public sealed class RatesWire
{
  public Currency Base { get; set; }
  public Dictionary<string, decimal> Rates { get; set; } = new();
}

public static class RatesClientRegistration
{
  public static void AddRatesClient(IServiceCollection services)
  {
  TypedClientBase.AddEdgeClient(services, "rates-client");
  services.AddSingleton<IExchangeRatesClient, ExchangeRatesClient>();
  }
}

// FILE: src/StatisticsService/Services/StatisticsServices.cs
using StatisticsService.Clients;
using StatisticsService.Data;
using StatisticsService.Domain;

namespace StatisticsService.Services;

public interface IExchangeRatesService
{
  Dictionary<Currency, decimal> GetCurrentRates();
  decimal Convert(Currency from, Currency to, decimal? amount);
}

public sealed class ExchangeRatesService : IExchangeRatesService
{
  private readonly IExchangeRatesClient _client;
  private ExchangeRatesContainer? _container;

  public ExchangeRatesService(IExchangeRatesClient client)
  {
  _client = client;
  }

  public Dictionary<Currency, decimal> GetCurrentRates()
  {
  if (_container == null || _container.Date != DateOnly.FromDateTime(DateTime.Now))
  {
  _container = _client.GetRates(CurrencyBase.GetBase());
  }
  return new Dictionary<Currency, decimal>
  {
  [Currency.EUR] = _container.Rates[Currency.EUR.ToString()],
  [Currency.RUB] = _container.Rates[Currency.RUB.ToString()],
  [Currency.USD] = 1m
  };
  }

  public decimal Convert(Currency from, Currency to, decimal? amount)
  {
  if (amount == null)
  {
  throw new ArgumentException("amount");
  }
  var rates = GetCurrentRates();
  var ratio = DecimalMath.Divide(rates[to], rates[from], 4);
  return amount.Value * ratio;
  }
}

public interface IStatisticsService
{
  List<DataPoint> FindByAccountName(string? accountName);
  DataPoint Save(string accountName, Account account);
}

public sealed class StatisticsApplicationService : IStatisticsService
{
  private readonly IDataPointRepository _repository;
  private readonly IExchangeRatesService _rates;

  public StatisticsApplicationService(IDataPointRepository repository, IExchangeRatesService rates)
  {
  _repository = repository;
  _rates = rates;
  }

  public List<DataPoint> FindByAccountName(string? accountName)
  {
  if (string.IsNullOrEmpty(accountName))
  {
  throw new ArgumentException("accountName");
  }
  return _repository.FindByIdAccount(accountName);
  }

  public DataPoint Save(string accountName, Account account)
  {
  var instant = DateOnly.FromDateTime(DateTime.Now).ToDateTime(TimeOnly.MinValue);
  var point = new DataPoint
  {
  Id = new DataPointId(accountName, DateTime.SpecifyKind(instant, DateTimeKind.Local)),
  Incomes = account.Incomes.Select(CreateItemMetric).ToHashSet(),
  Expenses = account.Expenses.Select(CreateItemMetric).ToHashSet(),
  Rates = _rates.GetCurrentRates()
  };
  point.Statistics = CreateStatisticMetrics(point.Incomes, point.Expenses, account.Saving);
  _repository.Save(point);
  return point;
  }

  private Dictionary<StatisticMetric, decimal> CreateStatisticMetrics(HashSet<ItemMetric> incomes, HashSet<ItemMetric> expenses, Saving saving)
  {
  var savingAmount = _rates.Convert(saving.Currency, CurrencyBase.GetBase(), saving.Amount);
  var expensesAmount = expenses.Select(item => item.Amount).Aggregate(0m, (sum, amount) => sum + amount);
  var incomesAmount = incomes.Select(item => item.Amount).Aggregate(0m, (sum, amount) => sum + amount);
  return new Dictionary<StatisticMetric, decimal>
  {
  [StatisticMetric.EXPENSES_AMOUNT] = expensesAmount,
  [StatisticMetric.INCOMES_AMOUNT] = incomesAmount,
  [StatisticMetric.SAVING_AMOUNT] = savingAmount
  };
  }

  private ItemMetric CreateItemMetric(Item item)
  {
  var converted = _rates.Convert(item.Currency, CurrencyBase.GetBase(), item.Amount);
  var amount = DecimalMath.Divide(converted, TimePeriodRatio.GetBaseRatio(item.Period), 4);
  return new ItemMetric(item.Title, amount);
  }
}

public static class DecimalMath
{
  public static decimal Divide(decimal amount, decimal divisor, int scale)
  {
  return Math.Round(amount / divisor, scale, MidpointRounding.AwayFromZero);
  }
}

// FILE: src/StatisticsService/Controllers/StatisticsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StatisticsService.Domain;
using StatisticsService.Services;

namespace StatisticsService.Controllers;

[ApiController]
public sealed class StatisticsController : ControllerBase
{
  private readonly IStatisticsService _statistics;

  public StatisticsController(IStatisticsService statistics)
  {
  _statistics = statistics;
  }

  [Authorize(Policy = "user")]
  [HttpGet("current")]
  public IActionResult GetCurrentAccountStatistics()
  {
  return Ok(_statistics.FindByAccountName(User.Identity?.Name));
  }

  [HttpGet("{accountName}")]
  public IActionResult GetStatisticsByAccountName(string accountName)
  {
  var scope = User.FindFirst("scope")?.Value ?? "";
  var server = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("server");
  if (accountName == "demo" || server)
  {
  return Ok(_statistics.FindByAccountName(accountName));
  }
  if (User.Identity?.IsAuthenticated != true)
  {
  return Unauthorized();
  }
  return Forbid();
  }

  [Authorize(Policy = "server")]
  [HttpPut("{accountName}")]
  public IActionResult SaveAccountStatistics(string accountName, [FromBody] Account? account)
  {
  if (account == null || account.Saving == null || account.Incomes == null || account.Expenses == null)
  {
  return BadRequest();
  }
  if (account.Incomes.Any(item => item.Title.Length < 1 || item.Title.Length > 20))
  {
  return BadRequest();
  }
  _statistics.Save(accountName, account);
  return Ok();
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- C8 missing token returns 401 and a user token returns 200.
- C9 server token returns 200, user token returns 403, and demo without a token returns 200.
- C10 server token returns 200 and user token returns 403.
- A down rates host returns an empty rates map and base USD.
### STEP-011 Port the six StatisticsService test classes

Depends on: STEP-010.

Files:

- `tests/StatisticsService.Tests/StatisticsService.Tests.csproj`
- `tests/StatisticsService.Tests/StatisticsTests.cs`

Add StatisticsService.Tests with contextLoads, shouldRetrieveExchangeRates, shouldRetrieveExchangeRatesForSpecifiedCurrency, shouldGetStatisticsByAccountName, shouldGetCurrentAccountStatistics, shouldSaveAccountStatistics, shouldSaveDataPoint, shouldRewriteDataPointWithinADay, shouldReturnCurrentRatesWhenContainerIsEmptySoFar, shouldNotRequestRatesWhenTodaysContainerAlreadyExists, shouldConvertCurrency, shouldFailToConvertWhenAmountIsNull, shouldFindDataPointListByAccountName, shouldFailToFindDataPointWhenAccountNameIsNull, shouldFailToFindDataPointWhenAccountNameIsEmpty, and the service shouldSaveDataPoint. Keep the Java expected amounts 298.9802, 17.8861, 1250, 11.6361, and 6.25.

exact_snippet (bundle):

~~~csharp
// FILE: tests/StatisticsService.Tests/StatisticsService.Tests.csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
  <RootNamespace>StatisticsService.Tests</RootNamespace>
  <AssemblyName>StatisticsService.Tests</AssemblyName>
  <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.11.0" />
  <PackageReference Include="xunit" Version="2.9.3" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.13" />
  <PackageReference Include="Mongo2Go" Version="3.1.3" />
  </ItemGroup>
  <ItemGroup>
  <ProjectReference Include="..\..\src\StatisticsService\StatisticsService.csproj" />
  </ItemGroup>
</Project>

// FILE: tests/StatisticsService.Tests/StatisticsTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mongo2Go;
using MongoDB.Driver;
using StatisticsService.Clients;
using StatisticsService.Data;
using StatisticsService.Domain;
using StatisticsService.Services;
using PiggyMetrics.Shared.Mongo;

namespace StatisticsService.Tests;

public sealed class StatisticsApplicationFactory : WebApplicationFactory<Program>
{
  private readonly MongoDbRunner _runner = MongoDbRunner.Start();

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
  builder.UseSetting("Piggy:Mongo:ConnectionString", _runner.ConnectionString);
  builder.UseSetting("Piggy:Mongo:Database", "piggymetrics");
  builder.UseSetting("Piggy:Auth:UserInfoUrl", "http://127.0.0.1:9/uaa/users/current");
  }

  protected override void Dispose(bool disposing)
  {
  base.Dispose(disposing);
  _runner.Dispose();
  }
}

public class StatisticsServiceApplicationTests : IClassFixture<StatisticsApplicationFactory>
{
  private readonly StatisticsApplicationFactory _factory;

  public StatisticsServiceApplicationTests(StatisticsApplicationFactory factory)
  {
  _factory = factory;
  }

  [Fact]
  public void contextLoads()
  {
  using var client = _factory.CreateClient();
  Assert.NotNull(client);
  }
}

public class ExchangeRatesClientTest
{
  [Fact]
  public void shouldRetrieveExchangeRates()
  {
  var container = Client().GetRates(CurrencyBase.GetBase());
  Assert.Equal(DateOnly.FromDateTime(DateTime.Now), container.Date);
  Assert.Equal(CurrencyBase.GetBase(), container.Base);
  Assert.NotNull(container.Rates);
  Assert.True(container.Rates.ContainsKey(Currency.USD.ToString()));
  Assert.True(container.Rates.ContainsKey(Currency.EUR.ToString()));
  Assert.True(container.Rates.ContainsKey(Currency.RUB.ToString()));
  }

  [Fact]
  public void shouldRetrieveExchangeRatesForSpecifiedCurrency()
  {
  var requested = Currency.EUR;
  var container = Client().GetRates(CurrencyBase.GetBase());
  Assert.Equal(DateOnly.FromDateTime(DateTime.Now), container.Date);
  Assert.Equal(CurrencyBase.GetBase(), container.Base);
  Assert.NotNull(container.Rates);
  Assert.True(container.Rates.ContainsKey(requested.ToString()));
  }

  [Fact]
  public void shouldReturnFallbackWhenCalleeIsDown()
  {
  var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
  {
  ["Piggy:Rates:Url"] = "http://127.0.0.1:9"
  }).Build();
  var services = new ServiceCollection();
  services.AddSingleton<IConfiguration>(configuration);
  RatesClientRegistration.AddRatesClient(services);
  var client = services.BuildServiceProvider().GetRequiredService<IExchangeRatesClient>();
  var container = client.GetRates(Currency.USD);
  Assert.Equal(Currency.USD, container.Base);
  Assert.Empty(container.Rates);
  Assert.Equal(DateOnly.FromDateTime(DateTime.Now), container.Date);
  }

  private static IExchangeRatesClient Client()
  {
  var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
  {
  ["Piggy:Rates:Url"] = "https://api.exchangeratesapi.io"
  }).Build();
  var services = new ServiceCollection();
  services.AddSingleton<IConfiguration>(configuration);
  RatesClientRegistration.AddRatesClient(services);
  return services.BuildServiceProvider().GetRequiredService<IExchangeRatesClient>();
  }
}

public class StatisticsControllerTest
{
  private sealed class FakeStatistics : IStatisticsService
  {
  public List<DataPoint> Rows { get; } = new();
  public int Saves { get; private set; }
  public string? SavedName { get; private set; }

  public List<DataPoint> FindByAccountName(string? accountName) => Rows;

  public DataPoint Save(string accountName, Account account)
  {
  Saves += 1;
  SavedName = accountName;
  return new DataPoint();
  }
  }

  [Fact]
  public void shouldGetStatisticsByAccountName()
  {
  var point = new DataPoint { Id = new DataPointId("test", DateTime.UtcNow) };
  var fake = new FakeStatistics();
  fake.Rows.Add(point);
  var controller = new StatisticsService.Controllers.StatisticsController(fake);
  var result = Assert.IsType<OkObjectResult>(controller.GetStatisticsByAccountName("test"));
  var rows = Assert.IsAssignableFrom<List<DataPoint>>(result.Value);
  Assert.Equal("test", rows[0].Id.Account);
  }

  [Fact]
  public void shouldGetCurrentAccountStatistics()
  {
  var point = new DataPoint { Id = new DataPointId("test", DateTime.UtcNow) };
  var fake = new FakeStatistics();
  fake.Rows.Add(point);
  var controller = new StatisticsService.Controllers.StatisticsController(fake);
  controller.ControllerContext = new ControllerContext
  {
  HttpContext = new DefaultHttpContext
  {
  User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test") }, "test"))
  }
  };
  var result = Assert.IsType<OkObjectResult>(controller.GetCurrentAccountStatistics());
  var rows = Assert.IsAssignableFrom<List<DataPoint>>(result.Value);
  Assert.Equal("test", rows[0].Id.Account);
  }

  [Fact]
  public void shouldSaveAccountStatistics()
  {
  var account = new Account
  {
  Saving = new Saving { Amount = 1500m, Currency = Currency.USD, Interest = 3.32m, Deposit = true, Capitalization = false },
  Expenses = new List<Item> { new() { Title = "Grocery", Amount = 10m, Currency = Currency.USD, Period = TimePeriod.DAY } },
  Incomes = new List<Item> { new() { Title = "Salary", Amount = 9100m, Currency = Currency.USD, Period = TimePeriod.MONTH } }
  };
  var fake = new FakeStatistics();
  var controller = new StatisticsService.Controllers.StatisticsController(fake);
  var result = controller.SaveAccountStatistics("test", account);
  Assert.IsType<OkResult>(result);
  Assert.Equal(1, fake.Saves);
  }
}

public class DataPointRepositoryTest : IDisposable
{
  private readonly MongoDbRunner _runner = MongoDbRunner.Start();

  [Fact]
  public void shouldSaveDataPoint()
  {
  var repository = Repo();
  var pointId = new DataPointId("test-account", DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc));
  var point = new DataPoint
  {
  Id = pointId,
  Incomes = new HashSet<ItemMetric> { new("salary", 20000m) },
  Expenses = new HashSet<ItemMetric> { new("grocery", 1000m), new("vacation", 2000m) },
  Statistics = new Dictionary<StatisticMetric, decimal>
  {
  [StatisticMetric.SAVING_AMOUNT] = 400000m,
  [StatisticMetric.INCOMES_AMOUNT] = 20000m,
  [StatisticMetric.EXPENSES_AMOUNT] = 3000m
  }
  };
  repository.Save(point);
  var points = repository.FindByIdAccount(pointId.Account);
  Assert.Single(points);
  Assert.Equal(pointId.Date, points[0].Id.Date);
  Assert.Equal(point.Statistics.Count, points[0].Statistics.Count);
  Assert.Equal(point.Incomes.Count, points[0].Incomes.Count);
  Assert.Equal(point.Expenses.Count, points[0].Expenses.Count);
  }

  [Fact]
  public void shouldRewriteDataPointWithinADay()
  {
  var repository = Repo();
  var pointId = new DataPointId("test-account", DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc));
  repository.Save(new DataPoint
  {
  Id = pointId,
  Statistics = new Dictionary<StatisticMetric, decimal> { [StatisticMetric.SAVING_AMOUNT] = 100m }
  });
  repository.Save(new DataPoint
  {
  Id = new DataPointId(pointId.Account, pointId.Date),
  Statistics = new Dictionary<StatisticMetric, decimal> { [StatisticMetric.SAVING_AMOUNT] = 200m }
  });
  var points = repository.FindByIdAccount(pointId.Account);
  Assert.Single(points);
  Assert.Equal(200m, points[0].Statistics[StatisticMetric.SAVING_AMOUNT]);
  }

  private DataPointRepository Repo()
  {
  PiggyMongo.RegisterConventions();
  var database = new MongoClient(_runner.ConnectionString).GetDatabase("piggymetrics");
  return new DataPointRepository(database);
  }

  public void Dispose() => _runner.Dispose();
}

public sealed class FakeRatesClient : IExchangeRatesClient
{
  public int Calls { get; private set; }
  public ExchangeRatesContainer Container { get; } = new()
  {
  Rates = new Dictionary<string, decimal>
  {
  [Currency.EUR.ToString()] = 0.8m,
  [Currency.RUB.ToString()] = 80m
  }
  };

  public ExchangeRatesContainer GetRates(Currency baseCurrency)
  {
  Calls += 1;
  return Container;
  }
}

public class ExchangeRatesServiceImplTest
{
  [Fact]
  public void shouldReturnCurrentRatesWhenContainerIsEmptySoFar()
  {
  var client = new FakeRatesClient();
  var service = new ExchangeRatesService(client);
  var result = service.GetCurrentRates();
  Assert.Equal(1, client.Calls);
  Assert.Equal(client.Container.Rates[Currency.EUR.ToString()], result[Currency.EUR]);
  Assert.Equal(client.Container.Rates[Currency.RUB.ToString()], result[Currency.RUB]);
  Assert.Equal(1m, result[Currency.USD]);
  }

  [Fact]
  public void shouldNotRequestRatesWhenTodaysContainerAlreadyExists()
  {
  var client = new FakeRatesClient();
  var service = new ExchangeRatesService(client);
  service.GetCurrentRates();
  service.GetCurrentRates();
  Assert.Equal(1, client.Calls);
  }

  [Fact]
  public void shouldConvertCurrency()
  {
  var service = new ExchangeRatesService(new FakeRatesClient());
  var result = service.Convert(Currency.RUB, Currency.USD, 100m);
  Assert.Equal(0, 1.25m.CompareTo(result));
  }

  [Fact]
  public void shouldFailToConvertWhenAmountIsNull()
  {
  var service = new ExchangeRatesService(new FakeRatesClient());
  Assert.Throws<ArgumentException>(() => service.Convert(Currency.EUR, Currency.RUB, null));
  }
}

public sealed class ScriptedRates : IExchangeRatesService
{
  private static readonly Dictionary<Currency, decimal> Rates = new()
  {
  [Currency.EUR] = 0.8m,
  [Currency.RUB] = 80m,
  [Currency.USD] = 1m
  };

  public int CurrentCalls { get; private set; }

  public Dictionary<Currency, decimal> GetCurrentRates()
  {
  CurrentCalls += 1;
  return new Dictionary<Currency, decimal>(Rates);
  }

  public decimal Convert(Currency from, Currency to, decimal? amount)
  {
  return DecimalMath.Divide(amount!.Value, Rates[from], 4);
  }
}

public sealed class MemoryPoints : IDataPointRepository
{
  public DataPoint? Saved { get; private set; }
  public int Saves { get; private set; }
  public List<DataPoint> Rows { get; } = new();

  public void Save(DataPoint point)
  {
  Saves += 1;
  Saved = point;
  }

  public List<DataPoint> FindByIdAccount(string account) => Rows;
}

public class StatisticsServiceImplTest
{
  [Fact]
  public void shouldFindDataPointListByAccountName()
  {
  var repository = new MemoryPoints();
  var row = new DataPoint();
  repository.Rows.Add(row);
  var service = new StatisticsApplicationService(repository, new ScriptedRates());
  var result = service.FindByAccountName("test");
  Assert.Equal(repository.Rows, result);
  }

  [Fact]
  public void shouldFailToFindDataPointWhenAccountNameIsNull()
  {
  var service = new StatisticsApplicationService(new MemoryPoints(), new ScriptedRates());
  Assert.Throws<ArgumentException>(() => service.FindByAccountName(null));
  }

  [Fact]
  public void shouldFailToFindDataPointWhenAccountNameIsEmpty()
  {
  var service = new StatisticsApplicationService(new MemoryPoints(), new ScriptedRates());
  Assert.Throws<ArgumentException>(() => service.FindByAccountName(""));
  }

  [Fact]
  public void shouldSaveDataPoint()
  {
  var repository = new MemoryPoints();
  var rates = new ScriptedRates();
  var service = new StatisticsApplicationService(repository, rates);
  var account = new Account
  {
  Incomes = new List<Item> { new() { Title = "Salary", Amount = 9100m, Currency = Currency.USD, Period = TimePeriod.MONTH } },
  Expenses = new List<Item>
  {
  new() { Title = "Grocery", Amount = 500m, Currency = Currency.RUB, Period = TimePeriod.DAY },
  new() { Title = "Vacation", Amount = 3400m, Currency = Currency.EUR, Period = TimePeriod.YEAR }
  },
  Saving = new Saving { Amount = 1000m, Currency = Currency.EUR, Interest = 3.2m, Deposit = true, Capitalization = false }
  };
  var dataPoint = service.Save("test", account);
  Assert.Equal("test", dataPoint.Id.Account);
  var expectedDate = DateTime.SpecifyKind(DateOnly.FromDateTime(DateTime.Now).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
  Assert.Equal(expectedDate, dataPoint.Id.Date);
  Assert.Equal(0, 17.8861m.CompareTo(dataPoint.Statistics[StatisticMetric.EXPENSES_AMOUNT]));
  Assert.Equal(0, 298.9802m.CompareTo(dataPoint.Statistics[StatisticMetric.INCOMES_AMOUNT]));
  Assert.Equal(0, 1250m.CompareTo(dataPoint.Statistics[StatisticMetric.SAVING_AMOUNT]));
  Assert.Equal(0, 298.9802m.CompareTo(dataPoint.Incomes.Single(item => item.Title == "Salary").Amount));
  Assert.Equal(0, 11.6361m.CompareTo(dataPoint.Expenses.Single(item => item.Title == "Vacation").Amount));
  Assert.Equal(0, 6.25m.CompareTo(dataPoint.Expenses.Single(item => item.Title == "Grocery").Amount));
  Assert.Equal(rates.GetCurrentRates(), dataPoint.Rates);
  Assert.Equal(1, repository.Saves);
  }
}

public class StatisticsContractTests : IAsyncLifetime
{
  private WebApplication? _auth;
  private WebApplication? _rates;
  private StatisticsApplicationFactory? _factory;
  private HttpClient? _client;

  public async Task InitializeAsync()
  {
  var ratesBuilder = WebApplication.CreateBuilder();
  ratesBuilder.WebHost.UseUrls("http://127.0.0.1:0");
  _rates = ratesBuilder.Build();
  _rates.MapGet("/latest", () => Results.Text("{\"base\":\"USD\",\"rates\":{\"EUR\":0.8,\"RUB\":80,\"USD\":1}}", "application/json"));
  await _rates.StartAsync();
  var ratesAddress = _rates.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
  .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.First();

  var authBuilder = WebApplication.CreateBuilder();
  authBuilder.WebHost.UseUrls("http://127.0.0.1:0");
  _auth = authBuilder.Build();
  _auth.MapGet("/uaa/users/current", (HttpRequest request) =>
  {
  var header = request.Headers.Authorization.ToString();
  if (header == "Bearer server-token")
  {
  return Results.Text("{\"name\":\"statistics-service\",\"scope\":\"server\"}", "application/json");
  }
  if (header == "Bearer user-token")
  {
  return Results.Text("{\"name\":\"ada\",\"scope\":\"ui\"}", "application/json");
  }
  return Results.Text("{\"error\":\"invalid_token\"}", "application/json", Encoding.UTF8, StatusCodes.Status401Unauthorized);
  });
  await _auth.StartAsync();
  var address = _auth.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>()
  .Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>()!.Addresses.First();
  _factory = new StatisticsContractFactory(address, ratesAddress);
  _client = _factory.CreateClient();
  }

  [Fact]
  public async Task C8_user_token_returns_200_and_missing_token_returns_401()
  {
  var missing = await _client!.GetAsync("/statistics/current");
  Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
  var request = new HttpRequestMessage(HttpMethod.Get, "/statistics/current");
  request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
  var response = await _client.SendAsync(request);
  Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
  Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
  }

  [Fact]
  public async Task C9_server_token_returns_200_user_token_returns_403_demo_without_token_returns_200()
  {
  var demo = await _client!.GetAsync("/statistics/demo");
  Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
  var user = new HttpRequestMessage(HttpMethod.Get, "/statistics/ada");
  user.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
  var denied = await _client.SendAsync(user);
  Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
  var server = new HttpRequestMessage(HttpMethod.Get, "/statistics/ada");
  server.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "server-token");
  var allowed = await _client.SendAsync(server);
  Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
  }

  [Fact]
  public async Task C10_server_token_returns_200_and_user_token_returns_403()
  {
  var body = new StringContent("{\"incomes\":[{\"title\":\"Salary\",\"amount\":1,\"currency\":\"USD\",\"period\":\"MONTH\"}],\"expenses\":[{\"title\":\"Grocery\",\"amount\":1,\"currency\":\"USD\",\"period\":\"DAY\"}],\"saving\":{\"amount\":1,\"currency\":\"USD\",\"interest\":1,\"deposit\":true,\"capitalization\":false}}", Encoding.UTF8, "application/json");
  var user = new HttpRequestMessage(HttpMethod.Put, "/statistics/ada") { Content = body };
  user.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "user-token");
  var denied = await _client!.SendAsync(user);
  Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
  var serverBody = new StringContent("{\"incomes\":[{\"title\":\"Salary\",\"amount\":1,\"currency\":\"USD\",\"period\":\"MONTH\"}],\"expenses\":[{\"title\":\"Grocery\",\"amount\":1,\"currency\":\"USD\",\"period\":\"DAY\"}],\"saving\":{\"amount\":1,\"currency\":\"USD\",\"interest\":1,\"deposit\":true,\"capitalization\":false}}", Encoding.UTF8, "application/json");
  var server = new HttpRequestMessage(HttpMethod.Put, "/statistics/ada") { Content = serverBody };
  server.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "server-token");
  var allowed = await _client.SendAsync(server);
  Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
  }

  public async Task DisposeAsync()
  {
  _client?.Dispose();
  if (_factory != null)
  {
  await _factory.DisposeAsync();
  }
  if (_auth != null)
  {
  await _auth.StopAsync();
  }
  if (_rates != null)
  {
  await _rates.StopAsync();
  }
  }

  private sealed class StatisticsContractFactory : WebApplicationFactory<Program>
  {
  private readonly MongoDbRunner _runner = MongoDbRunner.Start();
  private readonly string _authAddress;
  private readonly string _ratesAddress;

  public StatisticsContractFactory(string authAddress, string ratesAddress)
  {
  _authAddress = authAddress;
  _ratesAddress = ratesAddress;
  }

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
  builder.UseSetting("Piggy:Mongo:ConnectionString", _runner.ConnectionString);
  builder.UseSetting("Piggy:Mongo:Database", "piggymetrics");
  builder.UseSetting("Piggy:Auth:UserInfoUrl", _authAddress + "/uaa/users/current");
  builder.UseSetting("Piggy:Rates:Url", _ratesAddress);
  }

  protected override void Dispose(bool disposing)
  {
  base.Dispose(disposing);
  _runner.Dispose();
  }
  }
}
~~~

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Acceptance:

- The sixteen ported tests pass.
- C8, C9, and C10 pass.
- shouldReturnFallbackWhenCalleeIsDown passes.
## Verify

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

Shared bearer tests accept an issued token and refuse a foreign token. AuthService contract tests cover C1, C2, and C3. StatisticsService contract tests cover C8, C9, C10, and the rates fallback.

## Out of scope

AccountService endpoints, NotificationService mail and cron jobs, and Gateway Zuul routes stay out of this package. The shell projects exist so the solution builds.
