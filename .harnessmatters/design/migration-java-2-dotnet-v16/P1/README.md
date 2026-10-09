# Migration Java 2 .NET v16 - Plan P1 (Solution skeleton and Shared)

Spec-Planner outcome package for plan key **P1**, produced in planning mode. The planner wrote no
production code into this repository; everything here is planning output.

Start with `final/PLAYBOOK.md` (eleven steps, every file body pinned in full) and `final/DAG.yaml`
(the iDAG). `final/HANDOFF.md` is the coding-agent brief. `planning/implementation-plan.yaml` is the
machine-readable source of truth for the STEPs, including each step's edge cases, acceptance list and
rollback command.

| Area | Contents |
|------|----------|
| `final/` | PLAYBOOK.md, DAG.yaml, HANDOFF.md, PACKAGE_MANIFEST.yaml, PLAYBOOK.import.json |
| `planning/` | implementation-plan.yaml (11 STEPs, 32 pinned files), implementation-dag.yaml, test-plan.yaml, risks.yaml |
| `architecture/` | decisions.yaml (15 decisions with rejected alternatives), options.yaml |
| `research/` | environment, repository, cross-repo, external, findings R-001..R-012 |
| `review/` | critic, critic-attack-log (16 vectors), gaps, handoff-simulation, decision-entropy, quality-scorecard, quality-gate |
| `spec/`, `migration/`, `intake/` | normalized spec, open questions, assumptions, migration mapping, raw spec |

Every shape in the plan is pinned from a cited file in the source Spring Boot reactor
(`sqshq/piggymetrics`), which was cloned read-only while the plan was written, or from upstream
Spring Boot, Spring Framework, Jackson and NuGet metadata read during the same run.

Validators run against the package: `validate-execution-depth`, `validate-replit-bar`,
`validate-playbook`, `validate-dag`, `validate-entropy`, `check-handoff` - all PASS.
