#!/usr/bin/env python3
"""Fail if critical unresolved decision-entropy items remain."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    print("Install deps: pip install -r requirements.txt", file=sys.stderr)
    raise SystemExit(2)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--file", required=True)
    args = ap.parse_args()
    data = yaml.safe_load(Path(args.file).read_text(encoding="utf-8")) or {}
    count = data.get("critical_unresolved_count")
    if count is None:
        items = data.get("items") or []
        count = sum(1 for i in items if i.get("severity") == "critical" and i.get("action") == "BLOCK")
    if int(count) > 0:
        print(f"FAIL critical_unresolved_count={count}")
        return 1
    print("PASS decision-entropy")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
