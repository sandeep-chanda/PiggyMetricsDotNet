# ADR 0006: Outbound clients keep the source timeout and failure policy

## Status

Accepted

## Context

Account, statistics, and notification call each other and an external rates service. The source clients each have a timeout and a defined failure behavior: some errors propagate to the caller, some use a fallback, and one is caught and logged inside the notification job.

## Decision

Typed HTTP clients use the source timeout (10 seconds on the shared edge client).

| Call | From | Request | On failure |
| --- | --- | --- | --- |
| E1 | AccountService `AuthServiceClient` | `POST /uaa/users` on auth-service | Propagates to the caller |
| E2 | AccountService `StatisticsServiceClient` | `PUT /statistics/{accountName}` on statistics-service | Fallback `StatisticsServiceClientFallback` |
| E3 | StatisticsService `ExchangeRatesClient` | `GET /latest` on rates-client | Fallback `ExchangeRatesClientFallback` |
| E4 | NotificationService `AccountServiceClient` | `GET /accounts/{accountName}` on account-service | Caught and logged in `NotificationServiceImpl` |

E1 runs during account creation, before the account document is saved. A failure from auth leaves the account uncreated and the error on the `POST /accounts` response.

E2 runs after the account document is saved. The fallback records the statistics error and the account update still stands.

Service clients send a `server`-scoped token from the client-credentials grant (ADR 0004).

## Consequences

- Auth outages fail account creation visibly.
- Statistics or rates outages do not fail the account update or the statistics request path that can fall back.
- A failed account lookup during notification does not surface as an HTTP error from the notification API; it is logged on the notification service.
