# Research Protocol

Research is the **largest** part of the system. No playbook until research survives critic/gates.

## Plan Mode

Read-only against the target repo. Findings only under `workspace/<task-id>/research/`.

## Stack discovery (hard)

Never assume a language, framework, package manager, or UI toolkit. Open whatever manifests **this** repo has. Environment research must set `discovered: true` with evidence paths.

## Five dimensions (required unless justified N/A)

1. **Spec** — requirements; **challenge** cited paths/scripts against the tree  
2. **Environment** — discover stack from manifests/CI/runtime files  
3. **Repository archaeology** — current impl, similar features, tests, patterns; precedents = **constraints**  
4. **Cross-repo** — shared libs/APIs/consumers when applicable  
5. **External/web** — targeted standards/migrations for the **discovered** stack  

## Mandatory research moves (when applicable)

| Trigger | Required research |
|---------|-------------------|
| Plan renames/removes a symbol (class, id, function, selector, module, route, config key) | Whole-repo **reference trace**; list every hit |
| User-visible copy/affordance grows or visual weight changes | **Constraint/layout** finding in this stack’s terms |
| Spec cites verify commands | Prove scripts exist in manifests/CI |

## Web priority

1. Official documentation (for discovered tech)  
2. Organisation patterns  
3. Maintainer guidance  
4. Proven production implementations  
5. High-quality community examples  

## Question-driven loop

```
Research question → Search → Evidence → Answers?
  No → deepen / new question / smart tool-MCP
  Yes → Finding (id, conclusion, evidence, confidence, implication)
```

## Default question bank

- How are similar features implemented **in this repository**?  
- Is there already an abstraction?  
- What shared library provides this?  
- What verify/build/test commands does **this** repo actually define?  
- Where else is this symbol referenced?  
- What layout/constraints contain this UI/copy (if visual)?  
- What does the framework officially recommend (for discovered stack)?  
- Known failure modes? Spec consistent with repo?  

## Migration triggers

upgrade, replacement, migration, refactoring, schema/API/infra/framework change, legacy replacement, platform migration.

## Competing approaches

Options A/B/C (+ existing abstraction); score; choose with evidence.

## Evidence rule

Every important conclusion needs sources (file:lines, cross-repo path, or URL). Confidence: high | medium | low.
