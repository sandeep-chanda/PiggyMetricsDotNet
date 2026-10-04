# Pinned sources for PiggyMetricsDotNet P1

Companion outcome file to `final/PLAYBOOK.md`. This carries the complete final body of every file plan P1 creates - 47 files across 13 steps - grouped by STEP id in the order the playbook lists them. Read a step's playbook section for the why, the invariants, the do-not list and the verification; read its section here for the exact text to write. Nothing here is summarised and nothing is left for the implementer to invent.

Every body below is the text of a file in a reference implementation that was built and run during planning: `dotnet build PiggyMetrics.sln -warnaserror` reported `0 Warning(s)` and `0 Error(s)`, and `dotnet test PiggyMetrics.sln` reported `Passed! - Failed: 0, Passed: 24`.

STEP-014 has no pinned file: it generates `PiggyMetrics.sln` with the two `dotnet` commands its playbook section pins.

## STEP-001 (P1.T1) - Pin the repository build contract: target framework and package versions

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

## STEP-002 (P1.T1) - Create the Shared class library and pin the source's timeouts

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

## STEP-003 (P1.T2) - Port the bearer handler that resolves a token against the authorization server

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

## STEP-004 (P1.T2) - Register the resource server and the user and server authorization policies

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

## STEP-005 (P1.T3) - Port the store conventions and the decimal serializer that match the documents on disk

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

## STEP-006 (P1.T3) - Register the Mongo database handle from the source's connection settings

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

## STEP-007 (P1.T4) - Port the client-credentials token cache and the outbound bearer handler

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

## STEP-008 (P1.T4) - Expose the typed-client base so each edge gets one HTTP client at the source's timeout

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

## STEP-009 (P1.T5) - Port the JSON options: enum names, ignored unknown fields, epoch-millisecond dates

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

## STEP-010 (P1.T5) - Port the actuator health endpoint with the source's path, body and status codes

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

## STEP-011 (P1.T5) - Compose the service defaults: port, context path, resource server, JSON and health

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

## STEP-012 (P1.T1) - Create the five runtime service projects on the source's ports and context paths

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

## STEP-013 (P1.T2) - Add the Shared test project that proves the plan's done-when clause

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
