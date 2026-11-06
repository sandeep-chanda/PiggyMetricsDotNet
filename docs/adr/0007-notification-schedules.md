# ADR 0007: Notification schedules keep the source crons and isolate failures

## Status

Accepted

## Context

NotificationService sends backup and remind mail on fixed schedules. In the source, one recipient's failure must not cancel the rest of that run.

## Decision

NotificationService hosts two schedules on the source cron expressions:

| Job | Cron | Meaning |
| --- | --- | --- |
| `NotificationServiceImpl.sendBackupNotifications` | `0 0 12 * * *` | Daily at 12:00 |
| `NotificationServiceImpl.sendRemindNotifications` | `0 0 0 * * *` | Daily at 00:00 |

Each run walks the recipients that are due. A failure for one recipient is isolated so the others still run.

The schedules are hosted by NotificationService (port 8000, path `/notifications`). Recipient documents stay in `notification-mongodb` / `piggymetrics` / `recipients` (ADR 0003).

## Consequences

- Backup and remind fire at the same times as the source jobs.
- A single SMTP, account-lookup, or recipient error does not drop the remaining notifications in that run.
- The HTTP API for current notification settings (C11, C12) is independent of these schedules; the schedules read the stored recipient settings.
