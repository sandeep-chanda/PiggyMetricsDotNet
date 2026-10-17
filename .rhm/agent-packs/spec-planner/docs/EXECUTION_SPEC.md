# Execution Spec — letter-to-letter (no imagination)

## Purpose of this planner

The outcome package is **not** a sketch for a coding agent to “figure out.”

It must be a **complete execution specification** so that:

- a **foreign coding agent**, or  
- a **competent human**, or  
- a **traditional implementer following steps literally**  

can implement the **original Spec to perfection** **without inventing** architecture, markup, APIs, copy, file layout, commands, or edge cases.

If the consumer must use imagination → **planner FAIL**. That negates the product.

## What “in-depth” means

Borderline / outline STEPs are **rejected**. Every product-changing STEP must include:

| Layer | Required content |
|-------|------------------|
| **Why** | Spec requirement + Decision refs |
| **Where** | Exact paths; line/region anchors from repo evidence |
| **Before** | Cite current implementation (`existing_implementation` + optional `before_snippet`) |
| **After** | **Final** `exact_snippet` / companion — complete post-change text for the touched region. In PLAYBOOK/HANDOFF that text uses the `~~~<lang>` placeholder in `docs/PLAYBOOK_FORMAT.md` |
| **How (machine)** | Ordered `exact_commands` that a shell can run; proven in this repo |
| **How (human)** | Observable checks that need no judgment (“button shows text X”) |
| **Do not** | Explicit bans (wrong files, scope creep, invented APIs) |
| **Verify** | Tests + acceptance that map 1:1 to Spec clauses for this step |
| **Rollback** | Exact revert path |
| **Trace** | REQ / DEC / R refs |

Optional but preferred when ambiguity would appear: `input_contract`, `output_contract`, `invariants`, `edge_cases[]`.

## iDAG = full skeleton, not a title graph

`planning/implementation-dag.yaml` / `final/DAG.yaml` must carry an **executable skeleton** per node — not only `id` + `playbook_ref`.

Each node includes at least:

- `id`, `playbook_ref`, `title`  
- `depends_on` (or edges equivalent)  
- `critical_path`  
- `exact_files`  
- `summary_spec` — one paragraph: what exists when this node is done (letter-level)  
- `verification` — commands and/or observable checks  
- `acceptance` — copy of plan acceptance (or refs)  
- `no_imagination: true`  

Edges define order. The node payload must be enough that a reader never asks “what do I write here?”

## Forbidden outcomes

- “Update the work cards to be more honest” without final markup/CSS  
- “Add skip link” without exact element, target id, styles, focus behavior  
- “Follow best practices” / “or equivalent” / dual shapes  
- iDAG that is only boxes and titles  
- Handing research prose to the coding agent and hoping they design  

## Handoff test (strengthened)

> Given **only** `final/PLAYBOOK.md` + `final/DAG.yaml` + TargetRepo (read) + raw Spec:  
> Can a cold implementer produce the Spec-compliant change **without asking a design question** and **without inventing any artifact text**?

- Any design question → FAIL  
- Any invented file/content not pinned → FAIL  
- Spec clause for this step unmapped to a STEP/acceptance → FAIL  

## PLAYBOOK.md (native Cursor Plan Mode)

`final/PLAYBOOK.md` **is** a Cursor Plan Mode `.plan.md` — see **`docs/PLAYBOOK_FORMAT.md`**.

**Constructive improve only:** same STEPs / pins / verify as before; native Cursor todos + body shape; then **playbook-importer** copies → `final/PLAYBOOK.md`. Do **not** remove step depth.

Flow (mandatory): SwitchMode → **plan** (todos ≈ STEPs, body keeps letter-to-letter depth, **pins in the PLAYBOOK_FORMAT placeholder**) → SwitchMode → **agent** → **invoke `playbook-importer`** → light adjust only inside importer (no thinning).

Do **not** invent PLAYBOOK with `render-playbook.py`. Do **not** skip the importer. Do **not** drop STEPs “for adaptivity.”

## Validator

`scripts/validate-execution-depth.py` enforces minimum depth on plan + iDAG.  
`scripts/validate-playbook.py` enforces native Cursor-plan shape (not a YAML dump).
