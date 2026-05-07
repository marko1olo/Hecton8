# PROJECT ATLAS

Version: 1.5.2
Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: live orientation map for the current HECTON-8 workspace
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `STRM_Persistent_Object_Registry.txt`, `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`, `PROJECT_LTS_Compatibility_Layer.txt`

## 1. What This File Is

This file is a source-backed workspace atlas.
It is not a readiness claim and not a profiler report.

It answers four narrow questions:

1. What is physically present in the repo now.
2. Where first-party runtime ownership actually lives.
3. Which active docs are already stale against current source.
4. Which files should be trusted first during later audits.

## 2. Workspace Snapshot

Repository root observed on 2026-05-04 contains these top-level entries of interest:

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
- non-canonical root compatibility mirror: `TERRAIN_AND_BIOME_REALITY_MAP.md`

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

Observed first-party C# file count under `Assets/_Project`: `1212`
Observed first-party C# file count under `Assets/_Project/Scripts`: `1171`
Observed first-party C# line count under `Assets/_Project/Scripts` by `Get-Content.Count`: `651253`
Observed first-party C# line count under `Assets/_Project/Scripts` by `Measure-Object -Line`: `559502`

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
- `Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`
- `Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`
- `Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`
- `Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md`
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

Latest documentation synchronization is `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`.
Latest current MCP readback in that May 6 pass reports active scene `00_BOOTSTRAP`, Play Mode off, compiling false, ready for tools, console error/warning entries `0`, renderer `PC_Renderer`, and `37` render textures. The earlier May 4 sweep readback with `18` warnings during `01_MAIN_MENU` Play Mode transition is historical, not the current editor-console boundary.

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

- `GlobalRegistryContracts.cs` now contains `37` direct public interfaces, not the older `19`, `27`, `31`, `33`, `34`, or `36` snapshots.
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

## 2026-05-05 Archivarius Docset Sync Delta

Mandates followed for this update:

