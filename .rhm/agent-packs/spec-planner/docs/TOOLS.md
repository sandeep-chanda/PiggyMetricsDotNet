# Tools & MCP — Cursor-first + per-agent allowlists

Spec Planner does **not** invent a tool runtime. **Cursor** is the host. This doc is the tool contract.

**Auth is provider-routed** — same idea as HM connection / `rhm_specs_master.target_repo_*`: use the SCM (and optional tracker) that **this Spec’s target project actually uses**. Do **not** require GitHub + Azure DevOps + Atlassian for every run.

## Plan Mode (hard)

| Allowed | Forbidden |
|---------|-----------|
| Read target / related repos | Write/edit/delete under target or related prod trees |
| Write `workspace/<task-id>/**` only | Shell mutations (`git commit`/`push`, installs) |
| Readonly discovery for the **active provider(s)** | Mutating MCP; inventing sibling APIs |

## Resolve provider first (mandatory before discovery auth)

Inputs (any available):

1. `intake/connection.meta.json` → `target_repo_provider`, `target_repo_remote_url`, `target_repo_slug`  
2. Else TargetRepo `git remote -v`  
3. Else Spec / job hints  

| `target_repo_provider` / remote host | Code-sibling discovery root | Auth needed for siblings |
|-------------------------------------|-----------------------------|---------------------------|
| `azure_devops` / `dev.azure.com` / `visualstudio.com` | **Same ADO organization + project** (list other repos in that project) | Azure DevOps only (`az login` + org) |
| `github` / `github.com` / `github.*.com` | **Same GitHub org (or user)** | GitHub only (PAT / `gh`) |
| `gitlab` / `gitlab.com` / self-hosted GitLab | **Same GitLab group/namespace** | GitLab token/MCP when configured |
| `bitbucket` / … | **Same Bitbucket workspace/project** | Bitbucket auth when configured |
| unknown | Skip remote sibling discovery; use `--related` paths only | none |

**Work trackers are separate** from code siblings:

| Need | Provider | When |
|------|----------|------|
| Jira / Confluence Spec context | Atlassian Rovo MCP | Only if Spec/job references Jira/Confluence |
| ADO work items | Azure DevOps MCP | Only if Spec/job references WI and provider is ADO (or mixed) |
| GitHub issues/projects | GitHub MCP | Only if Spec/job references them |

Example shape: `target_repo_provider: azure_devops`, remote  
`https://dev.azure.com/{org}/{project}/_git/{repo}` from the tipped RHM master  
→ sibling search = other repos under that **org / project**.  
→ **Do not** demand `GITHUB_PERSONAL_ACCESS_TOKEN` for an `azure_devops` job.

## Intelligence rule (Cursor-first)

```
Need evidence?
  ├─ Spec / files already answer → Finding; stop
  ├─ TargetRepo or known --related roots → Cursor Read/Grep/Glob/Explore/Shell (readonly)
  ├─ need web/docs → Cursor WebSearch / WebFetch / browser first
  ├─ need code siblings → resolve provider → use THAT provider’s MCP/CLI only
  ├─ need ticket/wiki context → use tracker MCP only if Spec needs it
  ├─ matching MCP missing → discovery_unavailable for that provider; continue with TargetRepo + --related
  └─ unknowable → ask user / assumption with confidence
```

## Job inputs + sibling discovery

Full intake contract: **`docs/CONNECTION.md`**.

Operators (or HM) supply with each job:

- Spec + TargetRepo path  
- `connection.meta.json` — provider, scm_root, **`sibling_repos[]`** (ADO project filter / GitHub org / …), auth **refs** only  
- Optional local `related_repos[]` for deep inspect  

**Azure:** siblings = other repos under the tipped master’s org/project root
(from `connection.meta.json` / Connections — never a fixed demo slug).

Same pattern for other providers: siblings = other repos under that provider’s project/org/group root.

Cross-Repo Researcher uses the **supplied sibling list** first; may verify via that provider’s MCP only.

## Capability map (Cursor-first)

| Need | Prefer first | Then (only matching provider) |
|------|--------------|--------------------------------|
| Code in TargetRepo | Cursor Read/Grep/Glob/Explore | — |
| Versions / CI | Manifests + readonly Shell | — |
| Official docs | Cursor WebSearch / WebFetch | — |
| Rendered docs | Cursor browser | — |
| Known related checkout | `--related` / multi-root | — |
| Sibling repos (ADO project) | — | **Azure DevOps MCP** list repos in project |
| Sibling repos (GitHub org) | — | **GitHub MCP** or `gh repo list` |
| Sibling repos (GitLab group) | — | GitLab MCP/CLI when configured |
| Ticket/Spec linkage | — | Atlassian and/or ADO WI / GitHub issues as needed |
| Validate package | `scripts/validate-*.py` | — |
| Delegate roles | Cursor Task + `.cursor/agents/*` | — |

