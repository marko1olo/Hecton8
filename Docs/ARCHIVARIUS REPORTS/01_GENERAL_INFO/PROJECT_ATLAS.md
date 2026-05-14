# PROJECT ATLAS

Version: 1.5.29
Date: 2026-05-13
Status: PENDING VERIFICATION (DOC_AUDIT R5 STATIC OVERRIDE / UNITY RUNTIME PROOF ABSENT)
Scope: live orientation map for the current HECTON-8 workspace
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `STRM_Persistent_Object_Registry.txt`, `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`, `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`, `PROJECT_LTS_Compatibility_Layer.txt`

## 1. What This File Is

This file is a source-backed workspace atlas.
It is not a readiness claim and not a profiler report.

## 2026-05-13 DOC_AUDIT X-Ray Override

Read `../../Reports/2026-05-13_DOC_AUDIT_XRAY.md` before trusting older counters below.

Current static corrections:

- first-party asmdefs under `Assets/_Project`: `24`
- previously unindexed current asmdefs include `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef` and `Assets/_Project/Scripts/Physics/Determinism/Hecton8.Physics.Determinism.asmdef`
- root `PROJECT_ATLAS.md` is a compatibility mirror; `Docs/PROJECT_ATLAS.md` is the current detailed asmdef atlas
- cited May 11 build artifacts `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` and `.log` are absent from the current filesystem
- source counts are volatile in the active multi-agent workspace and must be rerun before use
- latest R4 static source counters: `1411` project C# files, `1365` script C# files, `1401` non-test C# files, `869871` project physical lines, `852315` script physical lines, `867132` non-test physical lines, `215` interface declaration hits, `51` direct public interfaces in `GlobalRegistryContracts.cs`, and `24` first-party asmdefs
- latest R5 package/config correction: forbidden DOTween/MasterAudio/Easy Save/Astar UPM IDs are absent, but physical legacy Astar/Easy Save/DOTween/MasterAudio asset folders remain in `Assets` and are not proof of approved runtime usage

It answers four narrow questions:

1. What is physically present in the repo now.
2. Where first-party runtime ownership actually lives.
3. Which active docs are already stale against current source.
4. Which files should be trusted first during later audits.

## 2. Workspace Snapshot

2026-05-13 DOC_AUDIT R4 static source snapshot:

| Surface | Current Value |
|---|---:|
| first-party C# files under `Assets/_Project` | 1,411 |
| first-party C# physical lines under `Assets/_Project` | 869,871 |
| first-party non-test C# files | 1,401 |
| first-party non-test C# physical lines | 867,132 |
| interface declaration hits under `Assets/_Project` | 215 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 51 |
| source/domain ids in `Docs/Actual Domains of Project.txt` | 85 |
| first-party asmdefs under `Assets/_Project` | 24 |

Current first-party asmdefs:

- `Assets/_Project/Input/Hecton8.Input.Generated.asmdef`
- `Assets/_Project/Scripts/Cartography/Hecton8.Cartography.asmdef`
- `Assets/_Project/Scripts/Core/BootstrapContracts/Hecton8.Bootstrap.Contracts.asmdef`
- `Assets/_Project/Scripts/Core/Contracts/Hecton8.Core.Contracts.asmdef`
- `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef`
- `Assets/_Project/Scripts/Core/Time/Hecton8.Core.Time.asmdef`
- `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef`
- `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- `Assets/_Project/Scripts/Input/Hecton8.Input.asmdef`
- `Assets/_Project/Scripts/Lighting/Hecton8.Lighting.asmdef`
- `Assets/_Project/Scripts/Logistics/Hecton8.Logistics.asmdef`
- `Assets/_Project/Scripts/Optimization/Editor/Hecton8.Optimization.Editor.asmdef`
- `Assets/_Project/Scripts/Physics/Determinism/Hecton8.Physics.Determinism.asmdef`
- `Assets/_Project/Scripts/Physiology/Hecton8.Physiology.asmdef`
- `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef`
- `Assets/_Project/Scripts/QA/Editor/Hecton8.QA.Editor.asmdef`
- `Assets/_Project/Scripts/QA/Hecton8.QA.asmdef`
- `Assets/_Project/Scripts/UI/Editor/Hecton8.UI.Editor.asmdef`
- `Assets/_Project/Scripts/UI/Localization/Hecton8.UI.Localization.asmdef`
- `Assets/_Project/Scripts/World/Contracts/Hecton8.World.Contracts.asmdef`
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef`
- `Assets/_Project/Scripts/World/SpaceEngine098/Hecton8.SpaceEngine098Terrain.asmdef`
- `Assets/_Project/Tests/Editor/Hecton8.EditModeTests.asmdef`
- `Assets/_Project/Tests/PlayMode/Hecton8.PlayModeTests.asmdef`

