# HECTON-8 Doc Authority Classification

Date: 2026-05-15
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
Latest documentation/status override: `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.
Latest root/current-index cleanup: `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md`.
Latest historical machine-readable active manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`; counters and build-state are not current authority.
Latest `.agents-skills` visual-fake doctrine: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
Latest sorting authority: `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, amended by later synchronization passes.
Latest header state: May 11 manifest reports active markdown header debt `0` missing `Date:`, `0` missing `Status:`.
Latest root cleanup: `Docs/ROOT_DOCS_REFERENCE.md` and `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md`.

## 1. Read First / Current Authority

These files are the current navigation spine. Stable authority comes before dated evidence.

| Rank | File | Authority class | Why it matters |
|---:|---|---|---|
| 1 | `AGENTS.md` | PRIMARY OPERATING CONTRACT | Defines current rules, architecture constraints, verification discipline, and rejection conditions. |
| 2 | `.agents-skills/README.md` | PRIMARY MANDATE INDEX | Defines mandate registry buckets, read rule, and conflict resolution. |
| 3 | `.agents-skills/*` | PRIMARY MANDATES | Task-specific rules. Must be read selectively before code or technical reports. |
| 4 | `Docs/README.md` | PRIMARY DOC ENTRY | Current doc entry point and archive/deprecated routing. |
| 5 | `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` | PRIMARY ARCHITECTURE MAP | Whole-project architecture map and current authority boundary. |
| 6 | `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md` | PRIMARY RUNTIME PLAN | Runtime execution order, system ownership, and verification boundary. |
| 7 | `Docs/SYSTEMS_CONTRACTS.md` | PRIMARY SYSTEM CONTRACTS | Stable non-asset system contracts. |
| 8 | `Docs/QUALITY_GATES.md` | PRIMARY ACCEPTANCE GATES | Required proof gates before readiness claims. |
| 9 | `Docs/ARCHITECTURE/README.md` | PRIMARY ARCHITECTURE INDEX | Stable architecture pack index and visual-fake-first read order. |
| 10 | `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` | PRIMARY CINEMATIC-CHEAT LEDGER | Stable visual-realistic-fake doctrine and physical-simulation rejection gate. |
| 11 | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md` | PRIMARY ARCHIVARIUS INDEX | Stable local orientation and trust boundary. |
| 12 | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md` | PRIMARY WORKSPACE ATLAS | Current workspace orientation, active maps, and source-count deltas. |
| 13 | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md` | PRIMARY CONCEPT MAP | Current load-bearing/transitional/presentation/experimental system classification. |
| 14 | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOCSET_COVERAGE_MATRIX.md` | PRIMARY COVERAGE MAP | Domain-by-domain map of which docs to trust first. |
| 15 | `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md` | REPORT VAULT BOUNDARY | Confirms `02_ACTUAL_REPORTS` is evidence, not direct authority. |
| 16 | `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` | CURRENT STATIC OVERRIDE | Latest broad documentation/status correction boundary found in this pass; demotes missing May 11 artifacts and stale counters. |
| 17 | `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md` | CURRENT INDEX/ROOT CLEANUP | Current navigation honesty and root-cleanup evidence; static/filesystem only. |
| 18 | `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` | HISTORICAL EVIDENCE | May 11 counters and compile-only boundary; historical where May 13/May 15 conflicts. |
| 19 | `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` | HISTORICAL MACHINE-READABLE DOC MANIFEST | May 11 active markdown inventory snapshot; data artifact, not current runtime proof. |
| 20 | `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` | MANDATE EVIDENCE | Supporting audit for visual-fake-first promotion; stable mandates carry the rule. |
| 21 | `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` | DOC SORTING EVIDENCE | Sorting report evidence; lower authority than stable docs after later synchronization passes. |
| 22 | `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` | CONCEPTUAL STATE EVIDENCE | Historical/stable conceptual state anchor; still refuses runtime-certification claims. |
| 23 | `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NAMING_VIOLATIONS.md` | NAMING LEDGER | Current non-ASCII path/content inventory and English replacement proposals. |
| 24 | `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md` | DOMAIN REFERENCE | SpaceEngine 0.9.8 terrain math integration evidence; use after stable authority files. |
| 25 | `Docs/SPACE_ENGINE_RESEARCH/TERRAIN_AND_NOISE_098.md` | DOMAIN REFERENCE | Terrain/noise research extraction; adapt through HECTON mandates only. |

## 2. New / High-Value Reports

These are new enough and important enough to stay active.

| File | Authority class | Keep active? | Notes |
|---|---|---|---|
| `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` | CURRENT STATIC OVERRIDE | yes | Current broad documentation/status correction boundary for stale counters, missing artifacts, and compile-evidence labels. |
| `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md` | CURRENT INDEX/ROOT CLEANUP | yes | Current navigation honesty and root cleanup evidence; static/filesystem only. |
| `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` | HISTORICAL DOC/DATA CONTINUATION | yes | May 11 counters and compile-only boundary; historical where May 13/May 15 conflicts. |
| `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` | HISTORICAL MACHINE-READABLE DOC MANIFEST | yes | May 11 active markdown inventory snapshot with parsed `Date` and `Status`; data artifact only. |
| `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` | PRIMARY MANDATE DOCTRINE | yes | Current visual-fake-first mandate correction layer. |
| `Docs/Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md` | PREVIOUS DOC SYNCHRONIZATION | yes | Previous R186 sync; historical where May 11 data supersedes it. |
| `Docs/Reports/2026-05-08_ACTIVE_DOCUMENTATION_MANIFEST.json` | PREVIOUS MACHINE-READABLE DOC MANIFEST | yes | Previous manifest; historical where May 11 manifest supersedes it. |
| `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` | PRIMARY DOC SORTING MAP | yes | Latest documentation sorting map; root text handling, active bundle classes, and first-read report order. |
| `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md` | PRIMARY DOC CLEANUP QUEUE | yes | Structural cleanup queue: relocated root evidence logs and archive candidates; active missing-header debt is now `0`. |
| `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` | HISTORICAL DOCSET ACTUALITY | yes | Historical broad read-pass; current status/counter/root boundary starts at May 13/May 15. |
| `Docs/Reports/2026-05-04_WARNING_CLEANUP.md` | PRIMARY WARNING CLEANUP ADDENDUM | yes | Latest first-party warning cleanup and post-refresh Unity console readback boundary. |
| `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` | PRIMARY FOUNDATION GUARD ADDENDUM | yes | Latest guard-clean source/build addendum; foundation guard scan exits `0`. |
| `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` | CONCEPTUAL EVIDENCE | yes | Dated system-shape evidence retained for reference; stable authority now starts at `AGENTS.md`, `.agents-skills/README.md`, and stable `Docs/*.md` files. |
| `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` | PRIMARY TERRAIN/BIOME REPORT | yes | Canonical terrain/biome report; former root duplicate moved to `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`. |
| `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md` | ACTIVE CELESTIAL REPORT | yes | Source/build/controlled-console evidence only; no PlayMode visual/audio/profiler proof. |
| `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md` | ACTIVE CELESTIAL REPORT | yes | Source/build evidence only; no PlayMode smoke/profiler proof. |
| `Docs/Reports/2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md` | ACTIVE CELESTIAL REPORT | yes | Historical task evidence; superseded by May 4 sweep for global current-state truth. |
| `Docs/Reports/2026-05-04_HYDRAULIC_EROSION_ENGINE_SURGERY_LOG.md` | ACTIVE WORLD IMPLEMENTATION REPORT | yes | Hydraulic erosion source/surgery report; Unity import/compile, MapMagic execution, harness output, GCMonitor, and profiler proof remain pending. |
| `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md` | PRIMARY SPACEENGINE INTEGRATION | yes | Current SpaceEngine 0.9.8 terrain math integration and Burst kernel evidence. |
| `Docs/SPACE_ENGINE_RESEARCH/TERRAIN_AND_NOISE_098.md` | PRIMARY SPACEENGINE RESEARCH | yes | Current extracted SpaceEngine terrain/noise research; use before adapting SpaceEngine shape language. |
| `Docs/Reports/2026-05-05_HECTON_SANDBOX_BIOMES_OMEGA_SURGERY_LOG.md` | PRIMARY PLANETARY SANDBOX | yes | Current macro shelf and AUP sandbox terrain report. |
| `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NAMING_VIOLATIONS.md` | PRIMARY NAMING LEDGER | yes | Current non-ASCII path/content inventory and replacement queue. |
| `Docs/Reports/2026-05-05_ARCHIVARIUS_REALITY_DELTA.md` | PRIMARY REALITY DELTA | yes | Current Archivarius delta and untracked inventory snapshot. |
| `Docs/Reports/2026-05-15_COMPUTE_AUDIT/README.md` | ACTIVE COMPUTE AUDIT BUNDLE | yes | Same-day compute report slices moved out of repository root; static/report evidence only. |
| `Docs/Reports/2026-05-13_BROKEN_PREFABS_STATIC_SNAPSHOT.md` | GENERATED STATIC SNAPSHOT | yes | Former root generated prefab snapshot; not Unity import, Console, Play Mode, or player-build proof. |
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
| `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/` | DEPRECATED ROOT MIRRORS | Former root compatibility mirrors moved after canonical Docs paths existed. |
| `Docs/DEPRECATED/External_And_Log_Bundles/Root_Evidence_2026-05-15/` | DEPRECATED ROOT EVIDENCE | Former root logs, JSON/XML/PNG/zip, and stale cleanup script. |
| `Docs/Reports/DEPRECATED/2026-04-29_Static_Audit_Snapshots/` | DEPRECATED REPORT SNAPSHOTS | Early loose static reports with stale counts. |
| `Docs/_Archive/` | ARCHIVE | Historical work packages; do not treat as current unless explicitly promoted. |

## 5.1 Historical 2026-04-29 Handling

All 2026-04-29 reports and patch artifacts are historical unless a current May 5 or May 4 authority file explicitly promotes a narrow claim.

| Pattern | Authority class | Current handling |
|---|---|---|
| `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-29_*.md` | HISTORICAL EVIDENCE | Preserve for provenance; do not use as current runtime/source truth without a May 5/May 4 supersession note. |
| `Docs/Reports/2026-04-29_*.patch` | HISTORICAL PATCH ARTIFACT | Evidence only; paired current report or source diff must carry authority. |
| `Docs/DEPRECATED/2026-04-29_Audit_Bundles/` | DEPRECATED SNAPSHOT | Superseded by May 5/May 4 counts, interface scan, naming sweep, and source inventory. |
| `Docs/Reports/DEPRECATED/2026-04-29_Static_Audit_Snapshots/` | DEPRECATED REPORT SNAPSHOTS | Do not cite for current file counts, interface ownership, AUP compliance, or Zero-GC status. |

## 6. Root Text Surface

Current root text files after cleanup:

| File | Authority class |
|---|---|
| `AGENTS.md` | PRIMARY OPERATING CONTRACT |
| `MASTER_RELEASE_WORK_PLAN.md` | ACTIVE ROADMAP |
| `BUILD_PLAYTEST_ISSUES.md` | ACTIVE QA LEDGER |

Canonical terrain/biome authority is `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.
Legacy root text/scanner artifacts were moved to `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/`.
Former repository-root logs were moved to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Logs_2026-05-04/`.
May 15 root evidence/log/artifact spill moved to `Docs/DEPRECATED/External_And_Log_Bundles/Root_Evidence_2026-05-15/`.
Former root compatibility mirrors moved to `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`.

## 7. Practical Rule

When a task asks "what is current":

1. Read `AGENTS.md`.
2. Read relevant `.agents-skills/` mandates.
3. Read `Docs/README.md`.
4. Read `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.
5. Read `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md`.
6. Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`.
7. Read `Docs/Reports/2026-05-04_DOCUMENTATION_HEADER_ARCHIVE_QUEUE.md`.
8. Read `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`.
9. Read `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`.
10. Read `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`.
11. Read `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.
12. Read `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md` for SpaceEngine terrain work.
13. Read `Docs/Reports/2026-05-05_HECTON_SANDBOX_BIOMES_OMEGA_SURGERY_LOG.md` for Planetary Sandbox terrain work.
14. Read `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`.
15. Read `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`.
16. Read domain-specific current maps.
17. Open source files.

Do not start from archive, deprecated folders, copied external prompts, patch files, or old root artifacts.

STATUS: PENDING VERIFICATION
