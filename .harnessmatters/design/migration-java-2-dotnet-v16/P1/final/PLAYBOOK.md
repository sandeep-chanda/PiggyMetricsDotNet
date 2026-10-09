---
name: PiggyMetrics Java to .NET migration - P1 solution skeleton and Shared
overview: >-
  Stand up PiggyMetrics.sln, the Shared library and the five runtime service projects for the Java
  to .NET migration, carrying over the source's bearer validation, store conventions, edge timeout
  and token cache, actuator health contract and JSON contract. Eleven steps pin thirty-two files
  in full; P2 and P3 port the services on top.
isProject: true
todos:
- id: 1
  content: STEP-001 - Create the repository build configuration the whole solution inherits (P1.T1)
  status: pending
- id: 2
  content: STEP-002 - Create the solution and the Shared class library project (P1.T1)
  status: pending
- id: 3
  content: STEP-003 - Pin the shared JSON contract and the source's date rendering (P1.T5)
  status: pending
- id: 4
  content: STEP-004 - Pin the store conventions, serializers and collection naming (P1.T3)
  status: pending
- id: 5
  content: STEP-005 - Pin the bearer handler that resolves a token through the authorization server (P1.T2)
  status: pending
- id: 6
  content: STEP-006 - Register the bearer scheme and the user and server authorization policies (P1.T2)
  status: pending
- id: 7
  content: STEP-007 - Pin the typed-client base, the source's timeout and the client-credentials token cache (P1.T4)
  status: pending
- id: 8
  content: STEP-008 - Pin the actuator health endpoint and the service default composition (P1.T5)
  status: pending
- id: 9
  content: STEP-009 - Create the five runtime service projects on the shared defaults (P1.T1)
  status: pending
- id: 10
  content: STEP-010 - Add the Shared test project and prove the bearer handler accepts and refuses (P1.T2)
  status: pending
- id: 11
  content: STEP-011 - Gate P1 on a clean solution build and a green test run (P1.T1, P1.T2, P1.T3, P1.T4, P1.T5)
  status: pending
---

# P1 - Solution skeleton and Shared

The target repository is empty: `git ls-files` returns README.md, AGENTS.md and the HarnessMatters metadata and nothing else. Everything below is therefore new code, and every shape is pinned from a cited file in the source Spring Boot reactor (sqshq/piggymetrics), which was cloned read-only while this plan was written. Execute the steps literally. If something here is unspecified, stop: that is a planner defect, not a licence to invent.

## How to read the pins

Each step gives the complete final body of every file it touches, in a labelled fence under an `exact_snippet (path):` heading, and the shell commands under `exact_commands:`. Copy the bodies verbatim. Every pinned file is indented with one tab per level (DEC-015). Nothing here is an outline. Each step's edge cases, acceptance list and rollback command are in `planning/implementation-plan.yaml`, in the committed planner workspace.

## Decisions already made

- **DEC-001** Target net10.0 with an SDK roll-forward pin
- **DEC-002** Fixed src and tests layout with PiggyMetrics root namespaces
- **DEC-003** Validate bearer tokens by calling the authorization server's user-info endpoint
- **DEC-004** Name the two authorization policies user and server in Shared
- **DEC-005** Register one MongoDB convention pack reproducing Spring Data's write shape
- **DEC-006** Pin collection names with MongoCollectionAttribute
- **DEC-007** Use 10000 ms as the edge timeout
- **DEC-008** Cache one client-credentials token per process and renew it at expiry
- **DEC-009** Reproduce the source's JSON contract once, in Shared
- **DEC-010** Expose only /actuator/health, with the Boot 2.0 body shape
- **DEC-011** Create tests/Shared.Tests inside P1
- **DEC-012** Central package management with pinned versions
- **DEC-013** Carry over no service-discovery, config-server or Hystrix infrastructure
- **DEC-014** Offer a token-carrying client registration and a token-free one
- **DEC-015** Indent every pinned source file with tabs

Each decision's full statement, rejected alternatives and evidence are in `architecture/decisions.yaml`, shipped with this package.

## Repository layout when P1 is done

~~~text
PiggyMetrics.sln
global.json
Directory.Build.props
Directory.Packages.props
.gitignore
src/Shared/            class library: Json, Security, Mongo, Http, Health, ServiceDefaults
src/Gateway/           web, port 4000, health only
src/AuthService/       web, port 5000, path /uaa
src/AccountService/    web, port 6000, path /accounts
src/StatisticsService/ web, port 7000, path /statistics
src/NotificationService/ web, port 8000, path /notifications
tests/Shared.Tests/    xunit over TestHost, five facts on the bearer handler
~~~

## Source contracts this plan preserves

- Token validation is a call to `http://auth-service:5000/uaa/users/current` carrying the presented token; the tokens are opaque values in an InMemoryTokenStore, so no local validation is possible (account-service CustomUserInfoTokenServices.java:36-137, shared/application.yml:20-23).
- Two access shapes: `user` is `anyRequest().authenticated()`, `server` is `#oauth2.hasScope('server')` (auth-service UserController.java:22-31, statistics-service StatisticsController.java:20-36).
- Collections `accounts`, `users`, `datapoints`, `recipients` with lower-camel field names, the `@Id` member as `_id`, enums as strings, BigDecimal as a string, nulls omitted, dates as UTC BSON datetimes.
- An outbound service edge times out after 10000 ms (shared/application.yml:7-13); 20000 ms is the gateway's own setting (shared/gateway.yml:1-18).
- A java.util.Date is written as `2018-06-01T12:00:00.000+0000`: Spring Boot 2.0.3 turns WRITE_DATES_AS_TIMESTAMPS off and Jackson 2.9.6 StdDateFormat renders UTC with a colon-less offset.
- `GET /actuator/health` answers 200 with the body `{"status":"UP"}` and nothing else (config/Dockerfile:7, Boot 2.0 hides health detail by default).

## STEP-001 - Create the repository build configuration the whole solution inherits

Todos P1.T1. Depends on nothing. Critical path: yes.

**Why.** P1.T1 derives the .NET solution from account-service/pom.xml. The Maven reactor held the language level and every dependency version in one parent (pom.xml:17-27), so the modules declared none (account-service/pom.xml:19-96). DEC-001 and DEC-012 carry that single source of truth to MSBuild.

**Before.** TargetRepo tracks no project or MSBuild file. Source: pom.xml:1-50, account-service/pom.xml:19-96. Full citations in `planning/implementation-plan.yaml`.

**Files.** `global.json`, `.gitignore`, `Directory.Build.props`, `Directory.Packages.props`

exact_snippet (`global.json`):

~~~json
{
	"sdk": {
		"version": "10.0.100",
		"rollForward": "latestMajor",
		"allowPrerelease": false
	}
}
~~~

exact_snippet (`.gitignore`):

~~~text
bin/
obj/
.vs/
.vscode/
*.user
TestResults/
artifacts/
~~~

exact_snippet (`Directory.Build.props`):

~~~xml
<Project>
	<PropertyGroup>
		<TargetFramework>net10.0</TargetFramework>
		<LangVersion>latest</LangVersion>
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
		<InvariantGlobalization>true</InvariantGlobalization>
		<GenerateDocumentationFile>false</GenerateDocumentationFile>
		<EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild>
	</PropertyGroup>
</Project>
~~~

exact_snippet (`Directory.Packages.props`):

~~~xml
<Project>
	<PropertyGroup>
		<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
	</PropertyGroup>
	<ItemGroup>
		<PackageVersion Include="MongoDB.Driver" Version="3.12.0" />
	</ItemGroup>
	<ItemGroup>
		<PackageVersion Include="Microsoft.AspNetCore.TestHost" Version="10.0.12" />
		<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
		<PackageVersion Include="xunit" Version="2.9.3" />
		<PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
	</ItemGroup>
