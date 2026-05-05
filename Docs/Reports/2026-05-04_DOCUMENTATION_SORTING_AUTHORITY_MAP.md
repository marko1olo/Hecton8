# Documentation Sorting Authority Map

Date: `2026-05-04`
Status: `PENDING VERIFICATION`
Scope: repository-root text files, active `Docs/` authority, report sorting, archive/deprecated boundaries

## Mandates Followed

- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## What Was Checked

- `AGENTS.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`
- `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`
- repository-root `.md`, `.txt`, and `.log` surface
- active `Docs/**/*.md` inventory excluding `_Archive`, `DEPRECATED`, `Reports/DEPRECATED`, and `ARCHIVARIUS REPORTS/03_OBSOLETE`

## Inventory Snapshot

Captured before this report file was added:

| Surface | Count |
|---|---:|
| `Docs/**/*.md`, total | `402` |
| active `Docs/**/*.md`, excluding archive/deprecated/obsolete | `191` |
| root `.md` files | `4` |
| root `.txt` / `.log` files | `7` |
| active `Docs/Reports/*.md` files | `37` |
| active root `Docs/*.md` files | `9` |

This report intentionally increases active report and active documentation counts by one after it is written.
Historical post-write quick count after this report, before root-log relocation: `Docs/**/*.md` total `403`, active `Docs/**/*.md` `192`, `Docs/Reports/*.md` `38`, root `.md` files `4`, root `.txt` / `.log` files `7`.
After `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md` was added, counts were `Docs/**/*.md` total `404`, active `Docs/**/*.md` `193`, and `Docs/Reports/*.md` `39`.
After the root-log relocation addendum, Archivarius header normalization, SpaceEngine research header normalization, documentation authority smoke-guard addendum, 2026-05-05 Omega documentation influx, and the second root-log relocation, root `.txt` / `.log` files are `0`; total `Docs/**/*.md` is now `418`, active `Docs/**/*.md` excluding `_Archive`, `DEPRECATED`, `Reports`, and `ARCHIVARIUS REPORTS/03_OBSOLETE` is now `160`, and `Docs/Reports/*.md` is now `45`.

## Current Authority Stack

Use this order when deciding whether a document is current:

1. `AGENTS.md`
2. task-relevant `.agents-skills/*` mandates
3. current source files and fresh command logs
4. `Docs/README.md`
5. `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`
6. `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`
7. `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
8. `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`
9. `Docs/Reports/2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md`
10. `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`
11. `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
12. domain-specific active contracts and reports
13. historical reports only after reading their latest delta/supersession notes
14. archive/deprecated folders only as preserved evidence

## Root Sorting

Root files that remain active documentation anchors:

| File | Handling |
|---|---|
| `AGENTS.md` | primary operating contract; keep in root |
| `MASTER_RELEASE_WORK_PLAN.md` | active roadmap anchor; keep in root |
| `BUILD_PLAYTEST_ISSUES.md` | active QA/build observation ledger; keep in root |

Root compatibility mirror:

| File | Handling |
|---|---|
| `TERRAIN_AND_BIOME_REALITY_MAP.md` | non-canonical mirror; canonical path is `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` |

Root evidence artifacts, not documentation authority:

| Pattern | Handling |
|---|---|
| `BROKEN_PREFABS.md` | generated prefab-audit snapshot; current content reports `0` broken prefabs, but it is not a canonical report until summarized in `Docs/Reports/` |
| `*.log` in repository root | none remain after relocation; if new root logs appear, move them into a dated deprecated evidence bundle or summarize them in a dated report before citation |
| `*.txt` in repository root | classify before use; current scan found no active root `.txt` authority |

Original sorting pass made no physical root file moves because the worktree was dirty across source, assets, generated artifacts, reports, and deprecated raw logs.
Follow-up root-log relocation moved the seven tracked repository-root `.log` files to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/` with `git mv`.
Second root-log relocation moved two newly generated repository-root Omega build/restore logs to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-05/`.

## Active Docs Root Sorting

