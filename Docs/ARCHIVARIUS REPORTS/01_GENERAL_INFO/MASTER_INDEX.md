# HECTON-8 ARCHIVARIUS MASTER INDEX

**Date:** 2026-04-29  
**Status:** PENDING VERIFICATION  
**Scope:** `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO` + `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS`

---

## Purpose

This index lists the documents that physically exist in folders `01_GENERAL_INFO` and `02_ACTUAL_REPORTS` as of 2026-04-29.

It replaces stale references to renamed, moved, or obsolete reports.

This index is path-accurate, not truth-uniform.

- files dated `2026-04-28_*` inside `02_ACTUAL_REPORTS` still physically exist in the active folder
- several of those dated bundles are now historical static snapshots, not the preferred current-state authority
- for current-source corrections and current editor-state caveats, prefer `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md` first

## Coverage Snapshot

| Bucket | Count |
|---|---:|
| `01_GENERAL_INFO` markdown files | 13 |
| `02_ACTUAL_REPORTS` markdown files | 32 |
| `02_ACTUAL_REPORTS` CSV datasets | 1 |
| **Total files covered by this index** | **46** |

---

## 01. General Info

| File | Role |
|---|---|
| `ASSET_DEPENDENCY_MAP.md` | General asset-reference map and migration notes |
| `AUP_SURGERY_MAP.md` | AUP layout and migration planning |
| `BUILD_DEPENDENCY_GRAPH.md` | Bootstrap dependency and cold-load audit |
| `DEPENDENCY_GRAPH.md` | Runtime service and dependency overview |
| `EVENT_BUS_MAP.md` | Historical event-bus map with chronology caveat |
| `GLOSSARY.md` | Shared terminology |
| `HUD_EDITOR_SPEC.md` | HUD editor/layout spec |
| `INTERFACE_CONTRACT_TABLE.md` | Verified interface-to-implementor table |
| `INTERFACE_STRATEGY.md` | Interface cleanup and ownership strategy |
| `MASTER_INDEX.md` | This file |
| `PROJECT_ATLAS.md` | Live workspace atlas rebuilt against current file-system state |
| `STRUCTURAL_NARRATIVE.md` | One-frame code-walk narrative; not a measured profiler report |
| `SYSTEM_INTERCONNECT_MATRIX.md` | AUP-sensitive interconnect matrix |

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
| `2026-04-28_LIAR_DETECTION.md` | Claim-vs-code liar detection |
| `2026-04-28_HOT_PATH_VIOLATIONS.md` | Current-source hot-path and architecture debt snapshot |
| `2026-04-28_EVENT_LEAK_REPORT.md` | Current-source `HectonEventBus` subscription hygiene snapshot |
| `2026-04-28_CIRCULAR_DEPS.md` | Current asmdef dependency snapshot |
| `2026-04-28_CYRILLIC_SWEEP.md` | Measured non-ASCII asset-path sweep |
| `AGENTS_SKILLS_AUDIT.md` | `.agents-skills` coverage audit |
| `AUDIO_ROUTING_AUDIT.md` | Audio routing ownership audit |
| `DEBUG_LOG_DELETION_QUEUE.md` | Debug log cleanup queue |

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
