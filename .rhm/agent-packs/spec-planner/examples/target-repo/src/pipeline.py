"""Tiny synthetic target app — incremental customer fetch pattern."""

from __future__ import annotations


class CustomerClient:
    def fetch(self, customer_id: str) -> dict:
        return {"id": customer_id, "name": "Acme"}


class IncrementalPipeline:
    """Standard incremental ingestion pattern used by orders/payments."""

    def __init__(self, watermark: str | None = None) -> None:
        self.watermark = watermark

    def run(self, extract) -> list[dict]:
        rows = extract(self.watermark)
        return list(rows)


def extract_orders(watermark: str | None):
    return [{"order_id": 1, "ts": "2026-01-01"}]


def build_orders_pipeline() -> IncrementalPipeline:
    return IncrementalPipeline(watermark="2026-01-01")
