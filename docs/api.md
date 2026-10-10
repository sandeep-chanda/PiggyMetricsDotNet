# HTTP API

Contracts C1–C12 and the token endpoint. Paths are the same on the gateway (port 4000) and on the service that owns the prefix. The gateway leaves the prefix in place (ADR 0005).

| Prefix | Service | Direct base |
| --- | --- | --- |
| `/uaa` | AuthService | `http://auth-service:5000` |
| `/accounts` | AccountService | `http://account-service:6000` |
| `/statistics` | StatisticsService | `http://statistics-service:7000` |
| `/notifications` | NotificationService | `http://notification-service:8000` |

Send a bearer token as `Authorization: Bearer <access_token>` on every route except the token endpoint, `POST /accounts`, and the demo GET exception below.

JSON property names match the source spellings. Enums are strings. Account-service responses include null properties. Dates use the shared ISO `DateTimeOffset` converter.

Successful operations that the source declared as `void` return HTTP 200.

## Token endpoint

### C3 `POST /uaa/oauth/token`

Policy: client credentials (the OAuth client, not an end-user token).

Form body (`application/x-www-form-urlencoded`):

| Field | Required | Values |
| --- | --- | --- |
| `grant_type` | yes | `client_credentials`, `password`, or `refresh_token` |
| `username` | password grant | user name |
| `password` | password grant | user password |
| `refresh_token` | refresh grant | refresh token from an earlier issue |
| `scope` | no | a scope the client is allowed; omitted means the client's own scopes |
| `client_id`, `client_secret` | when Basic auth is absent | client id and secret |

Client authentication is an `Authorization: Basic` header of `client_id:client_secret`, or the form fields. `browser` has no secret.

| Client | Grants | Scope |
| --- | --- | --- |
| `browser` | `password`, `refresh_token` | `ui` |
| `account-service` | `client_credentials`, `refresh_token` | `server` |
| `statistics-service` | `client_credentials`, `refresh_token` | `server` |
| `notification-service` | `client_credentials`, `refresh_token` | `server` |

Each allowed grant returns a token the resource servers accept:

```json
{
  "access_token": "<token>",
  "token_type": "bearer",
  "expires_in": 43199,
  "scope": "server",
  "refresh_token": "<refresh>"
}
```

`expires_in` is the remaining access-token lifetime in seconds (43199 at issue).

| Condition | Status | Body |
| --- | --- | --- |
| Grant issued for an allowed client | 200 | token JSON above |
| Missing or bad client credentials | 401 | `invalid_client` |
| Client not allowed that `grant_type` | 401 | `invalid_client` |
| Missing `grant_type` or form body | 400 | `invalid_request` |
| Unknown user or bad password on `password` | 400 | `invalid_grant` |
| Unknown `refresh_token` | 400 | `invalid_grant` |
| `scope` outside the client | 400 | `invalid_scope` |
| Any other `grant_type` | 400 | `unsupported_grant_type` |

Error JSON is `{ "error": "<code>", "error_description": "<text>" }`.

## Auth

User body for create, and for `POST /accounts`:

| Field | Rules |
| --- | --- |
| `username` | required, length 3–20 |
| `password` | required, length 6–40 |

### C1 `GET /uaa/users/current`

`UserController.GetUser`. Policy `user`. Returns the principal.

Check: with a user token, the body matches the source principal; with no token, 401.

```json
{
  "name": "<principal name>",
  "username": "<username or omitted>",
  "authenticated": true,
  "clientOnly": false,
  "oauth2Request": {
    "clientId": "<client>",
    "scope": ["ui"]
  }
}
```

`username` is omitted when the token has no user (client-credentials tokens set `clientOnly` to true). `name` is the principal name. `oauth2Request.clientId` and `oauth2Request.scope` come from the token.

### C2 `POST /uaa/users`

`UserController.CreateUser`. Policy `server`. Body: `User` (validated). Returns void.

Check: a server token returns 200; a user token returns 403. No token returns 401. An invalid body returns 400.

The user is stored in `users` with `_id` = `username` and field `password`.

## Accounts

`Item`:

| Field | Rules |
| --- | --- |
| `title` | required, length 1–20 |
| `amount` | required number |
| `currency` | required: `USD`, `EUR`, or `RUB` |
| `period` | required: `YEAR`, `QUARTER`, `MONTH`, `DAY`, or `HOUR` |
| `icon` | required string |

`Saving`:

