# Documentation Governance

Date: `2026-05-02`
Status: `PENDING VERIFICATION`

Purpose: prevent workspace documentation from collapsing back into root-level noise.

Current-state boundary:

- This file defines documentation placement and authority order only.
- It is not runtime proof and does not certify that every listed document is current by itself.
- Current project truth starts at `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`, current source files, and fresh verification logs.

## Authority Order

1. `../AGENTS.md`
2. task-relevant `.agents-skills/*` mandates
3. `Docs/README.md`
4. `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`
5. `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
6. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
7. active execution docs under `Docs/`
8. long-lived reference bundles under `Docs/AI_Fauna`, `Docs/Flora_Pipeline`, `Docs/Scatter_Runtime`, `Docs/Legacy_World_Reference`, `Docs/Legacy_Backlog`, and similar category folders
9. deprecated/historical snapshots under `Docs/DEPRECATED/` and `Docs/Reports/DEPRECATED/`
10. archive bundles under `Docs/_Archive/`

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

## What Belongs In Deprecated

Move material into `Docs/DEPRECATED/` when it is:

- a dated audit snapshot superseded by a newer current-state report
- an external idea/prompt/log bundle that must be preserved but must not look active
- a compatibility copy whose original path is no longer canonical
- a static scan whose counts are stale but whose provenance is still useful

Move report-root snapshots into `Docs/Reports/DEPRECATED/` when they are superseded by newer reports but should stay near report history.

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
- when moving superseded docs to deprecated, add or update a local `README.md` explaining the replacement authority
- when shrinking root, update `Docs/ROOT_DOCS_REFERENCE.md`
- when refreshing broad documentation truth without rewriting every file, create a dated report under `Docs/Reports/` and update `Docs/README.md`, `Docs/Reports/README.md`, and the current-state anchor
- when archiving a large wave, update `Docs/_Archive/README.md` and the bundle manifest
- if a legacy document is kept for historical value but not active authority, put it in a reference bundle or archive, not in root
- if a filesystem lock blocks rename/delete, keep a temporary compatibility mirror but declare the canonical bundle path explicitly in `Docs/README.md`
- if a compatibility mirror is no longer needed as an active redirect, move it to `Docs/DEPRECATED/` with a local README and keep only the canonical bundle path in active indexes
- do not create new root-level `.md` or `.txt` files unless the file is an approved emergency anchor and the reason is explicit

## Red Flags

If you see these patterns, cleanup is overdue:

- root gains more than `5` non-anchor text docs
- the same topic exists in root, `Docs`, and `Ai findings`
- prompt packs sit next to live plans
- agent logs remain in active `Docs`
- empty shell directories survive after moves
