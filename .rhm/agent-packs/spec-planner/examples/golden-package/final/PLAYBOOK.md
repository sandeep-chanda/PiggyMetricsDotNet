# PLAYBOOK — golden-demo

## Plan Mode

This package was produced in Plan Mode. The target repository was **not** modified.

## Objective

Add incremental customer ingestion reusing `IncrementalPipeline`.

## Decisions

- **D-001:** Reuse IncrementalPipeline (see architecture/decisions.yaml)

## Steps

### STEP-001 — Add customer extract helper
- Depends on: (none)
- Files: `src/customers.py`
- Pattern: `src/payments.py:1-12`
- Do not: modify CustomerClient auth; new HTTP client

### STEP-002 — Wire IncrementalPipeline for customers
- Depends on: STEP-001
- Files: `src/customers.py`
- Pattern: `src/pipeline.py:12-28`
- Do not: custom watermark framework; change orders/payments

### STEP-003 — Tests
- Depends on: STEP-002
- Files: `tests/test_customers.py`
- Empty results + API failure

## iDAG

STEP-001 → STEP-002 → STEP-003
