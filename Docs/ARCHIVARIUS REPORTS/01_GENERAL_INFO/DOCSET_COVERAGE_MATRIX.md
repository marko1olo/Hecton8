# HECTON-8 DOCSET COVERAGE MATRIX

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: coverage map for active doc folders `01_GENERAL_INFO` and `02_ACTUAL_REPORTS`
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Purpose

This file answers four operational questions:

1. Which major project domains are already covered by active docs.
2. Which file should be treated as the first read for each domain.
3. Which files are historical snapshots versus current-source summaries.
4. Which coverage holes still remain.

It is a navigation and completeness tool.
It is not runtime proof.

## Coverage Model

Authority levels used below:

- `PRIMARY` = best current starting point in the active docset
- `SECONDARY` = useful support doc, narrower scope, or dated snapshot
- `HISTORICAL` = still useful evidence, but not preferred current authority
- `GAP` = no strong current-source authority yet

## Domain Coverage

| Domain | Current coverage | Primary files | Secondary files | Authority | Current gap |
|---|---|---|---|---|---|
| Workspace orientation | strong | `PROJECT_ATLAS.md`, `MASTER_INDEX.md` | `STRUCTURAL_NARRATIVE.md` | PRIMARY | none beyond drift risk |
| Docset health / trust boundaries | strong | `2026-04-30_ARCHIVARIUS_CONTINUATION_REVERIFICATION.md`, `MASTER_INDEX.md` | `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md`, this file | PRIMARY | older active bundles still need periodic downgrade checks |
| Bootstrap / init order | strong | `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md`, `BUILD_DEPENDENCY_GRAPH.md` | `DEPENDENCY_GRAPH.md`, `FRAME_TIMELINE.md` | PRIMARY | no live bootstrap branch traversal proof |
| Scene / prefab service-owner truth | medium | `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md` | `PROJECT_ATLAS.md`, `BUILD_DEPENDENCY_GRAPH.md` | PRIMARY | scan is bounded to current first-party scenes/prefabs, not all runtime states |
| GlobalRegistry / service ownership | strong | `INTERFACE_CONTRACT_TABLE.md`, `INTERFACE_HEALTH_DASHBOARD.md` | `INTERFACE_STRATEGY.md`, `DEPENDENCY_GRAPH.md`, `PROJECT_ATLAS.md` | PRIMARY | prefab/scene wiring proof still absent |
| Service authority drift / mixed singleton-registry ownership | medium | `2026-04-30_SERVICE_AUTHORITY_DRIFT.md` | `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md`, `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md`, `SINGLETON_VIOLATIONS.md` | PRIMARY | no live duplicate-registration or startup-regression replay |
| Interface truth / contract drift | strong | `INTERFACE_HEALTH_DASHBOARD.md` | `INTERFACE_CONTRACT_TABLE.md`, `INTERFACE_STRATEGY.md` | PRIMARY | no automated diff against future source drift |
| Event architecture | strong | `EVENT_FLOW_MAP.md` | `EVENT_BUS_MAP.md`, `FRAME_TIMELINE.md`, `2026-04-28_EVENT_LEAK_REPORT.md` | PRIMARY | full subscriber graph and teardown proof absent |
| Tick / frame execution | medium | `FRAME_TIMELINE.md` | `STRUCTURAL_NARRATIVE.md`, `DEPENDENCY_GRAPH.md` | PRIMARY | no measured frame-budget replay |
| Gameplay ownership across major domains | medium | `GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md` | `PLAYER_GAMEPLAY_CORE_MAP.md`, `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md`, `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md` | PRIMARY | still not exhaustive down to every minor subsystem/file |
| Tools / interaction / operational runtime | strong | `TOOLS_INTERACTION_OPERATIONAL_SYSTEM_MAP.md` | `PLAYER_GAMEPLAY_CORE_MAP.md`, `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md`, `2026-04-30_SAVE_PARTICIPANT_LEDGER.md`, `EVENT_FLOW_MAP.md` | PRIMARY | no live multi-tool traversal or stress proof |
| Narrative / discovery / progression ownership | strong | `NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md` | `GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md`, `2026-04-30_SAVE_PARTICIPANT_LEDGER.md`, `EVENT_FLOW_MAP.md`, `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` | PRIMARY | no live progression-arc traversal proof |
| Player / gameplay core | medium | `PLAYER_GAMEPLAY_CORE_MAP.md` | `PROJECT_ATLAS.md`, `INTERFACE_HEALTH_DASHBOARD.md`, `FRAME_TIMELINE.md` | PRIMARY | no measured integrated player traversal proof |
| Survival / damage / hazard runtime | strong | `SURVIVAL_DAMAGE_HAZARD_SYSTEM_MAP.md` | `PLAYER_GAMEPLAY_CORE_MAP.md`, `WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md`, `2026-04-30_SAVE_PARTICIPANT_LEDGER.md` | PRIMARY | no live integrated hazard/survival traversal proof |
| World / environment / submarine ownership | medium | `WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md` | `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md`, `PROJECT_ATLAS.md` | PRIMARY | still no measured runtime traversal/profiling proof for this stack |
| AUP / world-coordinate coupling | strong | `SYSTEM_INTERCONNECT_MATRIX.md`, `AUP_SURGERY_MAP.md` | `2026-04-28_DATA_DICTIONARY.md`, `2026-04-28_MEMORY_ALIGNMENT_FIX.md` | PRIMARY | live prefab/scene AUP misuse proof absent |
| Assembly / package dependency surface | medium | `2026-04-28_CIRCULAR_DEPS.md` | `DEPENDENCY_GRAPH.md`, `PROJECT_ATLAS.md` | PRIMARY | no Roslyn/full compile graph export |
| Audio ownership / routing | medium | `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` | `AUDIO_ROUTING_AUDIT.md`, `INTERFACE_HEALTH_DASHBOARD.md`, `DEPENDENCY_GRAPH.md`, `PROJECT_ATLAS.md` | PRIMARY | no live DSP/runtime mix-path verification |
| UI / HUD architecture | strong | `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` | `HUD_EDITOR_SPEC.md`, `INTERFACE_HEALTH_DASHBOARD.md`, `STRUCTURAL_NARRATIVE.md`, `SINGLETON_FIX_PRIORITY.md` | PRIMARY | no measured canvas/RT runtime cost proof |
| Editor/runtime forensic debt | medium | `2026-04-30_EDITOR_RUNTIME_FORENSICS.md` | `2026-04-28_HOT_PATH_VIOLATIONS.md`, `DEAD_CODE_GRAVEYARD.md`, `2026-04-30_ARCHIVARIUS_CONTINUATION_REVERIFICATION.md` | PRIMARY | no measured runtime replay or fix-validation proof |
| Save / load runtime truth | medium | `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md` | `EVENT_FLOW_MAP.md`, `FRAME_TIMELINE.md` | PRIMARY | no live slot traversal or corruption-recovery proof |
| Runtime persistence drift beyond save slots | medium | `2026-04-30_PERSISTENCE_AND_SCENE_SEARCH_DRIFT.md` | `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md`, `2026-04-30_SERVICE_AUTHORITY_DRIFT.md` | PRIMARY | no live artifact audit or persistence-failure replay |
| Save-participant breadth | medium | `2026-04-30_SAVE_PARTICIPANT_LEDGER.md` | `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md`, `EVENT_FLOW_MAP.md` | PRIMARY | participant ordering is source-backed, not live-validated |
| Rendering / RT / render graph | medium | `RENDERGRAPH_AUDIT.md`, `COMPUTE_BUFFER_AUDIT.md` | `SCENE_VRAM_FOOTPRINT.md`, `VRAM_BUDGET_AUDIT.md` | PRIMARY | no fresh measured RenderDoc / Frame Debugger authority |
| VRAM / memory budgets | medium | `VRAM_BUDGET_AUDIT.md` | `2026-04-28_VRAM_BUDGET_AUDIT.md`, `2026-04-28_VRAM_EXECUTION_LIST.md`, `SCENE_VRAM_FOOTPRINT.md`, `vram_detail.csv` | PRIMARY | current measured residency still absent |
| Struct layouts / data surfaces | medium | `2026-04-28_DATA_DICTIONARY.md` | `2026-04-28_MEMORY_ALIGNMENT_FIX.md`, `SYSTEM_INTERCONNECT_MATRIX.md` | SECONDARY | many notes remain dated and surgery-ready only after fresh reread |
| Asset dependency / referencer mapping | medium | `ASSET_DEPENDENCY_MAP.md` | `2026-04-28_ASSET_DEPENDENCY_MAP.md`, `ITEM_ASSET_GUIDS.md`, `PROJECT_CONTENT_LEDGER.md` | PRIMARY | no fresh Unity-side referencer export |
| Content authoring / hash / proxy validation | medium | `PROJECT_CONTENT_LEDGER.md` | `ITEM_ASSET_GUIDS.md`, `DEAD_CODE_GRAVEYARD.md` | PRIMARY | validator exists, but no fresh MCP execution proof yet |
| Dead assets / dead code | medium | `2026-04-28_DEAD_ASSET_SWEEP.md`, `DEAD_CODE_GRAVEYARD.md` | `DEBUG_LOG_DELETION_QUEUE.md` | PRIMARY | deletion-safe proof still weak without dependency graph |
| Hot-path / GC / profiler readiness | medium | `2026-04-28_HOT_PATH_VIOLATIONS.md`, `2026-04-28_PROFILING_PREPAREDNESS_AUDIT.md` | `FRAME_TIMELINE.md`, `2026-04-28_LIAR_DETECTION.md` | PRIMARY | no live profiler or GCMonitor pass |
| Singleton remediation | medium | `SINGLETON_VIOLATIONS.md`, `SINGLETON_FIX_PRIORITY.md` | `DEPENDENCY_GRAPH.md`, `INTERFACE_STRATEGY.md` | PRIMARY | roadmap still needs owner-by-owner execution proof |
| God objects / oversized owners | medium | `GOD_OBJECT_AUDIT.md` | `PROJECT_ATLAS.md`, `STRUCTURAL_NARRATIVE.md` | PRIMARY | no decomposition execution tracking |
| Hardcoded links / path debt | medium | `HARD_LINK_DEBT.md` | `ASSET_DEPENDENCY_MAP.md` | PRIMARY | no current automated path-lint authority |
| Non-ASCII / naming debt | medium | `2026-04-28_CYRILLIC_SWEEP.md`, `GLOSSARY.md` | `2026-04-28_DEAD_ASSET_SWEEP.md` | PRIMARY | build/toolchain impact still unproven |
| Construction / runtime integration | medium | `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md` | `MODULAR_EQUIPMENT_ENGINE_SURGERY_LOG.md`, `2026-04-29_EQUIPMENT_VOXEL_WELD_SURGERY_LOG.md`, `2026-04-29_LOGIC_SPANNER_OVERCHARGE_SURGERY_LOG.md` | PRIMARY | no live placement/rebuild stress proof |
| Construction / equipment surgery logs | medium | `2026-04-29_EQUIPMENT_VOXEL_WELD_SURGERY_LOG.md`, `2026-04-29_LOGIC_SPANNER_OVERCHARGE_SURGERY_LOG.md`, `MODULAR_EQUIPMENT_ENGINE_SURGERY_LOG.md` | patch files in same folder | SECONDARY | these are narrow repair logs, not full subsystem references |
| GlobalRegistry runtime authority | strong | `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md` | `INTERFACE_CONTRACT_TABLE.md`, `INTERFACE_HEALTH_DASHBOARD.md`, `BUILD_DEPENDENCY_GRAPH.md` | PRIMARY | measured runtime stability still absent |

