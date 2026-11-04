#!/usr/bin/env python3
"""Copy a native Cursor Plan Mode .plan.md into workspace final/PLAYBOOK.md."""
from __future__ import annotations

import argparse
import json
import os
import shutil
import time
from pathlib import Path


def _default_plans_dir() -> Path:
    home = Path.home()
    candidates = [
        home / ".cursor" / "plans",
        Path(os.environ.get("USERPROFILE", "")) / ".cursor" / "plans",
        home / "AppData" / "Roaming" / "Cursor" / "User" / "plans",
    ]
    for c in candidates:
        if c.is_dir():
            return c
    return home / ".cursor" / "plans"


def _newest_plan(plans_dir: Path, *, max_age_sec: float | None) -> Path | None:
    files = sorted(plans_dir.glob("*.plan.md"), key=lambda p: p.stat().st_mtime, reverse=True)
    if not files:
        return None
    if max_age_sec is None:
        return files[0]
    now = time.time()
    for f in files:
        if now - f.stat().st_mtime <= max_age_sec:
            return f
    # Fall back to newest even if older (caller may still pass --plan).
    return files[0]


def main() -> int:
    ap = argparse.ArgumentParser(
        description="Import Cursor Plan Mode .plan.md → final/PLAYBOOK.md"
    )
    ap.add_argument("--workspace", required=True, help="workspace/<task-id>")
    ap.add_argument(
        "--plan",
        default="",
        help="Path to a specific ~/.cursor/plans/*.plan.md (default: newest in plans dir)",
    )
    ap.add_argument(
        "--plans-dir",
        default="",
        help="Directory of Cursor plans (default: ~/.cursor/plans)",
    )
    ap.add_argument(
        "--max-age-sec",
        type=float,
        default=3600.0,
        help="Prefer a plan newer than this many seconds (default 3600). 0 = any newest.",
    )
    ap.add_argument(
        "--dry-run",
        action="store_true",
        help="Print source/dest only; do not write",
    )
    args = ap.parse_args()

    ws = Path(args.workspace)
    out = ws / "final" / "PLAYBOOK.md"
    meta_out = ws / "final" / "PLAYBOOK.import.json"
    out.parent.mkdir(parents=True, exist_ok=True)

    if args.plan:
        src = Path(args.plan)
    else:
        plans_dir = Path(args.plans_dir) if args.plans_dir else _default_plans_dir()
        if not plans_dir.is_dir():
            raise SystemExit(f"plans dir not found: {plans_dir}")
        max_age = None if args.max_age_sec <= 0 else float(args.max_age_sec)
        src = _newest_plan(plans_dir, max_age_sec=max_age)
        if src is None:
            raise SystemExit(f"no *.plan.md in {plans_dir}")

    if not src.is_file():
        raise SystemExit(f"plan not found: {src}")

    text = src.read_text(encoding="utf-8")
    if not text.lstrip().startswith("---"):
        raise SystemExit(f"not a Cursor plan (missing YAML frontmatter): {src}")

    print(f"source: {src}")
    print(f"dest:   {out}")
    if args.dry_run:
        return 0

    shutil.copyfile(src, out)
    if not text.endswith("\n"):
        out.write_text(text + "\n", encoding="utf-8")

    meta = {
        "source_plan": str(src.resolve()),
        "dest": str(out.resolve()),
        "imported_at_unix": time.time(),
        "bytes": out.stat().st_size,
        "importer": "scripts/import-cursor-plan.py",
    }
    meta_out.write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")

    print(f"Wrote {out} ({out.stat().st_size} bytes)")
    print(f"Wrote {meta_out}")
    print("Invoke playbook-importer after Plan→Agent; do not re-template the body.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
