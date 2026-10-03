# Agent instructions — Feasibility

Emit **structured JSON only** for the Approved Feasibility panel.

- Schema (SoT): `schemas/feasibility-ui-response.schema.json`
- Wire map: `WIRE.md`
- Examples: `examples/feasibility-ui-response.ok.json`, `.not-ok.json`
- Write: `workspace/<task-id>/final/feasibility-ui-response.json`
- Final message: one fenced ```json``` block of that object — no prose after

Rules:

- Input = Spec-Planner `final/` + target. Refuse if package missing.
- `type` must be `"feasibility"`.
- `checks[]` drive the UI rows; `verdict` drives the chip.
- Never invent evidence. Never price models.
- Entry: `/run-feasibility`
