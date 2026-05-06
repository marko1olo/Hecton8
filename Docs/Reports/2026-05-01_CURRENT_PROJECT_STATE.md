# HECTON-8 Current Project State

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: conceptual source-backed project state, not a runtime certification report

Path note: filename retained as the stable current-state anchor. This file now includes May 2 documentation/build-evidence, May 3 event-generation/foundation/settings/registry/job-barrier addenda, and a May 4 documentation/build/guard addendum.

## 2026-05-04 Current Addendum

- Latest broad documentation sweep: `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`.
- Latest first-party warning cleanup: `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`.
- Latest foundation guard repair addendum: `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`.
- Latest local Core compile after the warning cleanup reports `0 Warning(s)` and `0 Error(s)`.
- Fresh local Editor compile now reports `0 Warning(s)` and `0 Error(s)`.
- Latest `Hecton8.World.Dots.csproj --no-restore` build reports `0 Warning(s)` and `0 Error(s)`.
- Latest `Hecton8.PlayModeTests.csproj --no-restore` build reports `0 Warning(s)` and `0 Error(s)`.
- Fresh foundation guard scan after the repair exits `0`.
- Current guard inventory: `.Run(` sites `0`, hot-path `.Run(` review sites `0`, `.Complete(` text hits `5`, guarded dispatcher completion sites `1`, `UnsafeUtility.MemCpy outside guard` `0`, unauthorized Unity loop methods `0`, runtime Find API text hits `8`.
- Current source snapshot: `1118` first-party `.cs` under `Assets/_Project`, `1078` `.cs` under `Assets/_Project/Scripts`.
- Current active `Docs/**/*.md` inventory excluding archive/deprecated/obsolete: `191`.
- Current active/root markdown surface excluding archive/deprecated/packages/third-party readmes: `217`.
- Earlier documentation-sweep MCP readback after retry: active scene `01_MAIN_MENU`, editor in Play Mode transition, console errors `0`, console warnings `18`, render textures `32`, render texture bytes `68215964`.
- Latest current MCP recheck after the foundation guard repair: Unity `6000.4.1f1`, active scene `00_BOOTSTRAP`, Play Mode off, compiling false, ready for tools, console error/warning entries `0`. This is editor evidence only, not Play Mode proof.
- May 4 celestial/orbital/meteor/terrain reports are source/build/console evidence only. They do not prove PlayMode behavior, profiler GC, memory retention, in-scene visuals, in-scene audio, or save/load/runtime correctness.
- Root `TERRAIN_AND_BIOME_REALITY_MAP.md` is not active authority; use `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md`.

## Mandates Followed

- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`

## What This File Is

This file is the current conceptual entry point for agents.
It reconciles active docs with current source ownership at a system level.

This file is not proof of:

- clean Play Mode
- fixed editor deadlock
- zero GC
- stable memory retention
- shipping readiness

No bounded PlayMode gameplay run, GCMonitor capture, profiler pass, or memory-retention soak was captured for this update.
Local `Editor.log` evidence exists in `Docs/Reports/2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md`, `Docs/Reports/2026-05-01_EVENT_BUS_SPATIAL_HASH_COMPILE_DELTA.md`, and `Docs/Reports/2026-05-01_COMPILE_STABILIZATION_CONTINUATION.md`.
May 1, May 2, and May 3 build/guard details remain historical evidence in their dated reports. The May 4 addendum above supersedes source counts, build status, guard inventory, and MCP console boundary for current work. This file still does not provide Play Mode, GC, profiler, scene/prefab, or runtime-stability proof.
The May 3 settings persistence rebind added a `SettingsManager`/`UserOptionsPersistence` registry-order hardening pass and a fresh `dotnet build .\Hecton8.Core.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false /clp:Summary /v:minimal` result with `0 Warning(s)` / `0 Error(s)`.
The May 3 interaction prompt cache pass moved `EndingTerminalInteractable.GetInteractText()` to cached localized strings refreshed in lifecycle/language-change paths and produced a full `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false` result with `0 Warning(s)` / `0 Error(s)`. This is historical source/build evidence only.

Latest high-authority addenda:

- `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
- `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`
- `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`
- `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md`
- `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md`
- `Docs/Reports/2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md`
- `Docs/Reports/2026-05-03_CONSOLE_VR_READINESS_AUDIT.md`
- `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`
- `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SIGNAL_CLEANUP.md`
- `Docs/Reports/2026-05-03_OPTIMIZATION_REGISTRY_OWNERSHIP.md`
- `Docs/Reports/2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md`
- `Docs/Reports/2026-05-03_REGISTRY_RENDERABLE_AND_JOB_BARRIER_GUARD.md`
- `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
- `Docs/Reports/2026-05-03_HABITAT_GRAPH_ANCHOR_STATE_HARDENING.md`
- `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` - historical May 2 broad sweep, not latest authority.

## Current Runtime Shape

HECTON-8 is a real, large Unity runtime with several mature subsystems and several authority conflicts.
The codebase is not paperware, but it is not architecturally clean.

Current source-backed shape:

- Bootstrap is split across `BootstrapController`, `GameBootstrapper`, `SceneBootstrap`, scene roots, and service self-registration paths.
- `GlobalRegistry` is the dominant service locator, but not the only authority because singleton and `DontDestroyOnLoad` residues still exist. May 3 registry truth-state hardening removed the current blind broad `GlobalRegistry.Register*(...this...)` and renderable self-registration flag pattern by source scan; service sovereignty is still not globally solved.
- `SystemDispatcher` is the central cadence owner for tick, fixed tick, late-frame tick, event flushing, and slow tick, but many systems still own local job completion decisions.
- World runtime is real and heavy: `PersistentWorldRegistry`, `HectonWorldGenerator`, `HectonVoxelEngine`, `VoxelDeltaProcessor`, `WorldProceduralScatterDirector`, MapMagic vegetation bridge, geology directors, voxel nav, ocean, weather, fauna, and spatial hash all exist as substantial code. May 3 source/report evidence removed a forced PhysX-bake completion path from chunk retirement and added mesh-build watchdogs, but Play Mode eviction stress and memory-retention proof remain absent.
- Save runtime is real: `SaveManager` and `SaveBinaryStorage` implement a serious binary/native/integrity-oriented stack, but runtime save/load proof is still required.
- Narrative/lore/progression ownership is distributed and real: `HectonNarrativeDirector`, `LoreDatabaseManager`, `AudioLogSystem`, Atlas systems, `QuestManager`, scan/PDA/archive systems, and scripted arc directors are separate owners. Latest source recheck hardened `LoreDatabaseManager` hash lookup completion, save-service registration ownership, and found no duplicate hardcoded lore hashes in the 50 fixed industrial lore seeds; Play Mode and save/load roundtrip proof are still absent.
- Input rebinding is currently a compile-stabilized bridge: the live `RebindingManager` implementation is under the Core assembly path while preserving the old MonoScript GUID, and the old Input path is a tombstone with a new GUID for stale Unity/Bee source-list safety. This is source-compile evidence, not proof that Core asmdef purity is solved.
- UI/HUD runtime is one of the stronger production surfaces: `SuitHUDV4CanvasOverlay`, `TMP_TextRegistry`, `UIStateStore`, char-buffer formatting, and menu/PDA systems exist. Latest settings persistence hardening makes `SettingsManager` prefer `GlobalRegistry.Settings`, refresh `GlobalRegistry.UserOptions` before load/save helpers, and receive a bootstrap UI-phase persistence refresh. Presentation-owned gameplay state still exists elsewhere and must not be ignored.
- Construction/base runtime is real: `ConstructionManager`, `HabitatGraphManager`, `BaseModule`, `BaseAirlock`, logistics, integrity, life support, and module graph code exist. Latest source recheck added the missing pressure-buckling stress path and structural stress trigger bridge; the May 3 continuation also added a guarded `BaseAirlock` dispatcher-registration retry and `HabitatGraphManager` native-disposal defaulting. The May 3 anchor-state pass separated `HabitatGraphManager` authoritative `_anchorReachability` from traversal scratch so component/flood/fungal BFS paths cannot overwrite anchored/isolated truth before graph publish. Runtime gameplay, graph traversal behavior, graph teardown, and scene interaction proof are still absent.
- Audio runtime is real: `SpatialAudioManager` owns `IAudioService`, procedural audio systems exist, and audio events are partially queue-backed. Latest source recheck added the structural stress procedural audio payload/event bridge; legacy/static residue remains.
- Abyssal thermal/base coupling is now source-present: `AbyssalThermalManager` contains habitat thermal infiltration into active `BaseModule` room temperature on `SlowTick`. This is compile-checked only, not balanced or profiled.
- Event topology is mixed: several first-party lanes are `NativeQueue`-backed and flushed by `SystemDispatcher.LateUpdate()`, while direct static delegate buses and the managed mod-facing `HectonEventBus` still exist. Source recheck shows dispatcher budget and `HectonEventBus.MaxDispatchDepth` are present; NativeQueue generation split is source-present only for `ModRegistryEvents`, `BootstrapEvents`, `LocalizationEvents`, `InteractionEvents`, `CraftingEvents`, `ScanEvents`, `SaveEvents`, `InventoryEvents`, `WeatherEvents`, `QuestEvents`, `PowerGridTelemetryEvents`, `NarrativeEvents`, `NotificationEvents`, `FirstHourEvents`, `EndingEvents`, `AudioLogEvents`, `AtmosphereEvents`, `EclipseGameplayEvents`, `AcousticZoneEvents`, `PhysicsEventBus`, `CelestialEvents`, `FluidFeedbackEvents`, `RepairDroneTorchAcousticEvents`, `ElectrolysisAcousticEvents`, `AudioCaptionEvents`, `SpectrumEvents`, `ProceduralAudioEvents`, `HectonSubmarineOsEvents`, `LaserCutterEvents`, `MapMagicBiomeEvents`, `BiomeMatrixEvents`, `DirectorAIEvents`, `HectonDroneFleetEvents`, `FlashlightEvents`, `PlayerSignalEvents`, `HighPressureEvents`, `FatalPressureImplosionEvents`, `ModuleStatusEvents`, `DepthZoneEvents`, `SoundscapeEvents`, `EmergencyServiceRelayEvents`, `SargassumGlobalDragManager`, `AtlasSignalEvents`, `PlayerExpressionEvents`, `BaseIntegrityEvents`, `PDAIntrusionEvents`, `PDAEvents`, `SceneBootstrap`, `ObjectPoolDiagnostics`, `PerformanceEvents`, `RandomEventEvents`, `Atlas6Events`, and `GlobalRegistry` service rebound events, not globally proven.
- Object pooling has hardened exhaustion behavior by current source review, but project-wide "no runtime Instantiate" is not proven.

## Current System Authority Map

Concept-level companion:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`

