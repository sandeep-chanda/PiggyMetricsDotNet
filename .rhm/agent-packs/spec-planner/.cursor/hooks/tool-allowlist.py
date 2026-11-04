#!/usr/bin/env python3
"""Soft-deny MCP tools that look mutating during Plan Mode."""
from __future__ import annotations

import json
import re
import sys

MUTATE = re.compile(r"(create|update|delete|write|push|merge|close|comment|apply)", re.I)


def main() -> int:
    raw = sys.stdin.read()
    try:
        data = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        data = {}
    name = str(data.get("tool_name") or data.get("name") or data.get("tool") or "")
    if MUTATE.search(name):
        print(json.dumps({
            "permission": "deny",
            "user_message": f"Plan Mode: mutating MCP tool blocked: {name}",
        }))
        return 0
    print(json.dumps({"permission": "allow"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
