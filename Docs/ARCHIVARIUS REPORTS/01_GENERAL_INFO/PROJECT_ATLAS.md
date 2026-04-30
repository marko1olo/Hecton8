# PROJECT ATLAS

Version: 1.4.0
Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: live orientation map for the current HECTON-8 workspace
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `STRM_Persistent_Object_Registry.txt`

## 1. What This File Is

This file is a source-backed workspace atlas.
It is not a readiness claim and not a profiler report.

It answers four narrow questions:

1. What is physically present in the repo now.
2. Where first-party runtime ownership actually lives.
3. Which active docs are already stale against current source.
4. Which files should be trusted first during later audits.

## 2. Workspace Snapshot

Repository root observed on 2026-04-30 contains these top-level entries of interest:

- `.agents-skills`
- `Assets`
- `Docs`
- `Packages`
- `ProjectSettings`
- `UserSettings`
- `Lore`
- `NativeAudio`
- `ProfilerCaptures`
- `MemoryCaptures`
- `Tools`
- `Logs`
- `AGENTS.md`
- root audit/report markdown files

High-noise surfaces also exist:

- `Library`
- `Temp`
- multiple Unity-generated `.csproj` files
- archive-heavy and idea-heavy bundles inside `Docs`

## 3. First-Party Runtime Ownership

Primary first-party code still lives under:

- `Assets/_Project/Scripts`
- `Assets/_Project/Data`
- `Assets/_Project/Prefabs`
- `Assets/_Project/Scenes`
- `Assets/_Project/Art`
- `Assets/_Project/UI`

Observed first-party C# file count under `Assets/_Project`: `1038`
Observed first-party C# file count under `Assets/_Project/Scripts`: `998`

This is physical file count only.
It does not imply quality, cohesion, or compile health.

## 4. External / Package Surface

Third-party and package-heavy surfaces visible in the workspace:

- Crest
- MapMagic
- Odin Inspector
- More Mountains / Feel
- GPU Instancer
- Bakery
- Volumetric Light Beam
- legacy package traces such as A* and Easy Save assemblies

Presence on disk does not equal approved runtime usage.
Allowed usage remains constrained by `AGENTS.md`.

## 5. Mandate Registry

Observed mandate count in `.agents-skills/*.txt`: `52`

Operational rule remains unchanged:

- read `AGENTS.md` first
- load only task-relevant mandates
- do not infer behavior when a mandate already defines it

## 6. Documentation Surfaces

### 6.1 Root `Docs/`

`Docs/` is broader than `ARCHIVARIUS REPORTS`.
It contains active plans, audit bundles, archives, and historical noise.

Current active anchors include:

- `README.md`
- `ROOT_DOCS_REFERENCE.md`
- `SYSTEMS_CONTRACTS.md`
- `QUALITY_GATES.md`
- `DOC_GOVERNANCE.md`
- `HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `ARCHITECTURE/DISPATCH_PIPELINE.md`
- `ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `Reports/ILLEGAL_SINGLETONS.md`
- `Reports/GC_HOTPATH_VIOLATIONS.md`
- `Reports/BOOT_ORDER_VIOLATIONS.md`
- `Reports/NATIVE_ALLOCATION_AUDIT.md`

### 6.2 `Docs/ARCHIVARIUS REPORTS`

