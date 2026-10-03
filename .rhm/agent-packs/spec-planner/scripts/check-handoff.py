#!/usr/bin/env python3
"""Final handoff readiness: critic PASS, quality PASS, entropy 0, DAG ok, plan_mode."""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    print("Install deps: pip install -r requirements.txt", file=sys.stderr)
    raise SystemExit(2)

SCRIPTS = Path(__file__).resolve().parent


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--workspace", required=True)
    args = ap.parse_args()
    ws = Path(args.workspace)
    errs: list[str] = []

    state = yaml.safe_load((ws / "state.yaml").read_text(encoding="utf-8")) or {}
    if state.get("plan_mode") is not True:
        errs.append("plan_mode must be true")
    if state.get("target_repo_writes_allowed") is not False:
        errs.append("target_repo_writes_allowed must be false")

    critic = yaml.safe_load((ws / "review" / "critic.yaml").read_text(encoding="utf-8"))
    if (critic or {}).get("result") != "PASS":
        errs.append("critic not PASS")

    qg = yaml.safe_load((ws / "review" / "quality-gate.yaml").read_text(encoding="utf-8"))
    if (qg or {}).get("result") != "PASS":
        errs.append("quality-gate not PASS")
    checks = (qg or {}).get("checks") or {}
    if checks.get("plan_mode_no_target_writes") is not True:
        errs.append("quality-gate.plan_mode_no_target_writes must be true")

    entropy_path = ws / "review" / "decision-entropy.yaml"
    if entropy_path.is_file():
        r = subprocess.run(
            [sys.executable, str(SCRIPTS / "validate-entropy.py"), "--file", str(entropy_path)],
            capture_output=True,
            text=True,
        )
        if r.returncode != 0:
            errs.append("decision-entropy failed")
    else:
        errs.append("missing decision-entropy.yaml")

    dag = ws / "planning" / "implementation-dag.yaml"
    plan = ws / "planning" / "implementation-plan.yaml"
    r = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "validate-dag.py"),
            "--dag",
            str(dag),
            "--plan",
            str(plan),
        ],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        errs.append(f"dag invalid: {r.stdout.strip()}")

    for req in [
        "final/PLAYBOOK.md",
        "final/DAG.yaml",
        "final/HANDOFF.md",
        "final/PACKAGE_MANIFEST.yaml",
    ]:
        if not (ws / req).is_file():
            errs.append(f"missing {req}")

    r = subprocess.run(
        [sys.executable, str(SCRIPTS / "validate-replit-bar.py"), "--workspace", str(ws)],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        errs.append(f"replit-bar: {r.stdout.strip()}")

    r = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "validate-execution-depth.py"),
            "--workspace",
            str(ws),
        ],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        errs.append(f"execution-depth: {r.stdout.strip()}")

    r = subprocess.run(
        [
            sys.executable,
            str(SCRIPTS / "validate-playbook.py"),
            "--workspace",
            str(ws),
        ],
        capture_output=True,
        text=True,
    )
    if r.returncode != 0:
        errs.append(f"playbook-format: {r.stdout.strip()}")

    if errs:
        for e in errs:
            print(f"FAIL {e}")
        return 1
    print("PASS handoff")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
