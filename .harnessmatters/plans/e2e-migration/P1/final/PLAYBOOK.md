---
name: 'PiggyMetricsDotNet P1: solution skeleton and Shared'
overview: 'Port plan P1 of the PiggyMetrics Java to .NET migration: stand up the solution with one project
  per runtime service plus Shared, and build the Shared library the later service plans depend on - the
  bearer handler that resolves a token against the source authorization server, the Mongo conventions
  that keep the existing documents readable, the typed-client base at the source''s 10000 ms timeout with
  a client-credentials token cache, and the JSON and health contracts. Every file is pinned as complete
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

Plan key P1 of spec `E2E migration`, target repo `sandeep-chanda/PiggyMetricsDotNet`. Migration source: `github.com/sqshq/piggymetrics` @ `6bb2cf9ddbca980b664d3edbb6ff775d75369278`.

## What this plan delivers

P1 is the substrate every later service plan builds on. Fourteen steps create the root build contract, the Shared class library, the bearer handler that validates a token the way the source resource servers do, the `user` and `server` authorization policies, the Mongo conventions and decimal serializer that keep documents the Java services wrote readable, the Mongo database registration, the client-credentials token cache and the per-edge typed-client base, the Jackson-compatible JSON options, the actuator health endpoint, the service defaults that enforce the source's port and context path, the five runtime service projects, the Shared test suite, and the solution wiring.

Todos map one-to-one onto STEP ids. Each `## STEP-0nn` section below is the body of the matching todo; `final/DAG.yaml` carries the same ids with their dependency edges, and `final/PINS.md` carries the complete final body of all 47 files this plan creates, grouped by STEP id in the same order. Playbook section plus pin section together are the whole step; the split exists so the package transports in one piece, and nothing is summarised in either.

## Decisions already made (do not reopen)

- **D-002 net8.0**, pinned once in `Directory.Build.props`; proven by a green build on SDK 8.0.425.
- **D-004 user-info token validation, not JWT.** The source's tokens are opaque and held in an `InMemoryTokenStore`; every resource server resolves them through `CustomUserInfoTokenServices`. A local JWT validator would accept and refuse a different set of tokens.
- **D-006 decimals as BSON strings.** `spring-data-mongodb` 2.0.x registers `BigDecimalToStringConverter`, so every amount the Java services wrote is a string. Writing `Decimal128` would produce documents a still-running Java service cannot read.
- **D-009 token cache refreshes exactly at expiry, no skew**, as Spring's OAuth2 client context does.
- **D-010 Jackson-shaped JSON**: camel-case names, enum names, unknown members skipped, nulls written, dates as epoch milliseconds, because no source service overrides Jackson.
- **D-012 a prefix guard in front of `UsePathBase`.** `UsePathBase` alone still serves the unprefixed path; Spring's `server.servlet.context-path` answers 404. Measured both ways on a running service.
- **D-003 no Eureka and no config server.** No P1 todo names them; P1.T1 names five runtime services.

## Verification that already ran

Observations from a reference implementation built during planning, not predictions:

- `dotnet build PiggyMetrics.sln -warnaserror` reported `0 Warning(s)` and `0 Error(s)`.
- `dotnet test PiggyMetrics.sln` reported `Passed! - Failed: 0, Passed: 24`.
- The running account service answered `200` with `{"status":"UP"}` on `/accounts/actuator/health`.
- It answered `404` on `/actuator/health` and on `/accountsfoo/actuator/health`.

The target repository was not written to during planning.

## STEP-001 (P1.T1) - Pin the repository build contract: target framework and package versions

Depends on: nothing.

**Why.** P1.T1 needs one solution with a project per runtime service plus Shared. The source pins its build contract once in the parent pom and each module inherits it (R-001), so the port pins framework and package versions once at the root. D-001, D-002.

**Currently (source anchors).**
- pom.xml:1-50 - java.version 1.8, UTF-8, dependencyManagement import of spring-cloud Finchley.RELEASE
- account-service/pom.xml - declares only its own artifactId and dependencies, inherits the rest
- target repo - holds README.md and AGENTS.md only; no build file exists

