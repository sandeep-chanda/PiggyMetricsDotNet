---
description: Emit feasibility-ui-response.json for Approved Feasibility panel.
---

# Run feasibility

## Output (required)

1. Validate against `schemas/feasibility-ui-response.schema.json`.
2. Write `workspace/<task-id>/final/feasibility-ui-response.json`.
3. Reply with **only** that JSON in a ```json``` fence.

## Shape (UI)

```json
{
  "type": "feasibility",
  "spec_id": "…",
  "verdict": "ok",
  "note": "…",
  "returned_by": { "agent_label": "…", "region": "…" },
  "recommended_combo_key": "A",
  "checks": [
    { "sort_order": 0, "label": "repo graph", "verdict": "pass", "detail": "…" }
  ],
  "source_package": { "playbook_ref": "…", "idag_ref": "…" }
}
```

## Prerequisites

Spec-Planner `final/` present. Do not start Model combination.

See `WIRE.md` for UI/DB field map. Copy field names exactly — host upserts into `rhm_spec_feasibility_probes` / `_checks`.
