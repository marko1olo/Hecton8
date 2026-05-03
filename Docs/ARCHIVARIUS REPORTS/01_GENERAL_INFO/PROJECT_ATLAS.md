# PROJECT ATLAS

Version: 1.4.8
Date: 2026-05-03
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

Repository root observed on 2026-05-02 contains these top-level entries of interest:

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
- active root text files: `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`

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

Observed first-party C# file count under `Assets/_Project`: `1087`
Observed first-party C# file count under `Assets/_Project/Scripts`: `1047`
Observed first-party C# line count under `Assets/_Project/Scripts`: `571562`

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

DOTS / Entities package boundary:

- `com.unity.entities` is not declared in the current `Packages/manifest.json`.
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef` exists, but it is define-gated, `autoReferenced: false`, and not a live production assembly path without package/define proof.
- Current first-party source scan found no active `Unity.Entities`, `IComponentData`, `SystemBase`, or `ISystem` usage under `Assets/_Project/Scripts`.

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
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
- `ARCHITECTURE/DISPATCH_PIPELINE.md`
- `ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
- `Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`
- `Reports/2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md`
- `Reports/2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`
- `Reports/TOTAL_CODEBASE_AUDIT_V2.md`
- `Reports/OMEGA_CORE_ENFORCEMENT_2026-05-01.md`
- `Reports/AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`
- `Reports/DOOMSDAY_FLAW_REPORT.md`

### 6.2 `Docs/ARCHIVARIUS REPORTS`

This atlas is specifically aligned with:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS`

Coverage snapshot from current reverification pass:

- `01_GENERAL_INFO` markdown files: `24`
- `02_ACTUAL_REPORTS` markdown files: `46`
- `02_ACTUAL_REPORTS` csv files: `1`
- `02_ACTUAL_REPORTS` patch artifacts: `9`
- total indexed docs/datasets in folders `01` and `02`: `71`
- total physical non-meta files in folders `01` and `02`: `80`

Canonical entry files inside this bundle:

- `MASTER_INDEX.md`
- `DOC_AUTHORITY_CLASSIFICATION.md`
- `CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
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

Latest documentation sweep is `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`.
Latest MCP console snapshot cited by the May 1 documentation pass returned `0` error/warning entries.

This is not Play Mode proof and not runtime certification.

Previous editor-runtime forensic work had confirmed repeated editor-side `NullReferenceException` spam through:

- `WorldProceduralScatterDirector.get__desiredPlacements()`
- `WorldProceduralScatterDirector.BuildScatterPreviewGizmoSnapshot(...)`
- `Editor/WorldProceduralScatterPreviewGizmoDrawer.DrawScatterPreviewGizmos(...)`

Current interpretation:

- treat the scatter gizmo `NullReferenceException` as a previous active risk until a targeted repro proves it gone
- treat the latest empty MCP console read as a narrow editor-console snapshot only
- do not infer clean Play Mode, clean boot, or zero runtime errors from the empty snapshot

Authority files for this boundary:

- `02_ACTUAL_REPORTS/2026-04-30_EDITOR_RUNTIME_FORENSICS.md`
- `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`

## 7. Architecture Signals Rechecked Against Source

These points were revalidated against current source in this pass:

- `GlobalRegistryContracts.cs` now contains `33` direct public interfaces, not the older `19`, `27`, or `31` snapshots.
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

- `Docs/Reports/TOTAL_CODEBASE_AUDIT_V2.md`
- `Docs/Reports/OMEGA_CORE_ENFORCEMENT_2026-05-01.md`
- `Docs/Reports/AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`
- `Docs/Reports/DOOMSDAY_FLAW_REPORT.md`
- `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`
- `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
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

## 2026-05-03 Guard Scanner Delta

Mandates followed for this update:

- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety`
- `DBG_Telemetry_Crash_Reporting_PostMortem`

Current source-only guard artifact:

- `Tools/ReloadAudit/Scan-FoundationGuards.ps1`
- `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`

Current guard result from the May 3 scan:

