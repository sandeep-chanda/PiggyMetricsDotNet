#!/usr/bin/env python3
"""DEPRECATED — do not invent PLAYBOOK bodies from YAML.

PLAYBOOK.md must come from native Cursor Plan Mode:
  1) SwitchMode → plan
  2) Cursor creates ~/.cursor/plans/*.plan.md
  3) SwitchMode → agent
  4) python scripts/import-cursor-plan.py --workspace … [--plan path]

See docs/PLAYBOOK_FORMAT.md
"""
from __future__ import annotations

import argparse
import sys


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--workspace", default="")
    ap.add_argument("--out", default="")
    ap.add_argument("--plan", default="")
    ap.parse_args()
    print(
        "ERROR: render-playbook.py is retired.\n"
        "Flow: SwitchMode→plan → create Cursor plan → SwitchMode→agent → "
        "python scripts/import-cursor-plan.py --workspace <ws> [--plan <file.plan.md>]\n"
        "See docs/PLAYBOOK_FORMAT.md",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
