#!/usr/bin/env python3
"""Check prerequisite artifacts exist before a role may run."""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

PREREQS = {
    "spec-analyst": [],
    "environment-analyst": ["intake/job.yaml"],
    "repo-researcher": ["intake/job.yaml"],
    "cross-repo-researcher": ["intake/job.yaml"],
    "web-researcher": ["spec/normalized.yaml"],
    "research-synthesizer": [
        "research/environment.yaml",
        "research/repository.yaml",
    ],
    "architect": ["research/evidence.yaml", "spec/normalized.yaml"],
    "migration": ["architecture/decisions.yaml"],
    "planner": ["architecture/decisions.yaml", "research/evidence.yaml"],
    "dag-builder": ["planning/implementation-plan.yaml"],
    "critic": [
        "planning/implementation-plan.yaml",
        "planning/implementation-dag.yaml",
    ],
    "gap-loop": ["review/critic.yaml"],
    "quality-gate": [
        "review/critic.yaml",
        "planning/implementation-dag.yaml",
        "planning/implementation-plan.yaml",
    ],
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--workspace", required=True)
    ap.add_argument("--role", required=True, choices=sorted(PREREQS))
    args = ap.parse_args()
    ws = Path(args.workspace)
    missing = [rel for rel in PREREQS[args.role] if not (ws / rel).is_file()]
    if missing:
        for m in missing:
            print(f"BLOCK missing prerequisite: {m}")
        return 1
    print(f"PASS prereqs for {args.role}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
