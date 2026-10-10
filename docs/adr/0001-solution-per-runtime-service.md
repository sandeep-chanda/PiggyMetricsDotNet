# ADR 0001: One project per runtime service, plus Shared

## Status

Accepted

## Context

PiggyMetrics is a set of runtime services (gateway, auth, account, statistics, notification) with shared token checks and shared document conventions. The port has to keep those services independently deployable, on the source ports and context paths, while sharing the behavior that every resource server and store uses.

## Decision

`PiggyMetricsDotNet.sln` targets .NET 8 and contains:

- `Shared`
- `Gateway`, `AuthService`, `AccountService`, `StatisticsService`, `NotificationService`
- `Shared.Tests`, `Gateway.Tests`, `AuthService.Tests`, `AccountService.Tests`, `StatisticsService.Tests`, `NotificationService.Tests`, `Parity.Tests`

Each runtime service is its own web project. Settings are per service and environment: port, context path, MongoDB host, and OAuth client credentials. The values follow the source service configuration.

| Service | Port | Context path | MongoDB host |
| --- | --- | --- | --- |
| AuthService | 5000 | `/uaa` | `auth-mongodb` |
| AccountService | 6000 | `/accounts` | `account-mongodb` |
| StatisticsService | 7000 | `/statistics` | `statistics-mongodb` |
| NotificationService | 8000 | `/notifications` | `notification-mongodb` |
| Gateway | 4000 | — | — |

`Shared` holds the bearer handler and the store conventions. Service projects reference `Shared` and add only their own dependencies (MongoDB driver, and BCrypt on AuthService).

## Consequences

- A service can be built and hosted on its source port without loading the other services.
- Ports, paths, and store hosts stay aligned with the source deployment names (`auth-service:5000`, `account-service:6000`, `statistics-service:7000`, `notification-service:8000`).
- Cross-cutting token and document rules change in `Shared`, then apply to every resource server.
