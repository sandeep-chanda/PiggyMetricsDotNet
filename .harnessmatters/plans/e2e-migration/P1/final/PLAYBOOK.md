---
name: 'PiggyMetricsDotNet P1: solution skeleton and Shared'
overview: 'Port plan P1 of the PiggyMetrics Java to .NET migration: stand up the solution with one project
  per runtime service plus Shared, and build the Shared library the later service plans depend on - the
  bearer handler that resolves a token against the source authorization server, the Mongo conventions
  that keep the existing documents readable, the typed-client base at the source''s 10000 ms timeout with
  a client-credentials token cache, and the JSON and health contracts. Every file below is pinned as complete
  final text taken from a reference implementation that builds with zero warnings and passes 24 of 24
  tests.'
todos:
- id: step-001
  content: 'STEP-001 (P1.T1) - Pin the repository build contract: target framework and package versions'
  status: pending
- id: step-002
  content: STEP-002 (P1.T1) - Create the Shared class library and pin the source's timeouts
  status: pending
- id: step-003
  content: STEP-003 (P1.T2) - Port the bearer handler that resolves a token against the authorization
    server
  status: pending
- id: step-004
  content: STEP-004 (P1.T2) - Register the resource server and the user and server authorization policies
  status: pending
- id: step-005
  content: STEP-005 (P1.T3) - Port the store conventions and the decimal serializer that match the documents
    on disk
  status: pending
- id: step-006
  content: STEP-006 (P1.T3) - Register the Mongo database handle from the source's connection settings
  status: pending
- id: step-007
  content: STEP-007 (P1.T4) - Port the client-credentials token cache and the outbound bearer handler
  status: pending
- id: step-008
  content: STEP-008 (P1.T4) - Expose the typed-client base so each edge gets one HTTP client at the source's
    timeout
  status: pending
- id: step-009
  content: 'STEP-009 (P1.T5) - Port the JSON options: enum names, ignored unknown fields, epoch-millisecond
    dates'
  status: pending
- id: step-010
  content: STEP-010 (P1.T5) - Port the actuator health endpoint with the source's path, body and status
    codes
  status: pending
- id: step-011
  content: 'STEP-011 (P1.T5) - Compose the service defaults: port, context path, resource server, JSON
    and health'
  status: pending
- id: step-012
  content: STEP-012 (P1.T1) - Create the five runtime service projects on the source's ports and context
    paths
  status: pending
- id: step-013
  content: STEP-013 (P1.T2) - Add the Shared test project that proves the plan's done-when clause
  status: pending
- id: step-014
  content: STEP-014 (P1.T1) - Wire the solution and prove every project builds and the suite is green
  status: pending
isProject: true
---

# PiggyMetricsDotNet P1: solution skeleton and Shared

Plan key P1 of spec `E2E migration`, target repo `sandeep-chanda/PiggyMetricsDotNet`. The migration source is `github.com/sqshq/piggymetrics` at commit `6bb2cf9ddbca980b664d3edbb6ff775d75369278`.

## What this plan delivers

P1 is the substrate every later service plan builds on, so it is worth being precise about what lands. Fourteen steps create: the root build contract, the Shared class library, the bearer handler that validates a token the way the source resource servers do, the `user` and `server` authorization policies, the Mongo conventions and decimal serializer that keep documents the Java services wrote readable, the Mongo database registration, the client-credentials token cache and the per-edge typed-client base, the Jackson-compatible JSON options, the actuator health endpoint, the service defaults that enforce the source's port and context path, the five runtime service projects, the Shared test suite, and the solution wiring.

Todos map one-to-one onto STEP ids: every H2 below named `STEP-0nn` is the body of the matching todo, and `final/DAG.yaml` carries the same ids with their dependency edges.

## Source anchors this plan reads from

- `pom.xml:41-51` and `account-service/pom.xml` - the module list and the inherited build contract.
- `account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java` and `.../service/security/CustomUserInfoTokenServices.java` - how a token is validated.
- `account-service/src/main/java/com/piggymetrics/account/repository/AccountRepository.java`, `.../domain/Account.java`, `.../domain/Item.java`, `.../domain/Saving.java` and `mongodb/dump/account-service-dump.js` - what the documents actually look like.
- `config/src/main/resources/shared/application.yml` - the 10000 ms Hystrix timeout and the user-info URI shared by every service.
- `config/src/main/resources/shared/{auth,account,statistics,notification}-service.yml` and `gateway.yml` - ports, context paths, store hosts and OAuth2 client settings.
- `config/Dockerfile:7` and `docker-compose.yml` - the actuator health probe the orchestration gates on.

## Decisions already made (do not reopen)

- **D-002 net8.0.** Pinned once in `Directory.Build.props`; proven by a green build on SDK 8.0.425.
- **D-004 user-info token validation, not JWT.** The source's tokens are opaque and held in an `InMemoryTokenStore`; every resource server resolves them through `CustomUserInfoTokenServices`. A local JWT validator would accept and refuse a different set of tokens.
- **D-006 decimals as BSON strings.** `spring-data-mongodb` 2.0.x registers `BigDecimalToStringConverter`, so every amount the Java services wrote is a string. Writing `Decimal128` would produce documents a still-running Java service cannot read.
- **D-009 token cache refreshes exactly at expiry, no skew.** Spring's OAuth2 client context does the same; a skew would change the request pattern the authorization server sees.
- **D-010 Jackson-shaped JSON.** Camel-case names, enum names, unknown members skipped, nulls written, dates as epoch milliseconds, because no source service overrides Jackson.
- **D-012 a prefix guard in front of `UsePathBase`.** `UsePathBase` alone still serves the unprefixed path; Spring's `server.servlet.context-path` answers 404. Measured both ways on a running service.
- **D-003 no Eureka and no config server.** No P1 todo names them and P1.T1 names five runtime services.

## Verification that already ran

These are observations from a reference implementation built during planning, not predictions:

- `dotnet build PiggyMetrics.sln -warnaserror` reported `0 Warning(s)` and `0 Error(s)`.
- `dotnet test PiggyMetrics.sln` reported `Passed! - Failed: 0, Passed: 24`.
- The running account service answered `200` with `{"status":"UP"}` on `/accounts/actuator/health`.
- The same service answered `404` on `/actuator/health` and on `/accountsfoo/actuator/health`.

The target repository itself was not written to during planning.

## STEP-001 (P1.T1) - Pin the repository build contract: target framework and package versions

Depends on: nothing.

**Why.** P1.T1 requires one .NET solution carrying a project per runtime service plus Shared. The source pins its whole build contract once in the parent pom (account-service/pom.xml inherits groupId/version/java.version from pom.xml), so the port pins framework and package versions once at the repository root rather than per project. Decision D-001, D-002.

**Currently.** Source pom.xml lines 1-50 declare <java.version>1.8</java.version>, <project.build.sourceEncoding>UTF-8</project.build.sourceEncoding> and a <dependencyManagement> import of spring-cloud-dependencies Finchley.RELEASE; account-service/pom.xml declares only its own artifactId and inherits the rest. Target repo PiggyMetricsDotNet currently holds README.md and AGENTS.md only - no build files exist.

**Change.** Create Directory.Build.props and Directory.Packages.props at the repository root. Directory.Build.props fixes net8.0, C# 12, nullable reference types, implicit usings and warnings-as-errors for every project in the tree, mirroring the single java.version 1.8 / UTF-8 contract the source declares once in its parent pom. Directory.Packages.props turns on central package management and fixes the exact NuGet versions that the Shared library and the Shared test project consume, mirroring the source dependencyManagement block.

**Files.**
- `Directory.Build.props`
- `Directory.Packages.props`

exact_snippet (`Directory.Build.props`):

~~~xml
<Project>
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
~~~

exact_snippet (`Directory.Packages.props`):

~~~xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MongoDB.Driver" Version="2.28.0" />
    <PackageVersion Include="Microsoft.Extensions.Http" Version="8.0.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
~~~

exact_commands:

~~~bash
dotnet --version
~~~

**Invariants.**
- TargetFramework is net8.0 for every project in the repository; no project overrides it.
- ManagePackageVersionsCentrally is true, so no csproj carries a Version attribute on PackageReference.
- TreatWarningsAsErrors is true, so a warning in any later plan fails the build.
- No InvariantGlobalization property is set: the serializers pin CultureInfo.InvariantCulture explicitly instead.

**Edge cases.**
- A machine without the .NET 8 SDK fails at dotnet --version; install the .NET 8 SDK before continuing.
- A build host without ICU aborts the SDK with NETSDK1188 locale errors; install libicu before building.

**Do not.**
- Do not add a global.json pinning an SDK patch version; the plan was proven on 8.0.425 and the floor is net8.0.
- Do not add PackageReference Version attributes in any csproj while central package management is on.
- Do not introduce a Maven, Gradle, or npm build file into the .NET target repository.
- Do not port config, registry, monitoring or turbine-stream-service; P1.T1 names five runtime services only.

**Verify (machine).**
- dotnet --version prints 8.0.x
- test -f Directory.Build.props && test -f Directory.Packages.props

**Verify (human).**
- Directory.Build.props contains exactly one PropertyGroup and no ItemGroup.
- Directory.Packages.props lists MongoDB.Driver, Microsoft.Extensions.Http, Microsoft.NET.Test.Sdk, xunit and xunit.runner.visualstudio and nothing else.

**Acceptance.**
- Both props files exist at the repository root with the pinned content.
- Every project created by later steps resolves net8.0 and its package versions without declaring them.

**Rollback.** `git rm Directory.Build.props Directory.Packages.props`

**Trace.** requirements REQ-P1.T1; decisions D-001, D-002; research R-001, R-002

## STEP-002 (P1.T1) - Create the Shared class library and pin the source's timeouts

Depends on: STEP-001.

**Why.** P1.T1 requires a Shared project; P1.T4 requires the typed-client base to use the source's timeout. The source sets one Hystrix execution timeout for every service in config/src/main/resources/shared/application.yml and a wider one for the edge in gateway.yml, so both constants belong in Shared. Decision D-003, D-008.

**Currently.** config/src/main/resources/shared/application.yml sets hystrix.command.default.execution.isolation.thread.timeoutInMilliseconds: 10000 for all services; config/src/main/resources/shared/gateway.yml raises it to 20000 and sets ribbon.ReadTimeout/ConnectTimeout and zuul.host.connect-timeout-millis/socket-timeout-millis to 20000. account-service/pom.xml declares spring-cloud-starter-openfeign and spring-cloud-starter-netflix-hystrix as the client/timeout stack.

**Change.** Create src/Shared/PiggyMetrics.Shared.csproj as a net8.0 class library that takes a FrameworkReference on Microsoft.AspNetCore.App (so Shared can host the authentication handler, the health endpoint and the MVC JSON options) and PackageReferences on MongoDB.Driver and Microsoft.Extensions.Http. Add src/Shared/PiggyMetricsTimeouts.cs holding the two timeout constants read off the source configuration: 10000 ms for every service call and 20000 ms at the gateway.

**Files.**
- `src/Shared/PiggyMetrics.Shared.csproj`
- `src/Shared/PiggyMetricsTimeouts.cs`