## Coverage Gaps That Still Exist

| Gap | Why it matters |
|---|---|
| Exhaustive owner-to-file inventory for every minor subsystem | large codebase still lacks a full graph-level map down to every narrow feature file |
| Measured performance truth | no fresh profiler / GCMonitor / Frame Debugger authority for the current source snapshot |

## Recommended Read Paths By Task

| Task | Recommended read path |
|---|---|
| Need repo orientation fast | `PROJECT_ATLAS.md` -> `MASTER_INDEX.md` -> this file |
| Need broad gameplay ownership | `GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md` -> `PLAYER_GAMEPLAY_CORE_MAP.md` -> `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md` |
| Need survival / damage / hazard ownership | `SURVIVAL_DAMAGE_HAZARD_SYSTEM_MAP.md` -> `PLAYER_GAMEPLAY_CORE_MAP.md` -> `WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md` -> `2026-04-30_SAVE_PARTICIPANT_LEDGER.md` |
| Need tools / interaction / operational ownership | `TOOLS_INTERACTION_OPERATIONAL_SYSTEM_MAP.md` -> `PLAYER_GAMEPLAY_CORE_MAP.md` -> `2026-04-30_SAVE_PARTICIPANT_LEDGER.md` -> `EVENT_FLOW_MAP.md` |
| Need narrative/discovery/progression ownership | `NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md` -> `2026-04-30_SAVE_PARTICIPANT_LEDGER.md` -> `EVENT_FLOW_MAP.md` |
| Need world/environment/submarine ownership | `WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md` -> `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md` -> `PROJECT_ATLAS.md` |
| Need registry/runtime authority truth | `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md` -> `INTERFACE_HEALTH_DASHBOARD.md` -> `BUILD_DEPENDENCY_GRAPH.md` |
| Need mixed singleton/registry service-authority truth | `2026-04-30_SERVICE_AUTHORITY_DRIFT.md` -> `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md` -> `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md` |
| Need authored-vs-runtime owner truth | `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md` -> `BUILD_DEPENDENCY_GRAPH.md` -> `PROJECT_ATLAS.md` |
| Need interface/service truth | `INTERFACE_HEALTH_DASHBOARD.md` -> `INTERFACE_CONTRACT_TABLE.md` -> `INTERFACE_STRATEGY.md` |
| Need event-bus truth | `EVENT_FLOW_MAP.md` -> `EVENT_BUS_MAP.md` -> `2026-04-28_EVENT_LEAK_REPORT.md` |
| Need bootstrap/init context | `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md` -> `BUILD_DEPENDENCY_GRAPH.md` -> `DEPENDENCY_GRAPH.md` -> `FRAME_TIMELINE.md` |
| Need UI/audio/presentation ownership | `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` -> `INTERFACE_HEALTH_DASHBOARD.md` -> `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md` |
| Need live console / editor-runtime forensic truth | `2026-04-30_EDITOR_RUNTIME_FORENSICS.md` -> `2026-04-30_ARCHIVARIUS_CONTINUATION_REVERIFICATION.md` -> `2026-04-28_HOT_PATH_VIOLATIONS.md` |
| Need player/gameplay ownership truth | `PLAYER_GAMEPLAY_CORE_MAP.md` -> `INTERFACE_HEALTH_DASHBOARD.md` -> `FRAME_TIMELINE.md` |
| Need construction/runtime truth | `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md` -> `MODULAR_EQUIPMENT_ENGINE_SURGERY_LOG.md` -> `FRAME_TIMELINE.md` |
| Need save/load truth | `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md` -> `EVENT_FLOW_MAP.md` -> `FRAME_TIMELINE.md` |
| Need non-slot persistence / runtime scene-search truth | `2026-04-30_PERSISTENCE_AND_SCENE_SEARCH_DRIFT.md` -> `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md` -> `2026-04-30_SERVICE_AUTHORITY_DRIFT.md` |
| Need save participant inventory | `2026-04-30_SAVE_PARTICIPANT_LEDGER.md` -> `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md` -> `EVENT_FLOW_MAP.md` |
| Need AUP/world coupling truth | `SYSTEM_INTERCONNECT_MATRIX.md` -> `AUP_SURGERY_MAP.md` -> `2026-04-28_DATA_DICTIONARY.md` |
| Need rendering/memory context | `VRAM_BUDGET_AUDIT.md` -> `RENDERGRAPH_AUDIT.md` -> `COMPUTE_BUFFER_AUDIT.md` -> `SCENE_VRAM_FOOTPRINT.md` |
| Need content hash/proxy/collider truth | `PROJECT_CONTENT_LEDGER.md` -> `ITEM_ASSET_GUIDS.md` -> `DEAD_CODE_GRAVEYARD.md` |
| Need singleton cleanup context | `SINGLETON_VIOLATIONS.md` -> `SINGLETON_FIX_PRIORITY.md` -> `INTERFACE_HEALTH_DASHBOARD.md` |

## Current Trust Boundary

For current-source truth, prefer in this order:

1. `2026-04-30_ARCHIVARIUS_CONTINUATION_REVERIFICATION.md`
2. `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md`
3. current-source summaries in `01_GENERAL_INFO`
4. current-source rewrites in `02_ACTUAL_REPORTS`
5. dated `2026-04-28_*` bundles only after checking whether they were reframed as historical snapshots

Do not treat dated bundles as current runtime proof.

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improved discoverability and reduced risk of reading the wrong authority file first. |

## Verdict

Folders `01_GENERAL_INFO` and `02_ACTUAL_REPORTS` now have a clearer authority map, but they are not yet exhaustive runtime truth.
They are strongest on orientation, interfaces, event topology, registry/runtime authority, and player/construction/save/gameplay ownership mapping.
They are still weaker on measured runtime proof and graph-level exhaustive ownership down to every minor subsystem.

STATUS: PENDING VERIFICATION