Repository root observed on 2026-05-11 contains these top-level entries of interest:

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
- generated/root evidence snapshots: `BROKEN_PREFABS.md`, `CYRILLIC_PURGE_REPORT_2026-05-10.json`, `playmode_metrics.json`, `tools_list_mcp.json`, `codex_unity_compile.log`, `codex_unity_sync.log`
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
- UI runtime code is under `Assets/_Project/Scripts/UI`; no direct `Assets/_Project/UI` folder exists in the R4 scan

Observed first-party C# file count under `Assets/_Project`: `1411`
Observed first-party C# physical line count under `Assets/_Project`: `869871`
Observed first-party C# file count under `Assets/_Project/Scripts`: `1365`
Observed first-party C# physical line count under `Assets/_Project/Scripts`: `852315`
Line-count method: PowerShell over `System.IO.StreamReader.ReadLine()` for every matched file.

SOURCE DRIFT DETECTED: previous R186 Atlas scan recorded `1292` / `1248` C# files and `742892` script physical lines; May 11 documentation continuation recorded `1306` / `1262`; current May 13 DOC_AUDIT R4 records `1411` / `1365` C# files and `852315` script physical lines.

MASSIVE REFACTOR DETECTED: same-day script line-count delta exceeds `500` lines. Treat all earlier May 7 source-count claims as superseded by this timestamped snapshot unless a newer report states otherwise.

This is physical file count only.
It does not imply quality, cohesion, or compile health.

Current build-master compile state:

- No current artifact-backed compile proof was captured by DOC_AUDIT R5.
- The previously cited May 11 build artifact `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` is absent from the current filesystem, as is the matching raw log.
- Treat the May 11 compile-success line as dated report text. Current May 14 R41 evidence is the external root `Hecton8*.csproj` no-restore CLI sweep at `0 Warning(s)` / `0 Error(s)` after restore assets exist, not Unity runtime proof.
- Unity MCP proof was not run in the May 11 continuation or DOC_AUDIT R5; do not use prior MCP-clean wording as current evidence.
- Latest current-state override: `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.
- Official Unity release-page check on 2026-05-11 found `6000.4.6f1` released on `2026-05-05`; local project evidence still pins `6000.4.1f1`. This is version drift, not upgrade approval.

## 4. External / Package Surface

Third-party and package-heavy surfaces visible in the workspace:

- Crest
- MapMagic
- Odin Inspector
- More Mountains / Feel
- GPU Instancer
- Bakery
- Volumetric Light Beam
- legacy asset-package contamination: `Assets/AstarPathfindingProject` (`605` files), `Assets/Plugins/Easy Save 3` (`422` files), `Assets/Plugins/Demigiant` (`357` files, including DOTween/DOTweenPro), and `Assets/Plugins/DarkTonic/MasterAudio` (`346` runtime files plus separate editor/resources files)

Presence on disk does not equal approved runtime usage.
Allowed usage remains constrained by `AGENTS.md`.
R5 package-lock distinction: the forbidden UPM IDs are absent from `Packages/manifest.json`; the physical `Assets` folders above still remain contamination until stripped, isolated, or quarantined by build guards.

DOTS / Entities package boundary:

- `com.unity.entities` is not declared in the current `Packages/manifest.json`.
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef` exists, but it is define-gated, `autoReferenced: false`, and not a live production assembly path without package/define proof.
- Current first-party source scan found no active `Unity.Entities`, `IComponentData`, `SystemBase`, or `ISystem` usage under `Assets/_Project/Scripts`.