Use that file when the question is not "which exact class owns this line" but "is this domain load-bearing, transitional, presentation-only, experimental, or historical."

| Domain | Current Primary Owners | Current State |
|---|---|---|
| Bootstrap | `BootstrapController`, `GameBootstrapper`, `SceneBootstrap` | Split authority; not one sovereign startup path. |
| Registry | `GlobalRegistry`, `GlobalRegistryContracts` | Central and broad; singleton residue remains. |
| Cadence | `SystemDispatcher`, `GameTickManager` | Real dispatcher model; local job barriers still exist. |
| Scene loading | `SceneRuntimeService`, bootstrap route guards | Real async service surface; activation/warmup proof absent. |
| Pooling | `ObjectPoolManager`, `PoolBudgetProfile` | Hardened source direction; runtime purity unverified. |
| Save | `SaveManager`, `SaveBinaryStorage`, `SaveEvents` | Serious implementation; runtime validation pending. |
| World persistence | `PersistentWorldRegistry`, `WorldSpatialHashGrid` | Real native/state ownership; handle and deferred maintenance risks remain. |
| World generation | `HectonWorldGenerator`, `HectonVoxelEngine`, `VoxelDeltaProcessor` | Real and heavy; deadlock/stall risk remains in job lifecycle boundaries. |
| Scatter/vegetation | `WorldProceduralScatterDirector`, `HectonMapMagicVegetationBridge`, scatter backends | Real but oversized; editor/gizmo and job ownership risks remain. |
| Geology | `WorldGenerativeGeology*` family | Integration layer exists; dispatcher/registry guards were recently hardened by source review only. |
| Construction/base | `ConstructionManager`, `HabitatGraphManager`, `BaseModule`, `BaseAirlock` | Real gameplay domain; native lifecycle and authority boundaries require continued review. |
| Player/tools | `HectonPlayerMovement`, `PlayerTool`, `ModularEquipmentEngine`, `EquipmentInteractionHandler` | Real but overloaded; tool action and persistence ownership are split. |
| UI/HUD | `SuitHUDV4CanvasOverlay`, `TMP_TextRegistry`, `UIStateStore`, PDA/menu controllers | Comparatively mature; gameplay must not depend on UI/Animator events. |
| Audio | `SpatialAudioManager`, procedural audio renderers, audio event lanes | Real DSP direction; legacy service patterns remain. |
| Fauna/ecosystem | `FaunaBrain`, `FaunaDirector`, `EcosystemDirector`, fauna registries | Real but integration-heavy; headless/AUP risks remain. |
| Submarine | `HectonSubmarineOS`, `SubmarineCoreDirector`, `SubmarineElectrolysisModule`, structural systems | Real domain; mixed service/tick ownership is being purged gradually. |

