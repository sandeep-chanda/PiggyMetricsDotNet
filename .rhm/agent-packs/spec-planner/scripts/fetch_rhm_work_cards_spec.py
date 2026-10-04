#!/usr/bin/env python3
"""Export one RHM spec (+ master repo tip) by id / plan_key / title — no demo hardcodes.

Usage:
  RHM_SPEC_ID=<uuid> python fetch_rhm_work_cards_spec.py [out.md] [out.meta.json]
  RHM_PLAN_KEY=01_work_cards python …
  RHM_SPEC_TITLE='my title' python …
  python … --spec-id <uuid>
"""
from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
from pathlib import Path


async def main() -> int:
    import asyncpg

    ap = argparse.ArgumentParser(description="Export rhm_specs row by tip, not a fixed demo title")
    ap.add_argument("out_md", nargs="?", default="from-rhm-spec.md")
    ap.add_argument("out_meta", nargs="?", default=None)
    ap.add_argument("--spec-id", default=os.environ.get("RHM_SPEC_ID", ""))
    ap.add_argument("--plan-key", default=os.environ.get("RHM_PLAN_KEY", ""))
    ap.add_argument("--title", default=os.environ.get("RHM_SPEC_TITLE", ""))
    args = ap.parse_args()

    sid = (args.spec_id or "").strip()
    plan_key = (args.plan_key or "").strip()
    title = (args.title or "").strip()
    if not sid and not plan_key and not title:
        print(
            "ERROR: set RHM_SPEC_ID, RHM_PLAN_KEY, or RHM_SPEC_TITLE "
            "(or --spec-id / --plan-key / --title)",
            file=sys.stderr,
        )
        return 2

    url = os.environ.get(
        "DATABASE_URL", "postgresql://spec:localpass@127.0.0.1:5432/specforge"
    )
    out_md = Path(args.out_md)
    out_meta = Path(args.out_meta) if args.out_meta else out_md.with_suffix(".meta.json")

    conn = await asyncpg.connect(url)
    try:
        if sid:
            row = await conn.fetchrow(
                """
                SELECT s.id::text AS id, s.title, s.plan_key, s.status, s.spec_text,
                       s.rhm_specs_master_id::text AS master_id,
                       m.title AS master_title, m.target_repo_name, m.target_repo_slug,
                       m.target_repo_remote_url, m.target_repo_provider,
                       m.target_repo_default_branch, m.target_repo_connector_id, m.metadata
                FROM rhm_specs s
                LEFT JOIN rhm_specs_master m ON m.id = s.rhm_specs_master_id
                WHERE s.id = $1::uuid
                LIMIT 1
                """,
                sid,
            )
        elif plan_key:
            row = await conn.fetchrow(
                """
                SELECT s.id::text AS id, s.title, s.plan_key, s.status, s.spec_text,
                       s.rhm_specs_master_id::text AS master_id,
                       m.title AS master_title, m.target_repo_name, m.target_repo_slug,
                       m.target_repo_remote_url, m.target_repo_provider,
                       m.target_repo_default_branch, m.target_repo_connector_id, m.metadata
                FROM rhm_specs s
                LEFT JOIN rhm_specs_master m ON m.id = s.rhm_specs_master_id
                WHERE s.plan_key = $1
                ORDER BY s.updated_at DESC NULLS LAST
                LIMIT 1
                """,
                plan_key,
            )
        else:
            row = await conn.fetchrow(
                """
                SELECT s.id::text AS id, s.title, s.plan_key, s.status, s.spec_text,
                       s.rhm_specs_master_id::text AS master_id,
                       m.title AS master_title, m.target_repo_name, m.target_repo_slug,
                       m.target_repo_remote_url, m.target_repo_provider,
                       m.target_repo_default_branch, m.target_repo_connector_id, m.metadata
                FROM rhm_specs s
                LEFT JOIN rhm_specs_master m ON m.id = s.rhm_specs_master_id
                WHERE s.title = $1 OR s.title ILIKE $2
                ORDER BY s.updated_at DESC NULLS LAST
                LIMIT 1
                """,
                title,
                f"%{title}%",
            )
        if not row:
            print("ERROR: no rhm_specs row for the given tip", file=sys.stderr)
            return 1

        md = row["spec_text"] or ""
        out_md.parent.mkdir(parents=True, exist_ok=True)
        out_md.write_text(md, encoding="utf-8")

        meta_raw = row["metadata"]
        if isinstance(meta_raw, str):
            try:
                meta_raw = json.loads(meta_raw)
            except json.JSONDecodeError:
                meta_raw = {}
        meta_raw = meta_raw or {}

        meta = {
            "source": "rhm_specs",
            "spec_id": row["id"],
            "title": row["title"],
            "plan_key": row["plan_key"],
            "status": row["status"],
            "master_id": row["master_id"],
            "master_title": row["master_title"],
            "spec_bytes": len(md),
            "target_repo_name": row["target_repo_name"] or "",
            "target_repo_slug": row["target_repo_slug"] or "",
            "target_repo_remote_url": row["target_repo_remote_url"] or "",
            "target_repo_provider": row["target_repo_provider"] or "",
            "target_repo_default_branch": row["target_repo_default_branch"] or "main",
            "target_repo_connector_id": str(row["target_repo_connector_id"] or ""),
            "master_metadata": meta_raw,
        }
        out_meta.write_text(json.dumps(meta, indent=2, default=str), encoding="utf-8")
        print(json.dumps(meta, indent=2, default=str))
        print(f"exported_spec={out_md}")
        return 0
    finally:
        await conn.close()


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