## 5. Mandate Registry

Observed mandate count in `.agents-skills/*.txt`: `53`, plus `.agents-skills/README.md` as the registry index.

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
- `ARCHITECTURE/README.md`
- `ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`
- `ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
- `ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md`
- `ARCHITECTURE/DISPATCH_PIPELINE.md`
- `ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`
- `Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`
- `Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`
- `Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`
- `Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md`
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
- `../02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md`
- `EVENT_BUS_MAP.md`
- `../02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`
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

Latest documentation synchronization is `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
Fresh Unity MCP/Console proof was not captured in the May 11 documentation continuation. Older MCP readbacks from May 4-May 8 are historical snapshots only and must not be used as current Unity Console, Play Mode, profiler, GCMonitor, player-build, scene-wiring, or memory-retention proof.

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

- `GlobalRegistryContracts.cs` now contains `51` direct public interfaces, not the older `19`, `27`, `31`, `33`, `34`, `36`, `37`, `38`, `39`, `40`, or `41` snapshots.
- `SpatialAudioManager` directly implements `IAudioService` and registers through `GlobalRegistry.RegisterAudioService(this)`.
- `SuitHUDV4CanvasOverlay` is the direct first-party `IUIService` implementor found in current source scan.
- current bootstrap authority is split across `BootstrapController`, `GameBootstrapper`, and `SceneBootstrap`; it is not a single-owner startup surface.
- narrative/discovery/progression knowledge is distributed across `HectonNarrativeDirector`, `HectonDiscoveryManager`, `LoreDatabaseManager`, `AudioLogSystem`, Atlas systems, PDA archive systems, and scripted progression directors; it is not one subsystem.
- `IRenderable` is no longer a one-owner curiosity. Current direct implementors include:
  - `HectonUnderwaterVisuals`
  - `HectonSubmarineOS`
  - `MissionMarkerSystem`
- `HabitatIntegrityManager` and `HectonPlayerHealth` now implement the global `Hecton8.Core.IDamageReceiver` contract directly.
- `CombatDamageRuntime` is the current central packet lane for registered combat targets; `ToolHitUtility` routes tool hits into that lane when the receiver has a registered target id.
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
- `HectonPlayerHealth` is a real parallel HP/mutation branch, now also a packet-based `IDamageReceiver`, whose own file documents a weak persistence path because `SaveData` has no dedicated player-health DTO.

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

- `../02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md`
- `INTERFACE_CONTRACT_TABLE.md`
- `INTERFACE_STRATEGY.md`
- `DEPENDENCY_GRAPH.md`
- `../02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`
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

Historical measured source and documentation surface, superseded by the 2026-05-13 DOC_AUDIT R4 snapshot near the top of this file:

- `Assets/_Project`: `1306` C# files, `770577` physical lines.
- `Assets/_Project/Scripts`: `1262` C# files, `753858` physical lines.
- Script LOC method: PowerShell `System.IO.StreamReader.ReadLine()` over every `*.cs` file.
- SOURCE DRIFT DETECTED: previous R186 count was `1292` / `1248` C# files and `742892` script physical lines; current deltas are `+14` files under `Assets/_Project`, `+14` files under `Assets/_Project/Scripts`, and `+10966` script physical lines.
- MASSIVE REFACTOR DETECTED: the same-day script line delta is greater than `500`.
- `.agents-skills`: `53` mandate files.
- `Docs/**/*.md`: `449` markdown files.
- Active `Docs/**/*.md` excluding `_Archive`, `DEPRECATED`, `Reports/DEPRECATED`, and `03_OBSOLETE`: `236`.
- Active non-report docs excluding archive/deprecated/obsolete and `Docs/Reports`: `166`.
- Active `Docs/Reports/*.md`: `70`.
- Scripts created on `2026-05-07` by filesystem timestamp: `23`.
- Scripts modified on `2026-05-07` by filesystem timestamp: `197`.
- Git-untracked entries at the May 6 doc sync checkpoint: `15`.

