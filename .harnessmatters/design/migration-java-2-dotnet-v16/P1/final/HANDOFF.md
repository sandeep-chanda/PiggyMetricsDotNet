# Coding Agent Handoff - P1 Solution skeleton and Shared

> Execute `final/PLAYBOOK.md` and `final/DAG.yaml` literally. Do not reinterpret the architecture.
> Do not invent files, source, configuration keys, commands or package versions.
> If anything is unspecified, stop: that is a planner defect, not a licence to invent.

## Plan Mode note

This package was produced in planning mode. The planner did not modify the target repository; every artifact
lives under the planner workspace at `.rhm/agent-packs/spec-planner/workspace/mig-java-dotnet-v16-p1/`.

## What you are given

- `final/PLAYBOOK.md` - eleven steps, each with the complete final body of every file it creates.
- `final/DAG.yaml` - the iDAG: per node the files, a summary of the post-state, verification and acceptance.
- `planning/implementation-plan.yaml` - the same steps as machine-readable STEPs (in the committed workspace).
- `architecture/decisions.yaml` - the fifteen decisions and the alternatives they rejected.
- `planning/test-plan.yaml`, `planning/risks.yaml`.

## The job in one paragraph

The target repository is empty. Create the .NET solution the Java-to-.NET migration builds on: repository build
configuration, a Shared class library carrying the bearer handler, the store conventions, the typed-client base,
the health endpoint and the JSON contract, the five runtime service projects, and one test project that proves
the bearer handler accepts a token the source's authorization server issued and refuses one it did not.

## Order

STEP-001, then STEP-002. STEP-003, STEP-004, STEP-005 and STEP-007 are independent after STEP-002.
STEP-006 follows STEP-005. STEP-008 follows STEP-003. STEP-009 follows STEP-006 and STEP-008.
STEP-010 follows STEP-006. STEP-011 follows STEP-004, STEP-007, STEP-009 and STEP-010.

## Verify

exact_commands:

~~~bash
dotnet restore PiggyMetrics.sln
dotnet build PiggyMetrics.sln -c Debug --no-restore
dotnet test PiggyMetrics.sln -c Debug --no-build
dotnet sln PiggyMetrics.sln list
~~~

Done when the build reports zero errors for all seven projects and the test run reports five passed and zero
failed, which is both halves of the P1 done-when clause.

## Hard rules

- Do not add a package version to any csproj; `Directory.Packages.props` owns every version.
- Do not add JWT validation, a signing key or an introspection call; the source's tokens are opaque.
- Do not cache the user-info response.
- Do not use 20000 ms as a service edge timeout; that is the gateway's own setting.
- Do not add controllers, document classes, repositories, store registration or per-service settings beyond the
  Kestrel listener and the context path; those belong to P2, P3 and the remaining service plans.
- Do not add Eureka, Spring Cloud Config, Hystrix, Turbine or RabbitMQ counterparts.
- Do not push a branch or open a pull request; the runtime owns that.

## Pin convention

Source and command pins appear as labelled fences introduced by three tildes plus the language. Every pinned file is indented with one tab per
level. Copy the bodies verbatim.

Exact health body: `{"status":"UP"}`.
Exact failure message: `Could not fetch user details`.
Exact date rendering: `2018-06-01T12:00:00.000+0000`.
