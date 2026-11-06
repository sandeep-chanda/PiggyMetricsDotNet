# PiggyMetricsDotNet

.NET 8 port of PiggyMetrics. `PiggyMetricsDotNet.sln` holds a Shared library and one project per runtime service: Gateway, AuthService, AccountService, StatisticsService, and NotificationService.

Each service listens on its own port, uses its own context path, and reads its own settings (port, path, MongoDB, and client credentials). Shared supplies the bearer check the resource servers use and the store conventions for document ids and field names.

| Service | Project | Port | Path | Store |
| --- | --- | --- | --- | --- |
| Gateway | Gateway | 4000 | — | — |
| Auth | AuthService | 5000 | `/uaa` | `auth-mongodb` / `piggymetrics` / `users` |
| Account | AccountService | 6000 | `/accounts` | `account-mongodb` / `piggymetrics` / `accounts` |
| Statistics | StatisticsService | 7000 | `/statistics` | `statistics-mongodb` / `piggymetrics` / `datapoints` |
| Notification | NotificationService | 8000 | `/notifications` | `notification-mongodb` / `piggymetrics` / `recipients` |

The gateway forwards `/uaa/**`, `/accounts/**`, `/statistics/**`, and `/notifications/**` with the prefix intact. The same paths are served on the service ports above.

## Authentication

AuthService is the authorization server. It issues tokens for `client_credentials`, `password`, and `refresh_token` to the four clients (`browser`, `account-service`, `statistics-service`, `notification-service`). Resource servers validate a bearer token by calling `GET http://auth-service:5000/uaa/users/current`.

`GET /accounts/{name}` and `GET /statistics/{accountName}` also allow the account name `demo` with no token. Other calls follow the `user`, `server`, or anonymous policy on that route. See [docs/api.md](docs/api.md).

## Data

MongoDB database name is `piggymetrics` on each service host. Document ids and field names match the source collections:

- `users`: `_id` = username; field `password`
- `accounts`: `_id` = name; fields `name`, `lastSeen`, `incomes`, `expenses`, `saving`, `note`
- `datapoints`: `_id` = id; fields `id`, `incomes`, `expenses`, `statistics`, `rates`
- `recipients`: `_id` = accountName; fields `accountName`, `email`, `scheduledNotifications`

Embedded documents keep the source field names (`Item`, `Saving`, `DataPointId`, `ItemMetric`, `NotificationSettings`).

Default MongoDB settings are host per service (`auth-mongodb`, `account-mongodb`, `statistics-mongodb`, `notification-mongodb`), port `27017`, username `user`, and `authSource` set to the database. The password comes from `MONGODB_PASSWORD`.

Client secrets for the service clients come from `ACCOUNT_SERVICE_PASSWORD`, `STATISTICS_SERVICE_PASSWORD`, and `NOTIFICATION_SERVICE_PASSWORD`.

## Build

```bash
dotnet build PiggyMetricsDotNet.sln
dotnet test PiggyMetricsDotNet.sln
```

Test projects: `Shared.Tests`, `AuthService.Tests`, `AccountService.Tests`, `StatisticsService.Tests`, `NotificationService.Tests`, `Gateway.Tests`, and `Parity.Tests`.

## Decisions and API

- [ADR 0001](docs/adr/0001-solution-per-runtime-service.md) — solution layout and per-service settings
- [ADR 0002](docs/adr/0002-resource-server-bearer-check.md) — bearer check and route policies
- [ADR 0003](docs/adr/0003-mongodb-collections-and-field-names.md) — collections, ids, and field names
- [ADR 0004](docs/adr/0004-authorization-server-clients.md) — clients, grants, and scopes
- [ADR 0005](docs/adr/0005-gateway-routes.md) — gateway routes and port 4000
- [ADR 0006](docs/adr/0006-outbound-client-failure-policy.md) — outbound timeouts and failure behavior
- [ADR 0007](docs/adr/0007-notification-schedules.md) — backup and remind schedules
- [HTTP API](docs/api.md) — contracts C1–C12 and the token endpoint
