# Spec tip (no demo bake-in)

Do **not** commit a fixed RHM title, plan key, repo remote, or auth tip here.

Export the **selected** spec at runtime:

```bash
RHM_SPEC_ID=<uuid> python scripts/fetch_rhm_work_cards_spec.py from-rhm-spec.md from-rhm-spec.meta.json
# or RHM_PLAN_KEY=… / RHM_SPEC_TITLE=…
```

Repo + connector tip comes from `rhm_specs_master` for that row. SCM auth comes from Connections (not this file).
