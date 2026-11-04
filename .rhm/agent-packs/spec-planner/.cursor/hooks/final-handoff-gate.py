#!/usr/bin/env python3
"""On stop: if a workspace looks mid-flight toward APPROVED, remind about handoff gate."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WS = ROOT / "workspace"


def main() -> int:
    # Informational follow-up — do not hard-fail agent stop
    msg = (
        "Plan Mode reminder: approve handoff only after "
        "`python scripts/check-handoff.py --workspace workspace/<task-id>` PASSes. "
        "Never write to the target repository from this planner."
    )
    print(json.dumps({"user_message": msg}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