## Current Critical Truths

These are the current high-level truths to preserve across future docs:

- Latest broad compile evidence is May 4: serial local `Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, `Hecton8.World.Dots.csproj`, and `Hecton8.PlayModeTests.csproj` builds returned `0 Warning(s)` / `0 Error(s)`. Treat this as command-line compile evidence only.
- Latest registry/foundation guard evidence is May 4 after the unsafe-copy and menu-loop repair: `Tools/ReloadAudit/Scan-FoundationGuards.ps1` exits `0`. Current inventory: self-registration sites `500`, blind registry flag drift `0`, origin-shift listener drift `0`, `.Run(` sites `0`, hot-path `.Run(` review sites `0`, `.Complete(` text hits `5`, guarded dispatcher completion sites `1`, `UnsafeUtility.MemCpy outside guard` `0`, direct `InputManager.Instance` sites `0`, optimization singleton residue `0`, unauthorized Unity loop methods `0`, legacy coroutine sites `0`, forbidden runtime asset API sites `0`, release-reachable direct hot-path `Debug.Log` sites `0`, release-reachable one-hop debug-log review sites `0`, broad physics masks outside Editor `0`, and runtime Find API review hits `8`. Fresh MCP editor readback after script refresh reports `00_BOOTSTRAP`, Play Mode off, console errors `0`, warnings `0`. Treat this as source/guard/editor-compile evidence only, not runtime stall proof.
- May 3 interaction prompt cache evidence: `EndingTerminalInteractable` now registers as a localization language listener, returns cached localized prompt strings from `GetInteractText()`, caches the ATLAS-6 data-loaded warning text, and avoids redundant hover/active-indicator `SetActive` calls. Local Core build returned `0 Warning(s)` / `0 Error(s)`. Treat this as historical source/build evidence only, not runtime localization or UI-flow proof.
- Earlier May 2 local dotnet build evidence is clean for `Hecton8.Core.csproj`: restore build `0 Error(s)`, `136 Warning(s)`; latest post-restore `--no-restore` rerun `0 Error(s)`, `73 Warning(s)`. Treat this as compile evidence only.
- Do not claim the Play Mode/editor deadlock is fixed. Static patches reduced candidate classes; runtime proof is absent.
- Do not claim global zero-GC. Some systems are designed for zero-GC, but project-wide profiler proof is absent.
- Do not claim DOTS is production architecture. DOTS/Entities is currently a seam/stub direction, not the live backbone.
- Do not claim Entities is installed. Current `Packages/manifest.json` does not declare `com.unity.entities`; `Assets/_Project/Scripts/World/Dots` is a gated placeholder lane.
- Do not claim Core asmdef isolation is complete. The active enforcement report explicitly rejected blind removal.
- Do not claim event architecture is fully queue-backed or globally generation-split. The project uses both queue-backed lanes and direct/static managed buses; event budget/depth guards exist, and only `ModRegistryEvents` / `BootstrapEvents` / `LocalizationEvents` / `InteractionEvents` / `CraftingEvents` / `ScanEvents` / `SaveEvents` / `InventoryEvents` / `WeatherEvents` / `QuestEvents` / `PowerGridTelemetryEvents` / `NarrativeEvents` / `NotificationEvents` / `FirstHourEvents` / `EndingEvents` / `AudioLogEvents` / `AtmosphereEvents` / `EclipseGameplayEvents` / `AcousticZoneEvents` / `PhysicsEventBus` / `CelestialEvents` / `FluidFeedbackEvents` / `RepairDroneTorchAcousticEvents` / `ElectrolysisAcousticEvents` / `AudioCaptionEvents` / `SpectrumEvents` / `ProceduralAudioEvents` / `HectonSubmarineOsEvents` / `LaserCutterEvents` / `MapMagicBiomeEvents` / `BiomeMatrixEvents` / `DirectorAIEvents` / `HectonDroneFleetEvents` / `FlashlightEvents` / `PlayerSignalEvents` / `HighPressureEvents` / `FatalPressureImplosionEvents` / `ModuleStatusEvents` / `DepthZoneEvents` / `SoundscapeEvents` / `EmergencyServiceRelayEvents` / `SargassumGlobalDragManager` / `AtlasSignalEvents` / `PlayerExpressionEvents` / `BaseIntegrityEvents` / `PDAIntrusionEvents` / `PDAEvents` / `SceneBootstrap` / `ObjectPoolDiagnostics` / `PerformanceEvents` / `RandomEventEvents` / `Atlas6Events` / `GlobalRegistry` service rebound events currently have source-level same-frame reenqueue isolation.
- Do not use older scatter manifesto teardown examples as current native-memory law. Current `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt` and source ownership win.
- Do not claim all docs are current. Active entry docs are current enough to guide work; older dated docs must be treated as historical unless linked by current indexes.
- Do not treat presentation, QA harnesses, DOTS seams, networking placeholders, or modding boundaries as load-bearing gameplay just because source files exist.

## Current P0 Risks

1. Editor/Play Mode deadlock truth is still fragmented.
2. Runtime job completion ownership is still too local in several cadence-sensitive systems.
3. Headless gameplay state still depends on presentation in known places such as fauna look logic. `StorageCrate` no longer relies on an Animator event for the `Opening -> Open` transition at source level, but Play Mode proof is absent.
4. Broad physics masks and default layer fallbacks are partially reduced. Current source recheck no longer finds the earlier cited `~0` query masks in `AutonomousExtractorSystem`, `WorldCaveDirector`, or `WorldProceduralFieldSampler`; scene-layer validation is still required before claiming physics filtering is complete.
5. Service authority remains mixed between registry-owned services and singleton/DDOL owners.
6. Large world/scatter/fauna/player files remain reliability risks because ownership concentration is too high.

## Documentation Authority

Use this read order for current state:

1. `../AGENTS.md`
2. task-relevant files under `.agents-skills/`
3. `Docs/README.md`
4. `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`
5. `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`
6. `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`
7. `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md`
8. `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md`
9. `Docs/Reports/2026-05-03_SETTINGS_PERSISTENCE_REGISTRY_REBIND.md`
10. `Docs/Reports/2026-05-03_REGISTRY_RENDERABLE_AND_JOB_BARRIER_GUARD.md`
11. `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
12. `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`
13. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
14. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
15. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/02_SYSTEM_REALITY_MATRIX.md`
16. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md`
17. system-specific docs and source files

`Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` is the current blunt project-level verdict.
It does not replace this file as the system-shape entry point; it summarizes the overall risk/readiness conclusion.

If an older document disagrees with this file and current source, treat the older document as historical until rechecked.

## Current System-Specific Maps

These maps are still useful for domain navigation, but none of them is runtime proof:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PLAYER_GAMEPLAY_CORE_MAP.md` - player-facing gameplay ownership.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md` - construction, habitat graph, modules, logistics, and power.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/TOOLS_INTERACTION_OPERATIONAL_SYSTEM_MAP.md` - tools, interaction routing, scanner/cutter/repair/beacon branches.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/SURVIVAL_DAMAGE_HAZARD_SYSTEM_MAP.md` - survival, damage, pressure, thermal, hazards, and stress consequences.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md` - narrative, scan, lore, Atlas, quest, and progression ownership.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` - UI, HUD, visor, PDA, and audio presentation ownership.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md` - world, environment, ocean, debris, ecology, and submarine ownership.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md` - broad owner ledger for major gameplay domains.
- `Docs/PROCEDURAL_ASSET_PIPELINE.md` - procedural asset production contract.
- `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` - procedural category/runtime architecture contract.
- `Docs/QUALITY_GATES.md` - validation gates; checklist only, not proof by itself.
- `Docs/ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md` - readable geology/seismic ownership reference.
- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md` - audio DSP architecture reference.
- `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md` - GPU abyssal flow/weather architecture reference; verify shader/console before runtime claims.
- `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md` - UI zero-GC contract and pattern reference.
- `Docs/ARCHITECTURE/SAVE_V8_BINARY_SPEC.md` and `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` - save container and paging contracts.
- `Docs/ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md` - habitat graph/logistics contract.
- `Docs/ARCHITECTURE/HEADLESS_ECOSYSTEM_SIMULATION.md` - headless fauna/ecosystem contract.
- `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md` and `Docs/ARCHITECTURE/KINEMATICS_AUP_INTEGRATION.md` - AUP/floating-origin/kinematics standards.
- `Docs/ARCHITECTURE/DRONE_FLEET_PROTOCOL.md` - headless drone fleet/native-slot architecture reference.
- `Docs/ARCHITECTURE/EQUIPMENT_SOA_LAYOUT.md` - equipment/tool SOA contract; not prefab/save/UI proof.
- `Docs/ARCHITECTURE/KINETIC_ENTANGLEMENT.md`, `Docs/ARCHITECTURE/ORGANIC_ENTROPY_MATH.md`, and `Docs/ARCHITECTURE/MIGRATORY_FLORA_SYSTEM.md` - organic/flora interaction contracts; re-open source/assets before surgery.
- `Docs/ARCHITECTURE/PROJECT_CONTENT_LEDGER.md` - resource/hash coordination ledger; authored data reference, not scene validation.
- `Docs/ARCHITECTURE/QUEST_DAG_PROTOCOL.md`, `Docs/ARCHITECTURE/REACTIVE_ECONOMY_SYSTEM.md`, and `Docs/ARCHITECTURE/ZERO_GC_FABRICATION.md` - quest, economy, and fabrication contracts.
- `Docs/ARCHITECTURE/SCANNER_DATA_MINING.md`, `Docs/ARCHITECTURE/SUBMARINE_OS_MANUAL.md`, and `Docs/ARCHITECTURE/TRAUMA_GLITCH_SYSTEM.md` - scanner/PDA, submarine OS, and trauma/presentation contracts.
- `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md` and `Docs/ARCHITECTURE/URP_SCREENSHOT_PIPELINE.md` - third-party boundary and save-thumbnail rendering contracts.
- `Docs/AI_Fauna/README.md` - fauna concept and coverage reference; not runtime spawn or prefab proof.
- `Docs/Flora_Pipeline/README.md` - active flora execution bundle; not final asset/import/runtime validation proof.
- `Docs/Legacy_World_Reference/README.md` and `Docs/Legacy_Backlog/README.md` - historical reference only, not current authority.

Line-number evidence inside older maps may drift after source edits. Treat class/file ownership as the useful layer and re-open source before surgery.

## Verification State

- Play Mode: MCP saw editor state in Play Mode transition with active scene `01_MAIN_MENU`; no bounded gameplay run, scene-flow assertion, profiler pass, or GC capture was recorded.
- Unity console: earlier May 4 documentation-sweep MCP readback returned console errors `0` and console warnings `18`; latest current recheck returned `0` error/warning entries. Treat both as editor-console readback only, not Play Mode proof.
- MCP: earlier May 4 readback saw `ready_for_tools: true`, active scene `01_MAIN_MENU`, build settings `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`, render textures `32`, and render texture bytes `68215964`; latest current recheck saw `00_BOOTSTRAP`, Play Mode off, compiling false, ready for tools, and console error/warning entries `0`.
- Runtime GC: not measured.
- Memory retention: not measured.
- Code compilation: latest May 4 serial `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` returned `0 Warning(s)` / `0 Error(s)`; latest May 4 serial `Hecton8.Editor.csproj`, `Hecton8.World.Dots.csproj`, and `Hecton8.PlayModeTests.csproj` builds also returned `0 Warning(s)` / `0 Error(s)`. May 3 and May 2 build results remain historical compile evidence. This is dotnet compile evidence, not Unity Play Mode proof.
- Settings persistence rebind compile: `dotnet build .\Hecton8.Core.csproj --no-restore /m:1 /nr:false /p:UseSharedCompilation=false /clp:Summary /v:minimal` returned `0 Warning(s)` and `0 Error(s)`. This is compile evidence only, not settings-panel scene flow or PlayerPrefs persistence proof.
- Console/VR readiness cleanup compile: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false` returned `0 Warning(s)` and `0 Error(s)` after URP shadow-distance authority and cached haptic-device dispatch cleanup. This is source compile evidence only, not console/VR runtime proof.
- Unity batchmode: historical May 3 log `Temp/CodexArtifacts/unity-batch-2026-05-03-foundation-hardening-after-watchdogs.log` exits batchmode successfully, reports Tundra build success, reports Mono reload success, and strict compiler-failure scan found `0` matches. Older conflicting artifacts remain historical unless re-run cleanly.
- Unity EditMode tests: May 3 spatial-hash self-test evidence passed `3/3` in `Temp/CodexArtifacts/editmode-results-2026-05-03-spatialhash-selftest-after-collections.xml`. This does not prove runtime pressure or Play Mode behavior.

## Regression Model

CPU: habitat graph anchor-state patch adds no new traversal pass; it redirects existing BFS visited writes to a separate native scratch buffer. Renderable/service truth-state checks add lifecycle-only bucket containment or slot `ReferenceEquals` work on registration. No profiler numbers were captured. May 3 foundation-hardening runtime changes still need Play Mode eviction stress.
GC: no managed allocations were added in the habitat graph hot paths or service/renderable registration hot paths; measured 0 B/frame proof is absent.
Memory: `HabitatGraphManager` now owns one additional persistent `NativeArray<byte>` sized to node capacity. Runtime retention is unmeasured. Registry guard tooling changes no runtime memory.
Cadence: the prior tether visual, `PlayerInventory`, `CraftingSystem`, `QuestStateManager`, `SargassumMicroFaunaBoids`, `FaunaSimulationEngine`, scatter, flora, and wreck `.Run(` inventory is now absent from the current first-party source guard. `FloraInteractionManager` cascade phase seeds now schedule an `IJobParallelFor` and publish after late-frame completion. This is source inventory only; runtime frame-time and cascade latency remain unverified.
Correctness: current evidence includes May 4 serial Core/Editor/DOTS/PlayModeTests compile `0 Warning(s)` / `0 Error(s)`, post-repair May 4 guard scan exit `0`, latest warning-cleanup Unity console readback `0` error/warning entries after clear/script refresh, May 3 Unity batchmode success from the earlier foundation pass, and May 3 spatial-hash EditMode self-test `3/3`. Gameplay behavior, lore save/load roundtrip, construction graph traversal behavior, tether visual scene behavior, inventory degradation/reactive chemistry, crafting/deconstruction gameplay behavior, quest signal progression, service/renderable scene registration pressure, and construction graph teardown remain unverified.

## Hot Path Impact

Habitat graph hot paths still use preallocated native arrays and indexed loops. The new `_traversalVisited` allocation is persistent graph-buffer memory, not per-frame heap work. Service/renderable registration changes are lifecycle-only and do not alter draw submission, physics packet, or debris simulation loops. Existing source changes in the dirty worktree still require their own runtime/profiler verification.

## Failure Modes

- This file will drift if future agents patch systems without updating active docs.
- Runtime verification can contradict static source review.
- Scene/prefab wiring can still contradict code ownership maps.
- Dirty worktree changes by other agents can make this snapshot incomplete without a fresh re-scan.

STATUS: PENDING VERIFICATION
