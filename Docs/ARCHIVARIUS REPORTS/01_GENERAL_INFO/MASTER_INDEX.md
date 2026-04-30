# HECTON-8 ARCHIVARIUS MASTER INDEX

**Date:** 2026-04-30  
**Status:** PENDING VERIFICATION  
**Scope:** `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO` + `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS`

---

## Purpose

This index lists the documents that physically exist in folders `01_GENERAL_INFO` and `02_ACTUAL_REPORTS` as of 2026-04-30.

It replaces stale references to renamed, moved, or obsolete reports.

This index is path-accurate, not truth-uniform.

- files dated `2026-04-28_*` inside `02_ACTUAL_REPORTS` still physically exist in the active folder
- several of those dated bundles are now historical static snapshots, not the preferred current-state authority
- for current-source corrections and current editor-state caveats, prefer `2026-04-30_ARCHIVARIUS_CONTINUATION_REVERIFICATION.md` first

## Coverage Snapshot

| Bucket | Count |
|---|---:|
| `01_GENERAL_INFO` markdown files | 22 |
| `02_ACTUAL_REPORTS` markdown files | 46 |
| `02_ACTUAL_REPORTS` CSV datasets | 1 |
| `02_ACTUAL_REPORTS` patch artifacts | 6 |
| **Total indexed docs/datasets** | **69** |
| **Total physical non-meta files in folders `01` and `02`** | **75** |

Patch artifacts in `02_ACTUAL_REPORTS` physically exist but are not current narrative authority by themselves.
Read the paired surgery log or audit note before treating a `.patch` file as implementation truth.

---

## 01. General Info

| File | Role |
|---|---|
| `ASSET_DEPENDENCY_MAP.md` | General asset-reference map and migration notes |
| `AUP_SURGERY_MAP.md` | AUP layout and migration planning |
| `BUILD_DEPENDENCY_GRAPH.md` | Bootstrap dependency and cold-load audit |
| `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md` | Current construction, habitat, logistics, and power ownership map |
| `DEPENDENCY_GRAPH.md` | Runtime service and dependency overview |
| `DOCSET_COVERAGE_MATRIX.md` | Domain-by-domain authority map and coverage-gap ledger |
| `EVENT_BUS_MAP.md` | Historical event-bus map with chronology caveat |
| `GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md` | Broad owner ledger across major gameplay domains |
| `GLOSSARY.md` | Shared terminology |
| `HUD_EDITOR_SPEC.md` | HUD editor/layout spec |
| `INTERFACE_CONTRACT_TABLE.md` | Verified interface-to-implementor table |
| `INTERFACE_STRATEGY.md` | Interface cleanup and ownership strategy |
| `MASTER_INDEX.md` | This file |
| `NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md` | Detailed ownership map for narrative, discovery, lore, Atlas progression, and PDA knowledge systems |
| `PLAYER_GAMEPLAY_CORE_MAP.md` | Current player-facing gameplay ownership map |
| `PROJECT_ATLAS.md` | Live workspace atlas rebuilt against current file-system state |
| `STRUCTURAL_NARRATIVE.md` | One-frame code-walk narrative; not a measured profiler report |
| `SURVIVAL_DAMAGE_HAZARD_SYSTEM_MAP.md` | Detailed ownership map for survival, pressure, thermal stress, hazard routing, parallel health semantics, and downstream stress consequences |
| `SYSTEM_INTERCONNECT_MATRIX.md` | AUP-sensitive interconnect matrix |
| `TOOLS_INTERACTION_OPERATIONAL_SYSTEM_MAP.md` | Detailed ownership map for player tools, interaction routing, scanner/cutter/repair/beacon branches, and adjacent operational save surfaces |
| `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` | Detailed ownership map for HUD, visor, PDA, audio, and presentation-layer runtime surfaces |
| `WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md` | Focused map for world, environment, debris, ocean, thermal, and submarine runtime owners |

---

## 02. Actual Reports

### Runtime / Architecture / Interfaces

| File | Role |
|---|---|
| `EVENT_FLOW_MAP.md` | Source-backed event topology for current first-party code |
| `INTERFACE_HEALTH_DASHBOARD.md` | Interface health summary corrected against live code |
| `SINGLETON_FIX_PRIORITY.md` | Singleton remediation roadmap |
| `SINGLETON_VIOLATIONS.md` | Singleton violation inventory |
| `GOD_OBJECT_AUDIT.md` | Large-owner decomposition audit |
| `HARD_LINK_DEBT.md` | Hardcoded asset-path debt audit |
| `FRAME_TIMELINE.md` | Frame sequencing report |
| `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md` | Current split-bootstrap authority map across `BootstrapController`, `GameBootstrapper`, and `SceneBootstrap` |
| `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md` | Authored-vs-runtime service ownership truth |
| `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md` | Current save/load pipeline truth |

### Performance / Memory / VRAM / Rendering

