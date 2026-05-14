# Documentation Governance

Date: 2026-05-14
Status: PENDING VERIFICATION

Purpose: prevent workspace documentation from collapsing back into root-level noise.

Current-state boundary:

- 2026-05-14 DOC_AUDIT override: read `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` before using May 11 counters or build-artifact links as current proof.
- The May 11 `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.*` proof files cited by several docs are absent from the current filesystem. Treat those references as stale report text. Current May 14/R43 external root `Hecton8*.csproj` no-restore CLI compile evidence is `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist, not Unity runtime proof.
- Current root text scan sees `6` root `.md`, `3` root `.log`, and `3` root `.json` files. Only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md` are active root authority.
- Current non-anchor root markdown files are not authority: `BROKEN_PREFABS.md` is a generated snapshot, `PROJECT_ATLAS.md` is a compatibility mirror for `Docs/PROJECT_ATLAS.md`, and `TERRAIN_AND_BIOME_REALITY_MAP.md` is a compatibility mirror / stale legacy surface for `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.
- `PROJECT_ATLAS.md` and `Docs/PROJECT_ATLAS.md` are static first-party asmdef graph snapshots only. They are not package/config/runtime authority.
- 2026-05-13 DOC_AUDIT R7 patched `AGENTS.md` and `.codexrules/AGENTS.md` to current Low URP reality: `URP_Low` uses `Mobile_Renderer` at render scale `0.85`.
- 2026-05-13 package/player-settings drift is documented in `Docs/PROJECT_STATE_STATIC_XRAY.md` and `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`; manifest cleanliness does not mean asset-tree or PlayerSettings cleanliness.
- `Docs/?? ????.md` was a stale batch-prompt dump and was moved to `Docs/DEPRECATED/Root_Stale_Batch_Prompt_Dumps_2026-05-13/`.
- Current first-party asmdef scan sees `24` asmdefs under `Assets/_Project`; older `13`, `22`, and `23` asmdef atlas statements are stale.
- `Docs/PROJECT_STATE_STATIC_XRAY.md` is now a direct `Docs/` root static risk register. It is evidence-guiding documentation, not runtime proof.
- This file defines documentation placement and authority order only.
- It is not runtime proof and does not certify that every listed document is current by itself.
- Long-lived project authority lives in stable docs first. Dated reports are evidence snapshots and counters.
- Current `.agents-skills` visual-fake doctrine is promoted into `../AGENTS.md`, `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`, and `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`; `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` is supporting evidence.
- Current documentation synchronization counters are overridden by `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`; `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` and `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` remain historical May 11 evidence and manifest structure, not current numeric truth where May 13/R41/R42/R43 conflicts.
- Current documentation sorting authority starts at this file plus `Docs/README.md`. `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` and later synchronization reports are evidence for why the sorting changed.
- Current project truth starts at `../AGENTS.md`, `.agents-skills/README.md`, task-relevant `.agents-skills/*`, `Docs/README.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`, `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`, current source files, and fresh verification logs. Dated reports support these files; they do not outrank them.
- Current warning-cleanup evidence starts at `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`; current Omega build warning classification is scoped in `Docs/Reports/2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md`.

## Authority Order

1. `../AGENTS.md`
2. `.agents-skills/README.md`
3. task-relevant `.agents-skills/*` mandates
4. `Docs/README.md`
5. `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
6. `Docs/PROJECT_STATE_STATIC_XRAY.md`
7. `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
8. `Docs/SYSTEMS_CONTRACTS.md`
9. `Docs/QUALITY_GATES.md`
10. `Docs/ARCHITECTURE/README.md`
11. `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
12. `Docs/DOC_GOVERNANCE.md`
13. `Docs/ROOT_DOCS_REFERENCE.md`
14. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`
15. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
16. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
17. current source files
18. fresh verification logs and artifacts
19. `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md`
20. dated reports under `Docs/Reports/` and `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/`
21. active execution docs under `Docs/`
22. long-lived reference bundles under `Docs/AI_Fauna`, `Docs/Flora_Pipeline`, `Docs/Scatter_Runtime`, `Docs/Legacy_World_Reference`, `Docs/Legacy_Backlog`, and similar category folders
23. deprecated/historical snapshots under `Docs/DEPRECATED/` and `Docs/Reports/DEPRECATED/`
24. archive bundles under `Docs/_Archive/`

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

## Standard Agent Context Exclusion

`Docs/DEPRECATED/`, `Docs/Reports/DEPRECATED/`, `Docs/_Archive/`, and `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/` are excluded from standard agent context loading.

Standard context loads must start from active anchors, current reports, and task-specific architecture files only. Deprecated folders may be opened only when the task explicitly asks for history, provenance, or a migration audit.

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
- when refreshing broad documentation truth without rewriting every file, create a dated report under `Docs/Reports/` and update the stable authority docs that own the durable rule
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