</Project>
~~~

exact_commands:

~~~bash
git ls-files
dotnet --info
~~~

**Verify.**
- test -f global.json && test -f Directory.Build.props && test -f Directory.Packages.props && test -f .gitignore
- python3 -c "import json;json.load(open('global.json'))"

**Invariants.**
- No csproj in this repository declares a TargetFramework element; the property comes from Directory.Build.props only.
- No csproj in this repository declares a Version attribute on PackageReference; versions come from Directory.Packages.props only.
- global.json rollForward latestMajor keeps the build working on an SDK newer than 10.0.100 without a second pin.

**Do not.**
- Do not add a Version attribute to any PackageReference anywhere in the repository.
- Do not add NuGet.config, a props file per project, or a second TargetFramework declaration.
- Do not pin an SDK feature band that forbids roll-forward.
- Do not create any source project in this step.

## STEP-002 - Create the solution and the Shared class library project

Todos P1.T1. Depends on STEP-001. Critical path: yes.

**Why.** P1.T1 names Shared beside the five runtime services, and the P1 goal line calls it the library every service plan builds on. Shared holds ASP.NET Core and MongoDB types, so it needs the Microsoft.AspNetCore.App framework reference and the one NuGet dependency P1 uses.

**Before.** No solution exists. Source: pom.xml:38-48 (reactor module list); the four infrastructure modules are out of scope per DEC-013. Full citations in `planning/implementation-plan.yaml`.

**Files.** `PiggyMetrics.sln`, `src/Shared/Shared.csproj`

