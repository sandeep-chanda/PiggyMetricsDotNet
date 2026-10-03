#!/usr/bin/env python3
"""Validate Replit-grade 10/10: stack-agnostic soft/instructional pin checks."""
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

REQUIRED_VECTORS = {
    "wrong_paths",
    "missing_scripts",
    "scope_creep",
    "weak_evidence",
    "missing_alternatives",
    "dag_order",
    "residual_entropy",
    "a11y_or_security_or_ops",
    "test_gaps",
    "spec_plan_contradiction",
    "soft_shape_variance",
    "instructional_pin",
    "reference_trace",
    "constraint_risk",
    "imagination_gap",
    "thin_dag",
}

SOFT_PATTERNS = [
    re.compile(r"\be\.g\.\b", re.I),
    re.compile(r"\bfor example\b", re.I),
    re.compile(r"\band/or\b", re.I),
    re.compile(r"\bor equivalent\b", re.I),
    re.compile(r"\bif needed\b", re.I),
    re.compile(r"\bif necessary\b", re.I),
    re.compile(r"\bor platform\b", re.I),
    re.compile(r"\beither\b.+\bor\b", re.I | re.S),
]

# Edit recipes in pins — language-agnostic FAIL
INSTRUCTIONAL_PATTERNS = [
    re.compile(r"\breplace\b.+\bwith\b", re.I),
    re.compile(r"\brename\b.+\bto\b", re.I),
    re.compile(r"\bdelete these\b", re.I),
    re.compile(r"\bport (the |old |existing )", re.I),
    re.compile(r"\bupdate the\b", re.I),
    re.compile(r"\bsee above\b", re.I),
    re.compile(r"\bsame as before\b", re.I),
]


def _text_blobs(step: dict) -> list[tuple[str, str]]:
    out: list[tuple[str, str]] = []
    for key in (
        "title",
        "change",
        "why",
        "expected_result",
        "compatibility",
        "error_handling",
        "rollback",
    ):
        val = step.get(key)
        if isinstance(val, str) and val.strip():
            out.append((key, val))
    for key in (
        "exact_copy",
        "exact_aria",
        "exact_markup",
        "exact_snippet",
        "exact_css",
        "exact_companion",
        "exact_branch",
    ):
        val = step.get(key)
        if isinstance(val, str) and val.strip():
            out.append((key, val))
        elif isinstance(val, dict):
            out.append((key, yaml.safe_dump(val)))
    return out


def _has_primary_snippet(step: dict) -> bool:
    return bool(step.get("exact_snippet") or step.get("exact_markup"))


def _companion_required(step: dict) -> bool | None:
    if "companion_required" in step:
        return bool(step.get("companion_required"))
    if "css_required" in step:
        return bool(step.get("css_required"))
    return None


