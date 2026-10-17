# Coding agent handoff

Execute the implementation playbook and iDAG literally. Do not reinterpret architecture. Do not invent files, copy, APIs, or commands. If something is unspecified, stop.

## Plan Mode note

This package was produced as a Cursor plan and imported by playbook-importer. The planner did not modify product source in the target repository.

## Package

- PLAYBOOK.md carries STEP-001 through STEP-011 with file bundles.
- DAG.yaml is the iDAG. Every node sets no_imagination true.
- Decisions D-001 through D-011 fix the framework, token check, Mongo names, timeouts, and scope.

## Concrete pins

AuthService listens on port 5000 with path base `/uaa`. StatisticsService listens on port 7000 with path base `/statistics`.

exact_commands:

~~~bash
dotnet build PiggyMetrics.sln --nologo
dotnet test PiggyMetrics.sln --nologo
~~~

exact_snippet (`src/Shared/Health/PiggyHealthExtensions.cs`):

~~~csharp
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

Exact copy: `{"status":"UP"}`.

## Out of scope

Do not add AccountService business endpoints, NotificationService jobs, or Gateway routes while executing this package.