Current top-level `Assets/_Project/Scripts` ownership counts:

| Folder | C# files |
|---|---:|
| `(root)` | `335` |
| `Editor` | `169` |
| `World` | `152` |
| `Gameplay` | `123` |
| `UI` | `84` |
| `Core` | `49` |
| `Construction` | `35` |
| `Optimization` | `22` |
| `Interaction` | `22` |
| `Visor` | `22` |
| `ModdingAPI` | `21` |
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
- MapMagic bridge nodes: `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs`
- dev/editor smoke runners:
  - `Assets/_Project/Scripts/Dev/SpaceEngine098TerrainSmokeTester.cs`
  - `Assets/_Project/Scripts/Editor/SpaceEngine098TerrainSmokeTestRunner.cs`
  - `Assets/_Project/Scripts/Editor/SpaceEngineResearchSmokeTester.cs`
  - `Assets/_Project/Scripts/Editor/SpaceEngineResearchJsonWriter.cs`
  - `Assets/_Project/Scripts/Editor/SpaceEngineResearchContracts.cs`

Current Planetary Sandbox / terrain shelf authority:

- sandbox shelf Burst jobs: `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfJobs.cs`
- sandbox shelf MapMagic node: `Assets/_Project/Scripts/Plugins/MapMagic/HectonSandboxAbyssalShelfMapMagicNode.cs`
- sandbox shelf smoke tester: `Assets/_Project/Scripts/World/HectonSandboxAbyssalShelfSmokeTester.cs`
- standalone smoke output: `Docs/SPACE_ENGINE_RESEARCH/HectonSandboxAbyssalShelfStandaloneSmoke.json`
- current sandbox surgery report: `Docs/Reports/2026-05-05_HECTON_SANDBOX_BIOMES_OMEGA_SURGERY_LOG.md`
- planetary canvas smoke: `Assets/_Project/Scripts/World/PlanetaryCanvasSmokeTester.cs`
- planetary canvas editor runners:
  - `Assets/_Project/Scripts/Editor/PlanetaryCanvasSmokeTestRunner.cs`
  - `Assets/_Project/Scripts/Editor/PlanetaryCanvasMapMagicGraphIntegrator.cs`
- terrain splatmap Burst job: `Assets/_Project/Scripts/World/WorldProceduralTerrainSplatmapJobs.cs`
- terrain splatmap MapMagic node: `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs`

Burst kernel ownership classification:

- `SpaceEngine098TerrainKernels.cs` belongs to the SpaceEngine 0.9.8 terrain research integration domain.
- `HectonSandboxAbyssalShelfJobs.cs` belongs to the Planetary Sandbox / macro shelf terrain domain.
- `WorldProceduralTerrainSplatmapJobs.cs` belongs to the Planetary Canvas / terrain splatmap bridge domain.
- No explicit per-agent owner string was found in source. Archivarius ownership is therefore domain-owned: World generation terrain agents, not Bootstrap/Core.

Mandate sync result for today's additions:

- `53` mandate `.txt` files exist.
- Empty mandate files: `0`.
- Zero-GC/allocation-related mandate files by name: `4`.
- AUP/coordinate/submarine/voxel/MapMagic-related mandate files by name: `6`.
- Created-today script scan found `0` `using System.Linq`, `.Where(`, `.Select(`, or `.ToList(` hits.
- Created-today script scan found `4` `transform.position` hits, `0` `Vector3.Distance` hits, and `0` `math.distance` hits. No scan hit proved a greater-than-50-meter distance calculation from `transform.position`; the hits are event origin, presentation placement, or fallback position reads and remain review items.
- Created-today script scan found `25` `.Complete(` tokens and `12` `.Run(` tokens. Sampled hits are editor smoke, standalone smoke, MapMagic/generator cold paths, or comments/string guards; no hot `Tick`/`Update` completion was proven by this pass.
- Created-today script scan found `7` string interpolation or `string.Format` tokens. Sampled hits are smoke logs, source-token guards, or cold object naming; no hot `Tick`/`Update` string allocation was proven by this pass.

