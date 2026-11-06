# ADR 0002: Resource servers validate bearer tokens through the auth user-info endpoint

## Status

Accepted

## Context

Account, statistics, and notification protect routes with the same rules as the source resource servers. A caller presents a bearer token issued by AuthService. The resource server has to accept that token the same way the source does, including the demo-account exception on two GET routes.

## Decision

`Shared` provides a bearer handler that validates the caller's token by calling `GET http://auth-service:5000/uaa/users/current`. AuthService itself authenticates tokens it issued (see ADR 0004) and serves that user-info route.

Authorization policies:

- `user` — the caller is authenticated. No token yields 401.
- `server` — the caller is authenticated and the token includes scope `server`. A user token yields 403. No token yields 401.
- anonymous — `POST /accounts` does not require a token.
- demo exception — `GET /accounts/{name}` and `GET /statistics/{accountName}` succeed when the name is `demo`, including when no token is present. Any other name requires scope `server`.

Route assignment:

| Policy | Routes |
| --- | --- |
| `user` | `GET /uaa/users/current`, `GET /accounts/current`, `PUT /accounts/current`, `GET /statistics/current`, `GET /notifications/recipients/current`, `PUT /notifications/recipients/current` |
| `server` | `POST /uaa/users`, `PUT /statistics/{accountName}` |
| `server`, or the name `demo` | `GET /accounts/{name}`, `GET /statistics/{accountName}` |
| anonymous | `POST /accounts` |

`GET` and `PUT` `current` routes take the account name from the authenticated principal.

## Consequences

- A token the auth server issues is acceptable to every resource server that can reach `http://auth-service:5000/uaa/users/current`.
- User-scoped and server-scoped tokens stay distinct: scope `ui` (the browser client) cannot call server routes, and scope `server` is what service clients present.
- The demo account remains readable without a token, on both account and statistics.