**Change.** Create Directory.Build.props and Directory.Packages.props at the repository root. Directory.Build.props fixes net8.0, C# 12, nullable reference types, implicit usings and warnings-as-errors for every project in the tree, mirroring the single java.version 1.8 / UTF-8 contract the source declares once in its parent pom. Directory.Packages.props turns on central package management and fixes the exact NuGet versions that the Shared library and the Shared test project consume, mirroring the source dependencyManagement block.

**Files.**
- `Directory.Build.props`
- `Directory.Packages.props`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-001`. Nothing there is summarised.

exact_commands:

~~~bash
dotnet --version
~~~

**Invariants.**
- TargetFramework is net8.0 for every project in the repository; no project overrides it.
- ManagePackageVersionsCentrally is true, so no csproj carries a Version attribute on PackageReference.
- TreatWarningsAsErrors is true, so a warning in any later plan fails the build.
- No InvariantGlobalization property is set: the serializers pin CultureInfo.InvariantCulture explicitly instead.

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

## STEP-002 (P1.T1) - Create the Shared class library and pin the source's timeouts

Depends on: STEP-001.

**Why.** P1.T1 needs a Shared project and P1.T4 needs the source's timeout. The source sets one Hystrix timeout for every service and a wider one for the edge (R-003), so both constants belong in Shared. D-003, D-008.

**Currently (source anchors).**
- config/src/main/resources/shared/application.yml - hystrix ... timeoutInMilliseconds: 10000
- config/src/main/resources/shared/gateway.yml - the same key at 20000, plus ribbon and zuul timeouts at 20000
- account-service/pom.xml - spring-cloud-starter-openfeign and spring-cloud-starter-netflix-hystrix

**Change.** Create src/Shared/PiggyMetrics.Shared.csproj as a net8.0 class library that takes a FrameworkReference on Microsoft.AspNetCore.App (so Shared can host the authentication handler, the health endpoint and the MVC JSON options) and PackageReferences on MongoDB.Driver and Microsoft.Extensions.Http. Add src/Shared/PiggyMetricsTimeouts.cs holding the two timeout constants read off the source configuration: 10000 ms for every service call and 20000 ms at the gateway.

**Files.**
- `src/Shared/PiggyMetrics.Shared.csproj`
- `src/Shared/PiggyMetricsTimeouts.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-002`. Nothing there is summarised.

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- PiggyMetricsTimeouts.Default is 10000 milliseconds, matching hystrix.command.default.execution.isolation.thread.timeoutInMilliseconds in application.yml.
- PiggyMetricsTimeouts.Gateway is 20000 milliseconds, matching gateway.yml.
- Shared uses FrameworkReference Microsoft.AspNetCore.App, never a PackageReference on an ASP.NET Core assembly.
- Shared carries no ProjectReference to any service project; dependencies point one way only.

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

## STEP-003 (P1.T2) - Port the bearer handler that resolves a token against the authorization server

Depends on: STEP-002.

**Why.** P1.T2 needs a bearer handler that validates a token the way the source resource servers do, by calling http://auth-service:5000/uaa/users/current. CustomUserInfoTokenServices is that check in the source (R-004, R-005), and P1's done-when clause is about this handler. D-004, D-005.

**Currently (source anchors).**
- account-service/.../config/ResourceServerConfig.java:50-53 - new CustomUserInfoTokenServices(sso.getUserInfoUri(), sso.getClientId())
- account-service/.../service/security/CustomUserInfoTokenServices.java:36-37 - PRINCIPAL_KEYS order
- CustomUserInfoTokenServices.java:67-75 - error member throws InvalidTokenException
- CustomUserInfoTokenServices.java:96-106 - oauth2Request.clientId and oauth2Request.scope
- CustomUserInfoTokenServices.java:112-137 - any exception becomes the error map
- config/src/main/resources/shared/application.yml - user-info-uri: http://auth-service:5000/uaa/users/current

**Change.** Add src/Shared/Security/UserInfoAuthenticationOptions.cs (scheme options carrying the user-info URI and the client id), src/Shared/Security/PiggyMetricsClaims.cs (the client_id and scope claim types), and src/Shared/Security/UserInfoAuthenticationHandler.cs. The handler reads the Authorization header, calls the user-info endpoint with that bearer token over a named HttpClient, treats a non-success status, a transport failure, and a body carrying an error member as a refusal, resolves the principal name by walking the source's PRINCIPAL_KEYS order (user, username, userid, user_id, login, id, name), and projects oauth2Request.clientId and every oauth2Request.scope entry into claims. Challenge answers 401 with WWW-Authenticate Bearer; forbid answers 403.

**Files.**
- `src/Shared/Security/UserInfoAuthenticationOptions.cs`
- `src/Shared/Security/PiggyMetricsClaims.cs`
- `src/Shared/Security/UserInfoAuthenticationHandler.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-003`. Nothing there is summarised.

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

## STEP-004 (P1.T2) - Register the resource server and the user and server authorization policies

Depends on: STEP-003.

**Why.** The downstream plans express their guards as policy user and policy server (P2.T3, P2.T4, P3.T4 to P3.T6). Those names must resolve to the source's two guards: an authenticated principal, and #oauth2.hasScope('server'). D-005.

**Currently (source anchors).**
- auth-service/.../controller/UserController.java:22 - GET /users/current, authenticated only
- auth-service/.../controller/UserController.java:27-28 - POST /users, @PreAuthorize(#oauth2.hasScope('server'))
- statistics-service/.../controller/StatisticsController.java:25,31 - the same scope check
- account-service/.../config/ResourceServerConfig.java:56-60 - anyRequest().authenticated()

**Change.** Add src/Shared/Security/PiggyMetricsPolicies.cs naming the two policies and the server scope value, and src/Shared/Security/SecurityServiceCollectionExtensions.cs exposing AddPiggyMetricsResourceServer. The extension reads security:oauth2:resource:user-info-uri and security:oauth2:client:clientId from configuration, registers the named user-info HttpClient with the source's 10000 ms timeout, registers the UserInfoBearer scheme backed by the handler, and registers the user policy as an authenticated-principal requirement and the server policy as an authenticated principal carrying a scope claim whose value is server.

**Files.**
- `src/Shared/Security/PiggyMetricsPolicies.cs`
- `src/Shared/Security/SecurityServiceCollectionExtensions.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-004`. Nothing there is summarised.

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- The policy named user requires an authenticated principal and nothing else.
- The policy named server requires an authenticated principal carrying a scope claim whose value is server.
- The user-info HttpClient uses PiggyMetricsTimeouts.Default, never a framework default.
- The user-info URI defaults to http://auth-service:5000/uaa/users/current when configuration omits it, matching application.yml.

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

## STEP-005 (P1.T3) - Port the store conventions and the decimal serializer that match the documents on disk

Depends on: STEP-002.

**Why.** P1.T3 needs store conventions and serializers that keep the documents' field names and ids against the same databases and collections. The .NET services read databases the Java services wrote, so the BSON shape is a compatibility contract (R-006, R-007). D-006, D-007.

**Currently (source anchors).**
- account-service/.../domain/Account.java:13-17 - @Document(collection = accounts), @Id on a camel-case field
- account-service/.../domain/Item.java:13, Saving.java:8-12 - BigDecimal members
- mongodb/dump/account-service-dump.js - _id demo, camel-case members, numeric amounts 1300 and 3.32, enum names USD and MONTH
- spring-data-mongodb 2.0.x MongoConverters.java:72-73 - BigDecimalToStringConverter registered
- spring-data-mongodb 2.0.x MappingMongoConverter.java:501-505 - null properties skipped on write

**Change.** Add src/Shared/Persistence/DecimalAsStringSerializer.cs and src/Shared/Persistence/MongoConventions.cs. The serializer writes System.Decimal as a BSON string under the invariant culture, matching Spring Data MongoDB's BigDecimalToStringConverter, and reads String, Double, Int32, Int64 and Decimal128 so it also reads the numeric amounts the source dump seeds. MongoConventions registers one convention pack once per process: camel-case element names (Java field names are camel case and C# properties are Pascal case), ignore-extra-elements, ignore-if-null (Spring Data skips null properties on write), and enum-as-string.

**Files.**
- `src/Shared/Persistence/DecimalAsStringSerializer.cs`
- `src/Shared/Persistence/MongoConventions.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-005`. Nothing there is summarised.

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

## STEP-006 (P1.T3) - Register the Mongo database handle from the source's connection settings

Depends on: STEP-005.

**Why.** Every downstream repository (P2.T2 on auth-mongodb, P3.T3 on statistics-mongodb) needs a database handle built from the same configuration keys the source reads. D-007.

**Currently (source anchors).**
- config/src/main/resources/shared/auth-service.yml - host auth-mongodb, user, ${MONGODB_PASSWORD}, database piggymetrics, port 27017
- account-service.yml, statistics-service.yml, notification-service.yml - the same shape per store
- mongodb/init.sh - creates the user with roles readWrite on db piggymetrics

**Change.** Add src/Shared/Persistence/MongoStoreOptions.cs binding the source's spring:data:mongodb section (host, port, database, username, password) and src/Shared/Persistence/PersistenceServiceCollectionExtensions.cs exposing AddPiggyMetricsStore. The extension registers the convention pack, binds the options, and registers a singleton IMongoClient and a singleton IMongoDatabase resolved from the configured database name, authenticating against that same database when a username is set.

**Files.**
- `src/Shared/Persistence/MongoStoreOptions.cs`
- `src/Shared/Persistence/PersistenceServiceCollectionExtensions.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-006`. Nothing there is summarised.

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

## STEP-007 (P1.T4) - Port the client-credentials token cache and the outbound bearer handler

Depends on: STEP-002.

**Why.** P1.T4 needs a client-credentials token cache. The source obtains a service token through OAuth2FeignRequestInterceptor over a DefaultOAuth2ClientContext, which holds it until expiry (R-008). D-008, D-009.

**Currently (source anchors).**
- account-service/.../config/ResourceServerConfig.java:40-43 - OAuth2FeignRequestInterceptor over DefaultOAuth2ClientContext
- config/src/main/resources/shared/account-service.yml - clientId account-service, accessTokenUri http://auth-service:5000/uaa/oauth/token, grant-type client_credentials, scope server
- auth-service/.../config/OAuth2AuthorizationConfig.java:44-70 - the four registered clients
- Spring ClientCredentialsResourceDetails - authentication scheme defaults to header, which is HTTP Basic

**Change.** Add src/Shared/Http/OAuth2ClientOptions.cs binding the source's security:oauth2:client section, src/Shared/Http/ClientCredentialsTokenCache.cs, and src/Shared/Http/ClientCredentialsHandler.cs. The cache posts the client-credentials grant to the configured access-token URI with HTTP Basic client authentication and a form carrying grant_type and scope, holds the issued token until the instant expires_in names, and serializes concurrent callers through a mutex so a burst of requests triggers one token request. The delegating handler attaches that token as a bearer header on every outbound call.

**Files.**
- `src/Shared/Http/OAuth2ClientOptions.cs`
- `src/Shared/Http/ClientCredentialsTokenCache.cs`
- `src/Shared/Http/ClientCredentialsHandler.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-007`. Nothing there is summarised.

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

## STEP-008 (P1.T4) - Expose the typed-client base so each edge gets one HTTP client at the source's timeout

Depends on: STEP-007.

**Why.** P1.T4 needs an HTTP client per edge at the source's timeout. The source declares one Feign interface per edge, and the rates edge is the only anonymous one (R-009). D-008.

**Currently (source anchors).**
- account-service/.../client/StatisticsServiceClient.java:10 - @FeignClient(name = statistics-service, fallback = ...)
- account-service/.../client/AuthServiceClient.java:9 - @FeignClient(name = auth-service)
- notification-service/.../client/AccountServiceClient.java:9 - @FeignClient(name = account-service)
- statistics-service/.../client/ExchangeRatesClient.java:10 - @FeignClient(url = ${rates.url}, name = rates-client, fallback = ...), and statistics-service ResourceServerConfig registers no interceptor

**Change.** Add src/Shared/Http/HttpServiceCollectionExtensions.cs exposing three registrations: AddPiggyMetricsClientCredentials binds the client options, registers the token HttpClient at the source's 10000 ms timeout and registers the cache as a singleton and the handler as transient; AddPiggyMetricsClient registers a typed client at the source's timeout with the client-credentials handler attached, for an edge that calls a guarded PiggyMetrics service; AddPiggyMetricsAnonymousClient registers a typed client at the same timeout without the handler, for the rates edge, which the source declares with a url and no token.

**Files.**
- `src/Shared/Http/HttpServiceCollectionExtensions.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-008`. Nothing there is summarised.

exact_commands:

~~~bash
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
~~~

**Invariants.**
- Every typed client registered through these extensions carries PiggyMetricsTimeouts.Default.
- A client registered through AddPiggyMetricsClient attaches the client-credentials handler; one registered through AddPiggyMetricsAnonymousClient does not.
- The base address is absolute, so a relative request path resolves against the configured service host.
- The token HttpClient is a separate named client, so attaching the handler to a typed client cannot recurse into the token request.

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

## STEP-009 (P1.T5) - Port the JSON options: enum names, ignored unknown fields, epoch-millisecond dates

Depends on: STEP-002.

**Why.** P1.T5 names three JSON behaviours, and the downstream contract rows C1 to C3 and C8 to C10 replay with the source's body, so the wire shape must match Jackson's (R-010, R-011). D-010.

**Currently (source anchors).**
- config/src/main/resources/shared - holds no spring.jackson key in any file, so Jackson defaults apply
- account-service/.../domain/Account.java:14 - @JsonIgnoreProperties(ignoreUnknown = true); Account.java:20 - java.util.Date lastSeen
- statistics-service/.../domain/Currency.java:5 and .../timeseries/DataPoint.java:28-31 - enums as values and as map keys

**Change.** Add src/Shared/Json/EpochMillisecondsDateTimeConverter.cs and src/Shared/Json/PiggyMetricsJson.cs. The converter writes a DateTime as the epoch millisecond count Jackson emits for java.util.Date and reads a number, a numeric string, or an ISO text. PiggyMetricsJson builds the serializer options once (camel-case property names, unknown members skipped, nulls written, enums as names, numbers readable from strings) and exposes AddPiggyMetricsJson to apply the same options to the MVC pipeline.

**Files.**
- `src/Shared/Json/EpochMillisecondsDateTimeConverter.cs`
- `src/Shared/Json/PiggyMetricsJson.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-009`. Nothing there is summarised.

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

## STEP-010 (P1.T5) - Port the actuator health endpoint with the source's path, body and status codes

Depends on: STEP-002.

**Why.** P1.T5 needs health checks. The source exposes actuator health and the orchestration gates on it: config/Dockerfile probes it and docker-compose waits on service_healthy (R-012). D-011.

**Currently (source anchors).**
- config/Dockerfile:7 - HEALTHCHECK CMD curl -f http://localhost:8888/actuator/health
- docker-compose.yml:30,45,64,89,115,140,163,178 - condition: service_healthy
- config/.../SecurityConfig.java:18 - antMatchers(/actuator/**).permitAll()
- account-service/pom.xml - spring-boot-starter-actuator

**Change.** Add src/Shared/Health/HealthEndpoints.cs exposing AddPiggyMetricsHealth and MapPiggyMetricsHealth. The endpoint answers at /actuator/health relative to the service's context path, writes the actuator body shape (a single status member reading UP or DOWN, with no detail, matching Boot 2.0's default of hiding details), answers 200 for healthy and degraded and 503 for unhealthy, and stays anonymous because the source permits /actuator/** without authentication.

**Files.**
- `src/Shared/Health/HealthEndpoints.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-010`. Nothing there is summarised.

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

## STEP-011 (P1.T5) - Compose the service defaults: port, context path, resource server, JSON and health

Depends on: STEP-004, STEP-009, STEP-010.

**Why.** Each ported service must listen on the source's port under the source's context path with the same security, JSON and health wiring. Centralising that in Shared is what makes P2.T7 and P3.T8 configuration-only steps. D-012.

**Currently (source anchors).**
- config/src/main/resources/shared/auth-service.yml - context-path /uaa, port 5000
- account-service.yml /accounts 6000; statistics-service.yml /statistics 7000; notification-service.yml /notifications 8000; gateway.yml port 4000, no context path
- measured: UsePathBase alone served the unprefixed path 200, which Spring's context-path answers 404 for

**Change.** Add src/Shared/Configuration/ServerOptions.cs naming the two source configuration keys and src/Shared/PiggyMetricsServiceDefaults.cs exposing AddPiggyMetricsDefaults and UsePiggyMetricsDefaults. The builder extension binds the listener to server:port, adds controllers with the ported JSON options, adds the resource server and adds health. The application extension enforces server:servlet:context-path by answering 404 to any request outside that prefix before rebasing the pipeline onto it, then wires routing, authentication, authorization, the health endpoint and the controllers.

**Files.**
- `src/Shared/Configuration/ServerOptions.cs`
- `src/Shared/PiggyMetricsServiceDefaults.cs`

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-011`. Nothing there is summarised.

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

