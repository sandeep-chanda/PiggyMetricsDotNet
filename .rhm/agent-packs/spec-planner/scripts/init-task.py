#!/usr/bin/env python3
"""Bootstrap workspace/<task-id>/ for a Spec + TargetRepo planning run (Plan Mode only)."""
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    p = argparse.ArgumentParser(description="Init spec-planner task workspace (plan_only)")
    p.add_argument("--spec", required=True, help="Path to spec file")
    p.add_argument("--target", required=True, help="Path to target repo (read-only for planner)")
    p.add_argument("--task-id", default="", help="Optional task id")
    p.add_argument("--related", action="append", default=[], help="Related repo local path (repeatable)")
    p.add_argument(
        "--connection",
        default="",
        help="Path to connection.meta.json (provider, sibling_repos, auth refs)",
    )
    args = p.parse_args()

    spec = Path(args.spec).resolve()
    target = Path(args.target).resolve()
    if not spec.is_file():
        raise SystemExit(f"spec not found: {spec}")
    if not target.exists():
        raise SystemExit(f"target repo not found: {target}")

    connection_src = Path(args.connection).resolve() if args.connection else None
    if connection_src is not None and not connection_src.is_file():
        raise SystemExit(f"connection meta not found: {connection_src}")

    task_id = args.task_id or datetime.now(timezone.utc).strftime("task-%Y%m%d-%H%M%S")
    ws = ROOT / "workspace" / task_id
    if ws.exists():
        raise SystemExit(f"workspace already exists: {ws}")

    for sub in [
        "intake",
        "spec",
        "research/findings",
        "architecture",
        "planning",
        "review",
        "final",
    ]:
        (ws / sub).mkdir(parents=True, exist_ok=True)

    (ws / "intake" / "raw-spec.md").write_text(spec.read_text(encoding="utf-8"), encoding="utf-8")

    connection_meta_path = ""
    if connection_src is not None:
        dest = ws / "intake" / "connection.meta.json"
        dest.write_text(connection_src.read_text(encoding="utf-8"), encoding="utf-8")
        connection_meta_path = str(dest)

    related = [str(Path(r).resolve()) for r in args.related]
    created = datetime.now(timezone.utc).isoformat()
    related_block = "related_repos: []\n" if not related else "related_repos:\n" + "".join(f"  - {r}\n" for r in related)
    conn_line = f"connection_meta_path: {connection_meta_path}\n" if connection_meta_path else "connection_meta_path: \"\"\n"
    job_text = (
        f"task_id: {task_id}\n"
        f"spec_path: {spec}\n"
        f"target_repo_path: {target}\n"
        f"{conn_line}"
        f"{related_block}"
        "mode: plan_only\n"
        "constraints:\n"
        "  - Cursor Plan Mode only\n"
        "  - No writes to target repository\n"
        "  - Writes only under this workspace\n"
        "  - Use connection.meta sibling_repos + auth refs (docs/CONNECTION.md)\n"
        f"created_at: {created}\n"
    )
    (ws / "intake" / "job.yaml").write_text(job_text, encoding="utf-8")

    (ws / "state.yaml").write_text(
        f"task_id: {task_id}\n"
        "phase: intake\n"
        "loops: 0\n"
        "blockers: []\n"
        "approved: false\n"
        "plan_mode: true\n"
        "target_repo_writes_allowed: false\n",
        encoding="utf-8",
    )

    (ws / "research" / "questions.yaml").write_text(
        "questions:\n"
        "  - id: Q-001\n"
        "    question: How are similar features implemented in this repository?\n"
        "    dimension: repository\n"
        "    status: open\n"
        "    finding_refs: []\n"
        "  - id: Q-002\n"
        "    question: Is there already an abstraction for incremental ingestion?\n"
        "    dimension: repository\n"
        "    status: open\n"
        "    finding_refs: []\n"
        "  - id: Q-003\n"
        "    question: What does the framework/environment officially recommend?\n"
        "    dimension: external\n"
        "    status: open\n"
        "    finding_refs: []\n",
        encoding="utf-8",
    )

    meta = {"workspace": str(ws), "mode": "plan_only", "target_repo_writes_allowed": False}
    print(json.dumps(meta, indent=2))
    print(f"Initialized {ws}")
    print("Plan Mode: do not write to target repo. Write artifacts under this workspace only.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