| Field | Rules |
| --- | --- |
| `amount` | required number |
| `currency` | required: `USD`, `EUR`, or `RUB` |
| `interest` | required number |
| `deposit` | required boolean |
| `capitalization` | required boolean |

`Account` (collection `accounts`, `_id` = `name`):

| Field | Rules |
| --- | --- |
| `name` | account name; the document id |
| `lastSeen` | date-time |
| `incomes` | array of `Item` |
| `expenses` | array of `Item` |
| `saving` | required `Saving` |
| `note` | string, max length 20000 |

### C4 `GET /accounts/{name}`

`AccountController.GetAccountByName`. Policy: scope `server`, or `name` is `demo`. Returns `Account`.

| Caller | Status |
| --- | --- |
| Server token | 200 |
| User token when `name` is not `demo` | 403 |
| No token when `name` is `demo` | 200 |
| No token when `name` is not `demo` | 401 |

### C5 `GET /accounts/current`

`AccountController.GetCurrentAccount`. Policy `user`. Returns the `Account` whose name is the principal.

Check: a user token returns the source body; no token returns 401.

### C6 `PUT /accounts/current`

`AccountController.SaveCurrentAccount`. Policy `user`. Body: `Account` (validated). Returns void.

Check: a user token returns the source body (200); no token returns 401. An invalid body returns 400.

The update applies `incomes`, `expenses`, `saving`, and `note` to the authenticated account and sets `lastSeen` to the current time. The account document is saved, then statistics are updated (`PUT /statistics/{accountName}`). If that statistics call fails, `StatisticsServiceClientFallback` handles it and the saved account remains (ADR 0006).

### C7 `POST /accounts`

`AccountController.CreateNewAccount`. Policy anonymous. Body: `User` (validated). Returns `Account`.

Check: a request with no token returns the source status and body.

| Condition | Status |
| --- | --- |
| New user and account | 200, body is the created `Account` |
| Invalid or missing user | 400 |
| Account name already exists | 400 |

Creation calls `POST /uaa/users` with the account-service client credentials. If that call fails, the error propagates and the account is not saved (ADR 0006). On success the new account is stored with `name` = `username`, `lastSeen` set to now, and a default `saving`: amount `0`, currency `USD`, interest `0`, `deposit` false, `capitalization` false.

## Statistics

Statistics `Account` is the request shape, not the account document. Fields: `incomes`, `expenses`, `saving`, with the same embedded `Item` and `Saving` field names as accounts.

`DataPoint` (collection `datapoints`, `_id` = `id`): fields `id`, `incomes`, `expenses`, `statistics`, `rates`, embedding `DataPointId` and `ItemMetric`.

### C8 `GET /statistics/current`

`StatisticsController.GetCurrentAccountStatistics`. Policy `user`. Returns `DataPoint[]` for the principal.

Check: a user token returns the source body; no token returns 401.

### C9 `GET /statistics/{accountName}`

`StatisticsController.GetStatisticsByAccountName`. Policy: scope `server`, or `accountName` is `demo`. Returns `DataPoint[]`.

| Caller | Status |
| --- | --- |
| Server token | 200 |
| User token when `accountName` is not `demo` | 403 |
| No token when `accountName` is `demo` | 200 |
| No token when `accountName` is not `demo` | 401 |

### C10 `PUT /statistics/{accountName}`

`StatisticsController.SaveAccountStatistics`. Policy `server`. Body: statistics `Account` (validated). Returns void.

Check: a server token returns 200; a user token returns 403. No token returns 401.

Rates used while saving statistics come from `GET /latest` on the rates client. On failure, `ExchangeRatesClientFallback` applies (ADR 0006). The client uses the source timeout.

## Notifications

`Recipient` (collection `recipients`, `_id` = `accountName`): fields `accountName`, `email`, `scheduledNotifications`. `scheduledNotifications` embeds `NotificationSettings`.

### C11 `GET /notifications/recipients/current`

`RecipientController.GetCurrentNotificationsSettings`. Policy `user`. Returns the current recipient settings.

Check: a user token returns the source body; no token returns 401.

### C12 `PUT /notifications/recipients/current`

`RecipientController.SaveCurrentNotificationsSettings`. Policy `user`. Body: `Recipient` (validated). Returns the saved settings.

Check: a user token returns the source body; no token returns 401.

Backup and remind mail are not HTTP routes. They run on the hosted schedules in ADR 0007 (`0 0 12 * * *` and `0 0 0 * * *`). A failure for one recipient does not stop the others. Account lookups from those jobs (`GET /accounts/{accountName}`) that fail are caught and logged in `NotificationServiceImpl`.