exact_snippet (`src/Shared/Shared.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<RootNamespace>PiggyMetrics.Shared</RootNamespace>
		<AssemblyName>Shared</AssemblyName>
	</PropertyGroup>
	<ItemGroup>
		<FrameworkReference Include="Microsoft.AspNetCore.App" />
	</ItemGroup>
	<ItemGroup>
		<PackageReference Include="MongoDB.Driver" />
	</ItemGroup>
</Project>
~~~

exact_commands:

~~~bash
dotnet new sln --name PiggyMetrics --output .
mkdir -p src/Shared/Json src/Shared/Security src/Shared/Mongo src/Shared/Http src/Shared/Health
dotnet sln PiggyMetrics.sln add src/Shared/Shared.csproj
dotnet restore src/Shared/Shared.csproj
~~~

**Verify.**
- dotnet sln PiggyMetrics.sln list
- dotnet build src/Shared/Shared.csproj -c Debug

**Invariants.**
- Shared stays a class library; it never becomes a web project and never gains an entry point.
- Shared references no service project; dependencies point from the services into Shared only.
- Shared's assembly name stays Shared so later plans reference it by that name.

**Do not.**
- Do not hand-author the solution GUIDs; produce PiggyMetrics.sln with dotnet new sln.
- Do not add a TargetFramework or a package version to Shared.csproj.
- Do not add Eureka, Spring Cloud Config, Hystrix or Turbine client equivalents to Shared.
- Do not create the service projects in this step.

## STEP-003 - Pin the shared JSON contract and the source's date rendering

Todos P1.T5. Depends on STEP-002. Critical path: no.

**Why.** P1.T5 asks for enums as names, unknown fields ignored and the source's date format. R-007 proves the source renders a java.util.Date through StdDateFormat as UTC yyyy-MM-dd'T'HH:mm:ss.SSSZ with a colon-less offset. DEC-009 reproduces that contract once, in Shared, so no service re-derives it.

**Before.** No JSON configuration exists. Source: spring-boot 2.0.3 JacksonAutoConfiguration:86, spring-framework 5.0.7 Jackson2ObjectMapperBuilder:687-694, jackson-databind 2.9.6 StdDateFormat:53 and :151, Currency.java:3-8. Full citations in `planning/implementation-plan.yaml`.

**Files.** `src/Shared/Json/PiggyMetricsJson.cs`

exact_snippet (`src/Shared/Json/PiggyMetricsJson.cs`):

~~~csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PiggyMetrics.Shared.Json;

// Reads and writes exactly as the source stack does: Spring Boot 2.0.3 disables
// SerializationFeature.WRITE_DATES_AS_TIMESTAMPS, so Jackson 2.9.6 falls back to StdDateFormat, whose pattern
// is yyyy-MM-dd'T'HH:mm:ss.SSSZ rendered in UTC with a colon-less offset (2018-06-01T12:00:00.000+0000).
public sealed class JacksonDateTimeConverter : JsonConverter<DateTime>
{
	public const string WriteFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";
	public const string WriteOffset = "+0000";

	public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			return DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64()).UtcDateTime;
		}

		var text = reader.GetString();
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new JsonException("A date value must not be empty.");
		}

		if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochMilliseconds))
		{
			return DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds).UtcDateTime;
		}

		return DateTimeOffset.Parse(
			text,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal).UtcDateTime;
	}

	public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
	{
		var utc = value.Kind == DateTimeKind.Unspecified
			? DateTime.SpecifyKind(value, DateTimeKind.Utc)
			: value.ToUniversalTime();
		writer.WriteStringValue(utc.ToString(WriteFormat, CultureInfo.InvariantCulture) + WriteOffset);
	}
}

// The single JSON contract for every PiggyMetrics .NET service: camelCase names, enum values as their declared
// names, unknown incoming members skipped, Jackson's date rendering.
public static class PiggyMetricsJson
{
	public static readonly JsonSerializerOptions Options = CreateOptions();

	public static JsonSerializerOptions CreateOptions()
	{
		var options = new JsonSerializerOptions();
		Configure(options);
		options.MakeReadOnly();
		return options;
	}

	public static void Configure(JsonSerializerOptions options)
	{
		options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		options.PropertyNameCaseInsensitive = false;
		options.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
		options.NumberHandling = JsonNumberHandling.AllowReadingFromString;
		options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
		options.Converters.Add(new JacksonDateTimeConverter());
	}
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/Shared.csproj -c Debug
~~~

**Verify.**
- dotnet build src/Shared/Shared.csproj -c Debug

**Invariants.**
- Every service uses PiggyMetricsJson.Configure; no service builds its own JsonSerializerOptions.
- A DateTime is always rendered in UTC with exactly three fractional digits and the literal +0000 offset.
- An incoming member the target type does not declare is skipped and never raises an error.
- An enum written as an integer in an incoming payload is rejected, because the source's Jackson default rejects it too.

**Do not.**
- Do not set PropertyNameCaseInsensitive to true; the source's Jackson matches property names case-sensitively.
- Do not register System.Text.Json's default ISO-8601 DateTime handling; it emits a colon in the offset and a variable fractional length.
- Do not add Newtonsoft.Json.
- Do not add a naming policy to the enum converter; the source writes the declared enum constant name.

## STEP-004 - Pin the store conventions, serializers and collection naming

Todos P1.T3. Depends on STEP-002. Critical path: no.

**Why.** P1.T3 asks for store conventions and serializers against the same databases and collections. The source inherits them from Spring Data MongoDB: @Id becomes _id, property names are written in Java camelCase, nulls are skipped, enums are strings and BigDecimal is a string. The MongoDB .NET driver defaults the other way on all four points (R-010), so DEC-005 and DEC-006 state them explicitly.

**Before.** No store code exists. Source: Account.java:14-37, AccountRepository.java:7-11, User.java:10-17, DataPoint.java:15-24, Recipient.java:11-16, shared/account-service.yml:10-16, shared/auth-service.yml:1-8, mongodb/init.sh:11-13, mongodb/dump/account-service-dump.js. Full citations in `planning/implementation-plan.yaml`.

**Files.** `src/Shared/Mongo/MongoConventions.cs`, `src/Shared/Mongo/MongoExtensions.cs`

exact_snippet (`src/Shared/Mongo/MongoConventions.cs`):

~~~csharp
using System.Globalization;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace PiggyMetrics.Shared.Mongo;

// Names the collection a document type lives in, so the .NET services keep the collection names the source's
// @Document(collection = "...") annotations established.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MongoCollectionAttribute : Attribute
{
	public MongoCollectionAttribute(string name)
	{
		Name = name;
	}

	public string Name { get; }
}

// Stores a decimal the way Spring Data MongoDB's BigDecimalToStringConverter does: as a BSON string. Reads
// tolerate the numeric shapes a hand-seeded document carries.
public sealed class BigDecimalSerializer : SerializerBase<decimal>
{
	public override decimal Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
	{
		var reader = context.Reader;
		var bsonType = reader.GetCurrentBsonType();
		switch (bsonType)
		{
			case BsonType.String:
				return decimal.Parse(reader.ReadString(), NumberStyles.Float, CultureInfo.InvariantCulture);
			case BsonType.Double:
				return (decimal)reader.ReadDouble();
			case BsonType.Int32:
				return reader.ReadInt32();
			case BsonType.Int64:
				return reader.ReadInt64();
			case BsonType.Decimal128:
				return (decimal)reader.ReadDecimal128();
			default:
				throw new FormatException($"A decimal cannot be read from BSON type {bsonType}.");
		}
	}

	public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, decimal value)
	{
		context.Writer.WriteString(value.ToString(CultureInfo.InvariantCulture));
	}
}

// One registration point for the document conventions every PiggyMetrics store shares.
public static class MongoConventions
{
	public const string ConventionPackName = "piggymetrics";

	private static int _registered;

	public static void Register()
	{
		if (Interlocked.Exchange(ref _registered, 1) == 1)
		{
			return;
		}

		BsonSerializer.RegisterSerializer(typeof(decimal), new BigDecimalSerializer());
		BsonSerializer.RegisterSerializer(typeof(decimal?), new NullableSerializer<decimal>(new BigDecimalSerializer()));
		BsonSerializer.RegisterSerializer(typeof(DateTime), new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime));
		BsonSerializer.RegisterSerializer(
			typeof(DateTime?),
			new NullableSerializer<DateTime>(new DateTimeSerializer(DateTimeKind.Utc, BsonType.DateTime)));

		var pack = new ConventionPack
		{
			new CamelCaseElementNameConvention(),
			new IgnoreExtraElementsConvention(true),
			new IgnoreIfNullConvention(true),
			new EnumRepresentationConvention(BsonType.String),
		};

		ConventionRegistry.Register(ConventionPackName, pack, _ => true);
	}

	public static string CollectionName<TDocument>()
	{
		var attribute = typeof(TDocument).GetCustomAttribute<MongoCollectionAttribute>();
		if (attribute is null)
		{
			throw new InvalidOperationException(
				$"{typeof(TDocument).FullName} must carry [MongoCollection] so its collection name stays pinned.");
		}

		return attribute.Name;
	}
}
~~~

exact_snippet (`src/Shared/Mongo/MongoExtensions.cs`):

~~~csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace PiggyMetrics.Shared.Mongo;

// The source's spring.data.mongodb.* keys, carried over key for key.
public sealed class MongoStoreOptions
{
	public const string SectionName = "Spring:Data:MongoDB";

	public string Host { get; set; } = "localhost";

	public int Port { get; set; } = 27017;

	public string Database { get; set; } = "piggymetrics";

	public string Username { get; set; } = string.Empty;

	public string Password { get; set; } = string.Empty;

	public string ConnectionString { get; set; } = string.Empty;

	// The source authenticates against the same database it reads, because mongodb/init.sh creates the user inside
	// the piggymetrics database.
	public string BuildConnectionString()
	{
		if (!string.IsNullOrWhiteSpace(ConnectionString))
		{
			return ConnectionString;
		}

		var database = Uri.EscapeDataString(Database);
		if (string.IsNullOrEmpty(Username))
		{
			return $"mongodb://{Host}:{Port}/{database}";
		}

		var user = Uri.EscapeDataString(Username);
		var password = Uri.EscapeDataString(Password);
		return $"mongodb://{user}:{password}@{Host}:{Port}/{database}?authSource={database}";
	}
}

public static class MongoExtensions
{
	public static IServiceCollection AddPiggyMetricsStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		MongoConventions.Register();

		services.Configure<MongoStoreOptions>(configuration.GetSection(MongoStoreOptions.SectionName));

		services.AddSingleton<IMongoClient>(provider =>
		{
			var options = provider.GetRequiredService<IOptions<MongoStoreOptions>>().Value;
			return new MongoClient(options.BuildConnectionString());
		});

		services.AddSingleton(provider =>
		{
			var options = provider.GetRequiredService<IOptions<MongoStoreOptions>>().Value;
			return provider.GetRequiredService<IMongoClient>().GetDatabase(options.Database);
		});

		return services;
	}

	public static IMongoCollection<TDocument> GetDocuments<TDocument>(this IMongoDatabase database)
	{
		return database.GetCollection<TDocument>(MongoConventions.CollectionName<TDocument>());
	}
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/Shared.csproj -c Debug
~~~

**Verify.**
- dotnet build src/Shared/Shared.csproj -c Debug

**Invariants.**
- MongoConventions.Register runs at most once per process and is safe to call from every service.
- Element names are written with a lower-case first letter so existing documents keep matching.
- A null member is not written, matching Spring Data MongoDB, so no document grows a null field.
- An enum member is stored as its declared name, never as an ordinal.
- A decimal member is written as a BSON string, matching BigDecimalToStringConverter.
- A DateTime member is stored as a BSON UTC datetime and read back with Kind Utc.
- A document type without MongoCollectionAttribute fails fast instead of defaulting to a guessed collection name.

**Do not.**
- Do not register a camelCase convention that also lower-cases the _id element name; the id member is mapped by the driver's id convention.
- Do not store a decimal as Decimal128; the source stores it as a string.
- Do not infer a collection name from the class name.
- Do not create document classes, repositories or indexes here; those belong to the per-service plans.

## STEP-005 - Pin the bearer handler that resolves a token through the authorization server

Todos P1.T2. Depends on STEP-002. Critical path: yes.

**Why.** P1.T2 asks for a Shared bearer handler that validates a token the way the resource servers do, calling the user-info endpoint. The source's tokens are opaque values in an InMemoryTokenStore with no published key, so resolving them at that endpoint is the only possible validation (DEC-003). The same call also yields the client id and scopes the controllers gate on.

**Before.** No security code exists. Source: account-service ResourceServerConfig.java:23-60, CustomUserInfoTokenServices.java:36, :68-73, :97-107, :129-137, statistics-service ResourceServerConfig.java:17-24, shared/application.yml:20-23. Full citations in `planning/implementation-plan.yaml`.

**Files.** `src/Shared/Security/PiggyMetricsAuth.cs`, `src/Shared/Security/UserInfoAuthenticationHandler.cs`

exact_snippet (`src/Shared/Security/PiggyMetricsAuth.cs`):

~~~csharp
using Microsoft.AspNetCore.Authentication;

namespace PiggyMetrics.Shared.Security;

// Names and constants shared by every resource server, taken from the source stack.
public static class PiggyMetricsAuth
{
	public const string Scheme = "Bearer";
	public const string HttpClientName = "piggymetrics-userinfo";

	public const string UserPolicy = "user";
	public const string ServerPolicy = "server";

	public const string ScopeClaimType = "scope";
	public const string ClientIdClaimType = "client_id";

	public const string UiScope = "ui";
	public const string ServerScope = "server";

	public const string DefaultAuthority = "ROLE_USER";
	public const string UnknownPrincipal = "unknown";
	public const string DefaultUserInfoUri = "http://auth-service:5000/uaa/users/current";
	public const string UserInfoUriConfigurationKey = "Security:OAuth2:Resource:UserInfoUri";

	// CustomUserInfoTokenServices.PRINCIPAL_KEYS, in the source's probe order.
	public static readonly string[] PrincipalKeys =
	{
		"user", "username", "userid", "user_id", "login", "id", "name",
	};
}

public sealed class UserInfoAuthenticationOptions : AuthenticationSchemeOptions
{
	public string UserInfoUri { get; set; } = PiggyMetricsAuth.DefaultUserInfoUri;

	public string HttpClientName { get; set; } = PiggyMetricsAuth.HttpClientName;
}
~~~

exact_snippet (`src/Shared/Security/UserInfoAuthenticationHandler.cs`):

~~~csharp
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Security;

// The .NET counterpart of CustomUserInfoTokenServices: a presented bearer token is accepted only when the
// authorization server's user-info endpoint resolves it, and the resolved document supplies the principal
// name, the calling client id and the granted scopes.
public sealed class UserInfoAuthenticationHandler : AuthenticationHandler<UserInfoAuthenticationOptions>
{
	private const string BearerPrefix = "Bearer ";
	private const string FailureMessage = "Could not fetch user details";

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
		var header = Request.Headers.Authorization.ToString();
		if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return AuthenticateResult.NoResult();
		}

		var token = header[BearerPrefix.Length..].Trim();
		if (token.Length == 0)
		{
			return AuthenticateResult.NoResult();
		}

		JsonElement document;
		try
		{
			var client = _httpClientFactory.CreateClient(Options.HttpClientName);
			using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInfoUri);
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
			using var response = await client.SendAsync(request, Context.RequestAborted);
			if (!response.IsSuccessStatusCode)
			{
				Logger.LogInformation("{Message}: status {Status}", FailureMessage, (int)response.StatusCode);
				return AuthenticateResult.Fail(FailureMessage);
			}

			await using var stream = await response.Content.ReadAsStreamAsync(Context.RequestAborted);
			using var parsed = await JsonDocument.ParseAsync(stream, cancellationToken: Context.RequestAborted);
			document = parsed.RootElement.Clone();
		}
		catch (Exception exception)
		{
			Logger.LogInformation("{Message}: {Reason}", FailureMessage, exception.Message);
			return AuthenticateResult.Fail(FailureMessage);
		}

		if (document.ValueKind != JsonValueKind.Object || document.TryGetProperty("error", out _))
		{
			return AuthenticateResult.Fail(FailureMessage);
		}

		var principal = BuildPrincipal(document);
		return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
	}

	private static ClaimsPrincipal BuildPrincipal(JsonElement document)
	{
		var identity = new ClaimsIdentity(PiggyMetricsAuth.Scheme, ClaimTypes.Name, ClaimTypes.Role);
		identity.AddClaim(new Claim(ClaimTypes.Name, ReadPrincipalName(document)));

		if (document.TryGetProperty("oauth2Request", out var request) && request.ValueKind == JsonValueKind.Object)
		{
			if (request.TryGetProperty("clientId", out var clientId) && clientId.ValueKind == JsonValueKind.String)
			{
				identity.AddClaim(new Claim(PiggyMetricsAuth.ClientIdClaimType, clientId.GetString()!));
			}

			if (request.TryGetProperty("scope", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
			{
				foreach (var scope in scopes.EnumerateArray())
				{
					if (scope.ValueKind == JsonValueKind.String)
					{
						identity.AddClaim(new Claim(PiggyMetricsAuth.ScopeClaimType, scope.GetString()!));
					}
				}
			}
		}

		foreach (var authority in ReadAuthorities(document))
		{
			identity.AddClaim(new Claim(ClaimTypes.Role, authority));
		}

		return new ClaimsPrincipal(identity);
	}

	private static string ReadPrincipalName(JsonElement document)
	{
		foreach (var key in PiggyMetricsAuth.PrincipalKeys)
		{
			if (!document.TryGetProperty(key, out var value))
			{
				continue;
			}

			var text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
		}

		return PiggyMetricsAuth.UnknownPrincipal;
	}

	private static List<string> ReadAuthorities(JsonElement document)
	{
		if (!document.TryGetProperty("authorities", out var value))
		{
			return new List<string> { PiggyMetricsAuth.DefaultAuthority };
		}

		var authorities = new List<string>();
		Collect(value, authorities);
		return authorities;
	}

	private static void Collect(JsonElement value, List<string> authorities)
	{
		switch (value.ValueKind)
		{
			case JsonValueKind.String:
				var text = value.GetString() ?? string.Empty;
				var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				authorities.AddRange(parts);
				break;
			case JsonValueKind.Array:
				foreach (var item in value.EnumerateArray())
				{
					Collect(item, authorities);
				}

				break;
			case JsonValueKind.Object:
				if (value.TryGetProperty("authority", out var authority) && authority.ValueKind == JsonValueKind.String)
				{
					authorities.Add(authority.GetString()!);
				}

				break;
		}
	}
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/Shared.csproj -c Debug
~~~

**Verify.**
- dotnet build src/Shared/Shared.csproj -c Debug

**Invariants.**
- A token is never trusted on its own; every authenticated request resolves it against the user-info endpoint.
- A non-success user-info status, a transport failure and a document carrying an error member all end as a failed authentication, never as an anonymous success.
- The principal name is taken from the first PRINCIPAL_KEYS member present, in that exact order.
- One scope claim is added per entry of oauth2Request.scope, so a scope check never has to re-parse a list.
- No user-info response is cached; the source caches nothing on the resource-server side.

**Do not.**
- Do not add JWT validation, a signing key, an introspection endpoint or Microsoft.AspNetCore.Authentication.JwtBearer; the source's tokens are opaque in-memory tokens.
- Do not cache the user-info response.
- Do not fall back to an anonymous principal when the user-info call fails.
- Do not read the token from a query string or a cookie.
- Do not register the scheme here; registration is STEP-006.

## STEP-006 - Register the bearer scheme and the user and server authorization policies

Todos P1.T2. Depends on STEP-005. Critical path: yes.

**Why.** P2.T3 and P3.T4 ask for policy user; P2.T4, P3.T5 and P3.T6 ask for policy server. In the source those are anyRequest().authenticated() and @PreAuthorize("#oauth2.hasScope('server')"). DEC-004 puts both in Shared under those exact names so no service plan invents one.

**Before.** No registration code exists. Source: account-service ResourceServerConfig.java:55-60, auth-service UserController.java:26-31, statistics-service StatisticsController.java:26-36, OAuth2AuthorizationConfig.java:43-64. Full citations in `planning/implementation-plan.yaml`.

**Files.** `src/Shared/Security/SecurityExtensions.cs`

exact_snippet (`src/Shared/Security/SecurityExtensions.cs`):

~~~csharp
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
~~~

exact_commands:

~~~bash
dotnet build src/Shared/Shared.csproj -c Debug
~~~

**Verify.**
- dotnet build src/Shared/Shared.csproj -c Debug

**Invariants.**
- The policy names stay the literals user and server; later plans reference PiggyMetricsAuth.UserPolicy and PiggyMetricsAuth.ServerPolicy.
- Both policies bind to the Bearer scheme explicitly, so adding another scheme later cannot silently satisfy them.
- An authenticated caller lacking the server scope is forbidden rather than challenged, which is the source's distinction between 401 and 403.

**Do not.**
- Do not name the policies anything other than user and server.
- Do not grant the server policy to a caller whose scope claim is ui.
- Do not register the authorization policies in a service project; they live in Shared.
- Do not add role-based requirements; the source gates on scope, not on authority.

## STEP-007 - Pin the typed-client base, the source's timeout and the client-credentials token cache

Todos P1.T4. Depends on STEP-002. Critical path: no.

**Why.** P1.T4 asks for a typed-client base with the source's timeout, a client-credentials token cache and one HTTP client per edge. The source wraps each Feign edge in a Hystrix command whose default execution timeout is 10000 ms and caches the token in an OAuth2ClientContext until it expires. DEC-014 keeps two registration methods because statistics-service declares no OAuth2FeignRequestInterceptor.

**Before.** No HTTP client code exists. Source: account-service ResourceServerConfig.java:33-50, shared/account-service.yml:1-8, shared/statistics-service.yml:1-8, shared/application.yml:7-13, ExchangeRatesClient.java:10-16. Full citations in `planning/implementation-plan.yaml`.

**Files.** `src/Shared/Http/ServiceClients.cs`, `src/Shared/Http/HttpClientExtensions.cs`

exact_snippet (`src/Shared/Http/ServiceClients.cs`):

~~~csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PiggyMetrics.Shared.Http;

// The source's security.oauth2.client.* block, plus the call timeout the source enforces through
// hystrix.command.default.execution.isolation.thread.timeoutInMilliseconds.
public sealed class ServiceClientOptions
{
	public const string SectionName = "Security:OAuth2:Client";
	public const int SourceTimeoutMilliseconds = 10000;

	public string ClientId { get; set; } = string.Empty;

	public string ClientSecret { get; set; } = string.Empty;

	public string AccessTokenUri { get; set; } = "http://auth-service:5000/uaa/oauth/token";

	public string GrantType { get; set; } = "client_credentials";

	public string Scope { get; set; } = "server";

	public TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(SourceTimeoutMilliseconds);
}

public interface IClientCredentialsTokenProvider
{
	Task<string> GetTokenAsync(CancellationToken cancellationToken);
}

// Caches one client-credentials access token per process and renews it once the lifetime the authorization
// server reported has elapsed, which is what DefaultOAuth2ClientContext plus
// DefaultOAuth2AccessToken.isExpired() do in the source.
public sealed class ClientCredentialsTokenProvider : IClientCredentialsTokenProvider
{
	public const string TokenHttpClientName = "piggymetrics-token";

	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ServiceClientOptions _options;
	private readonly SemaphoreSlim _gate = new(1, 1);

	private string _token = string.Empty;
	private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

	public ClientCredentialsTokenProvider(IHttpClientFactory httpClientFactory, IOptions<ServiceClientOptions> options)
	{
		_httpClientFactory = httpClientFactory;
		_options = options.Value;
	}

	public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
	{
		if (IsCachedTokenUsable())
		{
			return _token;
		}

		await _gate.WaitAsync(cancellationToken);
		try
		{
			if (IsCachedTokenUsable())
			{
				return _token;
			}

			var issued = await RequestTokenAsync(cancellationToken);
			_token = issued.AccessToken;
			_expiresAt = issued.ExpiresAt;
			return _token;
		}
		finally
		{
			_gate.Release();
		}
	}

	private bool IsCachedTokenUsable()
	{
		return _token.Length > 0 && DateTimeOffset.UtcNow < _expiresAt;
	}

	private async Task<(string AccessToken, DateTimeOffset ExpiresAt)> RequestTokenAsync(CancellationToken cancellationToken)
	{
		var client = _httpClientFactory.CreateClient(TokenHttpClientName);
		using var request = new HttpRequestMessage(HttpMethod.Post, _options.AccessTokenUri);

		var credentials = Convert.ToBase64String(
			Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
		request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
		request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["grant_type"] = _options.GrantType,
			["scope"] = _options.Scope,
		});

		using var response = await client.SendAsync(request, cancellationToken);
		response.EnsureSuccessStatusCode();

		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

		var accessToken = document.RootElement.GetProperty("access_token").GetString() ?? string.Empty;
		var lifetime = document.RootElement.TryGetProperty("expires_in", out var expiresIn)
			&& expiresIn.ValueKind == JsonValueKind.Number
				? TimeSpan.FromSeconds(expiresIn.GetInt64())
				: TimeSpan.Zero;

		return (accessToken, DateTimeOffset.UtcNow.Add(lifetime));
	}
}

// Attaches the cached client-credentials token to every outgoing call, the way OAuth2FeignRequestInterceptor
// does for the source's service-to-service edges.
public sealed class ClientCredentialsHandler : DelegatingHandler
{
	private readonly IClientCredentialsTokenProvider _tokenProvider;

	public ClientCredentialsHandler(IClientCredentialsTokenProvider tokenProvider)
	{
		_tokenProvider = tokenProvider;
	}

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		var token = await _tokenProvider.GetTokenAsync(cancellationToken);
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		return await base.SendAsync(request, cancellationToken);
	}
}
~~~

exact_snippet (`src/Shared/Http/HttpClientExtensions.cs`):

~~~csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PiggyMetrics.Shared.Http;

public static class HttpClientExtensions
{
	public static IServiceCollection AddPiggyMetricsClientCredentials(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.Configure<ServiceClientOptions>(configuration.GetSection(ServiceClientOptions.SectionName));
		services.AddHttpClient(ClientCredentialsTokenProvider.TokenHttpClientName, client =>
		{
			client.Timeout = TimeSpan.FromMilliseconds(ServiceClientOptions.SourceTimeoutMilliseconds);
		});
		services.AddSingleton<IClientCredentialsTokenProvider, ClientCredentialsTokenProvider>();
		services.AddTransient<ClientCredentialsHandler>();
		return services;
	}

	// One HTTP client per service-to-service edge, carrying the client-credentials token.
	public static IHttpClientBuilder AddPiggyMetricsServiceClient<TClient>(
		this IServiceCollection services,
		string name,
		Uri baseAddress)
		where TClient : class
	{
		return services
			.AddPiggyMetricsEdgeClient<TClient>(name, baseAddress)
			.AddHttpMessageHandler<ClientCredentialsHandler>();
	}

	// One HTTP client per edge that the source calls without a token.
	public static IHttpClientBuilder AddPiggyMetricsEdgeClient<TClient>(
		this IServiceCollection services,
		string name,
		Uri baseAddress)
		where TClient : class
	{
		return services.AddHttpClient<TClient>(name, client =>
		{
			client.BaseAddress = baseAddress;
			client.Timeout = TimeSpan.FromMilliseconds(ServiceClientOptions.SourceTimeoutMilliseconds);
		});
	}
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/Shared.csproj -c Debug
~~~

**Verify.**
- dotnet build src/Shared/Shared.csproj -c Debug

**Invariants.**
- Every outbound edge uses a 10000 ms timeout, the source's Hystrix default execution timeout.
- One token is fetched per process and reused until the reported lifetime has elapsed; concurrent callers wait on one fetch rather than issuing several.
- The token request authenticates the client with HTTP Basic, which is the scheme Spring's client-credentials provider uses by default.
- A token response carrying no expires_in is treated as already expired, so the next call fetches a fresh token.
- A failed token request surfaces as an exception on the caller's edge rather than an unauthenticated outbound call.

**Do not.**
- Do not use 20000 ms here; that value is the gateway's Zuul and Ribbon timeout, not the service edge timeout.
- Do not add Polly, a circuit breaker or a retry policy; the source's fallback behaviour belongs to the per-service plans.
- Do not cache the token in a static field shared across services or persist it.
- Do not add a refresh-token flow; the service clients use the client-credentials grant.
- Do not create a concrete edge client here; each edge is registered by the plan that owns it.

## STEP-008 - Pin the actuator health endpoint and the service default composition

Todos P1.T5. Depends on STEP-003. Critical path: no.

**Why.** P1.T5 asks for health checks. config/Dockerfile:7 probes /actuator/health, which fixes the path and the healthy status code, and Boot 2.0 hides health detail, which fixes the body. DEC-010 reproduces both. The same step composes the defaults every service shares so the five entry points stay three lines long.

**Before.** No health or bootstrap code exists. Source: account-service/pom.xml:48-51, config/Dockerfile:7, shared/auth-service.yml:9-12, shared/account-service.yml:18-21, shared/statistics-service.yml:18-21, shared/notification-service.yml:9-12, shared/gateway.yml:43-44, docker-compose.yml. Full citations in `planning/implementation-plan.yaml`.

**Files.** `src/Shared/Health/HealthExtensions.cs`, `src/Shared/ServiceDefaults.cs`

exact_snippet (`src/Shared/Health/HealthExtensions.cs`):

~~~csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PiggyMetrics.Shared.Health;

// Reproduces the source's actuator health contract: GET /actuator/health answers {"status":"UP"} with 200, and
// {"status":"DOWN"} with 503, with no detail body.
public static class HealthExtensions
{
	public const string HealthPath = "/actuator/health";
	public const string UpBody = "{\"status\":\"UP\"}";
	public const string DownBody = "{\"status\":\"DOWN\"}";

	public static IServiceCollection AddPiggyMetricsHealthChecks(this IServiceCollection services)
	{
		services.AddHealthChecks();
		return services;
	}

	public static IEndpointRouteBuilder MapPiggyMetricsHealthChecks(this IEndpointRouteBuilder endpoints)
	{
		var options = new HealthCheckOptions
		{
			ResponseWriter = WriteStatusAsync,
		};
		options.ResultStatusCodes[HealthStatus.Healthy] = StatusCodes.Status200OK;
		options.ResultStatusCodes[HealthStatus.Degraded] = StatusCodes.Status200OK;
		options.ResultStatusCodes[HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable;

		endpoints.MapHealthChecks(HealthPath, options).AllowAnonymous();
		return endpoints;
	}

	private static Task WriteStatusAsync(HttpContext context, HealthReport report)
	{
		context.Response.ContentType = "application/json";
		var body = report.Status == HealthStatus.Unhealthy ? DownBody : UpBody;
		return context.Response.WriteAsync(body);
	}
}
~~~

exact_snippet (`src/Shared/ServiceDefaults.cs`):

~~~csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PiggyMetrics.Shared.Health;
using PiggyMetrics.Shared.Json;

namespace PiggyMetrics.Shared;

public sealed record ServiceIdentity(string Name);

// The composition every PiggyMetrics .NET service starts from: the shared JSON contract, the actuator health
// endpoint, and the authentication and authorization middleware slots.
public static class ServiceDefaults
{
	public const string ContextPathConfigurationKey = "Server:Servlet:ContextPath";

	public static WebApplicationBuilder AddPiggyMetricsDefaults(
		this WebApplicationBuilder builder,
		string applicationName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);

		builder.Services.AddSingleton(new ServiceIdentity(applicationName));
		builder.Services
			.AddControllers()
			.AddJsonOptions(options => PiggyMetricsJson.Configure(options.JsonSerializerOptions));
		builder.Services.ConfigureHttpJsonOptions(options => PiggyMetricsJson.Configure(options.SerializerOptions));
		builder.Services.AddAuthentication();
		builder.Services.AddAuthorization();
		builder.Services.AddPiggyMetricsHealthChecks();

		return builder;
	}

	public static WebApplication UsePiggyMetricsDefaults(this WebApplication app)
	{
		var contextPath = app.Configuration[ContextPathConfigurationKey];
		if (!string.IsNullOrWhiteSpace(contextPath))
		{
			app.UsePathBase(contextPath);
		}

		app.UseRouting();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapPiggyMetricsHealthChecks();
		app.MapControllers();

		return app;
	}
}
~~~

exact_commands:

~~~bash
dotnet build src/Shared/Shared.csproj -c Debug
~~~

**Verify.**
- dotnet build src/Shared/Shared.csproj -c Debug

**Invariants.**
- The health path is /actuator/health under the service's own path base, matching the source's actuator path under its servlet context path.
- The health body carries the single member status whose value is UP or DOWN; no detail is exposed, matching Boot 2.0's default.
- The health endpoint is always anonymous, so an orchestrator probe never needs a token.
- UsePiggyMetricsDefaults applies the path base before routing, so every route lands under the source's context path.
- AddPiggyMetricsDefaults registers authentication and authorization services unconditionally, so UsePiggyMetricsDefaults is valid in a service that adds no scheme of its own.

**Do not.**
- Do not expose /actuator/info, /actuator/env, /actuator/metrics or any other actuator endpoint; Boot 2.0 exposes health and info over the web and only health is probed.
- Do not include check names, durations or exception detail in the health body.
- Do not require authentication on the health endpoint.
- Do not register MVC controllers in the gateway project; it maps only the health endpoint in P1.

## STEP-009 - Create the five runtime service projects on the shared defaults

Todos P1.T1. Depends on STEP-006, STEP-008. Critical path: yes.

**Why.** P1.T1 names one project per runtime service. Each must exist and build before its own plan adds endpoints, documents and settings. DEC-002 fixes the project, assembly and namespace names so the later plans address them unambiguously.

**Before.** No service project exists. Source: GatewayApplication.java:7-15, the four @SpringBootApplication classes, the per-service ports and context paths in config/src/main/resources/shared/, auth-service WebSecurityConfig.java:18-36. Full citations in `planning/implementation-plan.yaml`.

**Files.** `src/Gateway/Gateway.csproj`, `src/Gateway/Program.cs`, `src/Gateway/appsettings.json`, `src/AuthService/AuthService.csproj`, `src/AuthService/Program.cs`, `src/AuthService/appsettings.json`, `src/AccountService/AccountService.csproj`, `src/AccountService/Program.cs`, `src/AccountService/appsettings.json`, `src/StatisticsService/StatisticsService.csproj`, `src/StatisticsService/Program.cs`, `src/StatisticsService/appsettings.json`, `src/NotificationService/NotificationService.csproj`, `src/NotificationService/Program.cs`, `src/NotificationService/appsettings.json`

exact_snippet (`src/Gateway/Gateway.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<RootNamespace>PiggyMetrics.Gateway</RootNamespace>
		<AssemblyName>Gateway</AssemblyName>
	</PropertyGroup>
	<ItemGroup>
		<ProjectReference Include="../Shared/Shared.csproj" />
	</ItemGroup>
</Project>
~~~

exact_snippet (`src/Gateway/Program.cs`):

~~~csharp
using PiggyMetrics.Shared.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddPiggyMetricsHealthChecks();

var app = builder.Build();
app.UseRouting();
app.MapPiggyMetricsHealthChecks();
app.Run();
~~~

exact_snippet (`src/Gateway/appsettings.json`):

~~~json
{
	"Kestrel": {
		"Endpoints": {
			"Http": {
				"Url": "http://0.0.0.0:4000"
			}
		}
	}
}
~~~

exact_snippet (`src/AuthService/AuthService.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<RootNamespace>PiggyMetrics.AuthService</RootNamespace>
		<AssemblyName>AuthService</AssemblyName>
	</PropertyGroup>
	<ItemGroup>
		<ProjectReference Include="../Shared/Shared.csproj" />
	</ItemGroup>
</Project>
~~~

exact_snippet (`src/AuthService/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults("auth-service");

var app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();
~~~

exact_snippet (`src/AuthService/appsettings.json`):

~~~json
{
	"Server": {
		"Servlet": {
			"ContextPath": "/uaa"
		}
	},
	"Kestrel": {
		"Endpoints": {
			"Http": {
				"Url": "http://0.0.0.0:5000"
			}
		}
	}
}
~~~

exact_snippet (`src/AccountService/AccountService.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<RootNamespace>PiggyMetrics.AccountService</RootNamespace>
		<AssemblyName>AccountService</AssemblyName>
	</PropertyGroup>
	<ItemGroup>
		<ProjectReference Include="../Shared/Shared.csproj" />
	</ItemGroup>
</Project>
~~~

exact_snippet (`src/AccountService/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;
using PiggyMetrics.Shared.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults("account-service");
builder.Services.AddPiggyMetricsResourceServer(builder.Configuration);

var app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();
~~~

exact_snippet (`src/AccountService/appsettings.json`):

~~~json
{
	"Server": {
		"Servlet": {
			"ContextPath": "/accounts"
		}
	},
	"Kestrel": {
		"Endpoints": {
			"Http": {
				"Url": "http://0.0.0.0:6000"
			}
		}
	}
}
~~~

exact_snippet (`src/StatisticsService/StatisticsService.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<RootNamespace>PiggyMetrics.StatisticsService</RootNamespace>
		<AssemblyName>StatisticsService</AssemblyName>
	</PropertyGroup>
	<ItemGroup>
		<ProjectReference Include="../Shared/Shared.csproj" />
	</ItemGroup>
</Project>
~~~

exact_snippet (`src/StatisticsService/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;
using PiggyMetrics.Shared.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults("statistics-service");
builder.Services.AddPiggyMetricsResourceServer(builder.Configuration);

var app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();
~~~

exact_snippet (`src/StatisticsService/appsettings.json`):

~~~json
{
	"Server": {
		"Servlet": {
			"ContextPath": "/statistics"
		}
	},
	"Kestrel": {
		"Endpoints": {
			"Http": {
				"Url": "http://0.0.0.0:7000"
			}
		}
	}
}
~~~

exact_snippet (`src/NotificationService/NotificationService.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<RootNamespace>PiggyMetrics.NotificationService</RootNamespace>
		<AssemblyName>NotificationService</AssemblyName>
	</PropertyGroup>
	<ItemGroup>
		<ProjectReference Include="../Shared/Shared.csproj" />
	</ItemGroup>
</Project>
~~~

exact_snippet (`src/NotificationService/Program.cs`):

~~~csharp
using PiggyMetrics.Shared;
using PiggyMetrics.Shared.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddPiggyMetricsDefaults("notification-service");
builder.Services.AddPiggyMetricsResourceServer(builder.Configuration);

var app = builder.Build();
app.UsePiggyMetricsDefaults();
app.Run();
~~~

exact_snippet (`src/NotificationService/appsettings.json`):

~~~json
{
	"Server": {
		"Servlet": {
			"ContextPath": "/notifications"
		}
	},
	"Kestrel": {
		"Endpoints": {
			"Http": {
				"Url": "http://0.0.0.0:8000"
			}
		}
	}
}
~~~

exact_commands:

~~~bash
mkdir -p src/Gateway src/AuthService src/AccountService src/StatisticsService src/NotificationService
dotnet sln PiggyMetrics.sln add src/Gateway/Gateway.csproj src/AuthService/AuthService.csproj src/AccountService/AccountService.csproj src/StatisticsService/StatisticsService.csproj src/NotificationService/NotificationService.csproj
dotnet build PiggyMetrics.sln -c Debug
~~~

**Verify.**
- dotnet sln PiggyMetrics.sln list
- dotnet build PiggyMetrics.sln -c Debug

**Invariants.**
- Each service project's assembly name equals its project name, and its root namespace is PiggyMetrics plus that name.
- Each service's listening port and context path match the source: 4000 with no path, 5000 with /uaa, 6000 with /accounts, 7000 with /statistics, 8000 with /notifications.
- AuthService does not register the user-info resource server, because it is the service that issues the tokens.
- Every service project references Shared and no service project references another service project.
- appsettings.json carries only the Kestrel listener and the context path in P1; store keys, client credentials, rates addresses and scheduling keys arrive with the per-service settings todos.

**Do not.**
- Do not add controllers, documents, repositories, store registration or client registration in this step.
- Do not add Eureka, Spring Cloud Config, Hystrix, Turbine or RabbitMQ client equivalents.
- Do not add a resource-server registration to Gateway or AuthService.
- Do not add Swagger, CORS or HTTPS redirection; the source has none of them.
- Do not move the ports or context paths into code; they stay in appsettings.json.

## STEP-010 - Add the Shared test project and prove the bearer handler accepts and refuses

Todos P1.T2. Depends on STEP-006. Critical path: no.

**Why.** The P1 done-when clause is that Shared's bearer handler accepts a token the source's authorization server issued and refuses one it did not. DEC-011 makes that machine-checkable here, because no later plan owns Shared. The stub answers the user-info endpoint the way the source's authorization server does, so the assertions bind to the real contract.

**Before.** No test project exists. Source: CustomUserInfoTokenServices.java:68-107 and OAuth2AuthorizationConfig.java:43-64; the stub document follows the Jackson rendering of the OAuth2Authentication that /uaa/users/current returns. Full citations in `planning/implementation-plan.yaml`.

**Files.** `tests/Shared.Tests/Shared.Tests.csproj`, `tests/Shared.Tests/Security/UserInfoAuthenticationHandlerTests.cs`

exact_snippet (`tests/Shared.Tests/Shared.Tests.csproj`):

~~~xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<RootNamespace>PiggyMetrics.Shared.Tests</RootNamespace>
		<AssemblyName>Shared.Tests</AssemblyName>
		<IsPackable>false</IsPackable>
		<IsTestProject>true</IsTestProject>
	</PropertyGroup>
	<ItemGroup>
		<FrameworkReference Include="Microsoft.AspNetCore.App" />
	</ItemGroup>
	<ItemGroup>
		<PackageReference Include="Microsoft.AspNetCore.TestHost" />
		<PackageReference Include="Microsoft.NET.Test.Sdk" />
		<PackageReference Include="xunit" />
		<PackageReference Include="xunit.runner.visualstudio" />
	</ItemGroup>
	<ItemGroup>
		<ProjectReference Include="../../src/Shared/Shared.csproj" />
	</ItemGroup>
</Project>
~~~

exact_snippet (`tests/Shared.Tests/Security/UserInfoAuthenticationHandlerTests.cs`):

~~~csharp
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PiggyMetrics.Shared.Security;
using Xunit;

namespace PiggyMetrics.Shared.Tests.Security;

public sealed class UserInfoAuthenticationHandlerTests
{
	private const string UserInfoUri = "http://auth-service:5000/uaa/users/current";

	private const string IssuedUserToken = "issued-user-token";
	private const string IssuedServerToken = "issued-server-token";
	private const string ForeignToken = "foreign-token";

	private const string UserPrincipalJson =
		"{\"authorities\":[],\"authenticated\":true,\"principal\":\"demo\",\"credentials\":\"\"," +
		"\"oauth2Request\":{\"clientId\":\"browser\",\"scope\":[\"ui\"]},\"clientOnly\":false,\"name\":\"demo\"}";

	private const string ServerPrincipalJson =
		"{\"authorities\":[],\"authenticated\":true,\"principal\":\"account-service\",\"credentials\":\"\"," +
		"\"oauth2Request\":{\"clientId\":\"account-service\",\"scope\":[\"server\"]},\"clientOnly\":true," +
		"\"name\":\"account-service\"}";

	[Fact]
	public async Task ShouldAcceptTokenIssuedByAuthorizationServer()
	{
		var result = await CallAsync("/user", IssuedUserToken);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("demo", result.Body);
	}

	[Fact]
	public async Task ShouldRefuseTokenTheAuthorizationServerDidNotIssue()
	{
		var result = await CallAsync("/user", ForeignToken);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	[Fact]
	public async Task ShouldRefuseRequestWithoutToken()
	{
		var result = await CallAsync("/user", token: null);

		Assert.Equal(HttpStatusCode.Unauthorized, result.Status);
	}

	[Fact]
	public async Task ShouldAllowServerPolicyForServerScope()
	{
		var result = await CallAsync("/server", IssuedServerToken);

		Assert.Equal(HttpStatusCode.OK, result.Status);
		Assert.Equal("account-service", result.Body);
	}

	[Fact]
	public async Task ShouldRejectServerPolicyForUiScope()
	{
		var result = await CallAsync("/server", IssuedUserToken);

		Assert.Equal(HttpStatusCode.Forbidden, result.Status);
	}

	private static async Task<(HttpStatusCode Status, string Body)> CallAsync(string path, string? token)
	{
		using var host = await CreateHostAsync();
		var client = host.GetTestClient();

		using var request = new HttpRequestMessage(HttpMethod.Get, path);
		if (token is not null)
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		}

		using var response = await client.SendAsync(request);
		var body = await response.Content.ReadAsStringAsync();
		return (response.StatusCode, body);
	}

	private static async Task<IHost> CreateHostAsync()
	{
		var host = new HostBuilder()
			.ConfigureWebHost(webHost =>
			{
				webHost.UseTestServer();
				webHost.ConfigureServices(services =>
				{
					var configuration = new ConfigurationBuilder()
						.AddInMemoryCollection(new Dictionary<string, string?>
						{
							[PiggyMetricsAuth.UserInfoUriConfigurationKey] = UserInfoUri,
						})
						.Build();

					services.AddLogging();
					services.AddRouting();
					services.AddPiggyMetricsResourceServer(configuration);
					services
						.AddHttpClient(PiggyMetricsAuth.HttpClientName)
						.ConfigurePrimaryHttpMessageHandler(() => new AuthorizationServerStub());
				});
				webHost.Configure(app =>
				{
					app.UseRouting();
					app.UseAuthentication();
					app.UseAuthorization();
					app.UseEndpoints(endpoints =>
					{
						endpoints
							.MapGet("/user", (HttpContext context) => context.User.Identity!.Name)
							.RequireAuthorization(PiggyMetricsAuth.UserPolicy);
						endpoints
							.MapGet("/server", (HttpContext context) => context.User.Identity!.Name)
							.RequireAuthorization(PiggyMetricsAuth.ServerPolicy);
					});
				});
			})
			.Build();

		await host.StartAsync();
		return host;
	}

	// Answers the way the source's authorization server answers GET /uaa/users/current: the serialized
	// OAuth2Authentication for a token it issued, 401 for anything else.
	private sealed class AuthorizationServerStub : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var token = request.Headers.Authorization?.Parameter ?? string.Empty;
			var body = token switch
			{
				IssuedUserToken => UserPrincipalJson,
				IssuedServerToken => ServerPrincipalJson,
				_ => string.Empty,
			};

			if (body.Length == 0)
			{
				return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
			}

			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			});
		}
	}
}
~~~

exact_commands:

~~~bash
mkdir -p tests/Shared.Tests/Security
dotnet sln PiggyMetrics.sln add tests/Shared.Tests/Shared.Tests.csproj
dotnet test tests/Shared.Tests/Shared.Tests.csproj -c Debug
~~~

**Verify.**
- dotnet test tests/Shared.Tests/Shared.Tests.csproj -c Debug

**Invariants.**
- The tests exercise the real handler through the real policy pipeline; the handler itself is never mocked.
- The stub answers only the two issued tokens, so a passing accept test cannot come from a permissive stub.
- The refusal assertions distinguish 401 for an unresolvable token from 403 for a resolved token lacking the server scope.
- The test project runs with no MongoDB, no network and no authorization server present.

**Do not.**
- Do not reach the network; the stub is the only transport.
- Do not assert on log output instead of on status codes.
- Do not weaken the stub to answer 200 for an unknown token.
- Do not add a mocking package; the stub is a plain HttpMessageHandler.

## STEP-011 - Gate P1 on a clean solution build and a green test run

Todos P1.T1, P1.T2, P1.T3, P1.T4, P1.T5. Depends on STEP-004, STEP-007, STEP-009, STEP-010. Critical path: yes.

**Why.** The P1 done-when clause has two halves: every project builds, and the bearer handler accepts and refuses correctly. This is the single gate that proves both before P2 and P3 start, and the commit boundary that names the five P1 todo ids.

**Before.** After STEP-001 to STEP-010 the solution holds Shared, the five service projects and tests/Shared.Tests. Source: pom.xml:38-48 (the reactor build). Full citations in `planning/implementation-plan.yaml`.

**Files.** `PiggyMetrics.sln`

exact_commands:

~~~bash
dotnet restore PiggyMetrics.sln
dotnet build PiggyMetrics.sln -c Debug --no-restore
dotnet test PiggyMetrics.sln -c Debug --no-build
dotnet sln PiggyMetrics.sln list
git add -A
git commit -m "P1.T1 P1.T2 P1.T3 P1.T4 P1.T5: solution skeleton and Shared"
~~~

**Verify.**
- dotnet build PiggyMetrics.sln -c Debug --no-restore
- dotnet test PiggyMetrics.sln -c Debug --no-build

**Invariants.**
- The build runs from the repository root over PiggyMetrics.sln, not project by project.
- No warning about a missing package version appears, which proves central package management stayed intact.
- The commit message names every P1 todo id it completes.

**Do not.**
- Do not mark P1 complete while any project fails to build or any test fails.
- Do not add a project to the solution that P1.T1 does not name.
- Do not start P2 or P3 work in this commit.
- Do not push or open a pull request from this step; the runtime owns that.

## Done when

- `dotnet build PiggyMetrics.sln -c Debug` succeeds for all seven projects.
- `dotnet test PiggyMetrics.sln -c Debug` reports five passed and zero failed.
- The bearer handler accepts a token the source's authorization server issued and refuses one it did not, proven by ShouldAcceptTokenIssuedByAuthorizationServer and ShouldRefuseTokenTheAuthorizationServerDidNotIssue.
- One commit names P1.T1, P1.T2, P1.T3, P1.T4 and P1.T5.

## Out of scope for P1

- Endpoints, documents, repositories and per-service settings for AuthService (P2), StatisticsService (P3) and the remaining service plans.
- Eureka service registry, Spring Cloud Config server, Hystrix dashboard, Turbine stream and RabbitMQ bus; no .NET counterpart is named by P1.T1.
- The gateway routing table and its 20000 ms Zuul and Ribbon timeouts.
- Mongo document classes, indexes and seeding.
- Pushing a branch or opening a pull request; the runtime owns that.