exact_snippet (`src/Shared/PiggyMetrics.Shared.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>PiggyMetrics.Shared</RootNamespace>
    <AssemblyName>PiggyMetrics.Shared</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="MongoDB.Driver" />
    <PackageReference Include="Microsoft.Extensions.Http" />
  </ItemGroup>
</Project>
~~~

exact_snippet (`src/Shared/PiggyMetricsTimeouts.cs`):

~~~csharp
namespace PiggyMetrics.Shared;

public static class PiggyMetricsTimeouts
{
  public static readonly TimeSpan Default = TimeSpan.FromMilliseconds(10000);

  public static readonly TimeSpan Gateway = TimeSpan.FromMilliseconds(20000);
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- PiggyMetricsTimeouts.Default is 10000 milliseconds, matching hystrix.command.default.execution.isolation.thread.timeoutInMilliseconds in application.yml.
- PiggyMetricsTimeouts.Gateway is 20000 milliseconds, matching gateway.yml.
- Shared uses FrameworkReference Microsoft.AspNetCore.App, never a PackageReference on an ASP.NET Core assembly.
- Shared carries no ProjectReference to any service project; dependencies point one way only.

**Edge cases.**
- A class library without the framework reference cannot see AuthenticationHandler or HealthCheckOptions, so the later Shared steps fail to compile.

**Do not.**
- Do not give Shared a dependency on a service project.
- Do not use the Microsoft.NET.Sdk.Web SDK for Shared; it is a library, not a host.
- Do not invent a timeout value; the two constants come from the source configuration.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings

**Verify (human).**
- PiggyMetricsTimeouts.Default reads TimeSpan.FromMilliseconds(10000).
- PiggyMetricsTimeouts.Gateway reads TimeSpan.FromMilliseconds(20000).

**Acceptance.**
- The Shared library builds clean on its own.
- Both source timeouts are available to every later step as named constants.

**Rollback.** `git rm -r src/Shared`

**Trace.** requirements REQ-P1.T1, REQ-P1.T4; decisions D-003, D-008; research R-003

## STEP-003 (P1.T2) - Port the bearer handler that resolves a token against the authorization server

Depends on: STEP-002.

**Why.** P1.T2 requires a bearer handler that validates a token the way the source resource servers do, by calling http://auth-service:5000/uaa/users/current. The source's CustomUserInfoTokenServices is the single shared implementation of that check across account, statistics and notification services, and the plan's done-when clause is that the handler accepts a token the source's authorization server issued and refuses one it did not. Decision D-004, D-005.

**Currently.** account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java:L50-L53 builds CustomUserInfoTokenServices(sso.getUserInfoUri(), sso.getClientId()); account-service/src/main/java/com/piggymetrics/account/service/security/CustomUserInfoTokenServices.java:L36-L37 declares PRINCIPAL_KEYS = { user, username, userid, user_id, login, id, name }; L67-L75 loadAuthentication throws InvalidTokenException when the map contains error; L96-L106 getRequest reads oauth2Request.clientId and oauth2Request.scope; L112-L137 getMap returns singletonMap(error, Could not fetch user details) on any exception. config/src/main/resources/shared/application.yml sets security.oauth2.resource.user-info-uri: http://auth-service:5000/uaa/users/current.

**Change.** Add src/Shared/Security/UserInfoAuthenticationOptions.cs (scheme options carrying the user-info URI and the client id), src/Shared/Security/PiggyMetricsClaims.cs (the client_id and scope claim types), and src/Shared/Security/UserInfoAuthenticationHandler.cs. The handler reads the Authorization header, calls the user-info endpoint with that bearer token over a named HttpClient, treats a non-success status, a transport failure, and a body carrying an error member as a refusal, resolves the principal name by walking the source's PRINCIPAL_KEYS order (user, username, userid, user_id, login, id, name), and projects oauth2Request.clientId and every oauth2Request.scope entry into claims. Challenge answers 401 with WWW-Authenticate Bearer; forbid answers 403.

**Files.**
- `src/Shared/Security/UserInfoAuthenticationOptions.cs`
- `src/Shared/Security/PiggyMetricsClaims.cs`
- `src/Shared/Security/UserInfoAuthenticationHandler.cs`

exact_snippet (`src/Shared/Security/UserInfoAuthenticationOptions.cs`):

~~~csharp
using Microsoft.AspNetCore.Authentication;

namespace PiggyMetrics.Shared.Security;

public sealed class UserInfoAuthenticationOptions : AuthenticationSchemeOptions
{
  public string UserInfoUri { get; set; } = string.Empty;

  public string ClientId { get; set; } = string.Empty;
}
~~~

exact_snippet (`src/Shared/Security/PiggyMetricsClaims.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Security;

public static class PiggyMetricsClaims
{
  public const string ClientId = "client_id";

  public const string Scope = "scope";
}
~~~

exact_snippet (`src/Shared/Security/UserInfoAuthenticationHandler.cs`):

~~~csharp
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Security;

public sealed class UserInfoAuthenticationHandler : AuthenticationHandler<UserInfoAuthenticationOptions>
{
  public const string SchemeName = "UserInfoBearer";

  public const string HttpClientName = "piggymetrics-userinfo";

  public static readonly string[] PrincipalKeys =
    { "user", "username", "userid", "user_id", "login", "id", "name" };

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
    string? token = ReadBearerToken();
    if (token is null)
    {
      return AuthenticateResult.NoResult();
    }

    JsonElement? body = await ReadUserInfoAsync(token).ConfigureAwait(false);
    if (body is null)
    {
      return AuthenticateResult.Fail("Could not fetch user details");
    }

    JsonElement map = body.Value;
    if (map.ValueKind != JsonValueKind.Object || map.TryGetProperty("error", out _))
    {
      return AuthenticateResult.Fail("Invalid access token");
    }

    ClaimsPrincipal principal = BuildPrincipal(map);
    return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
  }

  protected override Task HandleChallengeAsync(AuthenticationProperties properties)
  {
    Response.StatusCode = StatusCodes.Status401Unauthorized;
    Response.Headers.WWWAuthenticate = "Bearer";
    return Task.CompletedTask;
  }

  protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
  {
    Response.StatusCode = StatusCodes.Status403Forbidden;
    return Task.CompletedTask;
  }

  private string? ReadBearerToken()
  {
    string? header = Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(header))
    {
      return null;
    }

    const string prefix = "Bearer ";
    if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
      return null;
    }

    string token = header[prefix.Length..].Trim();
    return token.Length == 0 ? null : token;
  }

  private async Task<JsonElement?> ReadUserInfoAsync(string token)
  {
    try
    {
      HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
      using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInfoUri);
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      using HttpResponseMessage response = await client
        .SendAsync(request, Context.RequestAborted)
        .ConfigureAwait(false);
      if (!response.IsSuccessStatusCode)
      {
        Logger.LogInformation(
          "Could not fetch user details: status {Status}",
          (int)response.StatusCode);
        return null;
      }

      await using Stream stream = await response.Content
        .ReadAsStreamAsync(Context.RequestAborted)
        .ConfigureAwait(false);
      using JsonDocument document = await JsonDocument
        .ParseAsync(stream, cancellationToken: Context.RequestAborted)
        .ConfigureAwait(false);
      return document.RootElement.Clone();
    }
    catch (Exception exception)
    {
      Logger.LogInformation("Could not fetch user details: {Message}", exception.Message);
      return null;
    }
  }

  private ClaimsPrincipal BuildPrincipal(JsonElement map)
  {
    var claims = new List<Claim> { new(ClaimTypes.Name, ReadPrincipalName(map)) };

    if (map.TryGetProperty("oauth2Request", out JsonElement request)
      && request.ValueKind == JsonValueKind.Object)
    {
      if (request.TryGetProperty("clientId", out JsonElement clientId)
        && clientId.ValueKind == JsonValueKind.String)
      {
        claims.Add(new Claim(PiggyMetricsClaims.ClientId, clientId.GetString()!));
      }

      if (request.TryGetProperty("scope", out JsonElement scope)
        && scope.ValueKind == JsonValueKind.Array)
      {
        foreach (JsonElement entry in scope.EnumerateArray())
        {
          if (entry.ValueKind == JsonValueKind.String)
          {
            claims.Add(new Claim(PiggyMetricsClaims.Scope, entry.GetString()!));
          }
        }
      }
    }

    var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
    return new ClaimsPrincipal(identity);
  }

  private static string ReadPrincipalName(JsonElement map)
  {
    foreach (string key in PrincipalKeys)
    {
      if (!map.TryGetProperty(key, out JsonElement value))
      {
        continue;
      }

      return value.ValueKind switch
      {
        JsonValueKind.String => value.GetString() ?? "unknown",
        JsonValueKind.Null => "unknown",
        _ => value.ToString(),
      };
    }

    return "unknown";
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- PrincipalKeys keeps the source order user, username, userid, user_id, login, id, name; the first member present wins.
- A body carrying an error member is a refusal, exactly as CustomUserInfoTokenServices.loadAuthentication treats it.
- A transport failure is a refusal, never an acceptance; the source converts the exception into the same error map.
- A request with no Authorization header yields NoResult, so anonymous endpoints stay reachable the way antMatchers(/, /demo).permitAll() keeps them reachable in the source.
- The principal falls back to the literal unknown when no PRINCIPAL_KEYS member is present, matching getPrincipal.
- Every oauth2Request.scope entry becomes its own scope claim, so a scope check is a claim check.

**Edge cases.**
- oauth2Request absent from the body: the handler still succeeds with the principal name and no client_id or scope claim.
- A token issued by a different authorization server: the user-info call answers 401, the handler refuses.
- An Authorization header whose scheme is not Bearer: treated as absent, yielding NoResult.
- A principal member that is not a string: rendered through its JSON text rather than throwing.

**Do not.**
- Do not validate the token locally as a JWT; the source resource servers never parse the token, they resolve it against the authorization server.
- Do not cache the user-info response; the source resolves every request through CustomUserInfoTokenServices.
- Do not add Microsoft.AspNetCore.Authentication.JwtBearer to the solution.
- Do not answer 403 when the token is missing; a missing token is 401.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings
- dotnet test tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj --filter UserInfoAuthenticationHandlerTest (after STEP-013) reports 8 passing tests

**Verify (human).**
- The handler sends GET to the configured user-info URI with an Authorization header of Bearer plus the inbound token.
- A 401 from the user-info endpoint produces a failed authenticate result with a null principal.

**Acceptance.**
- A token the source's authorization server issued is accepted and carries the principal name the source would report.
- A token the source's authorization server did not issue is refused.
- A service token carries a scope claim with the value server.

**Rollback.** `git rm -r src/Shared/Security`

**Trace.** requirements REQ-P1.T2; decisions D-004, D-005; research R-004, R-005

## STEP-004 (P1.T2) - Register the resource server and the user and server authorization policies

Depends on: STEP-003.

**Why.** The downstream plans express their endpoint guards as policy user and policy server (P2.T3, P2.T4, P3.T4, P3.T5, P3.T6). Those two names must resolve to the source's two guards: an authenticated principal, and the OAuth2 scope check #oauth2.hasScope('server'). Decision D-005.

**Currently.** auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22 guards GET /users/current with the default authenticated requirement and L27-L28 guards POST /users with @PreAuthorize("#oauth2.hasScope('server')"); statistics-service/src/main/java/com/piggymetrics/statistics/controller/StatisticsController.java:L25 and L31 use the same scope check. account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java:L56-L60 configures anyRequest().authenticated().

**Change.** Add src/Shared/Security/PiggyMetricsPolicies.cs naming the two policies and the server scope value, and src/Shared/Security/SecurityServiceCollectionExtensions.cs exposing AddPiggyMetricsResourceServer. The extension reads security:oauth2:resource:user-info-uri and security:oauth2:client:clientId from configuration, registers the named user-info HttpClient with the source's 10000 ms timeout, registers the UserInfoBearer scheme backed by the handler, and registers the user policy as an authenticated-principal requirement and the server policy as an authenticated principal carrying a scope claim whose value is server.

**Files.**
- `src/Shared/Security/PiggyMetricsPolicies.cs`
- `src/Shared/Security/SecurityServiceCollectionExtensions.cs`

exact_snippet (`src/Shared/Security/PiggyMetricsPolicies.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Security;

public static class PiggyMetricsPolicies
{
  public const string User = "user";

  public const string Server = "server";

  public const string ServerScope = "server";
}
~~~

exact_snippet (`src/Shared/Security/SecurityServiceCollectionExtensions.cs`):

~~~csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PiggyMetrics.Shared.Security;

public static class SecurityServiceCollectionExtensions
{
  public static IServiceCollection AddPiggyMetricsResourceServer(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    string userInfoUri = configuration["security:oauth2:resource:user-info-uri"]
      ?? "http://auth-service:5000/uaa/users/current";
    string clientId = configuration["security:oauth2:client:clientId"] ?? string.Empty;

    services.AddHttpClient(UserInfoAuthenticationHandler.HttpClientName, client =>
    {
      client.Timeout = PiggyMetricsTimeouts.Default;
    });

    services
      .AddAuthentication(UserInfoAuthenticationHandler.SchemeName)
      .AddScheme<UserInfoAuthenticationOptions, UserInfoAuthenticationHandler>(
        UserInfoAuthenticationHandler.SchemeName,
        options =>
        {
          options.UserInfoUri = userInfoUri;
          options.ClientId = clientId;
        });

    services.AddAuthorizationBuilder()
      .AddPolicy(PiggyMetricsPolicies.User, policy => policy.RequireAuthenticatedUser())
      .AddPolicy(PiggyMetricsPolicies.Server, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(PiggyMetricsClaims.Scope, PiggyMetricsPolicies.ServerScope));

    return services;
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- The policy named user requires an authenticated principal and nothing else.
- The policy named server requires an authenticated principal carrying a scope claim whose value is server.
- The user-info HttpClient uses PiggyMetricsTimeouts.Default, never a framework default.
- The user-info URI defaults to http://auth-service:5000/uaa/users/current when configuration omits it, matching application.yml.

**Edge cases.**
- A user token carries scope ui, so it satisfies the user policy and fails the server policy, which is the 403 the downstream contract rows expect.
- Configuration supplying a different user-info URI overrides the default, which is how a test host points the handler at a stub.

**Do not.**
- Do not name the policies anything other than user and server; the downstream plans reference those names.
- Do not grant the server policy to any authenticated principal; the scope claim is the discriminator.
- Do not register a fallback authorization policy that would guard endpoints the source leaves anonymous.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings

**Verify (human).**
- PiggyMetricsPolicies.User is the literal user and PiggyMetricsPolicies.Server is the literal server.
- AddPiggyMetricsResourceServer registers exactly one authentication scheme, named UserInfoBearer.

**Acceptance.**
- A service calling AddPiggyMetricsResourceServer can guard an endpoint with the user policy or the server policy without declaring either itself.

**Rollback.** `git checkout -- src/Shared/Security`

**Trace.** requirements REQ-P1.T2; decisions D-005; research R-004, R-005

## STEP-005 (P1.T3) - Port the store conventions and the decimal serializer that match the documents on disk

Depends on: STEP-002.

**Why.** P1.T3 requires store conventions and serializers that keep the documents' field names and ids, against the same databases and collections. The .NET services read databases that the Java services wrote, so the BSON shape is a compatibility contract, not a preference. Decision D-006, D-007.

**Currently.** account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L13 declares @Document(collection = "accounts") with @Id on the camel-case field name and fields lastSeen, incomes, expenses, saving, note; Item.java and Saving.java hold BigDecimal amount and enum currency/period. mongodb/dump/account-service-dump.js seeds _id demo with camel-case members lastSeen, note, expenses, incomes, saving, numeric amounts 1300 and 3.32, and enum names USD and MONTH. spring-data-mongodb 2.0.x MongoConverters.java:L72-L73 registers BigDecimalToStringConverter and StringToBigDecimalConverter; MappingMongoConverter.java:L501-L505 skips a null property instead of writing it.

**Change.** Add src/Shared/Persistence/DecimalAsStringSerializer.cs and src/Shared/Persistence/MongoConventions.cs. The serializer writes System.Decimal as a BSON string under the invariant culture, matching Spring Data MongoDB's BigDecimalToStringConverter, and reads String, Double, Int32, Int64 and Decimal128 so it also reads the numeric amounts the source dump seeds. MongoConventions registers one convention pack once per process: camel-case element names (Java field names are camel case and C# properties are Pascal case), ignore-extra-elements, ignore-if-null (Spring Data skips null properties on write), and enum-as-string.

**Files.**
- `src/Shared/Persistence/DecimalAsStringSerializer.cs`
- `src/Shared/Persistence/MongoConventions.cs`

exact_snippet (`src/Shared/Persistence/DecimalAsStringSerializer.cs`):

~~~csharp
using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace PiggyMetrics.Shared.Persistence;

public sealed class DecimalAsStringSerializer : StructSerializerBase<decimal>
{
  public static readonly DecimalAsStringSerializer Instance = new();

  public override decimal Deserialize(
    BsonDeserializationContext context,
    BsonDeserializationArgs args)
  {
    IBsonReader reader = context.Reader;
    BsonType bsonType = reader.GetCurrentBsonType();
    switch (bsonType)
    {
      case BsonType.String:
        return decimal.Parse(reader.ReadString(), CultureInfo.InvariantCulture);
      case BsonType.Double:
        return (decimal)reader.ReadDouble();
      case BsonType.Int32:
        return reader.ReadInt32();
      case BsonType.Int64:
        return reader.ReadInt64();
      case BsonType.Decimal128:
        return (decimal)reader.ReadDecimal128();
      case BsonType.Null:
        reader.ReadNull();
        return 0m;
      default:
        throw new FormatException(
          $"Cannot deserialize BSON {bsonType} into System.Decimal.");
    }
  }

  public override void Serialize(
    BsonSerializationContext context,
    BsonSerializationArgs args,
    decimal value)
  {
    context.Writer.WriteString(value.ToString(CultureInfo.InvariantCulture));
  }
}
~~~

exact_snippet (`src/Shared/Persistence/MongoConventions.cs`):

~~~csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace PiggyMetrics.Shared.Persistence;

public static class MongoConventions
{
  public const string PackName = "piggymetrics";

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
      new IgnoreIfNullConvention(true),
      new EnumRepresentationConvention(BsonType.String),
    };

    ConventionRegistry.Register(PackName, pack, _ => true);

    BsonSerializer.RegisterSerializer(typeof(decimal), DecimalAsStringSerializer.Instance);
    BsonSerializer.RegisterSerializer(
      typeof(decimal?),
      new NullableSerializer<decimal>(DecimalAsStringSerializer.Instance));
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- A decimal is written as a BSON string, matching every amount and interest the Java services wrote.
- A decimal is read from String, Double, Int32, Int64 or Decimal128, so documents seeded by mongodb/dump/account-service-dump.js still load.
- Element names are camel case, so a C# property named LastSeen maps to the stored member lastSeen.
- A null property is omitted from the document, matching MappingMongoConverter.
- An unknown member in a stored document is ignored rather than throwing.
- An enum is stored under its name, matching the USD and MONTH values in the dump.
- Register is idempotent: a second call registers nothing, so repeated host startups in one test process stay safe.

**Edge cases.**
- A BSON null in a decimal member reads as 0 rather than throwing, since the source's @NotNull fields are never null on the write path.
- A document written by the Java service and a document written by the .NET service both read back to the same decimal value.

**Do not.**
- Do not store decimals as Decimal128; the Java services wrote strings and a mixed collection would break the Java services if they are still running.
- Do not rename a collection or a database; P1.T3 requires the same databases and collections.
- Do not use a Pascal-case element name convention; the stored members are camel case.
- Do not write null members into documents.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings
- dotnet test tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj --filter MongoConventionsTest (after STEP-013) reports 6 passing tests

**Verify (human).**
- Serializing a sample item writes amount as a BSON string.
- Deserializing a document whose amount is the number 1300 yields the decimal 1300.

**Acceptance.**
- A document the Java account service wrote round-trips through the .NET types without losing a field or changing a BSON type.

**Rollback.** `git rm -r src/Shared/Persistence`

**Trace.** requirements REQ-P1.T3; decisions D-006, D-007; research R-006, R-007

## STEP-006 (P1.T3) - Register the Mongo database handle from the source's connection settings

Depends on: STEP-005.

**Why.** Every downstream repository (P2.T2 UserRepository on auth-mongodb, P3.T3 DataPointRepository on statistics-mongodb) needs a database handle built from the same keys the source reads. Decision D-007.

**Currently.** config/src/main/resources/shared/auth-service.yml sets spring.data.mongodb host auth-mongodb, username user, password ${MONGODB_PASSWORD}, database piggymetrics, port 27017; statistics-service.yml, account-service.yml and notification-service.yml repeat the same shape against statistics-mongodb, account-mongodb and notification-mongodb. mongodb/init.sh creates the user with roles readWrite on db piggymetrics, so the authentication database is piggymetrics rather than admin.

**Change.** Add src/Shared/Persistence/MongoStoreOptions.cs binding the source's spring:data:mongodb section (host, port, database, username, password) and src/Shared/Persistence/PersistenceServiceCollectionExtensions.cs exposing AddPiggyMetricsStore. The extension registers the convention pack, binds the options, and registers a singleton IMongoClient and a singleton IMongoDatabase resolved from the configured database name, authenticating against that same database when a username is set.

**Files.**
- `src/Shared/Persistence/MongoStoreOptions.cs`
- `src/Shared/Persistence/PersistenceServiceCollectionExtensions.cs`

exact_snippet (`src/Shared/Persistence/MongoStoreOptions.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Persistence;

public sealed class MongoStoreOptions
{
  public const string SectionName = "spring:data:mongodb";

  public string Host { get; set; } = string.Empty;

  public int Port { get; set; } = 27017;

  public string Database { get; set; } = "piggymetrics";

  public string Username { get; set; } = "user";

  public string Password { get; set; } = string.Empty;
}
~~~

exact_snippet (`src/Shared/Persistence/PersistenceServiceCollectionExtensions.cs`):

~~~csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace PiggyMetrics.Shared.Persistence;

public static class PersistenceServiceCollectionExtensions
{
  public static IServiceCollection AddPiggyMetricsStore(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    MongoConventions.Register();

    services.Configure<MongoStoreOptions>(
      configuration.GetSection(MongoStoreOptions.SectionName));

    services.AddSingleton<IMongoClient>(provider =>
    {
      MongoStoreOptions options = provider
        .GetRequiredService<IOptions<MongoStoreOptions>>().Value;
      return new MongoClient(BuildSettings(options));
    });

    services.AddSingleton(provider =>
    {
      MongoStoreOptions options = provider
        .GetRequiredService<IOptions<MongoStoreOptions>>().Value;
      return provider.GetRequiredService<IMongoClient>().GetDatabase(options.Database);
    });

    return services;
  }

  public static MongoClientSettings BuildSettings(MongoStoreOptions options)
  {
    var settings = new MongoClientSettings
    {
      Server = new MongoServerAddress(options.Host, options.Port),
    };

    if (!string.IsNullOrEmpty(options.Username))
    {
      settings.Credential = MongoCredential.CreateCredential(
        options.Database,
        options.Username,
        options.Password);
    }

    return settings;
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- The configuration section is spring:data:mongodb, so a ported service keeps the source's key names.
- The default port is 27017 and the default database is piggymetrics, matching every shared yml.
- The credential authenticates against the configured database, matching the readWrite role mongodb/init.sh grants on piggymetrics.
- AddPiggyMetricsStore registers the convention pack before any document type is serialized.
- No username means no credential, so an unauthenticated local Mongo still connects.

**Edge cases.**
- A host supplying only the database name connects to that database on localhost port 27017.
- Calling AddPiggyMetricsStore twice in one host leaves a single registered convention pack.

**Do not.**
- Do not authenticate against the admin database; the source user is scoped to piggymetrics.
- Do not build a connection string by concatenation; the settings object carries the credential.
- Do not register the client as scoped or transient; the driver client is designed to be a singleton.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings

**Verify (human).**
- MongoStoreOptions.SectionName is the literal spring:data:mongodb.
- BuildSettings creates a credential only when a username is configured.

**Acceptance.**
- A service configured with the source's mongodb keys resolves an IMongoDatabase pointing at the same database the Java service used.

**Rollback.** `git checkout -- src/Shared/Persistence`

**Trace.** requirements REQ-P1.T3; decisions D-007; research R-006

## STEP-007 (P1.T4) - Port the client-credentials token cache and the outbound bearer handler

Depends on: STEP-002.

**Why.** P1.T4 requires a client-credentials token cache. The source obtains a service token through OAuth2FeignRequestInterceptor over a DefaultOAuth2ClientContext, which holds the token until it expires and only then requests another. Decision D-008, D-009.

**Currently.** account-service/src/main/java/com/piggymetrics/account/config/ResourceServerConfig.java:L40-L43 builds OAuth2FeignRequestInterceptor(new DefaultOAuth2ClientContext(), clientCredentialsResourceDetails()); config/src/main/resources/shared/account-service.yml sets security.oauth2.client clientId account-service, clientSecret ${ACCOUNT_SERVICE_PASSWORD}, accessTokenUri http://auth-service:5000/uaa/oauth/token, grant-type client_credentials, scope server; statistics-service.yml and notification-service.yml repeat it for their own client ids. Spring's ClientCredentialsResourceDetails defaults its authentication scheme to header, which is HTTP Basic client authentication.

**Change.** Add src/Shared/Http/OAuth2ClientOptions.cs binding the source's security:oauth2:client section, src/Shared/Http/ClientCredentialsTokenCache.cs, and src/Shared/Http/ClientCredentialsHandler.cs. The cache posts the client-credentials grant to the configured access-token URI with HTTP Basic client authentication and a form carrying grant_type and scope, holds the issued token until the instant expires_in names, and serializes concurrent callers through a mutex so a burst of requests triggers one token request. The delegating handler attaches that token as a bearer header on every outbound call.

**Files.**
- `src/Shared/Http/OAuth2ClientOptions.cs`
- `src/Shared/Http/ClientCredentialsTokenCache.cs`
- `src/Shared/Http/ClientCredentialsHandler.cs`

exact_snippet (`src/Shared/Http/OAuth2ClientOptions.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Http;

public sealed class OAuth2ClientOptions
{
  public const string SectionName = "security:oauth2:client";

  public string ClientId { get; set; } = string.Empty;

  public string ClientSecret { get; set; } = string.Empty;

  public string AccessTokenUri { get; set; } = "http://auth-service:5000/uaa/oauth/token";

  public string GrantType { get; set; } = "client_credentials";

  public string Scope { get; set; } = "server";
}
~~~

exact_snippet (`src/Shared/Http/ClientCredentialsTokenCache.cs`):

~~~csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Http;

public sealed class ClientCredentialsTokenCache
{
  public const string HttpClientName = "piggymetrics-token";

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IOptions<OAuth2ClientOptions> _options;
  private readonly SemaphoreSlim _mutex = new(1, 1);

  private string? _accessToken;
  private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

  public ClientCredentialsTokenCache(
    IHttpClientFactory httpClientFactory,
    IOptions<OAuth2ClientOptions> options)
  {
    _httpClientFactory = httpClientFactory;
    _options = options;
  }

  public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
  {
    if (!IsExpired())
    {
      return _accessToken!;
    }

    await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (!IsExpired())
      {
        return _accessToken!;
      }

      (string token, DateTimeOffset expiresAt) = await RequestTokenAsync(cancellationToken)
        .ConfigureAwait(false);
      _accessToken = token;
      _expiresAt = expiresAt;
      return token;
    }
    finally
    {
      _mutex.Release();
    }
  }

  private bool IsExpired()
  {
    return _accessToken is null || _expiresAt <= DateTimeOffset.UtcNow;
  }

  private async Task<(string Token, DateTimeOffset ExpiresAt)> RequestTokenAsync(
    CancellationToken cancellationToken)
  {
    OAuth2ClientOptions options = _options.Value;
    HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

    using var request = new HttpRequestMessage(HttpMethod.Post, options.AccessTokenUri);
    request.Headers.Authorization = new AuthenticationHeaderValue(
      "Basic",
      Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
    request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
      ["grant_type"] = options.GrantType,
      ["scope"] = options.Scope,
    });

    using HttpResponseMessage response = await client
      .SendAsync(request, cancellationToken)
      .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    await using Stream stream = await response.Content
      .ReadAsStreamAsync(cancellationToken)
      .ConfigureAwait(false);
    using JsonDocument document = await JsonDocument
      .ParseAsync(stream, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    JsonElement root = document.RootElement;
    string token = root.GetProperty("access_token").GetString()
      ?? throw new InvalidOperationException("Token response had no access_token.");
    long expiresIn = root.TryGetProperty("expires_in", out JsonElement expires)
      && expires.ValueKind == JsonValueKind.Number
        ? expires.GetInt64()
        : 0L;

    return (token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
  }
}
~~~

exact_snippet (`src/Shared/Http/ClientCredentialsHandler.cs`):

~~~csharp
using System.Net.Http.Headers;

namespace PiggyMetrics.Shared.Http;

public sealed class ClientCredentialsHandler : DelegatingHandler
{
  private readonly ClientCredentialsTokenCache _tokenCache;

  public ClientCredentialsHandler(ClientCredentialsTokenCache tokenCache)
  {
    _tokenCache = tokenCache;
  }

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    string token = await _tokenCache
      .GetAccessTokenAsync(cancellationToken)
      .ConfigureAwait(false);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- The token request is a POST to security:oauth2:client:accessTokenUri carrying HTTP Basic client authentication.
- The form body carries grant_type and scope taken from configuration, defaulting to client_credentials and server.
- A cached token is reused until the instant expires_in names; the source applies no early-refresh skew and neither does this cache.
- Concurrent callers waiting on an expired token produce one token request, not one per caller.
- A non-success token response throws rather than caching an empty token.

**Edge cases.**
- A token response without expires_in is treated as already expired, so the next call requests a fresh token rather than caching forever.
- An authorization server that is down surfaces the transport failure to the caller, matching the source, where the Feign call then falls back.

**Do not.**
- Do not apply an early-refresh skew; the source refreshes exactly at expiry and a skew would change the request pattern the authorization server sees.
- Do not send the client secret in the form body; the source uses the header authentication scheme.
- Do not share one cache instance across two client ids; the cache is registered per host and a host has one client id.
- Do not log the access token or the client secret.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings
- dotnet test tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj --filter ClientCredentialsTokenCacheTest (after STEP-013) reports 3 passing tests

**Verify (human).**
- Three consecutive token requests against a cache holding a live token produce one outbound call.
- The outbound token request carries an Authorization header whose scheme is Basic.

**Acceptance.**
- A service edge calling a guarded endpoint carries a bearer token the resource servers accept.
- The token is requested once per expiry window rather than once per outbound call.

**Rollback.** `git rm -r src/Shared/Http`

**Trace.** requirements REQ-P1.T4; decisions D-008, D-009; research R-008

## STEP-008 (P1.T4) - Expose the typed-client base so each edge gets one HTTP client at the source's timeout

Depends on: STEP-007.

**Why.** P1.T4 requires an HTTP client per edge with the source's timeout. The source declares one Feign interface per edge (E1 account to statistics, E2 account to auth, E3 statistics to rates-client, E4 notification to account) and the rates edge is anonymous while the others carry a service token. Decision D-008.

**Currently.** account-service/src/main/java/com/piggymetrics/account/client/StatisticsServiceClient.java:L10 declares @FeignClient(name = "statistics-service", fallback = StatisticsServiceClientFallback.class); account-service/src/main/java/com/piggymetrics/account/client/AuthServiceClient.java:L9 declares @FeignClient(name = "auth-service"); notification-service/src/main/java/com/piggymetrics/notification/client/AccountServiceClient.java:L9 declares @FeignClient(name = "account-service"); statistics-service/src/main/java/com/piggymetrics/statistics/client/ExchangeRatesClient.java:L10 declares @FeignClient(url = "${rates.url}", name = "rates-client", fallback = ExchangeRatesClientFallback.class) and statistics-service.yml sets rates.url to https://api.exchangeratesapi.io.

**Change.** Add src/Shared/Http/HttpServiceCollectionExtensions.cs exposing three registrations: AddPiggyMetricsClientCredentials binds the client options, registers the token HttpClient at the source's 10000 ms timeout and registers the cache as a singleton and the handler as transient; AddPiggyMetricsClient registers a typed client at the source's timeout with the client-credentials handler attached, for an edge that calls a guarded PiggyMetrics service; AddPiggyMetricsAnonymousClient registers a typed client at the same timeout without the handler, for the rates edge, which the source declares with a url and no token.

**Files.**
- `src/Shared/Http/HttpServiceCollectionExtensions.cs`

exact_snippet (`src/Shared/Http/HttpServiceCollectionExtensions.cs`):

~~~csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PiggyMetrics.Shared.Http;

public static class HttpServiceCollectionExtensions
{
  public static IServiceCollection AddPiggyMetricsClientCredentials(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.Configure<OAuth2ClientOptions>(
      configuration.GetSection(OAuth2ClientOptions.SectionName));
    services.AddHttpClient(ClientCredentialsTokenCache.HttpClientName, client =>
    {
      client.Timeout = PiggyMetricsTimeouts.Default;
    });
    services.AddSingleton<ClientCredentialsTokenCache>();
    services.AddTransient<ClientCredentialsHandler>();
    return services;
  }

  public static IHttpClientBuilder AddPiggyMetricsClient<TClient, TImplementation>(
    this IServiceCollection services,
    string baseAddress)
    where TClient : class
    where TImplementation : class, TClient
  {
    return services
      .AddHttpClient<TClient, TImplementation>(client =>
      {
        client.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
        client.Timeout = PiggyMetricsTimeouts.Default;
      })
      .AddHttpMessageHandler<ClientCredentialsHandler>();
  }

  public static IHttpClientBuilder AddPiggyMetricsAnonymousClient<TClient, TImplementation>(
    this IServiceCollection services,
    string baseAddress)
    where TClient : class
    where TImplementation : class, TClient
  {
    return services.AddHttpClient<TClient, TImplementation>(client =>
    {
      client.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
      client.Timeout = PiggyMetricsTimeouts.Default;
    });
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- Every typed client registered through these extensions carries PiggyMetricsTimeouts.Default.
- A client registered through AddPiggyMetricsClient attaches the client-credentials handler; one registered through AddPiggyMetricsAnonymousClient does not.
- The base address is absolute, so a relative request path resolves against the configured service host.
- The token HttpClient is a separate named client, so attaching the handler to a typed client cannot recurse into the token request.

**Edge cases.**
- The rates edge is anonymous because the source declares it with a url rather than a service name and never attaches the interceptor to it.
- A downstream plan adding a fallback wraps the typed client implementation; the registration shape does not change.

**Do not.**
- Do not attach the client-credentials handler to the rates client.
- Do not set a timeout other than the source's 10000 ms on a service-to-service client.
- Do not register a single shared HttpClient for every edge; P1.T4 requires an HTTP client per edge.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings

**Verify (human).**
- Both client registrations set Timeout to PiggyMetricsTimeouts.Default.
- Only AddPiggyMetricsClient calls AddHttpMessageHandler.

**Acceptance.**
- A downstream plan registers its edge with one call and inherits the source's timeout and token behaviour.

**Rollback.** `git checkout -- src/Shared/Http`

**Trace.** requirements REQ-P1.T4; decisions D-008; research R-008, R-009

## STEP-009 (P1.T5) - Port the JSON options: enum names, ignored unknown fields, epoch-millisecond dates

Depends on: STEP-002.

**Why.** P1.T5 names three JSON behaviours, and the downstream contract rows (C1 to C3, C8 to C10) replay with the source's body, so the wire shape has to match Jackson's. Decision D-010.

**Currently.** No service sets spring.jackson.date-format or a serialization feature anywhere in config/src/main/resources/shared, so Jackson's defaults apply: WRITE_DATES_AS_TIMESTAMPS stays enabled and java.util.Date is written as epoch milliseconds. Spring's Jackson2ObjectMapperBuilder disables FAIL_ON_UNKNOWN_PROPERTIES, and account-service/src/main/java/com/piggymetrics/account/domain/Account.java:L14 repeats that with @JsonIgnoreProperties(ignoreUnknown = true). Account.java:L20 holds a java.util.Date lastSeen; statistics-service/src/main/java/com/piggymetrics/statistics/domain/Currency.java:L5 and DataPoint.java:L28-L31 use enums as values and as map keys, which Jackson writes as names.

**Change.** Add src/Shared/Json/EpochMillisecondsDateTimeConverter.cs and src/Shared/Json/PiggyMetricsJson.cs. The converter writes a DateTime as the epoch millisecond count Jackson emits for java.util.Date and reads a number, a numeric string, or an ISO text. PiggyMetricsJson builds the serializer options once (camel-case property names, unknown members skipped, nulls written, enums as names, numbers readable from strings) and exposes AddPiggyMetricsJson to apply the same options to the MVC pipeline.

**Files.**
- `src/Shared/Json/EpochMillisecondsDateTimeConverter.cs`
- `src/Shared/Json/PiggyMetricsJson.cs`

exact_snippet (`src/Shared/Json/EpochMillisecondsDateTimeConverter.cs`):

~~~csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiggyMetrics.Shared.Json;

public sealed class EpochMillisecondsDateTimeConverter : JsonConverter<DateTime>
{
  public override DateTime Read(
    ref Utf8JsonReader reader,
    Type typeToConvert,
    JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Number)
    {
      return DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64()).UtcDateTime;
    }

    if (reader.TokenType == JsonTokenType.String)
    {
      string? text = reader.GetString();
      if (long.TryParse(text, out long milliseconds))
      {
        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime;
      }

      return DateTime.Parse(
        text!,
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AdjustToUniversal
          | System.Globalization.DateTimeStyles.AssumeUniversal);
    }

    throw new JsonException("Expected epoch milliseconds for a date value.");
  }

  public override void Write(
    Utf8JsonWriter writer,
    DateTime value,
    JsonSerializerOptions options)
  {
    DateTime utc = value.Kind == DateTimeKind.Utc
      ? value
      : value.ToUniversalTime();
    writer.WriteNumberValue(new DateTimeOffset(utc).ToUnixTimeMilliseconds());
  }
}
~~~

exact_snippet (`src/Shared/Json/PiggyMetricsJson.cs`):

~~~csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace PiggyMetrics.Shared.Json;

public static class PiggyMetricsJson
{
  public static readonly JsonSerializerOptions Options = Create();

  public static JsonSerializerOptions Create()
  {
    var options = new JsonSerializerOptions
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      DictionaryKeyPolicy = null,
      PropertyNameCaseInsensitive = false,
      UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
      DefaultIgnoreCondition = JsonIgnoreCondition.Never,
      NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    options.Converters.Add(new JsonStringEnumConverter());
    options.Converters.Add(new EpochMillisecondsDateTimeConverter());
    return options;
  }

  public static IMvcBuilder AddPiggyMetricsJson(this IMvcBuilder builder)
  {
    builder.AddJsonOptions(json =>
    {
      JsonSerializerOptions source = Create();
      json.JsonSerializerOptions.PropertyNamingPolicy = source.PropertyNamingPolicy;
      json.JsonSerializerOptions.DictionaryKeyPolicy = source.DictionaryKeyPolicy;
      json.JsonSerializerOptions.PropertyNameCaseInsensitive =
        source.PropertyNameCaseInsensitive;
      json.JsonSerializerOptions.UnmappedMemberHandling = source.UnmappedMemberHandling;
      json.JsonSerializerOptions.DefaultIgnoreCondition = source.DefaultIgnoreCondition;
      json.JsonSerializerOptions.NumberHandling = source.NumberHandling;
      json.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
      json.JsonSerializerOptions.Converters.Add(new EpochMillisecondsDateTimeConverter());
    });

    return builder;
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- A DateTime is written as an epoch millisecond number, never as an ISO string.
- A non-UTC DateTime is converted to UTC before the epoch count is computed.
- An unknown JSON member is skipped rather than throwing, matching FAIL_ON_UNKNOWN_PROPERTIES disabled and @JsonIgnoreProperties(ignoreUnknown = true).
- A null member is written rather than omitted, matching Jackson's default inclusion.
- An enum is written as its name, both as a value and as a dictionary key.
- Property names are camel case, matching the Java field names on the wire.

**Edge cases.**
- A date arriving as a numeric string still reads, which keeps a hand-written client working.
- A date arriving as ISO text still reads, which keeps a test fixture working.
- DictionaryKeyPolicy stays unset so an enum map key keeps its exact name, matching the Map<Currency, BigDecimal> rates member.

**Do not.**
- Do not set a date format string; the source emits a number, not a formatted string.
- Do not omit null members; the source writes them.
- Do not apply a camel-case dictionary key policy, which would rewrite the enum map keys the statistics documents carry.
- Do not serialize enums as integers.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings
- dotnet test tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj --filter PiggyMetricsJsonTest (after STEP-013) reports 5 passing tests

**Verify (human).**
- Serializing a sample account whose lastSeen is 2018-06-15T10:30:00Z writes the member lastSeen as 1529058600000.
- Serializing a sample account whose currency is RUB writes the member currency as the string RUB.

**Acceptance.**
- A response body produced by a ported controller matches the source's body for the same document.
- A request body the source accepts is accepted unchanged, including one carrying members the type does not declare.

**Rollback.** `git rm -r src/Shared/Json`

**Trace.** requirements REQ-P1.T5; decisions D-010; research R-010, R-011

## STEP-010 (P1.T5) - Port the actuator health endpoint with the source's path, body and status codes

Depends on: STEP-002.

**Why.** P1.T5 requires health checks. The source exposes Spring Boot actuator health and the container orchestration depends on it: config/Dockerfile healthchecks http://localhost:8888/actuator/health and every service in docker-compose.yml waits on condition service_healthy. Decision D-011.

**Currently.** config/Dockerfile:L7 declares HEALTHCHECK CMD curl -f http://localhost:8888/actuator/health; docker-compose.yml waits on condition: service_healthy for config at lines 30, 45, 64, 89, 115, 140, 163 and 178; config/src/main/java/com/piggymetrics/config/SecurityConfig.java:L18 permits antMatchers("/actuator/**").permitAll(); account-service/pom.xml declares spring-boot-starter-actuator.

**Change.** Add src/Shared/Health/HealthEndpoints.cs exposing AddPiggyMetricsHealth and MapPiggyMetricsHealth. The endpoint answers at /actuator/health relative to the service's context path, writes the actuator body shape (a single status member reading UP or DOWN, with no detail, matching Boot 2.0's default of hiding details), answers 200 for healthy and degraded and 503 for unhealthy, and stays anonymous because the source permits /actuator/** without authentication.

**Files.**
- `src/Shared/Health/HealthEndpoints.cs`

exact_snippet (`src/Shared/Health/HealthEndpoints.cs`):

~~~csharp
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PiggyMetrics.Shared.Health;

public static class HealthEndpoints
{
  public const string Path = "/actuator/health";

  public static IServiceCollection AddPiggyMetricsHealth(this IServiceCollection services)
  {
    services.AddHealthChecks();
    return services;
  }

  public static IEndpointConventionBuilder MapPiggyMetricsHealth(
    this IEndpointRouteBuilder endpoints)
  {
    return endpoints.MapHealthChecks(Path, new HealthCheckOptions
    {
      ResponseWriter = WriteActuatorResponse,
      ResultStatusCodes =
      {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
      },
    }).AllowAnonymous();
  }

  public static Task WriteActuatorResponse(HttpContext context, HealthReport report)
  {
    context.Response.ContentType = "application/json";
    string status = report.Status == HealthStatus.Unhealthy ? "DOWN" : "UP";
    return context.Response.WriteAsync(
      JsonSerializer.Serialize(new Dictionary<string, string> { ["status"] = status }));
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- The path is /actuator/health relative to the configured context path, so the account service answers at /accounts/actuator/health.
- The body is exactly a JSON object carrying one status member, matching Boot 2.0's default health response with details hidden.
- The status text is UP for healthy and degraded and DOWN for unhealthy.
- Healthy and degraded answer 200; unhealthy answers 503.
- The endpoint is anonymous, matching the source's permitAll on /actuator/**.

**Edge cases.**
- A service with no registered health check reports healthy, which is the Boot behaviour when no indicator is down.
- A container healthcheck probing the unprefixed /actuator/health gets 404 once STEP-011 enforces the context path; the probe must use the prefixed path, as the source's probe does.

**Do not.**
- Do not expose health detail members; Boot 2.0 hides them by default and the ported body must match.
- Do not guard the health endpoint with the user or server policy.
- Do not map the endpoint at /health or /healthz.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings
- dotnet test tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj --filter HealthEndpointsTest (after STEP-013) reports 2 passing tests

**Verify (human).**
- A healthy service answers the actuator health path with the body {"status":"UP"} and status 200.
- An unhealthy service answers with the body {"status":"DOWN"} and status 503.

**Acceptance.**
- A container healthcheck written against the source's actuator path succeeds against the ported service.

**Rollback.** `git rm -r src/Shared/Health`

**Trace.** requirements REQ-P1.T5; decisions D-011; research R-012

## STEP-011 (P1.T5) - Compose the service defaults: port, context path, resource server, JSON and health

Depends on: STEP-004, STEP-009, STEP-010.

**Why.** Each ported service must listen on the source's port under the source's context path with the same security, JSON and health wiring. Centralising that in Shared is what lets P2.T7 and P3.T8 be configuration-only steps. Decision D-012.

**Currently.** config/src/main/resources/shared/auth-service.yml sets server.servlet.context-path /uaa and server.port 5000; account-service.yml sets /accounts and 6000; statistics-service.yml sets /statistics and 7000; notification-service.yml sets /notifications and 8000; gateway.yml sets server.port 4000 with no context path. Spring's server.servlet.context-path answers 404 outside the prefix; ASP.NET Core's UsePathBase alone does not, so the prefix guard restores the source's status code.

**Change.** Add src/Shared/Configuration/ServerOptions.cs naming the two source configuration keys and src/Shared/PiggyMetricsServiceDefaults.cs exposing AddPiggyMetricsDefaults and UsePiggyMetricsDefaults. The builder extension binds the listener to server:port, adds controllers with the ported JSON options, adds the resource server and adds health. The application extension enforces server:servlet:context-path by answering 404 to any request outside that prefix before rebasing the pipeline onto it, then wires routing, authentication, authorization, the health endpoint and the controllers.

**Files.**
- `src/Shared/Configuration/ServerOptions.cs`
- `src/Shared/PiggyMetricsServiceDefaults.cs`

exact_snippet (`src/Shared/Configuration/ServerOptions.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Configuration;

public sealed class ServerOptions
{
  public const string PortKey = "server:port";

  public const string ContextPathKey = "server:servlet:context-path";
}
~~~

exact_snippet (`src/Shared/PiggyMetricsServiceDefaults.cs`):

~~~csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Configuration;
using PiggyMetrics.Shared.Health;
using PiggyMetrics.Shared.Json;
using PiggyMetrics.Shared.Security;

namespace PiggyMetrics.Shared;

public static class PiggyMetricsServiceDefaults
{
  public static WebApplicationBuilder AddPiggyMetricsDefaults(
    this WebApplicationBuilder builder)
  {
    IConfiguration configuration = builder.Configuration;

    string? port = configuration[ServerOptions.PortKey];
    if (!string.IsNullOrWhiteSpace(port))
    {
      builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    }

    builder.Services.AddControllers().AddPiggyMetricsJson();
    builder.Services.AddPiggyMetricsResourceServer(configuration);
    builder.Services.AddPiggyMetricsHealth();
    return builder;
  }

  public static WebApplication UsePiggyMetricsDefaults(this WebApplication app)
  {
    string? contextPath = app.Configuration[ServerOptions.ContextPathKey];
    if (!string.IsNullOrWhiteSpace(contextPath) && contextPath != "/")
    {
      var basePath = new PathString(contextPath);
      app.Use(async (context, next) =>
      {
        if (!context.Request.Path.StartsWithSegments(basePath))
        {
          context.Response.StatusCode = StatusCodes.Status404NotFound;
          return;
        }

        await next().ConfigureAwait(false);
      });
      app.UsePathBase(basePath);
    }

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapPiggyMetricsHealth();
    app.MapControllers();
    return app;
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- A request whose path does not start with the configured context path answers 404, matching Spring's context-path.
- A request whose path starts with the context path is served with that prefix stripped, so a controller route is declared without it.
- A path that merely shares a prefix with the context path, such as /accountsfoo, answers 404.
- An empty context path leaves the pipeline unrebased, which is the gateway's shape.
- The listener binds to server:port when configuration supplies it.
- Authentication runs before authorization, so a policy sees the resolved principal.

**Edge cases.**
- The gateway configures an empty context path, so no prefix guard is installed and it serves at the root.
- A service without a configured port falls back to the host default rather than failing to start.

**Do not.**
- Do not rely on UsePathBase alone; without the prefix guard the service answers the unprefixed path, which the source does not.
- Do not hardcode a port or a context path in Shared; both come from configuration.
- Do not add HTTPS redirection; the source serves plain HTTP behind the gateway.
- Do not map controllers before authentication and authorization are wired.

**Verify (machine).**
- dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror exits 0 with 0 warnings
- curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:6000/accounts/actuator/health prints 200
- curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:6000/actuator/health prints 404

**Verify (human).**
- Starting the account service and requesting /accounts/actuator/health returns {"status":"UP"}.
- Requesting /actuator/health on the same service returns 404.

**Acceptance.**
- Each ported service listens on the source's port and serves only under the source's context path.

**Rollback.** `git rm src/Shared/PiggyMetricsServiceDefaults.cs && git rm -r src/Shared/Configuration`

**Trace.** requirements REQ-P1.T1, REQ-P1.T5; decisions D-012; research R-013

## STEP-012 (P1.T1) - Create the five runtime service projects on the source's ports and context paths

Depends on: STEP-011.

**Why.** P1.T1 requires one project per runtime service: Gateway, AuthService, AccountService, StatisticsService and NotificationService. Each is a host that consumes the Shared defaults and carries the port and context path its source module declares. Decision D-003, D-012.

**Currently.** Root pom.xml lines 41-51 list the modules config, monitoring, registry, gateway, auth-service, account-service, statistics-service, notification-service, turbine-stream-service; P1.T1 names only the five runtime services. Each Dockerfile EXPOSEs the matching port: gateway/Dockerfile 4000, auth-service/Dockerfile 5000, account-service/Dockerfile 6000, notification-service/Dockerfile 8000; statistics-service.yml sets 7000. Each module's entry point is a @SpringBootApplication class such as account-service/src/main/java/com/piggymetrics/account/AccountApplication.java.

**Change.** Create five web projects under src, each with a csproj referencing Shared, a Program.cs that calls the Shared builder and application extensions and exposes a public partial Program for the later test hosts, and an appsettings.json carrying that service's port, context path and the shared user-info URI. Gateway takes port 4000 with an empty context path; AuthService 5000 and /uaa; AccountService 6000 and /accounts; StatisticsService 7000 and /statistics; NotificationService 8000 and /notifications.

**Files.**
- `src/Gateway/PiggyMetrics.Gateway.csproj`
- `src/Gateway/Program.cs`
- `src/Gateway/appsettings.json`
- `src/AuthService/PiggyMetrics.AuthService.csproj`
- `src/AuthService/Program.cs`
- `src/AuthService/appsettings.json`
- `src/AccountService/PiggyMetrics.AccountService.csproj`
- `src/AccountService/Program.cs`
- `src/AccountService/appsettings.json`
- `src/StatisticsService/PiggyMetrics.StatisticsService.csproj`
- `src/StatisticsService/Program.cs`
- `src/StatisticsService/appsettings.json`
- `src/NotificationService/PiggyMetrics.NotificationService.csproj`
- `src/NotificationService/Program.cs`
- `src/NotificationService/appsettings.json`

exact_snippet (`src/Gateway/PiggyMetrics.Gateway.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>PiggyMetrics.Gateway</RootNamespace>
    <AssemblyName>PiggyMetrics.Gateway</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Shared\PiggyMetrics.Shared.csproj" />
  </ItemGroup>
</Project>
~~~

exact_snippet (`src/Gateway/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults();

WebApplication app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();

namespace PiggyMetrics.Gateway
{
  public partial class Program;
}
~~~

exact_snippet (`src/Gateway/appsettings.json`):

~~~json
{
  "server": {
    "port": 4000,
    "servlet": {
      "context-path": ""
    }
  },
  "security": {
    "oauth2": {
      "resource": {
        "user-info-uri": "http://auth-service:5000/uaa/users/current"
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
~~~

exact_snippet (`src/AuthService/PiggyMetrics.AuthService.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>PiggyMetrics.AuthService</RootNamespace>
    <AssemblyName>PiggyMetrics.AuthService</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Shared\PiggyMetrics.Shared.csproj" />
  </ItemGroup>
</Project>
~~~

exact_snippet (`src/AuthService/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults();

WebApplication app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();

namespace PiggyMetrics.AuthService
{
  public partial class Program;
}
~~~

exact_snippet (`src/AuthService/appsettings.json`):

~~~json
{
  "server": {
    "port": 5000,
    "servlet": {
      "context-path": "/uaa"
    }
  },
  "security": {
    "oauth2": {
      "resource": {
        "user-info-uri": "http://auth-service:5000/uaa/users/current"
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
~~~

exact_snippet (`src/AccountService/PiggyMetrics.AccountService.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>PiggyMetrics.AccountService</RootNamespace>
    <AssemblyName>PiggyMetrics.AccountService</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Shared\PiggyMetrics.Shared.csproj" />
  </ItemGroup>
</Project>
~~~

exact_snippet (`src/AccountService/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults();

WebApplication app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();

namespace PiggyMetrics.AccountService
{
  public partial class Program;
}
~~~

exact_snippet (`src/AccountService/appsettings.json`):

~~~json
{
  "server": {
    "port": 6000,
    "servlet": {
      "context-path": "/accounts"
    }
  },
  "security": {
    "oauth2": {
      "resource": {
        "user-info-uri": "http://auth-service:5000/uaa/users/current"
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
~~~

exact_snippet (`src/StatisticsService/PiggyMetrics.StatisticsService.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>PiggyMetrics.StatisticsService</RootNamespace>
    <AssemblyName>PiggyMetrics.StatisticsService</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Shared\PiggyMetrics.Shared.csproj" />
  </ItemGroup>
</Project>
~~~

exact_snippet (`src/StatisticsService/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults();

WebApplication app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();

namespace PiggyMetrics.StatisticsService
{
  public partial class Program;
}
~~~

exact_snippet (`src/StatisticsService/appsettings.json`):

~~~json
{
  "server": {
    "port": 7000,
    "servlet": {
      "context-path": "/statistics"
    }
  },
  "security": {
    "oauth2": {
      "resource": {
        "user-info-uri": "http://auth-service:5000/uaa/users/current"
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
~~~

exact_snippet (`src/NotificationService/PiggyMetrics.NotificationService.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <RootNamespace>PiggyMetrics.NotificationService</RootNamespace>
    <AssemblyName>PiggyMetrics.NotificationService</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Shared\PiggyMetrics.Shared.csproj" />
  </ItemGroup>
</Project>
~~~

exact_snippet (`src/NotificationService/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults();

WebApplication app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();

namespace PiggyMetrics.NotificationService
{
  public partial class Program;
}
~~~

exact_snippet (`src/NotificationService/appsettings.json`):

~~~json
{
  "server": {
    "port": 8000,
    "servlet": {
      "context-path": "/notifications"
    }
  },
  "security": {
    "oauth2": {
      "resource": {
        "user-info-uri": "http://auth-service:5000/uaa/users/current"
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
~~~

exact_commands:

~~~bash
dotnet build src/Gateway/PiggyMetrics.Gateway.csproj -warnaserror
dotnet build src/AuthService/PiggyMetrics.AuthService.csproj -warnaserror
dotnet build src/AccountService/PiggyMetrics.AccountService.csproj -warnaserror
dotnet build src/StatisticsService/PiggyMetrics.StatisticsService.csproj -warnaserror
dotnet build src/NotificationService/PiggyMetrics.NotificationService.csproj -warnaserror
~~~

**Invariants.**
- Exactly five service projects exist; config, registry, monitoring and turbine-stream-service are not ported.
- Each service carries the port its source Dockerfile or yml declares.
- Each service carries the context path its shared yml declares; the gateway's is empty.
- Each Program.cs exposes a public partial Program so a later test host can reference it.
- Every service depends on Shared and no service depends on another service project.
- The user-info URI in every appsettings.json is http://auth-service:5000/uaa/users/current, matching application.yml.

**Edge cases.**
- Running two services on one machine binds two different ports, so they do not collide.
- The gateway's empty context path means the prefix guard from STEP-011 is not installed for it.

**Do not.**
- Do not port config, registry, monitoring or turbine-stream-service.
- Do not add a controller, a repository or a domain type in this step; those belong to P2 and the later service plans.
- Do not change a port or a context path from the value its source module declares.
- Do not add a launchSettings.json that overrides the configured port.

**Verify (machine).**
- each of the five dotnet build commands exits 0 with 0 warnings
- test $(ls src | wc -l) -eq 6

**Verify (human).**
- src holds exactly Shared, Gateway, AuthService, AccountService, StatisticsService and NotificationService.
- AuthService/appsettings.json reads port 5000 and context-path /uaa.

**Acceptance.**
- Every runtime service project named by P1.T1 exists and builds.
- Each service starts on the source's port and answers its prefixed actuator health path.

**Rollback.** `git rm -r src/Gateway src/AuthService src/AccountService src/StatisticsService src/NotificationService`

**Trace.** requirements REQ-P1.T1; decisions D-003, D-012; research R-013, R-014

## STEP-013 (P1.T2) - Add the Shared test project that proves the plan's done-when clause

Depends on: STEP-011.

**Why.** P1's done-when clause is that Shared's bearer handler accepts a token the source's authorization server issued and refuses one it did not. That is a behavioural claim and needs an executable check. The same project locks the store conventions, the token cache, the JSON options and the health body, so a later plan cannot silently change the wire or the document shape. Decision D-013.

**Currently.** The source proves the same surface through auth-service/src/test/java/com/piggymetrics/auth/controller/UserControllerTest.java and the per-service repository and service tests that P2 and P3 port. The user-info bodies in the fixture are the Jackson rendering of the OAuth2Authentication that auth-service/src/main/java/com/piggymetrics/auth/controller/UserController.java:L22 returns, whose top-level name member is the first PRINCIPAL_KEYS match. The target repository currently has no test project.

**Change.** Create tests/PiggyMetrics.Shared.Tests with a csproj referencing Shared and the pinned test packages, three stubs (an HttpMessageHandler recording requests and bodies, an IHttpClientFactory over it, and an IOptionsMonitor), a fixture module holding the exact user-info bodies the source's authorization server returns for a browser user token and a service token plus the source's error body, and four test classes: eight bearer handler tests, three token cache tests, six store convention tests, five JSON tests and two health body tests.

**Files.**
- `tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj`
- `tests/PiggyMetrics.Shared.Tests/StubHttpMessageHandler.cs`
- `tests/PiggyMetrics.Shared.Tests/StubHttpClientFactory.cs`
- `tests/PiggyMetrics.Shared.Tests/StubOptionsMonitor.cs`
- `tests/PiggyMetrics.Shared.Tests/SourceTokenBodies.cs`
- `tests/PiggyMetrics.Shared.Tests/UserInfoAuthenticationHandlerTest.cs`
- `tests/PiggyMetrics.Shared.Tests/ClientCredentialsTokenCacheTest.cs`
- `tests/PiggyMetrics.Shared.Tests/MongoConventionsTest.cs`
- `tests/PiggyMetrics.Shared.Tests/PiggyMetricsJsonTest.cs`
- `tests/PiggyMetrics.Shared.Tests/HealthEndpointsTest.cs`

exact_snippet (`tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <RootNamespace>PiggyMetrics.Shared.Tests</RootNamespace>
    <AssemblyName>PiggyMetrics.Shared.Tests</AssemblyName>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Shared\PiggyMetrics.Shared.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
</Project>
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/StubHttpMessageHandler.cs`):

~~~csharp
using System.Net;
using System.Text;

namespace PiggyMetrics.Shared.Tests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
  private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

  public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
  {
    _responder = responder;
  }

  public List<HttpRequestMessage> Requests { get; } = new();

  public List<string> RequestBodies { get; } = new();

  public static HttpResponseMessage Json(HttpStatusCode status, string body)
  {
    return new HttpResponseMessage(status)
    {
      Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };
  }

  protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    Requests.Add(request);
    RequestBodies.Add(
      request.Content is null
        ? string.Empty
        : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult());
    return Task.FromResult(_responder(request));
  }
}
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/StubHttpClientFactory.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Tests;

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
  private readonly HttpMessageHandler _handler;

  public StubHttpClientFactory(HttpMessageHandler handler)
  {
    _handler = handler;
  }

  public HttpClient CreateClient(string name)
  {
    return new HttpClient(_handler, disposeHandler: false)
    {
      Timeout = PiggyMetricsTimeouts.Default,
    };
  }
}
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/StubOptionsMonitor.cs`):

~~~csharp
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Tests;

internal sealed class StubOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
{
  public StubOptionsMonitor(TOptions value)
  {
    CurrentValue = value;
  }

  public TOptions CurrentValue { get; }

  public TOptions Get(string? name) => CurrentValue;

  public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
}
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/SourceTokenBodies.cs`):

~~~csharp
namespace PiggyMetrics.Shared.Tests;

internal static class SourceTokenBodies
{
  public const string BrowserUserToken = """
    {
      "authorities": [],
      "details": null,
      "authenticated": true,
      "userAuthentication": {
        "authorities": [],
        "details": null,
        "authenticated": true,
        "principal": { "username": "demo", "enabled": true },
        "credentials": null,
        "name": "demo"
      },
      "principal": "demo",
      "credentials": "",
      "oauth2Request": {
        "clientId": "browser",
        "scope": ["ui"],
        "requestParameters": { "grant_type": "password", "username": "demo" },
        "resourceIds": [],
        "authorities": [],
        "approved": true,
        "refresh": false,
        "redirectUri": null,
        "responseTypes": [],
        "extensions": {},
        "grantType": "password",
        "refreshTokenRequest": null
      },
      "clientOnly": false,
      "name": "demo"
    }
    """;

  public const string ServiceServerToken = """
    {
      "authorities": [],
      "details": null,
      "authenticated": true,
      "userAuthentication": null,
      "principal": "statistics-service",
      "credentials": "",
      "oauth2Request": {
        "clientId": "statistics-service",
        "scope": ["server"],
        "requestParameters": { "grant_type": "client_credentials" },
        "resourceIds": [],
        "authorities": [],
        "approved": true,
        "refresh": false,
        "redirectUri": null,
        "responseTypes": [],
        "extensions": {},
        "grantType": "client_credentials",
        "refreshTokenRequest": null
      },
      "clientOnly": true,
      "name": "statistics-service"
    }
    """;

  public const string CouldNotFetchUserDetails = """
    { "error": "Could not fetch user details" }
    """;
}
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/UserInfoAuthenticationHandlerTest.cs`):

~~~csharp
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class UserInfoAuthenticationHandlerTest
{
  private const string UserInfoUri = "http://auth-service:5000/uaa/users/current";

  [Fact]
  public async Task ShouldAuthenticateTokenIssuedByAuthorizationServer()
  {
    var handlerStub = new StubHttpMessageHandler(_ =>
      StubHttpMessageHandler.Json(HttpStatusCode.OK, SourceTokenBodies.BrowserUserToken));

    AuthenticateResult result = await AuthenticateAsync(handlerStub, "valid-user-token");

    Assert.True(result.Succeeded);
    Assert.Equal("demo", result.Principal!.Identity!.Name);
    Assert.Equal("browser", result.Principal.FindFirstValue(PiggyMetricsClaims.ClientId));
    Assert.Equal("ui", result.Principal.FindFirstValue(PiggyMetricsClaims.Scope));
  }

  [Fact]
  public async Task ShouldCarryServerScopeForServiceToken()
  {
    var handlerStub = new StubHttpMessageHandler(_ =>
      StubHttpMessageHandler.Json(HttpStatusCode.OK, SourceTokenBodies.ServiceServerToken));

    AuthenticateResult result = await AuthenticateAsync(handlerStub, "valid-server-token");

    Assert.True(result.Succeeded);
    Assert.Equal("statistics-service", result.Principal!.Identity!.Name);
    Assert.Contains(
      result.Principal.Claims,
      claim => claim.Type == PiggyMetricsClaims.Scope && claim.Value == "server");
  }

  [Fact]
  public async Task ShouldSendBearerTokenToUserInfoEndpoint()
  {
    var handlerStub = new StubHttpMessageHandler(_ =>
      StubHttpMessageHandler.Json(HttpStatusCode.OK, SourceTokenBodies.BrowserUserToken));

    await AuthenticateAsync(handlerStub, "valid-user-token");

    HttpRequestMessage request = Assert.Single(handlerStub.Requests);
    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal(UserInfoUri, request.RequestUri!.ToString());
    Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
    Assert.Equal("valid-user-token", request.Headers.Authorization.Parameter);
  }

  [Fact]
  public async Task ShouldRejectTokenTheAuthorizationServerDidNotIssue()
  {
    var handlerStub = new StubHttpMessageHandler(_ =>
      StubHttpMessageHandler.Json(HttpStatusCode.Unauthorized, "{}"));

    AuthenticateResult result = await AuthenticateAsync(handlerStub, "forged-token");

    Assert.False(result.Succeeded);
    Assert.Null(result.Principal);
  }

  [Fact]
  public async Task ShouldRejectTokenWhenUserInfoReturnsError()
  {
    var handlerStub = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
      HttpStatusCode.OK,
      SourceTokenBodies.CouldNotFetchUserDetails));

    AuthenticateResult result = await AuthenticateAsync(handlerStub, "forged-token");

    Assert.False(result.Succeeded);
  }

  [Fact]
  public async Task ShouldNotAuthenticateWithoutAuthorizationHeader()
  {
    var handlerStub = new StubHttpMessageHandler(_ =>
      StubHttpMessageHandler.Json(HttpStatusCode.OK, SourceTokenBodies.BrowserUserToken));

    AuthenticateResult result = await AuthenticateAsync(handlerStub, token: null);

    Assert.True(result.None);
    Assert.Empty(handlerStub.Requests);
  }

  [Fact]
  public async Task ShouldChallengeWith401()
  {
    var handlerStub = new StubHttpMessageHandler(_ =>
      StubHttpMessageHandler.Json(HttpStatusCode.OK, SourceTokenBodies.BrowserUserToken));
    (UserInfoAuthenticationHandler handler, DefaultHttpContext context) =
      await CreateHandlerAsync(handlerStub, token: null);

    await handler.ChallengeAsync(properties: null);

    Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
  }

  [Fact]
  public async Task ShouldForbidWith403()
  {
    var handlerStub = new StubHttpMessageHandler(_ =>
      StubHttpMessageHandler.Json(HttpStatusCode.OK, SourceTokenBodies.BrowserUserToken));
    (UserInfoAuthenticationHandler handler, DefaultHttpContext context) =
      await CreateHandlerAsync(handlerStub, "valid-user-token");

    await handler.ForbidAsync(properties: null);

    Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
  }

  private static async Task<AuthenticateResult> AuthenticateAsync(
    StubHttpMessageHandler handlerStub,
    string? token)
  {
    (UserInfoAuthenticationHandler handler, _) = await CreateHandlerAsync(handlerStub, token);
    return await handler.AuthenticateAsync();
  }

  private static async Task<(UserInfoAuthenticationHandler Handler, DefaultHttpContext Context)>
    CreateHandlerAsync(StubHttpMessageHandler handlerStub, string? token)
  {
    var options = new UserInfoAuthenticationOptions
    {
      UserInfoUri = UserInfoUri,
      ClientId = "account-service",
    };

    var handler = new UserInfoAuthenticationHandler(
      new StubOptionsMonitor<UserInfoAuthenticationOptions>(options),
      NullLoggerFactory.Instance,
      UrlEncoder.Default,
      new StubHttpClientFactory(handlerStub));

    var context = new DefaultHttpContext();
    if (token is not null)
    {
      context.Request.Headers.Authorization = $"Bearer {token}";
    }

    var scheme = new AuthenticationScheme(
      UserInfoAuthenticationHandler.SchemeName,
      displayName: null,
      typeof(UserInfoAuthenticationHandler));
    await handler.InitializeAsync(scheme, context);
    return (handler, context);
  }
}
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/ClientCredentialsTokenCacheTest.cs`):

~~~csharp
using System.Net;
using Microsoft.Extensions.Options;
using PiggyMetrics.Shared.Http;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class ClientCredentialsTokenCacheTest
{
  [Fact]
  public async Task ShouldRequestTokenWithBasicAuthAndClientCredentialsGrant()
  {
    var handlerStub = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
      HttpStatusCode.OK,
      """{"access_token":"token-1","token_type":"bearer","expires_in":43199,"scope":"server"}"""));
    ClientCredentialsTokenCache cache = CreateCache(handlerStub);

    string token = await cache.GetAccessTokenAsync(CancellationToken.None);

    Assert.Equal("token-1", token);
    HttpRequestMessage request = Assert.Single(handlerStub.Requests);
    Assert.Equal(HttpMethod.Post, request.Method);
    Assert.Equal(
      "http://auth-service:5000/uaa/oauth/token",
      request.RequestUri!.ToString());
    Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
    string form = Assert.Single(handlerStub.RequestBodies);
    Assert.Contains("grant_type=client_credentials", form);
    Assert.Contains("scope=server", form);
  }

  [Fact]
  public async Task ShouldReuseCachedTokenUntilItExpires()
  {
    var handlerStub = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(
      HttpStatusCode.OK,
      """{"access_token":"token-1","token_type":"bearer","expires_in":43199,"scope":"server"}"""));
    ClientCredentialsTokenCache cache = CreateCache(handlerStub);

    await cache.GetAccessTokenAsync(CancellationToken.None);
    await cache.GetAccessTokenAsync(CancellationToken.None);
    await cache.GetAccessTokenAsync(CancellationToken.None);

    Assert.Single(handlerStub.Requests);
  }

  [Fact]
  public async Task ShouldRequestNewTokenOnceTheCachedOneExpired()
  {
    int issued = 0;
    var handlerStub = new StubHttpMessageHandler(_ =>
    {
      issued++;
      return StubHttpMessageHandler.Json(
        HttpStatusCode.OK,
        $$"""{"access_token":"token-{{issued}}","token_type":"bearer","expires_in":0,"scope":"server"}""");
    });
    ClientCredentialsTokenCache cache = CreateCache(handlerStub);

    string first = await cache.GetAccessTokenAsync(CancellationToken.None);
    string second = await cache.GetAccessTokenAsync(CancellationToken.None);

    Assert.Equal("token-1", first);
    Assert.Equal("token-2", second);
    Assert.Equal(2, handlerStub.Requests.Count);
  }

  private static ClientCredentialsTokenCache CreateCache(StubHttpMessageHandler handlerStub)
  {
    var options = new OAuth2ClientOptions
    {
      ClientId = "account-service",
      ClientSecret = "secret",
      AccessTokenUri = "http://auth-service:5000/uaa/oauth/token",
      GrantType = "client_credentials",
      Scope = "server",
    };
    return new ClientCredentialsTokenCache(
      new StubHttpClientFactory(handlerStub),
      Options.Create(options));
  }
}
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/MongoConventionsTest.cs`):

~~~csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using PiggyMetrics.Shared.Persistence;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public enum SampleCurrency
{
  USD,
  EUR,
  RUB,
}

public sealed class SampleItem
{
  public string Title { get; set; } = string.Empty;

  public decimal Amount { get; set; }

  public SampleCurrency Currency { get; set; }

  public string? Icon { get; set; }
}

public class MongoConventionsTest
{
  public MongoConventionsTest()
  {
    MongoConventions.Register();
  }

  [Fact]
  public void ShouldWriteSourceFieldNamesInCamelCase()
  {
    var document = new SampleItem { Title = "Rent", Amount = 1300m }.ToBsonDocument();

    Assert.True(document.Contains("title"));
    Assert.True(document.Contains("amount"));
    Assert.True(document.Contains("currency"));
  }

  [Fact]
  public void ShouldWriteDecimalAsStringLikeSpringDataMongo()
  {
    var document = new SampleItem { Title = "Rent", Amount = 1300m }.ToBsonDocument();

    Assert.Equal(BsonType.String, document["amount"].BsonType);
    Assert.Equal("1300", document["amount"].AsString);
  }

  [Fact]
  public void ShouldWriteEnumAsName()
  {
    var document = new SampleItem { Currency = SampleCurrency.EUR }.ToBsonDocument();

    Assert.Equal("EUR", document["currency"].AsString);
  }

  [Fact]
  public void ShouldOmitNullFieldsLikeSpringDataMongo()
  {
    var document = new SampleItem { Title = "Rent", Icon = null }.ToBsonDocument();

    Assert.False(document.Contains("icon"));
  }

  [Fact]
  public void ShouldReadNumericAmountSeededByTheSourceDump()
  {
    var document = new BsonDocument
    {
      ["title"] = "Rent",
      ["amount"] = 1300,
      ["currency"] = "USD",
      ["unknownField"] = "ignored",
    };

    SampleItem item = BsonSerializer.Deserialize<SampleItem>(document);

    Assert.Equal(1300m, item.Amount);
    Assert.Equal(SampleCurrency.USD, item.Currency);
  }

  [Fact]
  public void ShouldReadDecimalAmountStoredAsString()
  {
    var document = new BsonDocument
    {
      ["title"] = "Interest",
      ["amount"] = "3.32",
      ["currency"] = "USD",
    };

    SampleItem item = BsonSerializer.Deserialize<SampleItem>(document);

    Assert.Equal(3.32m, item.Amount);
  }
}
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/PiggyMetricsJsonTest.cs`):

~~~csharp
using System.Text.Json;
using PiggyMetrics.Shared.Json;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public sealed class SampleAccount
{
  public string Name { get; set; } = string.Empty;

  public DateTime LastSeen { get; set; }

  public SampleCurrency Currency { get; set; }

  public string? Note { get; set; }
}

public class PiggyMetricsJsonTest
{
  private static readonly JsonSerializerOptions Options = PiggyMetricsJson.Create();

  [Fact]
  public void ShouldWriteEnumAsName()
  {
    string json = JsonSerializer.Serialize(
      new SampleAccount { Currency = SampleCurrency.RUB },
      Options);

    Assert.Contains("\"currency\":\"RUB\"", json);
  }

  [Fact]
  public void ShouldWriteDateAsEpochMilliseconds()
  {
    var lastSeen = new DateTime(2018, 6, 15, 10, 30, 0, DateTimeKind.Utc);

    string json = JsonSerializer.Serialize(
      new SampleAccount { LastSeen = lastSeen },
      Options);

    Assert.Contains("\"lastSeen\":1529058600000", json);
  }

  [Fact]
  public void ShouldReadDateFromEpochMilliseconds()
  {
    SampleAccount account = JsonSerializer.Deserialize<SampleAccount>(
      """{"name":"demo","lastSeen":1529058600000,"currency":"USD"}""",
      Options)!;

    Assert.Equal(
      new DateTime(2018, 6, 15, 10, 30, 0, DateTimeKind.Utc),
      account.LastSeen);
  }

  [Fact]
  public void ShouldIgnoreUnknownFields()
  {
    SampleAccount account = JsonSerializer.Deserialize<SampleAccount>(
      """{"name":"demo","currency":"USD","unexpected":{"nested":true}}""",
      Options)!;

    Assert.Equal("demo", account.Name);
  }

  [Fact]
  public void ShouldKeepNullFieldsLikeJackson()
  {
    string json = JsonSerializer.Serialize(
      new SampleAccount { Name = "demo", Note = null },
      Options);

    Assert.Contains("\"note\":null", json);
  }
}
~~~

exact_snippet (`tests/PiggyMetrics.Shared.Tests/HealthEndpointsTest.cs`):

~~~csharp
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PiggyMetrics.Shared.Health;
using Xunit;

namespace PiggyMetrics.Shared.Tests;

public class HealthEndpointsTest
{
  [Fact]
  public async Task ShouldWriteActuatorUpBody()
  {
    string body = await WriteAsync(HealthStatus.Healthy);

    Assert.Equal("{\"status\":\"UP\"}", body);
  }

  [Fact]
  public async Task ShouldWriteActuatorDownBody()
  {
    string body = await WriteAsync(HealthStatus.Unhealthy);

    Assert.Equal("{\"status\":\"DOWN\"}", body);
  }

  private static async Task<string> WriteAsync(HealthStatus status)
  {
    var context = new DefaultHttpContext();
    var stream = new MemoryStream();
    context.Response.Body = stream;

    var report = new HealthReport(
      new Dictionary<string, HealthReportEntry>(),
      status,
      TimeSpan.Zero);

    await HealthEndpoints.WriteActuatorResponse(context, report);

    return Encoding.UTF8.GetString(stream.ToArray());
  }
}
~~~

exact_commands:

~~~bash
dotnet test tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj
~~~

**Invariants.**
- The suite reaches no network; every outbound call goes through the recording stub handler.
- The user-info fixtures carry the source's member names, including the top-level name member the handler resolves the principal from.
- The suite reports 24 passing tests and no skipped tests.
- The store convention tests assert the BSON type, not only the value, so a silent switch to Decimal128 fails.

**Edge cases.**
- The recording stub reads the request body at send time, because the framework disposes form content once the request completes.
- The store convention tests call Register in their constructor, which is safe because Register is idempotent.

**Do not.**
- Do not reach a live authorization server or a live Mongo instance from this suite.
- Do not assert on a principal name the source would not produce.
- Do not weaken an assertion to make a later change pass; change the implementation instead.

**Verify (machine).**
- dotnet test tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj reports Passed with 24 tests and 0 failures

**Verify (human).**
- ShouldAuthenticateTokenIssuedByAuthorizationServer and ShouldRejectTokenTheAuthorizationServerDidNotIssue both appear in the run.
- No test takes longer than a second, confirming nothing reaches the network.

**Acceptance.**
- P1's done-when clause is demonstrated: the handler accepts a token the source's authorization server issued and refuses one it did not.

**Rollback.** `git rm -r tests/PiggyMetrics.Shared.Tests`

**Trace.** requirements REQ-P1.T2, REQ-P1-DONE; decisions D-013; research R-004, R-005

## STEP-014 (P1.T1) - Wire the solution and prove every project builds and the suite is green

Depends on: STEP-012, STEP-013.

**Why.** P1.T1 requires one solution carrying every project, and P1's done-when clause opens with every project builds. The solution file is generated by the SDK rather than pinned, because its project identifiers are generated values. Decision D-001.

**Currently.** Root pom.xml lines 41-51 declare the module list that the solution replaces. The target repository has no solution file before this step.

**Change.** Generate PiggyMetrics.sln at the repository root with the SDK and add the six projects plus the test project to it in the order Shared, Gateway, AuthService, AccountService, StatisticsService, NotificationService, PiggyMetrics.Shared.Tests. Then build the whole solution with warnings as errors and run the whole test suite, which is the gate the downstream plans P2 and P3 build on.

**Files.**
- `PiggyMetrics.sln`

exact_commands:

~~~bash
dotnet new sln --name PiggyMetrics
dotnet sln PiggyMetrics.sln add src/Shared/PiggyMetrics.Shared.csproj src/Gateway/PiggyMetrics.Gateway.csproj src/AuthService/PiggyMetrics.AuthService.csproj src/AccountService/PiggyMetrics.AccountService.csproj src/StatisticsService/PiggyMetrics.StatisticsService.csproj src/NotificationService/PiggyMetrics.NotificationService.csproj tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj
dotnet build PiggyMetrics.sln -warnaserror
dotnet test PiggyMetrics.sln
~~~

**Invariants.**
- The solution carries exactly seven projects.
- The whole solution builds with zero warnings under warnings-as-errors.
- The whole suite reports 24 passing tests and zero failures.
- The solution file is generated, so its project identifiers are whatever the SDK assigns.

**Edge cases.**
- A build host without ICU aborts the SDK with NETSDK1188; install libicu before running these commands.
- Re-running dotnet sln add for a project already in the solution is a no-op rather than a duplicate entry.

**Do not.**
- Do not hand-write the solution file or pin its project identifiers.
- Do not add config, registry, monitoring or turbine-stream-service projects to the solution.
- Do not mark the build green while any warning remains; warnings are errors in this repository.

**Verify (machine).**
- dotnet build PiggyMetrics.sln -warnaserror exits 0 and prints 0 Warning(s) and 0 Error(s)
- dotnet test PiggyMetrics.sln prints Passed with 24 tests

**Verify (human).**
- dotnet sln PiggyMetrics.sln list shows seven projects.
- The build output names PiggyMetrics.Shared and the five service assemblies.

**Acceptance.**
- Every project builds, which is the first half of P1's done-when clause.
- The Shared suite is green, which is the second half.
- P2 and P3 can start against a solution that already carries their project and the Shared contracts they depend on.

**Rollback.** `git rm PiggyMetrics.sln`

**Trace.** requirements REQ-P1.T1, REQ-P1-DONE; decisions D-001; research R-001

## Out of scope

- Plans P2 and later (AuthService, StatisticsService, AccountService, NotificationService, Gateway behaviour). They depend on P1 and are planned separately.
- `config`, `registry`, `monitoring` and `turbine-stream-service`: P1.T1 names five runtime services and none of these is one.
- Service discovery, the config server, the RabbitMQ bus and Hystrix dashboard streams.
- Domain types, repositories, controllers and ported Java tests for any service.
- Any data migration: no collection or database is renamed, reshaped or copied.

## Done when

P1's done-when clause has two halves and both are executable:

- **Every project builds** - `dotnet build PiggyMetrics.sln -warnaserror` exits 0 with zero warnings (STEP-014).
- **Shared's bearer handler accepts a token the source's authorization server issued and refuses one it did not** - `ShouldAuthenticateTokenIssuedByAuthorizationServer` and `ShouldRejectTokenTheAuthorizationServerDidNotIssue` pass inside a green 24-test run (STEP-013).
