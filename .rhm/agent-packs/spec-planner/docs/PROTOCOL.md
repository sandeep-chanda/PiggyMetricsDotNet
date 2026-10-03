# Agent Protocol

## Quality target

**Replit-grade 10/10** (`docs/QUALITY_BAR.md`) for **any Spec + any target repo + any stack**. Discover technology from the tree — never assume React, CSS, npm, JSX, Python, Go, etc. Never hardcode product- or repo-specific answers into agents.

**Tools/MCP:** Cursor-first; per-agent allowlists + optional discovery MCP — full contract in `docs/TOOLS.md`.

## Operating mode (mandatory)

**Cursor Plan Mode only.** Artifact production only — never implement the Spec in the target repo.

| Allowed | Forbidden |
|---------|-----------|
| Read/search target repo | Write/edit/delete under target repo |
| Write under `workspace/<task-id>/` | Shell mutations that change target repo |
| Run validators on workspace artifacts | APPROVED without critic + quality gate PASS |

## Canonical I/O

- **Input:** Spec + TargetRepo + **connection meta** (provider, sibling repos under same project/org root, auth refs) + optional local related paths — see `docs/CONNECTION.md`  
- **Output:** Playbook + iDAG (+ research, decisions, gates)  

Cross-repo = siblings in that SCM project (ADO project filter / GitHub org / …), not unrelated hosts.

## Dual DAG

- **iDAG (required):** playbook step dependencies  
- **Runtime DAG (optional):** only if the Spec describes a runtime workflow  

## State machine

`intake` → `spec_analysis` → `research_parallel` → `research_synthesis` → `architecture` → `migration` (or N/A) → `planning` → `dag_build` → `critic` → `gap_loop` (as needed) → `quality_gate` → `package` → `approved`

Loop budget: max **3** critic cycles. Then `blocked` — never fake PASS. Gap loop must **root-cause** (re-research), not soft re-pin.

**Before APPROVED also require:**

- `review/critic-attack-log.yaml` (incl. soft_shape, instructional_pin, reference_trace, constraint_risk)
- `review/handoff-simulation.yaml` (critical unanswered = FAIL; covers exact **artifact** shape)
- `review/quality-scorecard.yaml` (every axis == 10)
- Final-artifact pins on PlanSteps (`docs/QUALITY_BAR.md`)

## Ambiguity rule

Never silently invent important decisions. Investigate first — tools/MCP when needed. Ask user only if unknowable.

## Agent roster

### Orchestrator

- **In:** Spec path, target repo path(s)  
- **Out:** `state.yaml`, final package  
- **Must not:** replace specialists; write target repo; inject repo-specific “recommended fixes”  

### Spec Analyst

- **Out:** `spec/normalized.yaml`, `open-questions.yaml`, `assumptions.yaml`  
- Challenge Spec against target when paths/scripts are cited  

### Environment Analyst

- **Out:** `research/environment.yaml`  
- **PASS:** stack discovered with evidence from **this** tree’s manifests  
- **FAIL:** claimed env without inspection  

### Repository Researcher

- **Out:** `research/repository.yaml` + findings  
- Precedents = constraints; **reference-trace** renames; **constraint-risk** for copy/size changes  

### Cross-Repo Researcher

- Resolve SCM provider from connection.meta / remote (**provider-routed** — not always GitHub)  
- Discover **sibling repos** under the same ADO project / GitHub org / GitLab group when that provider’s MCP is available  
- Known `--related` roots preferred for deep file evidence  
- **Out:** `research/cross-repo.yaml`  
- **FAIL:** inventing APIs; writing prod trees; demanding unrelated provider auth  

### External / Web Researcher

- Cursor WebSearch/WebFetch/browser first (`docs/TOOLS.md`)  
- **Out:** `research/external.yaml`  

### Synthesizer / Architect / Migration

Stack-agnostic; evidence-backed; tool classes per allowlist in `docs/TOOLS.md`.

### Implementation Planner

- **Out:** `planning/implementation-plan.yaml`, `test-plan.yaml`, `risks.yaml`  
- **Depth:** letter-to-letter STEPs per `docs/EXECUTION_SPEC.md` (before/after pins, invariants, edge_cases, machine/human verify)  
- **Pin:** final text only — never outlines for the coding agent to invent  
- **FAIL:** thin/outline STEPs; soft phrases; instructional pins; dual shapes  

### DAG Builder

- **Out:** `planning/implementation-dag.yaml` with **full skeleton** per node (`summary_spec`, `verification`, `acceptance`, `exact_files`, `no_imagination: true`)  
- **FAIL:** title-only nodes; missing plan STEPs; cycles  

### Critic / Red Team

- Includes **imagination_gap** and **thin_dag** — coding-agent invention or outline iDAG → FAIL → Gap Loop  

### Gap / Research Loop

- Root-cause re-research; do not soft-patch; do not hardcode product answers  

### Playbook Importer

- **When:** immediately after parent SwitchMode Plan → Agent for PLAYBOOK authoring  
- **Out:** `final/PLAYBOOK.md` + `final/PLAYBOOK.import.json` via `scripts/import-cursor-plan.py`  
- **Must:** `validate-playbook` PASS; never `render-playbook.py`; never invent body; PLAYBOOK/HANDOFF pins match `docs/PLAYBOOK_FORMAT.md` (no loose source)  
- **Must not:** SwitchMode itself; mark APPROVED  

### Quality Gate

- Scorecard every axis == 10; letter-to-letter plan; full-skeleton iDAG; zero coding-agent imagination; execution_depth_validated; plan_mode_no_target_writes; **playbook-importer** completed (`PLAYBOOK.import.json`)  

Only PASS → APPROVED `final/`.

## Traceability

`SPEC → REQUIREMENT → RESEARCH_QUESTION → EVIDENCE → FINDING → DECISION → PLAN_STEP → DAG_NODE → TEST → ACCEPTANCE`

## Conflict policy

Repo reality wins for “how we do it here.” Security/correctness vs external practice → Decision Record; never silent override.
