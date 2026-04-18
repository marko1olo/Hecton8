# Documentation Governance

Date: `2026-04-17`
Status: `PENDING VERIFICATION`

Purpose: prevent workspace documentation from collapsing back into root-level noise.

## Authority Order

1. `../AGENTS.md`
2. active execution docs under `Docs/`
3. long-lived reference bundles under `Docs/AI_Fauna`, `Docs/Legacy_World_Reference`, `Docs/Legacy_Backlog`, and similar category folders
4. archive bundles under `Docs/_Archive/`

## What Belongs In Root

Only the smallest active anchors:

- `AGENTS.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`

If a document is not one of those, it should have a strong reason to remain in root.

## What Belongs In Docs

### Active execution docs

Use dated folders or dated filenames for bounded audits/plans that still drive work:

- `Docs/YYYY-MM-DD_*`

### Reports

New reports and validation writeups belong under:

- `Docs/Reports/YYYY-MM-DD_TaskName.md`

If a report becomes a multi-file workstream, promote it to:

- `Docs/Reports/YYYY-MM-DD_TaskName/`

### Long-lived reference bundles

Use named folders for material that stays useful across many sessions:

- `Docs/AI_Fauna/`
- `Docs/Flora_Pipeline/`
- `Docs/Scatter_Runtime/`
- `Docs/Legacy_World_Reference/`
- `Docs/Legacy_Backlog/`

Add a local `README.md` when a bundle contains more than one important file.

## What Belongs In Archive

Move material into `Docs/_Archive/` when it is:

- a one-shot report
- a handoff/session log
- a prompt dump
- a temporary audit superseded by newer execution docs
- an old agent work package
- stale status reporting older than the current working slice

Archive threshold:

- if a report/plan is older than `5` days, no longer drives current work, and is not a long-lived reference contract, move it out of active `Docs`
- if a newer execution doc replaces an older one on the same topic, archive the older one in the same cleanup pass

## Naming Rules

- active execution docs: `YYYY-MM-DD_Short_Scope/...`
- reference bundles: stable category folders with clear names
- archive bundles: dated cleanup or dated delivery folders

Do not invent vague folders like `misc`, `temp docs`, `new stuff`, `agent notes`.

## Maintenance Rules

- when moving active docs, update `Docs/README.md`
- when shrinking root, update `Docs/ROOT_DOCS_REFERENCE.md`
- when archiving a large wave, update `Docs/_Archive/README.md` and the bundle manifest
- if a legacy document is kept for historical value but not active authority, put it in a reference bundle or archive, not in root
- if a filesystem lock blocks rename/delete, keep a temporary compatibility mirror but declare the canonical bundle path explicitly in `Docs/README.md`
- do not create new root-level `.md` or `.txt` files unless the file is an approved emergency anchor and the reason is explicit

## Red Flags

If you see these patterns, cleanup is overdue:

- root gains more than `5` non-anchor text docs
- the same topic exists in root, `Docs`, and `Ai findings`
- prompt packs sit next to live plans
- agent logs remain in active `Docs`
- empty shell directories survive after moves
