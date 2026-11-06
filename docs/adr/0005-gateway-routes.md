# ADR 0005: Gateway routes keep the service prefix

## Status

Accepted

## Context

Clients call PiggyMetrics through one gateway. The source gateway routes by path prefix and leaves that prefix on the upstream request, because each service is mounted on that prefix (`/uaa`, `/accounts`, `/statistics`, `/notifications`).

## Decision

Gateway listens on port 4000 and uses the source gateway timeouts. It forwards these prefixes intact:

| Prefix | Upstream |
| --- | --- |
| `/uaa/**` | `http://auth-service:5000` |
| `/accounts/**` | AccountService |
| `/statistics/**` | StatisticsService |
| `/notifications/**` | NotificationService |

Upstream services are the ones in ADR 0001 (ports 5000, 6000, 7000, and 8000). A request that arrives at the gateway as `/accounts/current` is forwarded as `/accounts/current`.

## Consequences

- Callers can use the gateway on port 4000 or the service port; the path is the same.
- Prefix-stripping would miss every controller, because the services mount routes under their context path.
- Gateway timeouts stay the source timeouts, so slow upstreams fail on the same budget as before.
