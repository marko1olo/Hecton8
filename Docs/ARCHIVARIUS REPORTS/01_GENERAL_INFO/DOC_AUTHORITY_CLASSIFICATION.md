# HECTON-8 Doc Authority Classification

Date: 2026-05-04
Status: PENDING VERIFICATION
Scope: active documentation importance and authority sorting across repository root and `Docs/`

Mandates followed:
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

This file answers one question: which documents are actually important now, and which are preserved only as history or evidence.

It is not runtime proof.
If this file disagrees with current source, source wins.
Latest sorting authority: `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`.
Latest header/archive cleanup queue: `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`.

## 1. Read First / Current Authority

These files are the current navigation spine.

| Rank | File | Authority class | Why it matters |
|---:|---|---|---|
| 1 | `AGENTS.md` | PRIMARY OPERATING CONTRACT | Defines current rules, architecture constraints, verification discipline, and rejection conditions. |
| 2 | `.agents-skills/*` | PRIMARY MANDATES | Task-specific rules. Must be read selectively before code or technical reports. |
| 3 | `Docs/README.md` | PRIMARY DOC ENTRY | Current doc entry point and archive/deprecated routing. |
| 4 | `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` | PRIMARY DOC SORTING MAP | Latest root/`Docs` sorting map, first-read report order, and archive/deprecated boundary. |
| 5 | `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md` | PRIMARY DOC CLEANUP QUEUE | Current structural cleanup queue for missing headers, relocated root logs, and archive candidates. |
| 6 | `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` | PRIMARY DOCSET ACTUALITY | Latest root/`Docs` read-pass, source-count sync, build/guard/MCP boundary, and pre-repair guard-failure history. |
| 7 | `Docs/Reports/2026-05-04_WARNING_CLEANUP.md` | PRIMARY WARNING CLEANUP ADDENDUM | Latest first-party warning cleanup and post-refresh Unity console readback boundary. |
| 8 | `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` | PRIMARY FOUNDATION GUARD ADDENDUM | Latest guard-clean source/build addendum; foundation guard scan exits `0`. |
| 9 | `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` | PRIMARY CURRENT STATE | Stable current-state anchor; now carries May 4 addendum and still refuses runtime-certification claims. |
| 10 | `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` | PRIMARY TERRAIN/BIOME REPORT | Canonical terrain/biome report; root duplicate is not authority. |
| 11 | `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` | PRIMARY PROJECT VERDICT | Blunt source/doc-backed project-level conclusion; explicitly not runtime proof. |
| 12 | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md` | PRIMARY WORKSPACE ATLAS | Current workspace orientation, active maps, and source-count deltas. |
| 13 | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md` | PRIMARY CONCEPT MAP | Current load-bearing/transitional/presentation/experimental system classification. |
| 14 | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOCSET_COVERAGE_MATRIX.md` | PRIMARY COVERAGE MAP | Domain-by-domain map of which docs to trust first. |
| 15 | `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/02_SYSTEM_REALITY_MATRIX.md` | PRIMARY RISK MATRIX | Historical broad subsystem reality map; read after current May 4/current-state anchors. |
| 16 | `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md` | PRIMARY ACTION QUEUE | Historical high-risk work queue and verification caveats; recheck against current reports before use. |

## 2. New / High-Value Reports

These are new enough and important enough to stay active.

| File | Authority class | Keep active? | Notes |
|---|---|---|---|
| `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` | PRIMARY DOC SORTING MAP | yes | Latest documentation sorting map; root text handling, active bundle classes, and first-read report order. |
| `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md` | PRIMARY DOC CLEANUP QUEUE | yes | Current structural cleanup queue: missing headers, relocated root evidence logs, and archive candidates. |
| `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` | PRIMARY DOCSET ACTUALITY | yes | Latest documentation read-pass, current source/doc counts, Core/Editor/DOTS/PlayModeTests compile evidence, MCP readback, and pre-repair guard-failure history. |
| `Docs/Reports/2026-05-04_WARNING_CLEANUP.md` | PRIMARY WARNING CLEANUP ADDENDUM | yes | Latest first-party warning cleanup and post-refresh Unity console readback boundary. |
| `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` | PRIMARY FOUNDATION GUARD ADDENDUM | yes | Latest guard-clean source/build addendum; foundation guard scan exits `0`. |
| `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` | PRIMARY CURRENT STATE | yes | Stable conceptual system-shape report; current path now includes May 4 evidence. |
| `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` | PRIMARY TERRAIN/BIOME REPORT | yes | Canonical terrain/biome report; root duplicate is not authority. |
| `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md` | ACTIVE CELESTIAL REPORT | yes | Source/build/controlled-console evidence only; no PlayMode visual/audio/profiler proof. |
| `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md` | ACTIVE CELESTIAL REPORT | yes | Source/build evidence only; no PlayMode smoke/profiler proof. |
| `Docs/Reports/2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md` | ACTIVE CELESTIAL REPORT | yes | Historical task evidence; superseded by May 4 sweep for global current-state truth. |
| `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` | HISTORICAL DOCSET ACTUALITY | yes | Previous documentation read-pass and May 2 build evidence. Read after May 4 sweep. |
| `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` | PRIMARY PROJECT VERDICT | yes | Honest project-level verdict after current docs/source scan; not Play Mode proof. |
| `Docs/Reports/2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md` | ACTIVE COMPILE DELTA | yes | Latest editor compile/MCP console evidence for Sargassum/Emergency relay listener migration and spatial-hash fix. |
| `Docs/Reports/2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md` | ACTIVE COMPILE DELTA | yes | Supersedes latest compile line numbers after `VegetationJobRecovery.cs.meta` restoration; records Bee/backend recovery and final MCP console zero-entry check. |
| `Docs/Reports/DOOMSDAY_FLAW_REPORT.md` | ACTIVE HIGH-RISK REPORT | yes | Current concurrency/headless/memory/raycast/event-risk map. |
| `Docs/Reports/AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md` | ACTIVE SURGERY REPORT | yes | Current coroutine/pool/telemetry surgery state; not runtime proof. |
| `Docs/Reports/OMEGA_CORE_ENFORCEMENT_2026-05-01.md` | ACTIVE COMPLIANCE REPORT WITH CAUTION | yes | Important because it rejects fake `MCP VERIFIED` status and records remaining compile/console limits. |
| `Docs/Reports/TOTAL_CODEBASE_AUDIT_V2.md` | ACTIVE STATIC AUDIT | yes | Broad static audit hit list; source-review level only. |
| `Docs/Reports/CI_VALIDATION_HOOKS_SURGERY_LOG.md` | ACTIVE SECONDARY REPORT | yes | Validator implementation notes; read after higher reports. |
| `Docs/Reports/NAVGRID_LEAK_PURGE_SURGERY_LOG.md` | ACTIVE SECONDARY REPORT | yes | Narrow surgery log; useful only for navgrid/leak context. |
| `Docs/Reports/OMEGA_PURGE_SURGERY_LOG.md` | ACTIVE SECONDARY REPORT | yes | Narrow cleanup history; not a project-state entry point. |
| `Docs/Reports/GC_SINGLETON_KILL_LIST.md` | ACTIVE SECONDARY LEDGER | yes | Useful singleton/debt ledger; not current-state authority by itself. |

## 3. Evidence Artifacts, Not Narrative Authority

These should remain near active reports, but they are not meant to be read first.

| File | Authority class | Handling |
|---|---|---|
| `Docs/Reports/2026-04-29_Habitat_Logistics_Graph_Diff.patch` | PATCH ARTIFACT | Keep as evidence next to related report/history. |
| `Docs/Reports/NAVGRID_LEAK_PURGE_DIFF.patch` | PATCH ARTIFACT | Keep as evidence next to `NAVGRID_LEAK_PURGE_SURGERY_LOG.md`. |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/*.patch` | PATCH ARTIFACTS | Evidence only; paired reports carry narrative authority. |
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/vram_detail.csv` | DATA ARTIFACT | Raw data, not prose authority. |

## 4. Active Reference Bundles

These are important, but they are not "latest project state" reports.

| Area | Entry point | Authority class |
|---|---|---|
| Concept-level system authority | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md` | PRIMARY CONCEPT MAP |
| Runtime architecture | `Docs/ARCHITECTURE/` | REFERENCE |
| Procedural asset pipeline | `Docs/PROCEDURAL_ASSET_PIPELINE.md` | ACTIVE CONTRACT |
| Procedural world categories | `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` | ACTIVE CONTRACT |
| Flora | `Docs/Flora_Pipeline/README.md` | REFERENCE / WORKING BUNDLE |
| Scatter | `Docs/Scatter_Runtime/README.md` | REFERENCE / WORKING BUNDLE |
| Fauna planning | `Docs/AI_Fauna/README.md` | REFERENCE |
| Legacy world lore | `Docs/Legacy_World_Reference/README.md` | LEGACY REFERENCE |
| Legacy backlog | `Docs/Legacy_Backlog/README.md` | LEGACY REFERENCE |

