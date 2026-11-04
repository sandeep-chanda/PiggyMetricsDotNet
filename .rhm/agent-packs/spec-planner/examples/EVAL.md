# Golden eval rubric

## Setup

1. Open `spec-planner` in Cursor **Plan Mode**.  
2. `/generate-plan` with:
   - Spec: `examples/specs/sample-feature.md`
   - Target: `examples/target-repo`
3. Or bootstrap:  
   `python scripts/init-task.py --spec examples/specs/sample-feature.md --target examples/target-repo`

## Must hold (live workspace after `/generate-plan`)

- No files under `examples/target-repo` modified by the planner  
- Workspace contains research findings citing `src/pipeline.py` IncrementalPipeline  
- Decision chooses reuse of IncrementalPipeline  
- Playbook STEPs are letter-to-letter (DO NOT / tests / acceptance / pins)  
- iDAG full-skeleton, acyclic, mapped  
- Critic + quality gate PASS including `plan_mode_no_target_writes`  
- `final/PLAYBOOK.import.json` present (playbook-importer / `import-cursor-plan.py`)  
- `scripts/validate-playbook.py`, `validate-execution-depth.py`, and  
  `scripts/check-handoff.py --workspace workspace/<id>` all PASS  

## Golden package note

`examples/golden-package/` is a **structure reference** for folder layout and sample STEPs.  
It is **not** a validator-green fixture: it may lack `PLAYBOOK.import.json`, Cursor plan  
frontmatter, and full letter-to-letter / iDAG pins that current scripts require.  
Do not claim `check-handoff.py` PASS against golden-package as-is — eval the live  
`workspace/<id>/` from Setup instead.

## Handoff test

New Agent session (implementation allowed): give only spec + `final/` + target path.  
Architectural questions ⇒ planner FAIL.
