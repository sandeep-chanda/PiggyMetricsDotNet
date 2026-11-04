#!/usr/bin/env python3
"""Enforce letter-to-letter execution depth on plan + iDAG (no outline / imagination)."""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    print("pip install -r requirements.txt", file=sys.stderr)
    raise SystemExit(2)

OUTLINE_PHRASES = re.compile(
    r"\b(make .+ (honest|better|accessible)|add (a )?skip|update (the )?ui|"
    r"as appropriate|best practice|similar to|figure out|implement properly)\b",
    re.I,
)


def _has_final_artifact(step: dict) -> bool:
    return bool(
        step.get("exact_snippet")
        or step.get("exact_markup")
        or step.get("exact_commands")
        or step.get("exact_branch")
    )


def _is_product_edit(step: dict) -> bool:
    files = step.get("exact_files") or step.get("files") or []
    joined = " ".join(str(f) for f in files).lower()
    if any(x in joined for x in (".git",)):
        return False
    title = (step.get("title") or "").lower()
    if "branch" in title and "create" in title:
        return False
    if step.get("exact_commands") and not any(
        str(f).endswith(ext)
        for f in files
        for ext in (
            ".js",
            ".jsx",
            ".ts",
            ".tsx",
            ".py",
            ".css",
            ".scss",
            ".html",
            ".vue",
            ".go",
            ".rs",
            ".java",
            ".kt",
            ".cs",
            ".json",
            ".yml",
            ".yaml",
            ".md",
            ".sql",
        )
    ):
        # verify-only / git-only
        if not step.get("exact_snippet") and not step.get("exact_markup"):
            return False
    return bool(files)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--workspace", required=True)
    args = ap.parse_args()
    ws = Path(args.workspace)
    errs: list[str] = []

    plan_path = ws / "planning" / "implementation-plan.yaml"
    dag_path = ws / "planning" / "implementation-dag.yaml"
    if not plan_path.is_file():
        errs.append("missing planning/implementation-plan.yaml")
    if not dag_path.is_file():
        errs.append("missing planning/implementation-dag.yaml")
    if errs:
        for e in errs:
            print(f"FAIL {e}")
        return 1

    plan = yaml.safe_load(plan_path.read_text(encoding="utf-8")) or {}
    dag = yaml.safe_load(dag_path.read_text(encoding="utf-8")) or {}
    steps = plan.get("steps") or []
    if len(steps) < 1:
        errs.append("plan has no steps")

    for s in steps:
        sid = s.get("id", "?")
        change = s.get("change") or ""
        if len(change.strip()) < 40:
            errs.append(f"{sid} change too thin (<40 chars) — outline rejected")
        if OUTLINE_PHRASES.search(change) and not _has_final_artifact(s):
            errs.append(f"{sid} outline-like change without final artifact pins")
        if not s.get("exact_files") and not s.get("files"):
            errs.append(f"{sid} missing exact_files/files")
        if not s.get("acceptance"):
            errs.append(f"{sid} missing acceptance")
        if not s.get("do_not"):
            errs.append(f"{sid} missing do_not")
        if _is_product_edit(s):
            if not (s.get("exact_snippet") or s.get("exact_markup")):
                errs.append(f"{sid} product edit missing exact_snippet/exact_markup")
            if not s.get("existing_implementation"):
                errs.append(f"{sid} product edit missing existing_implementation cite")
            if s.get("creates_file") is not True and not (
                s.get("before_snippet") or s.get("line_anchor")
            ):
                errs.append(
                    f"{sid} product edit needs before_snippet or line_anchor (or creates_file:true)"
                )
            if "companion_required" not in s and "css_required" not in s:
                # allow if no UI copy — still require explicit false when snippet present
                if s.get("exact_copy") or s.get("exact_aria"):
                    errs.append(f"{sid} missing companion_required/css_required")
            if not (s.get("machine_verify") or s.get("exact_commands") or s.get("tests")):
                errs.append(f"{sid} missing machine_verify/exact_commands/tests")
            if not (s.get("human_verify") or s.get("acceptance")):
                errs.append(f"{sid} missing human_verify/acceptance")
            if not s.get("invariants"):
                errs.append(f"{sid} product edit missing invariants[]")

    nodes = dag.get("nodes") or []
    for n in nodes:
        nid = n.get("id", "?")
        for req in (
            "playbook_ref",
            "title",
            "summary_spec",
            "exact_files",
            "verification",
            "acceptance",
            "no_imagination",
        ):
            if req not in n:
                errs.append(f"DAG node {nid} missing {req}")
        if n.get("no_imagination") is not True:
            errs.append(f"DAG node {nid} no_imagination must be true")
        summary = n.get("summary_spec") or ""
        if len(summary.strip()) < 40:
            errs.append(f"DAG node {nid} summary_spec too thin")
        ver = n.get("verification") or {}
        if not isinstance(ver, dict) or not (ver.get("machine") or ver.get("human")):
            errs.append(f"DAG node {nid} verification needs machine and/or human lists")
        acc = n.get("acceptance") or []
        if not acc:
            errs.append(f"DAG node {nid} acceptance empty")

    if errs:
        for e in errs:
            print(f"FAIL {e}")
        return 1
    print("PASS execution-depth")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