The active root `Docs/` folder is still limited to broad anchors:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`

These files are not proof by themselves. They are routing and contract surfaces.

## Active Bundle Sorting

| Folder | Class |
|---|---|
| `Docs/Reports/` | active reports and validation writeups |
| `Docs/ARCHITECTURE/` | active architecture reference bundle |
| `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/` | historical forensic audit bundle with current supersession notes |
| `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/` | workspace maps and authority classifications |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/` | older concrete audit outputs; many are evidence-level, not first-read authority |
| `Docs/SPACE_ENGINE_RESEARCH/` | primary SpaceEngine research and integration evidence bundle |
| `Docs/AI_Fauna/` | active/reference fauna planning bundle |
| `Docs/Flora_Pipeline/` | active/reference flora pipeline bundle |
| `Docs/Scatter_Runtime/` | active/reference scatter runtime bundle |
| `Docs/Legacy_World_Reference/` | preserved world/reference material, not runtime proof |
| `Docs/Legacy_Backlog/` | backlog/reference material, not runtime proof |

Archive and deprecated bundles:

| Folder | Class |
|---|---|
| `Docs/_Archive/` | historical work packages |
| `Docs/DEPRECATED/` | superseded or damaged material preserved for provenance |
| `Docs/Reports/DEPRECATED/` | superseded report-root snapshots |
| `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/` | obsolete Archivarius material |

## Report Sorting

Current first-read reports:

