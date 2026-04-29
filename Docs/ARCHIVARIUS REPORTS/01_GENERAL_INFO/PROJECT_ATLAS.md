# PROJECT ATLAS

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: live orientation map for the current HECTON-8 workspace
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## 1. What This File Is

This file is a source-backed workspace atlas.
It is not a readiness claim and not a profiler report.

It answers four narrow questions:

1. What is physically present in the repo now.
2. Where first-party runtime ownership actually lives.
3. Which active docs are already stale against current source.
4. Which files should be trusted first during later audits.

## 2. Workspace Snapshot

Repository root observed on 2026-04-29 contains these top-level entries of interest:

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

Observed first-party C# file count under `Assets/_Project/Scripts`: `970`

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

### 6.2 `Docs/ARCHIVARIUS REPORTS`

This atlas is specifically aligned with:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS`

Coverage snapshot from current reverification pass:

- `01_GENERAL_INFO` markdown files: `16`
- `02_ACTUAL_REPORTS` markdown files: `38`
- `02_ACTUAL_REPORTS` csv files: `1`
- total audited files in folders `01` and `02`: `55`

Canonical entry files inside this bundle:

- `MASTER_INDEX.md`
- `DOCSET_COVERAGE_MATRIX.md`
- `PLAYER_GAMEPLAY_CORE_MAP.md`
- `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md`
- `INTERFACE_CONTRACT_TABLE.md`
- `INTERFACE_HEALTH_DASHBOARD.md`
- `EVENT_BUS_MAP.md`
- `EVENT_FLOW_MAP.md`
- `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md`
- `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md`
- `2026-04-29_ARCHIVARIUS_DOCSET_REVERIFICATION.md`

## 7. Architecture Signals Rechecked Against Source

These points were revalidated against current source in this pass:

- `GlobalRegistryContracts.cs` now contains `27` interfaces, not `19`.
- `SpatialAudioManager` directly implements `IAudioService` and registers through `GlobalRegistry.RegisterAudioService(this)`.
- `SuitHUDV4CanvasOverlay` is the direct first-party `IUIService` implementor found in current source scan.
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
