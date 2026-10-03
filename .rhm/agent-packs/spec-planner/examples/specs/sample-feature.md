# Add incremental customer ingestion

## Goal

Add incremental customer ingestion that reuses the repository's existing `IncrementalPipeline` pattern (see orders/payments).

## Requirements

1. Fetch customers incrementally using a watermark.
2. Reuse `IncrementalPipeline`; do **not** invent a custom watermark framework.
3. Prefer extending or wrapping `CustomerClient` rather than replacing authentication.
4. Keep existing orders/payments pipelines working.
5. Add tests for empty results and API failure.

## Non-goals

- Migrating to a new HTTP stack
- Changing CI provider
