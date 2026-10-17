---
name: quality-gate
description: Quality Gate — 10/10 + letter-to-letter + native Cursor Plan PLAYBOOK via playbook-importer.
---

Evaluate `docs/QUALITY_BAR.md`, `docs/EXECUTION_SPEC.md`, `docs/PROTOCOL.md`, `docs/PLAYBOOK_FORMAT.md`.

## Required before PASS

- Critic PASS including **imagination_gap** and **thin_dag** clean  
- Handoff simulation: cold implementer needs **zero design questions**  
- Scorecard every axis == 10  
- Validators: `validate-dag`, `validate-entropy`, `validate-replit-bar`, `validate-execution-depth`, `validate-playbook`, `check-handoff`
- **`final/PLAYBOOK.import.json` present** (proves `playbook-importer` / `import-cursor-plan.py` ran)

## Checks

Prior checks plus:

- `letter_to_letter_plan: true`
- `idag_full_skeleton: true`
- `zero_coding_agent_imagination: true`
- `execution_depth_validated: true`
- `playbook_cursor_plan_parity: true` — PLAYBOOK imported from **native Cursor Plan Mode** via **playbook-importer**

## PLAYBOOK before APPROVED

Do **not** run `render-playbook.py`. Do **not** hand-author a thinner PLAYBOOK.

**Constructive:** Plan Mode plan must still include the **same STEPs** as todos + rich body (pins/verify/files). Format improve only.

1. Parent: SwitchMode → `plan` (todos ≈ STEPs; keep depth)
2. Parent: Create the Cursor plan (native)
3. Parent: SwitchMode → `agent`
4. Parent: **invoke `playbook-importer`** (mandatory) — copies plan → `final/PLAYBOOK.md`

```bash
python scripts/import-cursor-plan.py --workspace workspace/<task-id> --plan <path-to-.plan.md>
python scripts/validate-playbook.py --workspace workspace/<task-id>
```

5. FAIL if import meta missing **or** residual STEPs from YAML are absent from PLAYBOOK todos/body  
6. FAIL if PLAYBOOK/HANDOFF still have loose (unfenced) source **or** leftover ``` fences — pins must match `docs/PLAYBOOK_FORMAT.md` (`~~~<lang>` placeholder)

Only PASS → APPROVED `final/`.