def _companion_text(step: dict) -> str:
    return str(step.get("exact_companion") or step.get("exact_css") or "")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--workspace", required=True)
    args = ap.parse_args()
    ws = Path(args.workspace)
    errs: list[str] = []

    attack_path = ws / "review" / "critic-attack-log.yaml"
    if not attack_path.is_file():
        errs.append("missing review/critic-attack-log.yaml")
    else:
        attack = yaml.safe_load(attack_path.read_text(encoding="utf-8")) or {}
        vectors = attack.get("vectors") or []
        names = {v.get("vector") for v in vectors if isinstance(v, dict)}
        missing = REQUIRED_VECTORS - names
        if missing:
            errs.append(f"attack log missing vectors: {sorted(missing)}")
        if len(vectors) < 8:
            errs.append(f"attack log needs ≥8 vectors, got {len(vectors)}")
        for v in vectors:
            if not v.get("checked"):
                errs.append(f"vector not checked: {v.get('vector')}")
            if not v.get("evidence"):
                errs.append(f"vector missing evidence: {v.get('vector')}")
            if v.get("vector") in {
                "soft_shape_variance",
                "instructional_pin",
                "reference_trace",
                "constraint_risk",
                "imagination_gap",
                "thin_dag",
            }:
                if (v.get("finding") or "").lower() == "issue":
                    errs.append(f"{v.get('vector')} still finding=issue — Gap Loop incomplete")

    sim_path = ws / "review" / "handoff-simulation.yaml"
    if not sim_path.is_file():
        errs.append("missing review/handoff-simulation.yaml")
    else:
        sim = yaml.safe_load(sim_path.read_text(encoding="utf-8")) or {}
        if sim.get("result") != "PASS":
            errs.append("handoff-simulation result not PASS")
        unanswered = sim.get("critical_unanswered") or []
        if unanswered:
            errs.append(f"handoff-simulation critical_unanswered={unanswered}")
        qa = sim.get("qa") or []
        joined = " ".join(
            f"{q.get('q', '')} {q.get('a', '')}" for q in qa if isinstance(q, dict)
        ).lower()
        # Language-agnostic: must discuss exact artifact/snippet/shape — not require jsx
        if not any(
            k in joined
            for k in (
                "exact_snippet",
                "exact_markup",
                "artifact",
                "snippet",
                "final",
                "markup",
                "companion",
            )
        ):
            errs.append(
                "handoff-simulation must cover exact artifact/snippet shape (stack-agnostic)"
            )

    score_path = ws / "review" / "quality-scorecard.yaml"
    if not score_path.is_file():
        errs.append("missing review/quality-scorecard.yaml")
    else:
        score = yaml.safe_load(score_path.read_text(encoding="utf-8")) or {}
        axes = score.get("axes") or {}
        if not axes:
            errs.append("quality-scorecard has no axes")
        for name, val in axes.items():
            try:
                if float(val) < 10:
                    errs.append(f"scorecard {name}={val} < 10 (10/10 hard gate)")
            except (TypeError, ValueError):
                errs.append(f"scorecard {name} not numeric")

    plan_path = ws / "planning" / "implementation-plan.yaml"
    if plan_path.is_file():
        plan = yaml.safe_load(plan_path.read_text(encoding="utf-8")) or {}
        steps = plan.get("steps") or []
        pinned = False
        for s in steps:
            sid = s.get("id", "?")
            if (
                s.get("exact_copy")
                or s.get("exact_aria")
                or s.get("exact_commands")
                or s.get("exact_files")
                or _has_primary_snippet(s)
            ):
                pinned = True
            for field, text in _text_blobs(s):
                for pat in SOFT_PATTERNS:
                    if pat.search(text):
                        errs.append(f"{sid}.{field} soft-shape phrase matched /{pat.pattern}/")
                # Instructional patterns only FAIL inside exact_* pins (change prose may narrate)
                if field.startswith("exact_"):
                    for pat in INSTRUCTIONAL_PATTERNS:
                        if pat.search(text):
                            errs.append(
                                f"{sid}.{field} instructional pin matched /{pat.pattern}/ — pin final text only"
                            )
            if s.get("exact_copy") or s.get("exact_aria"):
                if not _has_primary_snippet(s):
                    errs.append(
                        f"{sid} has exact_copy/aria but missing exact_snippet or exact_markup"
                    )
                creq = _companion_required(s)
                if creq is None:
                    errs.append(
                        f"{sid} has exact_copy/aria but missing companion_required or css_required"
                    )
                elif creq is True:
                    comp = _companion_text(s)
                    if not comp.strip():
                        errs.append(
                            f"{sid} companion_required/css_required=true but missing exact_companion/exact_css"
                        )
                    elif comp.count("{") < 1 and ":" not in comp:
                        # very weak companion — likely prose
                        errs.append(f"{sid} companion pin looks empty of structured final text")
            title = (s.get("title") or "").lower()
            change = (s.get("change") or "").lower()
            cmds = " ".join(s.get("exact_commands") or [])
            if "checkout -b" in cmds or ("branch" in title and "create" in title):
                if not s.get("exact_branch"):
                    errs.append(f"{sid} branch step missing exact_branch")
            if "or platform" in change or "or platform" in title:
                errs.append(f"{sid} still contains 'or platform'")
        if steps and not pinned:
            errs.append("no PlanStep has pinning fields")
    else:
        errs.append("missing planning/implementation-plan.yaml")

    if errs:
        for e in errs:
            print(f"FAIL {e}")
        return 1
    print("PASS replit-bar")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