## MCP catalog (optional; enable what you use)

File: `.cursor/mcp.json` — a **catalog**, not a mandate to authenticate all.

| Server key | When to enable | Auth |
|------------|----------------|------|
| `azure-devops` | Jobs whose provider is `azure_devops` (or mixed ADO) | `az login`; `ADO_ORG` = org from remote |
| `github` | Jobs whose provider is `github` (or mixed GitHub) | `GITHUB_PERSONAL_ACCESS_TOKEN` or `gh auth` |
| `atlassian` | Jobs that need Jira/Confluence context | OAuth on first use |

Verified sources: [Azure DevOps MCP](https://github.com/microsoft/azure-devops-mcp), [GitHub MCP](https://github.com/modelcontextprotocol/servers), [Atlassian Rovo](https://developer.atlassian.com/cloud/rovo-mcp/guides/getting-started/).

Remove or disable unused servers in Cursor MCP UI. Plan Mode soft-denies mutating MCP names via `tool-allowlist.py`.

### Per-job enable (not “all auth”)

1. Read `connection.meta.json` / remote → pick **code provider**.  
2. Authenticate **only that** provider for sibling discovery.  
3. Authenticate a **tracker** only if Spec/job needs tickets/wiki.  
4. If auth missing → `discovery_unavailable: <provider>`; still plan from TargetRepo.

## Per-agent tool allowlist

Classes: `delegate`, `read_workspace`, `write_workspace`, `read_target`, `shell_readonly`, `web_cursor`, `browser_cursor`, `mcp_scm` (provider-routed: github|ado|gitlab|…), `mcp_tracker` (jira|ado_wi|github_issues), `validators`.

| Agent | Allowed | Forbidden |
|-------|---------|-----------|
| Orchestrator | `delegate`, `read_workspace`, `write_workspace` (state), `validators` | Solo deep research replacing specialists; prod codegen |
| Spec Analyst | `read_workspace`, `read_target` (light) | Web sprawl; SCM discovery; prod edits |
| Environment Analyst | `read_target`, `shell_readonly`, `read_workspace` | Inventing env; prod edits |
| Repo Researcher | `read_target`, `shell_readonly` (git), `read_workspace` | Web unless pattern gap; prod edits |
| Cross-Repo Researcher | `read_target`, `--related`, **`mcp_scm` for resolved provider**, `shell_readonly` | Wrong-provider auth spam; inventing APIs; prod writes |
| Web Researcher | `web_cursor`, `browser_cursor`, `read_workspace` | Prod edits; mutating MCP |
| Research Synthesizer | `read_workspace`, `write_workspace` (findings) | Unbounded web without a question |
| Architect | `read_workspace`, `write_workspace` (architecture/) | Coding |
| Migration | `read_workspace`, `web_cursor`, cross-repo / `mcp_scm` | Coding |
| Planner | `read_workspace`, `write_workspace` (planning/) | Coding |
| DAG Builder | `read_workspace`, `write_workspace`, `validators` | Coding |
| Critic | `read_workspace`, `read_target`, critic writes | Soft-pass; prod edits |
| Gap Loop | gaps/questions + `delegate` | Final APPROVED |
| Quality Gate | `validators`, gate writes | Weakening FAIL |

Critic may flag: used GitHub MCP on an `azure_devops`-only job (wrong provider), or skipped ADO sibling list when ADO MCP was configured and Spec needs shared libs.

## Web research helper

- Agent: `.cursor/agents/web-researcher.md`  
- Tools: Cursor WebSearch / WebFetch / browser (not a separate web MCP product)  
- Out: `research/external.yaml`

## Cross-repo helper

- Agent: `.cursor/agents/cross-repo-researcher.md`  
- Resolve provider → sibling discovery under same project/org root → optional deep read  
- Out: `research/cross-repo.yaml`

## Hooks

`.cursor/hooks.json` — shell-guard, block-production-edits, MCP mutate soft-deny, subagent-prereqs, final-handoff-gate.

See `docs/QUALITY_BAR.md`, `docs/PROTOCOL.md`.
