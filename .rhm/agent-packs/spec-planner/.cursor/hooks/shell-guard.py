#!/usr/bin/env python3
"""Deny shell commands that mutate the target repo or look destructive outside workspace."""
from __future__ import annotations

import json
import re
import sys

DENY = re.compile(
    r"(git\s+commit|git\s+push|git\s+reset\s+--hard|npm\s+install|pnpm\s+install|yarn\s+add|"
    r"pip\s+install|terraform\s+apply|kubectl\s+apply|rm\s+-rf|del\s+/s)",
    re.I,
)


def main() -> int:
    raw = sys.stdin.read()
    try:
        data = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        data = {}
    cmd = data.get("command") or data.get("command_line") or ""
    if DENY.search(cmd):
        print(json.dumps({
            "permission": "deny",
            "user_message": "Plan Mode: shell mutation blocked. Planner must not modify target repo or run destructive installs/commits.",
        }))
        return 0
    print(json.dumps({"permission": "allow"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
