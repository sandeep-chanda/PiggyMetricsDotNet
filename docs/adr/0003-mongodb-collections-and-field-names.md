# ADR 0003: Keep the source databases, collections, ids, and field names

## Status

Accepted

## Context

Existing PiggyMetrics data lives in MongoDB. The port has to read and write those documents without a rename of collections, ids, or fields. Each service already has its own MongoDB host and the database name `piggymetrics`.

## Decision

`Shared` defines the store conventions and the serializers for document field names and ids. Each service uses its own host and the same database and collection names.

| Service | Host | Database | Collection | `_id` | Fields |
| --- | --- | --- | --- | --- | --- |
| AuthService | `auth-mongodb` | `piggymetrics` | `users` | username | `username`, `password` |
| AccountService | `account-mongodb` | `piggymetrics` | `accounts` | name | `name`, `lastSeen`, `incomes`, `expenses`, `saving`, `note` |
| StatisticsService | `statistics-mongodb` | `piggymetrics` | `datapoints` | id | `id`, `incomes`, `expenses`, `statistics`, `rates` |
| NotificationService | `notification-mongodb` | `piggymetrics` | `recipients` | accountName | `accountName`, `email`, `scheduledNotifications` |

Embedded documents use the source field names:

- Account `incomes` and `expenses` embed `Item` (`title`, `amount`, `currency`, `period`, `icon`).
- Account `saving` embeds `Saving` (`amount`, `currency`, `interest`, `deposit`, `capitalization`).
- Statistics `Account` (the request shape) carries `incomes`, `expenses`, and `saving` with the same embedded names.
- `DataPoint` embeds `DataPointId` and `ItemMetric`.
- `Recipient.scheduledNotifications` embeds `NotificationSettings`.

The MongoDB user authenticates against `piggymetrics` (`authSource` is that database), not `admin`. The password is `MONGODB_PASSWORD`.

AuthService stores `password` on the user document and checks the password grant with BCrypt, so existing password hashes remain valid.

## Consequences

- The .NET services can use the same MongoDB databases as the source services.
- JSON and BSON property names stay on the source spellings (`lastSeen`, `accountName`, currency codes `USD`, `EUR`, `RUB`, periods `YEAR`, `QUARTER`, `MONTH`, `DAY`, `HOUR`).
- A document written by one side stays readable by the other as long as both use these ids and field names.
