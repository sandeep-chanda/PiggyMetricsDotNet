# Feasibility → UI / DB wire

Agent **must** emit a single JSON object matching `schemas/feasibility-ui-response.schema.json`.

Primary artifact: `workspace/<task-id>/final/feasibility-ui-response.json`  
Also print the same JSON as the final assistant message (fenced `json` block only — no prose after).

## UI panel map (`Feasibility · {specId}`)

| UI | JSON field |
|----|------------|
| Title id | `spec_id` |
| Subtitle `returned by X · Y` | `returned_by.agent_label` · `returned_by.region` |
| Chip | `verdict` (`ok` \| `not ok`) |
| Note | `note` |
| Row label | `checks[].label` |
| Row chip | `checks[].verdict` (`pass`\|`fail`\|`warn`\|`—`) |
| Row detail | `checks[].detail` |

## DB map

| Table | Columns ← JSON |
|-------|----------------|
| `rhm_spec_feasibility_probes` | `verdict`, `note`, `recommended_combo_key`, account/region fields from `returned_by` |
| `rhm_spec_feasibility_probe_checks` | `sort_order`, `label`, `verdict`, `detail` |

Examples: `examples/feasibility-ui-response.ok.json`, `.not-ok.json`.
