# Handoff — Walk 24 Oct migration P1

Execute `final/PLAYBOOK.md` and `final/DAG.yaml` **literally**.
Do **not** reinterpret architecture. Do **not** invent files, APIs, or package versions.
If anything is unspecified, **stop** — planner defect.

## Target

- Repo: `sandeep-chanda/PiggyMetricsDotNet` (greenfield)
- Plan key: `P1`
- Stack: .NET `net8.0` / `dotnet test`

## Pins reminder

exact_snippet / exact_companion / exact_commands use labeled `~~~lang` fences in PLAYBOOK.md.

## Done when

Every project builds; Shared bearer accepts an authorization-server-issued token and refuses one it did not — proven by:

~~~bash
dotnet test PiggyMetrics.sln
~~~