## STEP-012 (P1.T1) - Create the five runtime service projects on the source's ports and context paths

Depends on: STEP-011.

**Why.** P1.T1 names five runtime services: Gateway, AuthService, AccountService, StatisticsService and NotificationService. Each is a host consuming the Shared defaults with its own port and context path (R-013, R-014). D-003, D-012.

**Currently (source anchors).**
- pom.xml:41-51 - nine modules; P1.T1 names five runtime services
- gateway/Dockerfile EXPOSE 4000, auth-service 5000, account-service 6000, notification-service 8000; statistics-service.yml port 7000
- each module's entry point is a @SpringBootApplication class such as AccountApplication.java

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

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-012`. Nothing there is summarised.

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

## STEP-013 (P1.T2) - Add the Shared test project that proves the plan's done-when clause

Depends on: STEP-011.

**Why.** P1's done-when clause is behavioural: the handler accepts a token the source's authorization server issued and refuses one it did not. The same suite locks the document shape, the wire shape and the health body so a later plan cannot quietly change them. D-013.

**Currently (source anchors).**
- auth-service/.../controller/UserController.java:22 - the Principal this endpoint returns is what the fixtures render
- auth-service/.../controller/UserControllerTest.java - the source proves the same surface for P2
- target repo - has no test project

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

The complete final body of each file above is pinned in the companion outcome file `final/PINS.md`, section `STEP-013`. Nothing there is summarised.

exact_commands:

~~~bash
dotnet test tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj
~~~

**Invariants.**
- The suite reaches no network; every outbound call goes through the recording stub handler.
- The user-info fixtures carry the source's member names, including the top-level name member the handler resolves the principal from.
- The suite reports 24 passing tests and no skipped tests.
- The store convention tests assert the BSON type, not only the value, so a silent switch to Decimal128 fails.

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

## STEP-014 (P1.T1) - Wire the solution and prove every project builds and the suite is green

Depends on: STEP-012, STEP-013.

**Why.** P1.T1 needs one solution carrying every project and P1's done-when clause opens with every project builds. The solution file is generated rather than pinned because its project identifiers are generated values. D-001.

**Currently (source anchors).**
- pom.xml:41-51 - the module list the solution replaces
- target repo - has no solution file before this step

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

## Out of scope

- Plans P2 and later; they depend on P1 and are planned separately.
- `config`, `registry`, `monitoring` and `turbine-stream-service`: P1.T1 names five runtime services.
- Service discovery, the config server, the RabbitMQ bus and Hystrix dashboard streams.
- Domain types, repositories, controllers and ported Java tests for any service.
- Any data migration: no collection or database is renamed, reshaped or copied.

## Done when

- **Every project builds** - `dotnet build PiggyMetrics.sln -warnaserror` exits 0 with zero warnings (STEP-014).
- **The bearer handler accepts a token the source's authorization server issued and refuses one it did not** - `ShouldAuthenticateTokenIssuedByAuthorizationServer` and `ShouldRejectTokenTheAuthorizationServerDidNotIssue` pass inside a green 24-test run (STEP-013).

## Per-step edge cases and traceability

Each step's `edge_cases` list and its REQ/DEC/R trace are carried in `planning/implementation-plan.yaml` under the matching STEP id, which ships in this package and is committed at `.harnessmatters/plans/e2e-migration/P1/`.
