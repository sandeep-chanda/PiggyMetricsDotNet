# Agent instructions — Spec Planner

Plan Mode Spec → Playbook + iDAG for **any Spec + any target repo + any stack**.

- **Never** write production code into the target repository.  
- Writes only under `workspace/<task-id>/`.  
- **Discover** technology from the target tree — never assume a stack; never hardcode repo-specific fixes into agents.  
- Entry: `/generate-plan` — `.cursor/skills/generate-plan/SKILL.md`  
- After Plan Mode for PLAYBOOK: **must** invoke `.cursor/agents/playbook-importer.md` (copies native `.plan.md` → `final/PLAYBOOK.md`)  
- Outcome markdown (PLAYBOOK/HANDOFF): pin placeholder in `docs/PLAYBOOK_FORMAT.md` — labeled `~~~<lang>` fences; never a loose dump  
- Quality: `docs/QUALITY_BAR.md` + `docs/EXECUTION_SPEC.md` (letter-to-letter plan/iDAG; no coding-agent imagination)  
- Docs: `docs/PROTOCOL.md`, `docs/RESEARCH.md`, `docs/HANDOFF.md`, `docs/TOOLS.md`, `docs/PLAYBOOK_FORMAT.md`  