Interface health result from `GlobalRegistryContracts.cs`:

- Current direct public interface count: `51`.
- Interfaces with at least one source-line implementation/reference scan hit: not fully recounted after the 51st contract slot appeared.
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

Historical measured source/docs surface, superseded by the 2026-05-13 DOC_AUDIT R4 snapshot near the top of this file:

- `Assets/_Project`: `1292` C# files, `759122` physical lines.
- `Assets/_Project/Scripts`: `1248` C# files, `742892` physical lines.
- Script LOC method: PowerShell `System.IO.StreamReader.ReadLine()` over every `*.cs` file.
- SOURCE DRIFT DETECTED: previous same-day Atlas count was `1214` / `1173` C# files and `656678` script-line proxy count; current deltas are `+78` files under `Assets/_Project`, `+75` files under `Assets/_Project/Scripts`, and `+86214` script physical lines.
- MASSIVE REFACTOR DETECTED: the same-day script line delta is greater than `500`.
- Scripts directly under `Assets/_Project/Scripts`: `342`.
- Strict `StartCoroutine(` hits under `Assets/_Project/Scripts`: `0`.
- `Docs`: `897` total files in the current filesystem view (`866` non-meta).
- `Docs/ARCHIVARIUS REPORTS`: `153` total files in the current filesystem view (`122` non-meta).
- Active non-obsolete `Docs/**/*.md` markdown header debt: `0` missing `Date:`, `0` missing `Status:`.
- Active non-report markdown header debt: `0`.

These counts are orientation data only. They are not readiness metrics and should not drive task priority.

Current active forensic reports to read before older atlas claims:

1. `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`
2. `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`
3. `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`
4. `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`
5. `Docs/Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md`
5. `Docs/Reports/2026-05-08_ACTIVE_DOCUMENTATION_MANIFEST.json`
6. `Docs/Reports/2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md`
7. `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`
8. `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
9. `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
10. `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`
11. `Docs/Reports/CI_VALIDATION_HOOKS_SURGERY_LOG.md`
12. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md`

Current risk corrections:

- Headless simulation remains a first-order risk. The older `FaunaBrain.UpdateBioluminescentHypnosis()` player-camera dependency is stale by current source recheck; remaining fauna headless risk is perception/LOD logic still using player Transform/Rigidbody paths plus absent no-camera Play Mode proof. `StorageCrate` no longer relies on an Animator event for the `Opening -> Open` source transition, but Play Mode proof is absent.
- Jobs/Burst compliance improved at the `.Run(` level, but the May 4 guard scan still reports `.Complete(` inventory `5`; remaining risk is owner classification plus dispatcher-owned completion-window proof and runtime stall profiling.
- Event topology is stronger than old docs implied because late-frame dispatch budgeting and `HectonEventBus.MaxDispatchDepth = 4` exist, but recursive generation/depth is not globally proven safe.
- Core asmdef purification is not complete. The current safe position is staged bridge extraction, not blind reference removal.
- Object pool exhaustion behavior is safer by source review: spawn exhaustion publishes pool-exhausted telemetry and returns `null`; no global runtime instantiation purity claim is made for the entire project.
- Stable docs are now the conceptual entry point for current system ownership: `Docs/README.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, and `Docs/ARCHITECTURE/README.md`. `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` remains evidence/reference, not runtime proof.

Verification state:

- Unity console: no fresh Unity MCP/Console proof is available in the current session; `list_mcp_resources` returned an empty resource list. Older May 4-May 8 console snapshots are historical and must not be treated as current proof.
- Runtime: latest usable MCP recheck is historical only and reported active scene `00_BOOTSTRAP` with Play Mode off. Earlier MCP reported active scene `01_MAIN_MENU` and Play Mode transition state. Gameplay proof was not captured.
- GC/frame/memory: no fresh profiler or GCMonitor measurement.

STATUS: PENDING FINAL UNITY PROOF