- `OPT_Zero_GC_Policy_AllocFree_Mandate`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration`
- `PROJECT_LTS_Compatibility_Layer`

Current measured source and documentation surface:

- `Assets/_Project`: `1212` C# files.
- `Assets/_Project/Scripts`: `1171` C# files.
- Script LOC by PowerShell `Get-Content.Count` line count: `651253`.
- Script LOC by PowerShell `Measure-Object -Line` line count: `559502`.
- `.agents-skills`: `52` mandate files.
- `Docs/**/*.md`: `436` markdown files.
- Active `Docs/**/*.md` excluding `_Archive`, `DEPRECATED`, `Reports/DEPRECATED`, and `03_OBSOLETE`: `223`.
- Active non-report docs excluding archive/deprecated/obsolete and `Docs/Reports`: `162`.
- Active `Docs/Reports/*.md`: `61`.
- Scripts created on `2026-05-06` by filesystem timestamp: `6`.
- Scripts modified on `2026-05-06` by filesystem timestamp: `142`.
- Git-untracked entries at the May 6 doc sync checkpoint: `15`.

Current top-level `Assets/_Project/Scripts` ownership counts:

| Folder | C# files |
|---|---:|
| `(root)` | `336` |
| `Editor` | `166` |
| `World` | `162` |
| `Gameplay` | `113` |
| `UI` | `82` |
| `Core` | `41` |
| `Construction` | `32` |
| `Optimization` | `22` |
| `ModdingAPI` | `21` |
| `Visor` | `20` |
| `Audio` | `19` |
| `Fauna` | `17` |

Current SpaceEngine research authority:

- research corpus: `Docs/SPACE_ENGINE_RESEARCH/`
- extracted reference corpus: `Docs/SPACE_ENGINE_RESEARCH/_extracted/`
- current SpaceEngine integration report: `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md`
- terrain/noise research anchor: `Docs/SPACE_ENGINE_RESEARCH/TERRAIN_AND_NOISE_098.md`
- runtime terrain kernel assembly folder: `Assets/_Project/Scripts/World/SpaceEngine098/`
- kernel asmdef: `Assets/_Project/Scripts/World/SpaceEngine098/Hecton8.SpaceEngine098Terrain.asmdef`
- Burst kernel source: `Assets/_Project/Scripts/World/SpaceEngine098/SpaceEngine098TerrainKernels.cs`
- MapMagic bridge nodes: `Assets/_Project/Scripts/World/HectonSpaceEngine098MapMagicNodes.cs`
- dev/editor smoke runners:
  - `Assets/_Project/Scripts/Dev/SpaceEngine098TerrainSmokeTester.cs`
  - `Assets/_Project/Scripts/Editor/SpaceEngine098TerrainSmokeTestRunner.cs`
  - `Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs`
  - `Assets/_Project/Scripts/Editor/SpaceEngineResearchJsonWriter.cs`
  - `Assets/_Project/Scripts/Editor/SpaceEngineResearchContracts.cs`

Current Planetary Sandbox / terrain shelf authority:

- sandbox shelf Burst jobs: `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs`
- sandbox shelf MapMagic node: `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfMapMagicNode.cs`
- sandbox shelf smoke tester: `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs`
- standalone smoke output: `Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`
- current sandbox surgery report: `Docs/Reports/2026-05-05_HECTON_SANDBOX_BIOMES_OMEGA_SURGERY_LOG.md`
- planetary canvas smoke: `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs`
- planetary canvas editor runners:
  - `Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTestRunner.cs`
  - `Assets/_Project/Scripts/Editor/PlanetaryCanvasMapMagicGraphIntegrator.cs`
- terrain splatmap Burst job: `Assets/_Project/Scripts/World/WorldProceduralTerrainSplatmapJobs.cs`
- terrain splatmap MapMagic node: `Assets/_Project/Scripts/World/HectonTerrainSplatmapMapMagicNode.cs`

Burst kernel ownership classification:

- `SpaceEngine098TerrainKernels.cs` belongs to the SpaceEngine 0.9.8 terrain research integration domain.
- `HectonSandboxAbyssalShelfJobs.cs` belongs to the Planetary Sandbox / macro shelf terrain domain.
- `WorldProceduralTerrainSplatmapJobs.cs` belongs to the Planetary Canvas / terrain splatmap bridge domain.
- No explicit per-agent owner string was found in source. Archivarius ownership is therefore domain-owned: World generation terrain agents, not Bootstrap/Core.

Mandate sync result for today's additions:

- `52` mandate files exist.
- Empty mandate files: `0`.
- Zero-GC/allocation-related mandate files by name: `4`.
- AUP/coordinate/submarine/voxel/MapMagic-related mandate files by name: `6`.
- Created-today script scan found `0` `using System.Linq`, `.Where(`, `.Select(`, or `.ToList(` hits.
- Created-today script scan found `4` `transform.position` hits, `0` `Vector3.Distance` hits, and `0` `math.distance` hits. No scan hit proved a greater-than-50-meter distance calculation from `transform.position`; the hits are event origin, presentation placement, or fallback position reads and remain review items.
- Created-today script scan found `25` `.Complete(` tokens and `12` `.Run(` tokens. Sampled hits are editor smoke, standalone smoke, MapMagic/generator cold paths, or comments/string guards; no hot `Tick`/`Update` completion was proven by this pass.
- Created-today script scan found `7` string interpolation or `string.Format` tokens. Sampled hits are smoke logs, source-token guards, or cold object naming; no hot `Tick`/`Update` string allocation was proven by this pass.

Interface health result from `GlobalRegistryContracts.cs`:

- Current direct public interface count: `37`.
- Interfaces with at least one source-line implementation/reference scan hit: not recounted after the 37th contract slot appeared.
- Ghost interfaces by this static scan: `PENDING VERIFICATION`.

Naming sweep result:

- Non-ASCII path entries under `Assets/_Project` and `Docs`: `638`.
- Non-ASCII path entries excluding archive/deprecated/obsolete folders: `575`.
- Non-ASCII path entries under `Assets/_Project`: `570`.
- Non-ASCII path entries under `Docs`: `68`.
- Non-ASCII content files in active scan scope: `646`.
- Current active ledger: `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NAMING_VIOLATIONS.md`.

Reality delta:

- March/legacy terrain authority was effectively a hand-authored MapMagic graph surface: `Assets/MapMagic/Map_Graph/New Gen/USE IT.asset`.
- May terrain authority has shifted toward procedural, AUP-space generation: SpaceEngine 0.9.8 math research, Burst kernels, MapMagic bridge nodes, Planetary Sandbox macro shelf jobs, and 108-biome volumetric matrix reports.
- The correct current read path is source-first: SpaceEngine research report, sandbox shelf surgery log, terrain/biome reality map, then the specific Burst kernel source.

Verification boundary:

- This was a filesystem/source/doc scan.
- No Unity Play Mode, player build, profiler, or GCMonitor run was executed by this Archivarius pass.
- Requested `ARCHIVE SYNCHRONIZED` status is not claimable under `AGENTS.md`; project status remains `PENDING VERIFICATION`.

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

Current guard result from the May 4 scan:

- Scanner exit code after the May 4 unsafe-copy/menu-loop repair: `0`.
- Global registry self-registration inventory: `500` informational sites from the broad `GlobalRegistry.Register*(this)` scan.
- Blind registry flag drift: `0`.
- `UnsafeUtility.MemCpy` outside `UnsafeMemoryCopyGuard`: `0`.
- Legacy `PlayerSignalEvents.On*` subscriptions: `0`.
- Runtime `Find*`/`GameObject.Find*` hits outside Editor folders: `8` review hits, including `ModalWindow.EnsureInstanceAvailable` and `Dev/CelestialSyncSmokeTester.AutoResolve`.
- Synchronous job `.Run(` inventory: `0`.
- Hot-path synchronous `.Run(` review queue: `0`.
- `.Complete(` text inventory: `5`.
- Guarded dispatcher completion inventory: `1`, `DispatcherJobSwap.TryComplete` with the source-level `IsCompleted`/swap-window contract.
- Unauthorized Unity loop methods: `0`.
- Direct `InputManager.Instance` inventory: `0`; hot-path direct input singleton review sites remain `0`.
- `GlobalRegistry.NativeInputManager` now exposes the bootstrap-bound native input owner from the registered `InputDispatcher` for cold UI/Input-System bridge code that still requires concrete `InputAction` APIs.
- Release-reachable one-hop `Debug.Log` review inventory: `0` after dev-build-only guards were added to hot-path-adjacent diagnostics.
- `PlayerInventory` SlowTick now has static profiler markers for the SlowTick envelope, radioactive half-life inline kernel, and reactive-chemistry inline kernel.

This is a source scan and CI candidate, not Unity Play Mode proof. Do not claim zero GC, frame-time compliance, memory retention stability, or MCP verification from this artifact alone.

STATUS: PENDING VERIFICATION

## 2026-05-06 Atlas Delta

Mandates followed for this update:

- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `OPT_Native_Memory_Collections_JobSystem_Protocol`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin`
- `PHYS_Physics_Integrity_Determinism_ForceMode`
- `DBG_Telemetry_Crash_Reporting_PostMortem`

Current measured source/docs surface:

- `Assets/_Project`: `1212` C# files.
- `Assets/_Project/Scripts`: `1171` C# files.
- Script LOC by PowerShell `Get-Content.Count` line count: `651253`.
- Script LOC by PowerShell `Measure-Object -Line` line count: `559502`.
- Scripts directly under `Assets/_Project/Scripts`: `336`.
- Strict `StartCoroutine(` hits under `Assets/_Project/Scripts`: `0`.
- `Docs`: `865` non-meta files in the current filesystem view.
- `Docs/ARCHIVARIUS REPORTS`: `120` non-meta files in the current filesystem view.
- Full `Docs/**/*.md` markdown header debt: `0` missing `Date:`, `0` missing `Status:`.
- Active non-report markdown header debt: `0`.

These counts are orientation data only. They are not readiness metrics and should not drive task priority.

Current active forensic reports to read before older atlas claims:

1. `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`
2. `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
3. `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
4. `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`
5. `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md`
6. `Docs/Reports/2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`
7. `Docs/Reports/DOOMSDAY_FLAW_REPORT.md`
8. `Docs/Reports/AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`
9. `Docs/Reports/OMEGA_CORE_ENFORCEMENT_2026-05-01.md`
10. `Docs/Reports/CI_VALIDATION_HOOKS_SURGERY_LOG.md`
11. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/02_SYSTEM_REALITY_MATRIX.md`
12. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md`

Current risk corrections:

- Headless simulation remains a first-order risk. The older `FaunaBrain.UpdateBioluminescentHypnosis()` player-camera dependency is stale by current source recheck; remaining fauna headless risk is perception/LOD logic still using player Transform/Rigidbody paths plus absent no-camera Play Mode proof. `StorageCrate` no longer relies on an Animator event for the `Opening -> Open` source transition, but Play Mode proof is absent.
- Jobs/Burst compliance improved at the `.Run(` level, but the May 4 guard scan still reports `.Complete(` inventory `5`; remaining risk is owner classification plus dispatcher-owned completion-window proof and runtime stall profiling.
- Event topology is stronger than old docs implied because late-frame dispatch budgeting and `HectonEventBus.MaxDispatchDepth = 4` exist, but recursive generation/depth is not globally proven safe.
- Core asmdef purification is not complete. The current safe position is staged bridge extraction, not blind reference removal.
- Object pool exhaustion behavior is safer by source review: spawn exhaustion publishes pool-exhausted telemetry and returns `null`; no global runtime instantiation purity claim is made for the entire project.
- `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` is now the conceptual entry point for current system ownership. It is not a runtime proof document.

Verification state:

- Unity console: latest May 4 current MCP recheck returned `0` error/warning entries. The earlier May 4 documentation sweep saw `18` warning entries from `GlobalRegistry` unregister-for-non-registered tick buckets and `SystemDispatcher` slow-threshold telemetry during `01_MAIN_MENU` Play Mode transition; that snapshot is historical.
- Runtime: latest MCP recheck reported active scene `00_BOOTSTRAP` with Play Mode off. Earlier MCP reported active scene `01_MAIN_MENU` and Play Mode transition state. Gameplay proof was not captured.
- GC/frame/memory: no fresh profiler or GCMonitor measurement.

STATUS: PENDING VERIFICATION