This atlas is specifically aligned with:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS`

Coverage snapshot from current reverification pass:

- `01_GENERAL_INFO` markdown files: `22`
- `02_ACTUAL_REPORTS` markdown files: `46`
- `02_ACTUAL_REPORTS` csv files: `1`
- `02_ACTUAL_REPORTS` patch artifacts: `6`
- total indexed docs/datasets in folders `01` and `02`: `69`
- total physical non-meta files in folders `01` and `02`: `75`

Canonical entry files inside this bundle:

- `MASTER_INDEX.md`
- `DOCSET_COVERAGE_MATRIX.md`
- `NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md`
- `SURVIVAL_DAMAGE_HAZARD_SYSTEM_MAP.md`
- `TOOLS_INTERACTION_OPERATIONAL_SYSTEM_MAP.md`
- `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md`
- `GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md`
- `PLAYER_GAMEPLAY_CORE_MAP.md`
- `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md`
 - `WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md`
- `INTERFACE_CONTRACT_TABLE.md`
- `INTERFACE_HEALTH_DASHBOARD.md`
- `EVENT_BUS_MAP.md`
- `EVENT_FLOW_MAP.md`
- `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md`
- `2026-04-30_EDITOR_RUNTIME_FORENSICS.md`
- `2026-04-30_SERVICE_AUTHORITY_DRIFT.md`
- `2026-04-30_PERSISTENCE_AND_SCENE_SEARCH_DRIFT.md`
- `2026-04-29_GLOBALREGISTRY_RUNTIME_AUTHORITY_MATRIX.md`
- `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md`
- `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md`
- `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md`
- `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION_ADDENDUM.md`
 - `2026-04-30_SAVE_PARTICIPANT_LEDGER.md`
 - `2026-04-30_ARCHIVARIUS_CONTINUATION_REVERIFICATION.md`

### 6.3 Current Live Console Truth

Current reachable Unity console state is not clean.

This pass confirmed repeated editor-side `NullReferenceException` spam through:

- `WorldProceduralScatterDirector.get__desiredPlacements()`
- `WorldProceduralScatterDirector.BuildScatterPreviewGizmoSnapshot(...)`
- `Editor/WorldProceduralScatterPreviewGizmoDrawer.DrawScatterPreviewGizmos(...)`

Authority file for this snapshot:

- `02_ACTUAL_REPORTS/2026-04-30_EDITOR_RUNTIME_FORENSICS.md`

## 7. Architecture Signals Rechecked Against Source

These points were revalidated against current source in this pass:

- `GlobalRegistryContracts.cs` now contains `27` interfaces, not `19`.
- `SpatialAudioManager` directly implements `IAudioService` and registers through `GlobalRegistry.RegisterAudioService(this)`.
- `SuitHUDV4CanvasOverlay` is the direct first-party `IUIService` implementor found in current source scan.
- current bootstrap authority is split across `BootstrapController`, `GameBootstrapper`, and `SceneBootstrap`; it is not a single-owner startup surface.
- narrative/discovery/progression knowledge is distributed across `HectonNarrativeDirector`, `HectonDiscoveryManager`, `LoreDatabaseManager`, `AudioLogSystem`, Atlas systems, PDA archive systems, and scripted progression directors; it is not one subsystem.
- `IRenderable` is no longer a one-owner curiosity. Current direct implementors include:
  - `HectonUnderwaterVisuals`
  - `HectonSubmarineOS`
  - `MissionMarkerSystem`
- `HabitatIntegrityManager` now implements the global `Hecton8.Core.IDamageReceiver` contract directly.
- `GlobalRegistry` exposes a materially broader service surface than older docs described, including:
  - thermodynamics
  - logistics
  - world generation
  - encounter direction
  - quest system
  - power grid
  - submarine runtime context
  - submarine hull-breach read model

### 7.1 Current Service Owners Rechecked

Current code-backed service owners include:

- `InputDispatcher` -> `IInputService`
- `PhysicsApplySystem` -> `IPhysicsService`
- `SpatialAudioManager` -> `IAudioService`
- `SceneRuntimeService` -> `ISceneService`
- `SaveManager` -> `ISaveService`
- `SuitHUDV4CanvasOverlay` -> `IUIService`
- `PlayerRuntimeContextService` -> `IPlayerRuntimeContext`
- `PlayerInventoryManager` -> `IPlayerInventoryService`
- `ModularEquipmentEngine` -> `IModularEquipmentService`
- `PlayerSensoryManager` -> `IPlayerSensoryService`
- `EnvironmentRuntimeContextService` -> `IEnvironmentRuntimeContext`
- `GlobalWeatherDirector` -> `IWeatherService`
- `AbyssalThermalManager` -> `IThermodynamicsService`
- `ConstructionManager` -> `ILogisticsService`
- `WorldProceduralScatterDirector` -> `IWorldGenService`
- `HectonDirectorAI` -> `IEncounterDirectorService`
- `QuestManager` -> `IQuestSystem`
- `OceanKinematicsRuntimeService` -> `IHectonOceanKinematicsService`
- `EquipmentInteractionHandler` -> `IInteractionSignalService`
- `DebrisManager` -> `IDebrisService`
- `EcosystemDirector` -> `IEcosystemDirectorService`

### 7.3 Current Tools / Interaction Truth Rechecked

Current tool-domain findings added in this pass:

- `PlayerToolManager` is the hand-level active tool dispatch owner, not the deep runtime owner for tool stats or interaction routing.
- `PlayerTool` is the shared operational spine for equip/unequip, durability, battery drain, recoil queueing, and modular runtime registration.
- `ModularEquipmentEngine` is the compiled runtime owner for tool state, upgrades, battery, heat, and durability mirrors.
- `EquipmentInteractionHandler` is the authoritative deferred interaction-routing owner through a `NativeQueue<InteractionSignal>` plus staged `RaycastCommand` lanes.
- scanner persistence is split from scanner action: `ScannerTool` owns scan action, while `ScanLogSystem` owns scan discovery save state.
- beacon operations are split from hand tooling: `BeaconDeployerTool` is the action surface, while `BeaconNetworkSystem` is the marker-network and save participant owner.

### 7.4 Current Survival / Hazard Truth Rechecked

Current survival-domain findings added in this pass:

- `HectonSurvivalSystem` is the dominant survival authority for oxygen, pressure, thermal stress, integrity, decompression risk, death-cause resolution, and save state.
- `HazardZoneManager` is the runtime exposure and damage-routing owner rather than the source owner for every hazard.
- `HectonHazardManager` is the ingress bridge for authored and runtime hazard sources.
- `AbyssalThermalManager` is both thermodynamics service owner and a thermal hazard-source owner.
- `DeepPsychosisController` and `PlayerStressVFX` are downstream stress-consequence owners, not primary survival owners.
- `HectonPlayerHealth` is a real parallel HP/mutation branch whose own file documents a weak persistence path because `SaveData` has no dedicated player-health DTO.

### 7.2 Event Topology Rechecked

Current first-party event architecture is mixed:

- queue-backed deferred buses exist:
  - `SaveEvents`
  - `QuestEvents`
  - `ScanEvents`
  - `NarrativeEvents`
  - `AudioLogEvents`
- direct static `Action` buses also still exist:
  - `InteractionEvents`
  - `CraftingEvents`
  - `HectonSubmarineOsEvents`
  - multiple feature-embedded buses such as `PDAEvents` and `FlashlightEvents`
- `HectonEventBus` remains a separate managed modding bus

There is no single event architecture yet.
There is a partial migration plus legacy static-bus residue.

## 8. Confirmed Active Documentation Drift

These active docs were stale against current code before this pass:

- `INTERFACE_HEALTH_DASHBOARD.md`
- `INTERFACE_CONTRACT_TABLE.md`
- `INTERFACE_STRATEGY.md`
- `DEPENDENCY_GRAPH.md`
- `EVENT_FLOW_MAP.md`
- this file itself

## 8.1 Current Audit Artifacts

Current source-backed audit outputs now include:

- `Docs/Reports/ILLEGAL_SINGLETONS.md`
- `Docs/Reports/GC_HOTPATH_VIOLATIONS.md`
- `Docs/Reports/BOOT_ORDER_VIOLATIONS.md`
- `Docs/Reports/NATIVE_ALLOCATION_AUDIT.md`
- `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`

Current evidence-backed deltas from this pass:

- `GameBootstrapper` contains a real Kahn-style cycle validator, but bootstrap execution is still manual and registry-safe order is not guaranteed.
- `RenderDispatcher` and `GlobalPhysicsStateManager` still self-register in `Awake()` instead of explicit bootstrap-owned initialization.
- `PlayerRuntimeContextService` reads `GlobalRegistry.PlayerInventory` and `GlobalRegistry.PlayerSensory` before those owners are initialized in the player layer.
- reread of `VoxelDynamicNavGridRuntime.cs` now contradicts an older strict-criterion accusation: current source contains `ResetRuntime() -> DisposeAll()` plus per-record disposal, so that specific native-lifetime claim is stale.
- active service owners such as `InputDispatcher`, `SceneRuntimeService`, `PlayerRuntimeContextService`, `EnvironmentRuntimeContextService`, `ConstructionManager`, `AbyssalThermalManager`, and `QuestManager` still live in a mixed authority mode: singleton ownership plus registry publication, and in some cases `DontDestroyOnLoad`.
- runtime persistence is broader than the slot-save stack alone: `RebindingManager` persists input overrides separately, `GlobalProfileManager` persists a separate meta profile, and `HectonCaveVoxelAmbientOcclusionController` still keeps slow-tick fallback scene scans for cameras and voxel volumes.
- current `Assets/_Project` path sweep found `0` Cyrillic file paths, but Cyrillic comments remain widespread in source and shader files.
- a clean "20 agents reported zero console errors" proof surface was not found in the current docs corpus.

Primary stale claims that were removed:

- `IAudioService` ghost ownership
- `IUIService` ghost/fragmented ownership
- `IDamageReceiver` shadow-conflict claim
- `SaveEvents` and related buses described as direct static `Action` dispatch
- first-party script count reported as `922`
- registry-contract interface count reported as `19`

## 9. Recommended Read Order

Use this order for future technical work:

1. `AGENTS.md`
2. relevant mandate files from `.agents-skills`
3. `Docs/ROOT_DOCS_REFERENCE.md`
4. `Docs/SYSTEMS_CONTRACTS.md`
5. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/MASTER_INDEX.md`
6. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOCSET_COVERAGE_MATRIX.md`
7. the specific architecture or audit file tied to the touched system
8. source files themselves when the doc and code disagree

## 10. Regression Model

CPU: no runtime code changed by this atlas rewrite
GC: no gameplay-path changes; document-only pass
Memory: no runtime asset or scene mutation
Cadence: documentation truthfulness improved; no gameplay cadence impact
Correctness: improved by removing stale ownership claims and stale event-topology claims

## 11. Hot Path Impact

None. Markdown-only change.

## 12. Failure Modes

- counts will drift again if files are added and docs are not refreshed
- scene/prefab wiring may diverge from class-level ownership claims
- live compile/runtime state still needs Unity-side confirmation

## 13. Why This Version Was Kept

Kept because it is source-backed and narrower than the older narrative-heavy version.
Rejected content: stale counts, ghost-interface claims contradicted by source, and unsupported event-bus descriptions.

STATUS: PENDING VERIFICATION
