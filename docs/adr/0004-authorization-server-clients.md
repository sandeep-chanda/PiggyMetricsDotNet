# ADR 0004: Authorization server with the four source clients

## Status

Accepted

## Context

The source authorization server has four OAuth clients. Each client is limited to a fixed set of grants and scopes. Resource servers accept only tokens this server issues. Account creation and the token endpoint are part of the same auth service, under the `/uaa` path on port 5000.

## Decision

AuthService is the authorization server. `POST /uaa/oauth/token` accepts `grant_type` of `client_credentials`, `password`, or `refresh_token`, and only when that grant is enabled for the client. Client identity is HTTP Basic (`client_id:client_secret`) or the form fields `client_id` and `client_secret`.

| Client | Secret | Grants | Scopes |
| --- | --- | --- | --- |
| `browser` | none | `password`, `refresh_token` | `ui` |
| `account-service` | `ACCOUNT_SERVICE_PASSWORD` | `client_credentials`, `refresh_token` | `server` |
| `statistics-service` | `STATISTICS_SERVICE_PASSWORD` | `client_credentials`, `refresh_token` | `server` |
| `notification-service` | `NOTIFICATION_SERVICE_PASSWORD` | `client_credentials`, `refresh_token` | `server` |

When the request omits `scope`, the token uses the client's scopes. A requested scope must be one of those scopes.

Issued tokens include `access_token`, `token_type` `bearer`, `expires_in`, `scope`, and `refresh_token`. Access tokens last 43199 seconds. Each grant issues a token that the resource-server bearer check accepts (ADR 0002).

`GET /uaa/users/current` (policy `user`) returns the principal. `POST /uaa/users` (policy `server`) creates a user in the `users` collection. User documents use `_id` = username and a `password` field (ADR 0003).

## Consequences

- Browser sessions use the password grant and scope `ui`. Service-to-service calls use client credentials and scope `server`.
- An unauthorized grant or a bad client secret is rejected at the token endpoint, so those tokens never reach a resource server.
- Refresh stays available to every client that the source allowed to refresh.
