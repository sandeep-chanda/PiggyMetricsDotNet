#!/usr/bin/env python3
"""Export a Spec row from rhm_specs (+ master repo connection). Stack/product agnostic fetch."""
from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
from pathlib import Path


async def main() -> int:
    import asyncpg

    ap = argparse.ArgumentParser(description="Fetch one rhm_specs row by plan_key and/or title")
    ap.add_argument("--plan-key", default="", help="Exact plan_key match")
    ap.add_argument("--title", default="", help="Title ILIKE fragment, e.g. sub-spec-02-skip-link")
    ap.add_argument(
        "--out",
        default="",
        help="Output markdown path (default: workspace/_inbox/from-rhm-<slug>.md)",
    )
    args = ap.parse_args()
    if not args.plan_key and not args.title:
        print("ERROR: pass --plan-key and/or --title", file=sys.stderr)
        return 2

    url = os.environ.get(
        "DATABASE_URL", "postgresql://spec:localpass@127.0.0.1:5432/specforge"
    )
    slug = (args.plan_key or args.title or "spec").replace("/", "-").replace(" ", "-")
    out_md = Path(args.out) if args.out else Path(f"workspace/_inbox/from-rhm-{slug}.md")
    out_meta = out_md.with_suffix(".meta.json")

    conn = await asyncpg.connect(url)
    try:
        clauses = []
        params: list[object] = []
        if args.plan_key:
            params.append(args.plan_key)
            clauses.append(f"s.plan_key = ${len(params)}")
        if args.title:
            params.append(f"%{args.title}%")
            clauses.append(f"s.title ILIKE ${len(params)}")
        where = " OR ".join(clauses)

        row = await conn.fetchrow(
            f"""
            SELECT s.id::text AS id,
                   s.title,
                   s.plan_key,
                   s.status,
                   s.spec_text,
                   s.rhm_specs_master_id::text AS master_id,
                   m.title AS master_title,
                   m.target_repo_name,
                   m.target_repo_slug,
                   m.target_repo_remote_url,
                   m.target_repo_provider,
                   m.target_repo_default_branch,
                   m.target_repo_connector_id,
                   m.metadata
            FROM rhm_specs s
            LEFT JOIN rhm_specs_master m ON m.id = s.rhm_specs_master_id
            WHERE {where}
            ORDER BY s.updated_at DESC NULLS LAST
            LIMIT 1
            """,
            *params,
        )
        if not row:
            print("ERROR: no matching rhm_specs row", file=sys.stderr)
            sample = await conn.fetch(
                """
                SELECT id::text, title, plan_key, length(spec_text) AS len
                FROM rhm_specs
                ORDER BY updated_at DESC NULLS LAST
                LIMIT 20
                """
            )
            for s in sample:
                print("sample", dict(s), file=sys.stderr)
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
