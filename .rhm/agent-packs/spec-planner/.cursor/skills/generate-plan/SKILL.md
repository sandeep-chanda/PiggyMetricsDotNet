---
name: generate-plan
description: Spec→Playbook+iDAG. Stack-agnostic Replit-grade 10/10. PLAYBOOK via native Cursor Plan Mode + playbook-importer.
---

# /generate-plan

## Mode

Writes only under `workspace/<task-id>/`. Never modify TargetRepo.

Research / STEPs / iDAG / critic run in **Agent Mode**.  
**PLAYBOOK.md** is produced only via **native Cursor Plan Mode**, then **mandatory** `playbook-importer` subagent. Never invent PLAYBOOK with `render-playbook.py`.

## Inputs

1. Spec path  
2. TargetRepo path  
(+ optional related repos / connection info)

Bootstrap if needed:

```bash
python scripts/init-task.py --spec <SPEC> --target <TARGET> \
  --connection <connection.meta.json> \
  --related <local-sibling-path>   # repeatable
```

Connection meta must include provider + `sibling_repos` + auth refs (`docs/CONNECTION.md`, `templates/connection.meta.json`).

## Quality

Follow `docs/QUALITY_BAR.md`, `docs/EXECUTION_SPEC.md`, `docs/PROTOCOL.md`, `docs/TOOLS.md`, `docs/RESEARCH.md`, `docs/PLAYBOOK_FORMAT.md`.

- Any Spec / any repo / any stack — discover; never assume  
- Letter-to-letter STEPs + full-skeleton iDAG in YAML (**unchanged — do not remove**)  
- PLAYBOOK = real Cursor `.plan.md` that **still carries those STEPs as todos + rich body** (format improve only), then **playbook-importer** copies → `final/PLAYBOOK.md`  

## Phases

1. Spec Analyst  
2–6. Environment + Repo (+ Cross-repo if needed) + External  
7–8. Architect options + decisions  
9. Migration if triggered else N/A  
10. Planner — letter-to-letter STEPs (`docs/EXECUTION_SPEC.md`) — **same depth as before**  
11. DAG Builder — full skeleton nodes  
12. Critic + attack log (FAIL→Gap→rework; max 3)  
13. Handoff simulation  
14. Scorecard (all 10) + Quality Gate on YAML artifacts  
15. **PLAYBOOK via Cursor Plan Mode (mandatory — improve format, keep STEP depth):**

    **Constructive only:** do not drop STEPs, pins, verify, or coverage. Plan Mode presents the **same work** as a native Cursor plan (todos ≈ STEPs; body still letter-to-letter).

    **Why importer:** SwitchMode → Plan leaves Agent Mode. On return, **`playbook-importer` must run** so the `.plan.md` is copied to `final/PLAYBOOK.md`. Skip → package breaks.

    1. **SwitchMode → `plan`**
    2. Create the Cursor plan with:
       - **todos** covering every residual STEP (STEP ids in todo `content` or body)
       - **body** with the prior depth (files, exact artifacts, verify, do-not) in situation-shaped sections
       - **pins** written with the placeholder in `docs/PLAYBOOK_FORMAT.md`: `exact_snippet` / `exact_markup` / `exact_companion` as `~~~<lang>` fences; `exact_commands` as `~~~bash`; Exact copy/aria as one prose line. Never loose/unfenced source; never ``` fences.
       Ground in workspace research/decisions/STEPs — no imagination gaps. Note `~/.cursor/plans/<name>_<id>.plan.md`.
    3. **SwitchMode → `agent`**
    4. **Immediately invoke `playbook-importer`** with `workspace/<task-id>` and `--plan` from step 2.
    5. Never invent PLAYBOOK; never `render-playbook.py`; never thin STEPs during import.
    6. Importer PASS → full Quality Gate (`validate-playbook` + `check-handoff`)

## Exit

Print APPROVED/blocked, workspace path, validators, TargetRepo untouched, **playbook-importer** PASS, source `.plan.md` from `PLAYBOOK.import.json`, confirm **STEP coverage still present**, and PLAYBOOK/HANDOFF pins match `docs/PLAYBOOK_FORMAT.md` (no loose source).