- Scanner exit code: `0`.
- Global registry self-registration inventory: `496` informational sites from the broad `GlobalRegistry.Register*(this)` scan.
- Blind registry flag drift: `0`.
- `UnsafeUtility.MemCpy` outside `UnsafeMemoryCopyGuard`: `0`.
- Legacy `PlayerSignalEvents.On*` subscriptions: `0`.
- Runtime `Find*`/`GameObject.Find*` hits outside Editor folders: `0`.
- Synchronous job `.Run(` inventory: `0`.
- Hot-path synchronous `.Run(` review queue: `0`.
- Guarded dispatcher completion inventory: `1`, `DispatcherJobSwap.TryComplete` with the source-level `IsCompleted`/swap-window contract.
- Direct `InputManager.Instance` inventory: `0`; hot-path direct input singleton review sites remain `0`.
- `GlobalRegistry.NativeInputManager` now exposes the bootstrap-bound native input owner from the registered `InputDispatcher` for cold UI/Input-System bridge code that still requires concrete `InputAction` APIs.
- Release-reachable one-hop `Debug.Log` review inventory: `0` after dev-build-only guards were added to hot-path-adjacent diagnostics.
- `PlayerInventory` SlowTick now has static profiler markers for the SlowTick envelope, radioactive half-life inline kernel, and reactive-chemistry inline kernel.

This is a source scan and CI candidate, not Unity Play Mode proof. Do not claim zero GC, frame-time compliance, memory retention stability, or MCP verification from this artifact alone.

STATUS: PENDING VERIFICATION

## 2026-05-02 Atlas Delta

Mandates followed for this update:

- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin`
- `PHYS_Physics_Integrity_Determinism_ForceMode`
- `DBG_Telemetry_Crash_Reporting_PostMortem`

Current measured source surface:

- `Assets/_Project/Scripts`: `1047` C# files.
- Script LOC by PowerShell line count: `571562`.
- Strict `StartCoroutine(` hits under `Assets/_Project/Scripts`: `0`.
- `Docs`: `595` non-meta files in the current filesystem view.
- `Docs/ARCHIVARIUS REPORTS`: `119` non-meta files in the current filesystem view.

These counts are orientation data only. They are not readiness metrics and should not drive task priority.

Current active forensic reports to read before older atlas claims:

1. `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md`
2. `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
3. `Docs/Reports/2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`
4. `Docs/Reports/DOOMSDAY_FLAW_REPORT.md`
5. `Docs/Reports/AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`
6. `Docs/Reports/OMEGA_CORE_ENFORCEMENT_2026-05-01.md`
7. `Docs/Reports/CI_VALIDATION_HOOKS_SURGERY_LOG.md`
8. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/02_SYSTEM_REALITY_MATRIX.md`
9. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md`

Current risk corrections:

- Headless simulation remains a first-order risk. The older `FaunaBrain.UpdateBioluminescentHypnosis()` player-camera dependency is stale by current source recheck; remaining fauna headless risk is perception/LOD logic still using player Transform/Rigidbody paths plus absent no-camera Play Mode proof. `StorageCrate` no longer relies on an Animator event for the `Opening -> Open` source transition, but Play Mode proof is absent.
- Jobs/Burst compliance improved at the attribute level. The older `.Complete()` barrier call-site list is stale by May 2 grep; remaining risk is dispatcher-owned completion-window proof and runtime stall profiling.
- Event topology is stronger than old docs implied because late-frame dispatch budgeting and `HectonEventBus.MaxDispatchDepth = 4` exist, but recursive generation/depth is not globally proven safe.
- Core asmdef purification is not complete. The current safe position is staged bridge extraction, not blind reference removal.
- Object pool exhaustion behavior is safer by source review: spawn exhaustion publishes pool-exhausted telemetry and returns `null`; no global runtime instantiation purity claim is made for the entire project.
- `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` is now the conceptual entry point for current system ownership. It is not a runtime proof document.

Verification state:

- Unity console: latest May 1 MCP `read_console` returned `0` error/warning entries after the event-bus/spatial-hash compile refresh.
- Runtime: Play Mode was not launched.
- GC/frame/memory: no fresh profiler or GCMonitor measurement.

STATUS: PENDING VERIFICATION
