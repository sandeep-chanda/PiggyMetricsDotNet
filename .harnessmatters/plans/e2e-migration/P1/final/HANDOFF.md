# Coding agent handoff: PiggyMetricsDotNet P1

Execute `final/PLAYBOOK.md` in the order `final/DAG.yaml` gives. Do not reinterpret the architecture and do not invent artifact text: every file this plan creates is pinned as complete final source in the playbook.

## What you are holding

- `final/PLAYBOOK.md` - 14 todos, one per STEP, each with its complete final file text, commands, invariants, edge cases, do-not list and verification.
- `final/DAG.yaml` - the same 14 ids with dependency edges, per-node files, verification and acceptance.
- `planning/implementation-plan.yaml` - the machine source of truth for the STEPs.
- `architecture/decisions.yaml` - 13 decision records; `architecture/migration-plan.yaml` - the compatibility contracts this port must not break.
- `research/findings/` - 14 findings, each with file-and-line evidence from the migration source.

## Plan mode note

This package was produced in plan mode. The planner did not modify the target repository. A reference implementation was built outside the target tree so that the pinned text and the pinned commands are observations rather than proposals: the solution builds with zero warnings and the suite passes 24 of 24.

## Scope boundary

This is plan key **P1** of spec `E2E migration`, not the whole migration. P1 is the solution skeleton plus the Shared library. Plans P2 and later port the services themselves and depend on this one. Do not start a service's domain types, repositories, controllers or ported Java tests here.

## The four things most likely to go wrong

Each of these is a place where the idiomatic .NET choice silently diverges from the source. The plan pins the faithful choice; these notes say why, so you do not optimise them away.

### Decimals are BSON strings, not Decimal128

`spring-data-mongodb` 2.0.x registers `BigDecimalToStringConverter`, so every `amount` and `interest` the Java services wrote is a BSON string, while `mongodb/dump/account-service-dump.js` seeds them as numbers. The pinned serializer writes a string and reads string, double, int32, int64 and decimal128. `MongoConventionsTest` asserts the BSON type, not just the value, so a switch to `Decimal128` fails the suite rather than corrupting a shared collection.

### Tokens are resolved, not parsed

The source holds tokens in an `InMemoryTokenStore` and every resource server resolves them by calling `http://auth-service:5000/uaa/users/current`. The principal name comes from the first present member of `user`, `username`, `userid`, `user_id`, `login`, `id`, `name`. Any transport failure, any non-success status and any body carrying an `error` member is a refusal - the handler never fails open. Do not add a JWT bearer package.

### `UsePathBase` alone does not reproduce `context-path`

Spring's `server.servlet.context-path` answers 404 outside the prefix; `UsePathBase` happily serves the unprefixed path too. This was measured on a running service: before the guard, `/actuator/health` answered 200; after it, 404, while `/accounts/actuator/health` stayed 200. The guard is pinned in STEP-011. Without it a later contract replay can pass against a URL the source answers 404 for.

### Dates are epoch milliseconds

No source service sets `spring.jackson.date-format` or touches a serialization feature, so Jackson's defaults apply and `java.util.Date` goes out as a number. Exact copy: `2018-06-15T10:30:00Z` serializes as `1529058600000`.

## Commands you will run

Install the .NET 8 SDK and libicu first; without ICU the SDK aborts with `NETSDK1188`.

exact_commands:

~~~bash
dotnet --version
dotnet build src/Shared/PiggyMetrics.Shared.csproj -warnaserror
dotnet new sln --name PiggyMetrics
dotnet sln PiggyMetrics.sln add src/Shared/PiggyMetrics.Shared.csproj src/Gateway/PiggyMetrics.Gateway.csproj src/AuthService/PiggyMetrics.AuthService.csproj src/AccountService/PiggyMetrics.AccountService.csproj src/StatisticsService/PiggyMetrics.StatisticsService.csproj src/NotificationService/PiggyMetrics.NotificationService.csproj tests/PiggyMetrics.Shared.Tests/PiggyMetrics.Shared.Tests.csproj
dotnet build PiggyMetrics.sln -warnaserror
dotnet test PiggyMetrics.sln
~~~

## Done when

- `dotnet build PiggyMetrics.sln -warnaserror` exits 0 with `0 Warning(s)` and `0 Error(s)`.
- `dotnet test PiggyMetrics.sln` reports `Passed` with 24 tests and 0 failures.
- A started account service answers `200` on `/accounts/actuator/health` with the body `{"status":"UP"}` and `404` on `/actuator/health`.

## Commit convention

The spec asks that commit messages name the todo ids they complete. Each STEP carries its todo id (`P1.T1` through `P1.T5`) in its playbook heading, so a commit completing STEP-003 and STEP-004 names `P1.T2`.