| File | Role |
|---|---|
| `2026-04-28_VRAM_BUDGET_AUDIT.md` | Detailed dated VRAM audit |
| `VRAM_BUDGET_AUDIT.md` | Sanitized VRAM budget summary |
| `2026-04-28_VRAM_EXECUTION_LIST.md` | VRAM remediation queue |
| `SCENE_VRAM_FOOTPRINT.md` | Scene VRAM estimate |
| `RENDERGRAPH_AUDIT.md` | RenderGraph lifetime audit |
| `COMPUTE_BUFFER_AUDIT.md` | GraphicsBuffer / ComputeBuffer lifecycle audit |
| `2026-04-28_MEMORY_ALIGNMENT_FIX.md` | Struct alignment remediation notes |
| `2026-04-28_PROFILING_PREPAREDNESS_AUDIT.md` | Source-backed profiler-marker readiness snapshot |

### Data / Assets / Content

| File | Role |
|---|---|
| `2026-04-28_DATA_DICTIONARY.md` | Structs, layouts, alignment findings |
| `2026-04-28_ASSET_DEPENDENCY_MAP.md` | Dated asset dependency report |
| `ITEM_ASSET_GUIDS.md` | Item prefab GUID lookup table |
| `PROJECT_CONTENT_LEDGER.md` | Project content and asset-ledger snapshot |
| `2026-04-28_DEAD_ASSET_SWEEP.md` | Filesystem-only dead-asset candidate sweep with deletion claims downgraded |
| `DEAD_CODE_GRAVEYARD.md` | Dead-code registry |
| `vram_detail.csv` | Raw VRAM detail dataset backing the budget audit |

### Forensics / Compliance / Deep Audit

| File | Role |
|---|---|
| `2026-04-28_SUPREME_AUDITOR_REPORT.md` | Large dated static audit bundle; historical unless revalidated against 2026-04-29 source truth |
| `2026-04-28_DEEP_FORENSIC_AUDIT.md` | Dated deep static archaeology snapshot; contains stale counts and stale ownership conclusions |
| `2026-04-28_ETA2_SUPREME_SUMMARY.md` | Dated ETA2 summary snapshot; partially superseded by 2026-04-29 reverification |
| `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md` | This reverification pass over folders `01` and `02` |
| `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION_ADDENDUM.md` | Trust-boundary note clarifying that the 2026-04-29 reverification layer is no longer the newest active anchor |
| `2026-04-30_ARCHIVARIUS_CONTINUATION_REVERIFICATION.md` | Current continuation log for the next-day active docset expansion |
| `2026-04-30_EDITOR_RUNTIME_FORENSICS.md` | Current live-console and source-backed forensic follow-up for editor/runtime debt |
| `2026-04-30_SERVICE_AUTHORITY_DRIFT.md` | Current source-backed audit of mixed singleton, `DontDestroyOnLoad`, and `GlobalRegistry` service authority |
| `2026-04-30_PERSISTENCE_AND_SCENE_SEARCH_DRIFT.md` | Current source-backed audit of runtime persistence surfaces outside `SaveManager` and runtime scene-search fallback debt |
| `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md` | Detailed runtime authority matrix for `GlobalRegistry` publishers and bootstrap fallback coverage |
| `2026-04-30_SAVE_PARTICIPANT_LEDGER.md` | Broad `ISaveable` participant ledger and observed priority-band map |
| `2026-04-28_LIAR_DETECTION.md` | Claim-vs-code liar detection |
| `2026-04-28_HOT_PATH_VIOLATIONS.md` | Current-source hot-path and architecture debt snapshot |
| `2026-04-28_EVENT_LEAK_REPORT.md` | Current-source `HectonEventBus` subscription hygiene snapshot |
| `2026-04-28_CIRCULAR_DEPS.md` | Current asmdef dependency snapshot |
| `2026-04-28_CYRILLIC_SWEEP.md` | Measured non-ASCII asset-path sweep |
| `AGENTS_SKILLS_AUDIT.md` | `.agents-skills` coverage audit |
| `AUDIO_ROUTING_AUDIT.md` | Audio routing ownership audit |
| `DEBUG_LOG_DELETION_QUEUE.md` | Debug log cleanup queue |
| `MODULAR_EQUIPMENT_ENGINE_SURGERY_LOG.md` | Narrow equipment-runtime surgery log |
| `2026-04-29_EQUIPMENT_VOXEL_WELD_SURGERY_LOG.md` | Narrow equipment weld/runtime repair log |
| `2026-04-29_LOGIC_SPANNER_OVERCHARGE_SURGERY_LOG.md` | Narrow logic-spanner repair log |

---

## Confirmed Documentation Defects In The Previous Index

| Area | Confirmed defect |
|---|---|
| Old master index | Referenced many files that do not exist in folders `01_GENERAL_INFO` or `02_ACTUAL_REPORTS` |
| Legacy paths | Referred to `Assets/DOCS/AGENT_*` and other historical locations that are not live sources in the current workspace |
| Obsolete names | Used names such as `SUPREME_AUDITOR_CONTINUOUS_REPORT.md`, `DEEP_FORENSIC_AUDIT_REPORT.md`, `GLOBAL_ARCHITECTURE_MAP.md`, `ETA2_CIRCULAR_DEPS.md`, and `DEAD_ASSET_SWEEP_REPORT.md` without path proof |

---

## Obsolete Reference Policy

If a report name appears in older documents but is not listed above, treat it as one of:

1. Renamed into the dated `2026-04-28_*` report set.
2. Moved to `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE`.
3. Deleted or never created in the current workspace snapshot.

Do not trust stale references until the path is revalidated.
