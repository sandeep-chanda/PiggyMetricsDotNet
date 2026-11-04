#!/usr/bin/env python3
"""Validate a workspace YAML artifact against a JSON Schema."""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

try:
    import yaml
    from jsonschema import Draft202012Validator
except ImportError:
    print("Install deps: pip install -r requirements.txt", file=sys.stderr)
    raise SystemExit(2)

ROOT = Path(__file__).resolve().parents[1]
SCHEMAS = ROOT / "schemas"

SCHEMA_MAP = {
    "job": "job.schema.json",
    "state": "state.schema.json",
    "finding": "finding.schema.json",
    "decision": "decision.schema.json",
    "plan-step": "plan-step.schema.json",
    "implementation-plan": "implementation-plan.schema.json",
    "implementation-dag": "implementation-dag.schema.json",
    "runtime-dag": "runtime-dag.schema.json",
    "migration-plan": "migration-plan.schema.json",
    "critic": "critic-report.schema.json",
    "gaps": "gaps.schema.json",
    "decision-entropy": "decision-entropy.schema.json",
    "quality-gate": "quality-gate.schema.json",
    "package-manifest": "package-manifest.schema.json",
    "environment": "environment.schema.json",
    "normalized-spec": "normalized-spec.schema.json",
}


def load_schema(name: str) -> dict:
    path = SCHEMAS / SCHEMA_MAP[name]
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--type", required=True, choices=sorted(SCHEMA_MAP))
    ap.add_argument("--file", required=True)
    args = ap.parse_args()
    data = yaml.safe_load(Path(args.file).read_text(encoding="utf-8"))
    schema = load_schema(args.type)
    v = Draft202012Validator(schema)
    errors = sorted(v.iter_errors(data), key=lambda e: list(e.path))
    if errors:
        for e in errors:
            path = ".".join(str(x) for x in e.path) or "<root>"
            print(f"FAIL {path}: {e.message}")
        return 1
    print(f"PASS {args.type} {args.file}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
