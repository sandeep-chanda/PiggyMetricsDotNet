#!/usr/bin/env python3
"""subagentStart helper: expect JSON with workspace + role; block if prereqs missing."""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[2] / "scripts"


def main() -> int:
    raw = sys.stdin.read()
    try:
        data = json.loads(raw) if raw.strip() else {}
    except json.JSONDecodeError:
        data = {}
    # Best-effort: if hook payload lacks workspace/role, allow (orchestrator still enforces)
    ws = data.get("workspace") or data.get("cwd")
    role = data.get("role") or data.get("subagent_type") or data.get("agent")
    if not ws or not role or role not in {
        "spec-analyst", "environment-analyst", "repo-researcher", "cross-repo-researcher",
        "web-researcher", "research-synthesizer", "architect", "migration", "planner",
        "dag-builder", "critic", "gap-loop", "quality-gate",
    }:
        print(json.dumps({"permission": "allow"}))
        return 0
    r = subprocess.run(
        [sys.executable, str(SCRIPTS / "check-prereqs.py"), "--workspace", str(ws), "--role", str(role)],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        print(json.dumps({
            "permission": "deny",
            "user_message": r.stdout.strip() or "Plan Mode: subagent prerequisites missing",
        }))
        return 0
    print(json.dumps({"permission": "allow"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