- `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`
- `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`
- `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`
- `Docs/Reports/2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md`
- `Docs/Reports/2026-05-05_ARCHIVARIUS_REALITY_DELTA.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NAMING_VIOLATIONS.md`
- `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md`
- `Docs/Reports/2026-05-05_HECTON_SANDBOX_BIOMES_OMEGA_SURGERY_LOG.md`
- `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`
- `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
- `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`
- `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`

Current source/build or implementation reports, not runtime proof:

- `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md`
- `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md`
- `Docs/Reports/2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md`
- `Docs/Reports/2026-05-04_HYDRAULIC_EROSION_ENGINE_SURGERY_LOG.md`
- `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`
- `Docs/Reports/2026-05-03_REGISTRY_RENDERABLE_AND_JOB_BARRIER_GUARD.md`
- `Docs/Reports/2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md`
- `Docs/Reports/2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md`
- `Docs/Reports/2026-05-03_OPTIMIZATION_REGISTRY_OWNERSHIP.md`
- `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SIGNAL_CLEANUP.md`

Historical but still useful evidence reports:

- `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`
- May 1 compile, event, editor log, zero-GC job, and build-restoration reports
- non-dated surgery/audit reports such as `TOTAL_CODEBASE_AUDIT_V2.md`, `DOOMSDAY_FLAW_REPORT.md`, `AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`, `GC_SINGLETON_KILL_LIST.md`, and `CI_VALIDATION_HOOKS_SURGERY_LOG.md`

## Sorting Defects Found

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md` still used pre-repair guard-failure wording for the May 4 documentation sweep. That language is stale after `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`.
- Several active files lack a `Date:` line in the first 25 lines. This is not a runtime defect, but it makes authority sorting slower and should be fixed when those files are next touched.
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/` contains older April 28 reports that remain active only as evidence unless a current index promotes them.
- Root `.log` files were raw evidence artifacts and have now been moved out of repository root.

## Changes Made In This Pass

- Added this sorting authority map.
- Added `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md` as the current structural cleanup queue.
- Updated active indexes to point at this file as the current documentation sorting layer.
- Updated governance/root reference wording so the current authority stack includes warning cleanup and foundation guard repair before older state anchors.
- Updated the Archivarius authority classification so it no longer presents the pre-repair guard failure as current.

## 2026-05-05 Addendum

- Promoted `Docs/Reports/2026-05-05_OMEGA_AUTONOMY_FORENSIC_HARDENING.md` into the current first-read report stack for bounded Omega-autonomy verification.
- Current Omega build boundary is now `CodexArtifacts/dotnet-h8core-omega-autonomy-doc-continuation-build.log`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`. The last warning-bearing Omega baseline remains `CodexArtifacts/dotnet-h8core-omega-autonomy-current-build5.log`: `Build succeeded`, `48 Warning(s)`, `0 Error(s)`, with warnings owned by dependency/vendor packages and `0` first-party `Assets/_Project/Scripts` matches.
- Older warning-bearing artifact logs remain historical evidence only unless their exact build is rerun and promoted by a current report.
- Updated active reference-bundle boundary headers/read-first lines that still pointed at `2026-05-02` as the current-state boundary, and updated current Archivarius `01_GENERAL_INFO` read-first lines to include this sorting map.
- Moved seven repository-root `.log` files to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/`.
- Added a local README for that raw-evidence bundle and updated active root/docs references.
- Completed header normalization for active root `Docs/*.md` contract files and `Docs/ARCHITECTURE/*.md`.
- Completed header normalization for `60` files under `Docs/ARCHIVARIUS REPORTS/`.
- Completed header normalization for current `Docs/SPACE_ENGINE_RESEARCH/*.md` files.
- Added an editor-only documentation authority smoke guard at `Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs`.
- Added decomposition, stress, and telemetry-warning hooks to the editor-only documentation authority smoke guard, then normalized the new active SpaceEngine Omega audit header so the active missing-`Status:` count returned to `0`.
- Added the CI-facing documentation authority batch entrypoint `RunBatchAll`; direct Core/Editor Roslyn compile passed, but Unity batch execution is still blocked by licensing/project-lock environment failures and did not emit batch JSON.
- Expanded the relocated-root-log counter to scan every `Root_Logs_*` bundle and moved two newly generated root logs into the 2026-05-05 deprecated evidence bundle.
- Archivarius reality sync added `Docs/Reports/2026-05-05_ARCHIVARIUS_REALITY_DELTA.md` as the current SpaceEngine/Sandbox/source-count/naming/interface delta.
- Archivarius naming sync added `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NAMING_VIOLATIONS.md` as the current active non-ASCII path/content ledger.
- Current measured post-sync counts: `Docs/**/*.md` total `422`, active docs excluding archive/deprecated/obsolete `209`, active non-report docs excluding archive/deprecated/obsolete and `Docs/Reports` `162`, `Docs/Reports/*.md` `47`.
- SpaceEngine research and Planetary Sandbox logs are primary authority for terrain math/shelf work before old 2026-04-29 reports.
- Old `2026-04-29` reports and patch artifacts are historical unless a current May 5/May 4 authority file explicitly promotes a narrow claim.

No gameplay/runtime files, scenes, prefabs, project settings, packages, or assets were changed by this sorting pass. The only source changes are the editor-only smoke guard and compile-proof fixes in adjacent dirty smoke/import code.

## Do Not Claim

- Do not claim documentation is fully clean. This pass maps authority and updates the active indexes; it does not normalize every old file header. Current active header debt is `41`.
- Do not claim archive/deprecated content has been revalidated.
- Do not claim Play Mode stability, zero-GC, frame time, memory retention, or player-build readiness from documentation sorting.
- Do not claim Unity batch verification for documentation authority until `RunBatchAll` emits the batch JSON artifact from Unity with exit `0`.
- Do not cite root logs as current evidence unless a dated report summarizes the exact command and result.

## Regression Model

CPU: documentation-only change. No runtime code path touched.

GC: no gameplay code changed. Measured `0 B/frame` proof is absent.

Memory: no assets, textures, native allocations, Addressables groups, scenes, or project settings changed.

Cadence: no tick, dispatcher, bootstrap, scene transition, or load cadence changed.

Correctness: authority routing is clearer. Risk remains if a stale document is cited without reading supersession notes.

## Failure Modes

- Dirty worktree changes after this pass can invalidate source/build/guard claims.
- Future generated reports can overwrite dated scan files.
- A stale document can still be found by direct search and misused if the authority stack is ignored.
- Root log relocation is complete. Physical moves still need a separate clean-tree pass if old active reports must be relocated.

STATUS: PENDING VERIFICATION
