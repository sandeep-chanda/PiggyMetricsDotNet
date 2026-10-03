# Connection / job intake — Spec + Target + siblings + auth refs

Planner jobs receive **everything needed for provider-routed cross-repo research** up front.
Do **not** invent org/project/sibling lists. Do **not** store secrets in the workspace.

## Canonical inputs

| Input | Where | Purpose |
|-------|--------|---------|
| Spec | `intake/raw-spec.md` | What to plan |
| Target repo (local path) | `intake/job.yaml` → `target_repo_path` | Primary codebase (always wins) |
| Connection | `intake/connection.meta.json` | Provider, org/project root, remotes, **sibling repo list**, auth **refs** |
| Related local checkouts | `job.yaml` → `related_repos[]` | Paths on disk for deep Read/Grep |

## Azure DevOps cross-repo

Same ADO **project** repo filter — siblings of the **tipped target** from
`connection.meta.json` / RHM master (not a baked-in demo repo).

Discovery root = `organization` + `project` from the remote  
(`https://dev.azure.com/{org}/{project}/_git/{repo}`).

Cross-Repo Researcher:

1. Use the **supplied** sibling list in connection meta when present.  
2. Optionally refresh/verify via Azure DevOps MCP/CLI using the job’s **auth ref** (readonly list repos in that project).  
3. Deep-inspect only siblings that have a local path in `related_repos` (or discovered clone path).  
4. Remote-only siblings → cite URL; `local_inspect: blocked`.

## Same pattern — other providers

| Provider | Sibling root (like ADO project filter) | Sibling list field |
|----------|----------------------------------------|--------------------|
| `azure_devops` | Organization + **Project** | All `_git/*` repos in that project |
| `github` | **Org** (or user) | Repos in that org |
| `gitlab` | **Group** / namespace | Projects in that group |
| `bitbucket` | **Workspace** / project | Repos in that workspace |

Always: same provider as TargetRepo unless connection explicitly lists multi-provider related sets.

## Auth (refs only — never paste tokens into YAML/JSON)

In `connection.meta.json`:

```json
"auth": {
  "provider": "azure_devops",
  "method": "az_cli",
  "env_refs": ["ADO_ORG"],
  "notes": "Use az login for the account that can list repos in the tipped project"
}
```

GitHub example: `"env_refs": ["GITHUB_PERSONAL_ACCESS_TOKEN"]` or `"method": "gh_cli"`.  
Jira/tracker auth is separate and only when Spec needs tickets.

The operator (or HM connector) supplies real credentials in the environment / Cursor MCP — the planner only receives **which** auth to use.

## `connection.meta.json` shape

See `templates/connection.meta.json` and `schemas/connection.schema.json`.
