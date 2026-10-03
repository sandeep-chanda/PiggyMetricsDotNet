#!/usr/bin/env python3
"""Validate PLAYBOOK.md is a native Cursor Plan Mode document (imported)."""
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


def _split_frontmatter(text: str) -> tuple[dict | None, str]:
    if not text.startswith("---"):
        return None, text
    end = text.find("\n---", 3)
    if end < 0:
        return None, text
    try:
        fm = yaml.safe_load(text[3:end].strip()) or {}
    except Exception:
        return None, text
    body = text[end + 4 :].lstrip("\n")
    return fm if isinstance(fm, dict) else None, body


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--workspace", required=True)
    args = ap.parse_args()
    ws = Path(args.workspace)
    errs: list[str] = []

    playbook = ws / "final" / "PLAYBOOK.md"
    import_meta = ws / "final" / "PLAYBOOK.import.json"
    if not playbook.is_file():
        print("FAIL missing final/PLAYBOOK.md")
        return 1
    if not import_meta.is_file():
        errs.append(
            "missing final/PLAYBOOK.import.json — run playbook-importer / import-cursor-plan.py after Plan Mode"
        )

    text = playbook.read_text(encoding="utf-8")
    fm, body = _split_frontmatter(text)

    if fm is None:
        errs.append("missing YAML frontmatter — import a real Cursor .plan.md")
    else:
        if not fm.get("name"):
            errs.append("frontmatter missing name")
        if not fm.get("overview"):
            errs.append("frontmatter missing overview")
        todos = fm.get("todos")
        if not isinstance(todos, list) or not todos:
            errs.append("frontmatter todos must be a non-empty list")
        else:
            for i, t in enumerate(todos):
                if not isinstance(t, dict):
                    errs.append(f"todo[{i}] not a mapping")
                    continue
                if not t.get("id"):
                    errs.append(f"todo[{i}] missing id")
                if not t.get("content"):
                    errs.append(f"todo {t.get('id')} missing content")

    # Reject mechanical YAML-dump playbooks (old renderer)
    for pat, label in (
        (r"\*\*Todo id:\*\*", "mechanical Todo id dump"),
        (r"^## Todo -> Step map", "forced Todo->Step map template"),
        (r"### Change \(narrative", "mechanical narrative subsection"),
        (r"^name: Playbook ", "mechanical Playbook <task-id> name in body/frontmatter"),
    ):
        if re.search(pat, text, re.M):
            errs.append(f"not a native Cursor plan: {label} — re-import from Plan Mode")

    if fm and str(fm.get("name") or "").startswith("Playbook "):
        errs.append("name looks mechanical ('Playbook …') — use Cursor Plan Mode title")

    if not re.search(r"^#\s+\S", body, re.M):
        errs.append("body missing H1")
    if len(re.findall(r"^##\s+\S", body, re.M)) < 2:
        errs.append("body needs situation-shaped H2 sections (Cursor plan style)")
    if len(body) < 800:
        errs.append(f"body too thin ({len(body)} chars)")

    if errs:
        for e in errs:
            print("FAIL " + e.encode("ascii", "replace").decode("ascii"))
        return 1
    print("PASS playbook-format")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
