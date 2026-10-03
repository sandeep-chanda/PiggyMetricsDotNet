#!/usr/bin/env python3
"""Block Write/Edit tools outside workspace/ (Plan Mode — no target-repo writes)."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORKSPACE = (ROOT / "workspace").resolve()


def path_from_payload(data: dict) -> str:
    for key in ("path", "file_path", "filePath", "target", "uri"):
        if data.get(key):
            return str(data[key])
    args = data.get("arguments") or data.get("tool_input") or {}
    if isinstance(args, dict):
        for key in ("path", "file_path", "filePath", "target_notebook"):
            if args.get(key):
                return str(args[key])
    return ""


def main() -> int:
    raw = sys.stdin.read()
    try:
        data = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        data = {}
    path_s = path_from_payload(data)
    if not path_s:
        # unknown write target — deny to be safe in plan mode
        print(json.dumps({
            "permission": "deny",
            "user_message": "Plan Mode: write blocked (missing path). Only workspace/<task-id>/ is writable.",
        }))
        return 0
    path = Path(path_s)
    if not path.is_absolute():
        path = (ROOT / path).resolve()
    else:
        path = path.resolve()
    try:
        path.relative_to(WORKSPACE)
        print(json.dumps({"permission": "allow"}))
        return 0
    except ValueError:
        print(json.dumps({
            "permission": "deny",
            "user_message": f"Plan Mode: refuse write outside workspace/: {path}",
        }))
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
