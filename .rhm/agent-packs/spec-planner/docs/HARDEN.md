# Harden notes

- Plan Mode + workspace-only writes; hooks block target mutations.
- **Stack-agnostic 10/10** (`docs/QUALITY_BAR.md`): discover tech; final-artifact pins; reference-trace; constraint-risk; no instructional pins.
- Critic vectors include soft_shape_variance, instructional_pin, reference_trace, constraint_risk.
- `validate-replit-bar.py` enforces scorecard all 10, soft phrases, instructional pin patterns, companion/snippet rules without requiring a specific language (no JSX-only gate).
- Gap loop must root-cause re-research — never soft re-pin or inject repo-specific “recommended” answers into the agent.
- Question bank seeded in `init-task.py`; migration N/A needs justification.
- Tools: `docs/TOOLS.md` Cursor-first allowlists; **provider-routed** discovery (ADO/GitHub/GitLab/…); `.cursor/mcp.json` is an optional catalog — auth only the provider for this job.
- Execution: `docs/EXECUTION_SPEC.md` — plan + iDAG letter-to-letter; `validate-execution-depth.py`; critic vectors imagination_gap + thin_dag.
- PLAYBOOK: `docs/PLAYBOOK_FORMAT.md` — SwitchMode→plan (todos ≈ STEPs, **keep prior depth**, **pins in the PLAYBOOK_FORMAT placeholder**) → SwitchMode→agent → **`playbook-importer`** copies → `final/PLAYBOOK.md`. Constructive format improve only — never remove STEPs/pins.
