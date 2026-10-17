# Quality Bar — Replit-grade planning (10/10 hard gate)

Target: planning quality comparable to a strong Replit coding agent **before** any code — for **any Spec** + **any target repo** + **any stack**.

**APPROVE only when every scorecard axis is exactly 10.**  
Scores of 9 are **not** shippable. Soft pins are **FAIL**, not “good enough.”

Ground truth for Plan Mode: [Replit Plan mode](https://docs.replit.com/features/agent/plan-mode) — think first, structured tasks, no target writes until Build. Our bar adds **zero residual implementation-shape variance**.

## Non-negotiable: technology-agnostic

- **Discover** language, package manager, test/lint/build commands, UI toolkit (if any), SCM — from the target tree. Never assume React, CSS, npm, JSX, Python, etc.
- **Do not** bake product- or repo-specific fixes into the agent. The same protocol must work for a Go CLI, a mobile app, a Django service, or a Vite SPA.
- If an auditor can find a gap by reading the repo, the **Critic + research loop** must be able to find that same class of gap without being told the answer.

## Outcomes (what “10/10” means)

1. **Decision completeness** — foreign coding agent needs no architectural clarification  
2. **Repo-grounded** — cited paths/commands/patterns exist (or explicitly forbidden to invent)  
3. **Spec-challenged** — wrong paths, missing scripts, scope conflicts resolved via Decisions  
4. **Adversarial critic** — no rubber-stamp; attacks recorded; FAIL→FIX loop when warranted  
5. **Handoff simulation** — second-agent Q&A; critical unanswered = not done  
6. **Pinned micro-decisions** — one final artifact shape per change (see below)  
7. **Smart tools/MCP** — use when needed and present; never spam; never invent when a present tool could answer  
8. **Agentic convergence** — Critic FAIL → gap → targeted re-research → replan  
9. **Plan Mode** — zero target-repo writes  
10. **Any-repo** — stack discovered, never assumed  

**Plus hard product rule (`docs/EXECUTION_SPEC.md`):** plan + iDAG are **letter-to-letter**. Outline STEPs and title-only DAG nodes are FAIL. Coding-agent imagination is **unacceptable**.

## Pin model (stack-agnostic)

Pins describe **final desired text**, not edit recipes.

| Field | When | Rule |
|-------|------|------|
| `exact_files` | Always for edit steps | Concrete paths proven to exist (or explicitly created) |
| `exact_commands` | Verify/build/test/git | Only commands proven from manifests / CI / README. In PLAYBOOK/HANDOFF: `~~~bash` fence (`docs/PLAYBOOK_FORMAT.md`) |
| `exact_copy` | User-visible strings change | One string/template. In PLAYBOOK/HANDOFF: `Exact copy: \`…\`.` — not a fence |
| `exact_aria` / a11y name | Only if this stack has accessible names | One template; omit if N/A with Decision. In PLAYBOOK/HANDOFF: `Exact aria: \`…\`.` — not a fence |
| `exact_snippet` | Primary source edit (any language) | **Final** post-change source block — one shape. In PLAYBOOK/HANDOFF: labeled `~~~<lang>` fence; never a loose paragraph |
| `exact_markup` | Alias of `exact_snippet` for markup UIs | Same rule — final block only, same fence placeholder |
| `companion_required` | Renames, class/selector/symbol moves, split files | `true\|false` |
| `exact_companion` | When `companion_required: true` | **Final** text for styles/configs/other touched artifacts. In PLAYBOOK/HANDOFF: `~~~<lang>` fence |
| `exact_branch` | Branch create | One name; no alternates in plan body |

`css_required` / `exact_css` remain allowed as **aliases** of companion pins when the discovered companion is a stylesheet. Prefer `companion_required` + `exact_companion` for new plans.

### Final text vs instructional prose (FAIL)

These in any `exact_*` pin are **FAIL** (foreign agent would still invent):

- “replace X with Y”, “rename X to Y”, “delete these rules”, “update the … block”
- “see above”, “same as before”, “port the old rules”
- Comment-only instructions with no final selector/code blocks

### Reference trace (mandatory before PASS)

If the plan renames, removes, or repurposes a **symbol** (class, id, function, module, selector, route, config key):

1. Search the **entire target repo** for remaining references (stack-appropriate search).  
2. Every hit is either covered by a final pin, listed in `do_not` / out-of-scope Decision, or → Critic **FAIL**.  
3. Do not leave “update other call sites” as prose.

### Constraint / layout risk (mandatory when size/weight changes)

When replacing a short affordance with longer copy, an icon with a label, or changing visual/layout weight:

1. Inspect the **containing layout constraints** in whatever system this repo uses (CSS grid/flex, native layout XML, SwiftUI, Flutter, terminal width, table columns, …).  
2. Record a Finding + Decision.  
3. Pin companion styles/structure so the foreign agent does not guess.  
4. Critic vector `constraint_risk` must be checked.

## Zero soft-shape rule (hard)

In PlanStep `change` / `title` / pins — **FAIL** unless entropy ALLOW + single default used in plan:

- `e.g.`, `for example`, `and/or`, `or equivalent`
- `if needed`, `if necessary`, `optional` (critical-path product steps)
- `either … or …` offering two implementation shapes
- Unpinned alternate branch/tool names in the plan body

## Smart tools / MCP

Full contract: `docs/TOOLS.md` (Cursor-first map + per-agent allowlists + discovery).

| Situation | Behavior |
|-----------|----------|
| Clear from Spec + files read | Do not spam |
| Local search can answer | Cursor Grep/read/shell first |
| Need web/docs | Cursor **WebSearch / WebFetch / browser** first |
| Need org/sibling discovery | Resolve provider from connection.meta/remote → auth **only that** SCM MCP |
| Need ticket/wiki context | Tracker MCP only if Spec needs it (Jira ≠ code siblings) |
| MCP absent/fails | `discovery_unavailable`; never invent; TargetRepo still wins |
| Mutating MCP in Plan Mode | Deny |

## Forbidden rubber-stamps

- Critic clean without proving reference-trace + constraint_risk when applicable  
- Scorecard 10 while instructional pins remain  
- Assuming a stack without opening manifests  
- Handing the agent a repo-specific “recommended fix” instead of making it discover one  

## Scorecard

`review/quality-scorecard.yaml` — **every axis == 10** to APPROVE.
