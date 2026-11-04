#!/usr/bin/env python3
"""Validate iDAG: acyclic, nodes map to playbook steps, no orphan depends."""
from __future__ import annotations

import argparse
import sys
from collections import defaultdict, deque
from pathlib import Path

try:
    import yaml
except ImportError:
    print("Install deps: pip install -r requirements.txt", file=sys.stderr)
    raise SystemExit(2)


def has_cycle(nodes: list[str], edges: list[tuple[str, str]]) -> bool:
    adj: dict[str, list[str]] = defaultdict(list)
    indeg = {n: 0 for n in nodes}
    for a, b in edges:
        adj[a].append(b)
        indeg[b] = indeg.get(b, 0) + 1
        indeg.setdefault(a, indeg.get(a, 0))
    q = deque([n for n, d in indeg.items() if d == 0])
    seen = 0
    while q:
        u = q.popleft()
        seen += 1
        for v in adj[u]:
            indeg[v] -= 1
            if indeg[v] == 0:
                q.append(v)
    return seen != len(indeg)


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--dag", required=True)
    ap.add_argument("--plan", default="", help="Optional implementation-plan.yaml for step mapping")
    args = ap.parse_args()
    dag = yaml.safe_load(Path(args.dag).read_text(encoding="utf-8")) or {}
    nodes = [n["id"] for n in dag.get("nodes") or []]
    refs = {n["id"]: n.get("playbook_ref") for n in dag.get("nodes") or []}
    edges = [(e["from"], e["to"]) for e in dag.get("edges") or []]
    errs = []
    if not nodes:
        errs.append("no nodes")
    if has_cycle(nodes, edges):
        errs.append("cycle detected")
    for a, b in edges:
        if a not in nodes or b not in nodes:
            errs.append(f"edge {a}->{b} references missing node")
    if args.plan:
        plan = yaml.safe_load(Path(args.plan).read_text(encoding="utf-8")) or {}
        step_ids = {s["id"] for s in plan.get("steps") or []}
        for nid, pref in refs.items():
            if pref and pref not in step_ids and pref not in nodes:
                errs.append(f"node {nid} playbook_ref {pref} not in plan steps")
        for sid in step_ids:
            if sid not in nodes and sid not in refs.values():
                errs.append(f"plan step {sid} missing from DAG")
    if errs:
        for e in errs:
            print(f"FAIL {e}")
        return 1
    print(f"PASS dag {args.dag}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