## 5. Deprecated / Do Not Use As Current Truth

These are preserved, not active.

| Folder | Authority class | Reason |
|---|---|---|
| `Docs/DEPRECATED/2026-04-29_Audit_Bundles/` | DEPRECATED SNAPSHOT | Older counts/findings superseded by later reports. |
| `Docs/DEPRECATED/External_And_Log_Bundles/` | DEPRECATED SOURCE MATERIAL | Prompt dumps, copied references, raw logs. |
| `Docs/DEPRECATED/Root_Redirect_Stubs_2026-05-01/` | DEPRECATED COMPATIBILITY STUBS | Old flat redirects; canonical bundle paths are active. |
| `Docs/DEPRECATED/Encoding_Damaged_2026-05-01/` | DEPRECATED DAMAGED TEXT | Preserved because content is encoding-damaged. |
| `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/` | DEPRECATED ROOT ARTIFACTS | Old root docs/scans moved out of active root. |
| `Docs/Reports/DEPRECATED/2026-04-29_Static_Audit_Snapshots/` | DEPRECATED REPORT SNAPSHOTS | Early loose static reports with stale counts. |
| `Docs/_Archive/` | ARCHIVE | Historical work packages; do not treat as current unless explicitly promoted. |

## 6. Root Text Surface

Current root text files after cleanup:

| File | Authority class |
|---|---|
| `AGENTS.md` | PRIMARY OPERATING CONTRACT |
| `MASTER_RELEASE_WORK_PLAN.md` | ACTIVE ROADMAP |
| `BUILD_PLAYTEST_ISSUES.md` | ACTIVE QA LEDGER |
| `TERRAIN_AND_BIOME_REALITY_MAP.md` | NON-CANONICAL ROOT MIRROR |

Canonical terrain/biome authority is `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.
Legacy root text/scanner artifacts were moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/`.
Former repository-root logs were moved to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/`.

## 7. Practical Rule

When a task asks "what is current":

1. Read `AGENTS.md`.
2. Read relevant `.agents-skills/` mandates.
3. Read `Docs/README.md`.
4. Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`.
5. Read `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`.
6. Read `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`.
7. Read `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`.
8. Read `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`.
9. Read `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
10. Read `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`.
11. Read `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`.
12. Read domain-specific current maps.
13. Open source files.

Do not start from archive, deprecated folders, copied external prompts, patch files, or old root artifacts.

STATUS: PENDING VERIFICATION
