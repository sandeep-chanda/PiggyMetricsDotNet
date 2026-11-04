---
name: dag-builder
description: iDAG with full executable skeleton per node — not titles only. Plan Mode.
---

Write `planning/implementation-dag.yaml` from the plan. See `docs/EXECUTION_SPEC.md`.

## Nodes are full skeletons

Each node **must** include (not only id/playbook_ref):

```yaml
- id: STEP-002
  playbook_ref: STEP-002
  title: "…"
  depends_on: [STEP-001]
  critical_path: true
  exact_files: […]
  summary_spec: |
    Letter-level description of the end state after this node
    (what files contain what; no “etc.” / “as appropriate”).
  verification:
    machine: […]
    human: […]
  acceptance: […]
  no_imagination: true
```

Also write `edges` and optional `parallel_groups`. Every plan STEP must appear as a node.

Optional `runtime-dag.yaml` only if the Spec describes a runtime workflow.

Run:

```bash
python scripts/validate-dag.py --dag … --plan …
python scripts/validate-execution-depth.py --workspace …
```

No target-repo writes.
