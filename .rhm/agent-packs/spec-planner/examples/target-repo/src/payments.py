"""Payments reuse IncrementalPipeline."""

from pipeline import IncrementalPipeline


def extract_payments(watermark: str | None):
    return [{"payment_id": 1, "ts": "2026-01-02"}]


def build_payments_pipeline() -> IncrementalPipeline:
    return IncrementalPipeline(watermark="2026-01-02")
