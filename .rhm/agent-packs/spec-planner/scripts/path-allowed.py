#!/usr/bin/env python3
"""Return whether a filesystem path is inside planner workspace (allowed write)."""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORKSPACE = (ROOT / "workspace").resolve()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--path", required=True)
    ap.add_argument("--json-out", action="store_true")
    args = ap.parse_args()
    path = Path(args.path).resolve()
    allowed = False
    try:
        path.relative_to(WORKSPACE)
        allowed = True
    except ValueError:
        allowed = False
    # also allow writes under schemas/templates/docs/examples inside planner for bootstrap
    if not allowed:
        for rel in ("schemas", "templates", "docs", "examples", ".cursor"):
            try:
                path.relative_to((ROOT / rel).resolve())
                # still deny if somehow under a nested target — only examples/target-repo is sample
                if "examples" in path.parts and "target-repo" in path.parts and path.suffix in {
                    ".py",
                    ".ts",
                    ".js",
                }:
                    # golden/example target code may be edited by humans, not by planner agent hooks
                    allowed = False
                else:
                    allowed = rel != "examples" or "golden-package" in path.parts or path.name.endswith(".md")
            except ValueError:
                pass
    # Strict rule for agent: only workspace/
    allowed = False
    try:
        path.relative_to(WORKSPACE)
        allowed = True
    except ValueError:
        allowed = False

    payload = {"path": str(path), "allowed": allowed, "reason": "workspace_only" if allowed else "plan_mode_block_target_or_non_workspace"}
    if args.json_out:
        print(json.dumps(payload))
    else:
        print("ALLOW" if allowed else "DENY", str(path))
    return 0 if allowed else 1


if __name__ == "__main__":
    raise SystemExit(main())
