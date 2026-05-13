# HECTON-8 Global Architecture Map

Date: 2026-05-13
Status: PENDING VERIFICATION
Scope: source-backed architecture map only, not a profiler capture and not a build-certification report

## 2026-05-13 DOC_AUDIT X-Ray Override

Read `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` before trusting older numeric counters in this file.

Current static audit corrections:

- first-party asmdefs under `Assets/_Project`: `24`
- previously unindexed current asmdefs include `Hecton8.Core.Memory` and `Hecton8.Physics.Determinism`
- cited May 11 build artifacts `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` and `.log` are absent from the current filesystem
- latest R4 static source counters: `1411` project C# files, `1365` script C# files, `1401` non-test C# files, `869871` project physical lines, `852315` script physical lines, `867132` non-test physical lines, `215` interface declaration hits, and `51` direct public interfaces in `GlobalRegistryContracts.cs`
- latest R5/R6 package/config scan: Unity pin `6000.4.1f1`; URP `17.4.0`; Addressables `2.7.6`; Input System `1.19.0`; AI Navigation `2.0.11`; forbidden UPM IDs absent; physical Astar/Easy Save/Demigiant/DarkTonic folders still present; live `DOTWEEN` and heavy Standalone vendor scripting defines remain contamination; embedded Crest/MicroSplat/ShaderGraph package drift needs import/build proof
- R5 URP mapping correction: Low quality uses `URP_Low` with `Mobile_Renderer`, not `PC_Renderer`
- R7 authority correction: `AGENTS.md` and `.codexrules/AGENTS.md` now carry the same Low mapping and no-new-ES3 wording
- R8 world/scatter correction: world runtime code and data are substantial, but `GameBootstrapper` only creates `PersistentWorldRegistry`; scatter/field/chunk/MapMagic/vegetation/streaming managers still require scene/editor-authoring proof and runtime validation
- R9 root/atlas correction: root authority is only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`; root `PROJECT_ATLAS.md` is a compatibility mirror and atlas data is asmdef graph evidence only, not package/config/runtime proof
- R14/R19/R20/R21/R22 gameplay/economy correction: item/catalog/recipe/inventory/fabricator/scarcity/logistics code is real, but resource acquisition is not runtime-proven; R14 found `23 / 27` resource-node harvest items lacking `worldPrefab` and duplicate copper authority, R19 reduced the primary-harvest `worldPrefab` gap to `16 / 27`, R20/R21 extended `ContentSanityValidator` and reduced current resource-node primary-harvest gaps to `0 / 27` missing `worldPrefab` and `0 / 27` non-catalog, and R22 added duplicate `ItemData.PersistentId` / catalog ambiguity validation while runtime pickup proof remains pending
- R15 AI/Fauna correction: fauna archetype/template/biome/proxy data is real, but static scans did not prove production-scene `FaunaDirector` or `WorldFaunaSpawnRegistry` wiring; `GameBootstrapper` can register `DemiurgeFaunaSimulationService.Shared` as a ready `IFaunaSim` fallback with `ResidentSlotCapacity = 0`, so `IFaunaSim.IsReady` alone is not visible-fauna proof
- R16/R18 tools/PDA/first-hour correction: tool/scan/interaction architecture is real (`12` tool items, `12` held prefabs, `12` world prefabs, non-null tool worldPrefab refs, real scanner/scan-log/interaction/quest paths); R16 found enabled `ToolLoadoutProvisioner` startup grants on `Player.prefab`, and R18 disabled/gated that dev provisioning, but `PlayerPDA` is still prefab-headless and the `DiegeticPDAController` bridge placement remains unproven
- R17 rendering/visor/shader correction: active renderer assets contain real custom post/noir/SSDO/shaft features and `21` visor renderer-feature files implement `RecordRenderGraph`, but GPU Resident Drawer/GPU occlusion are disabled in scanned URP tier assets, `16` visor feature files still use `AddUnsafePass`, and source-ready GPR/light-shaft/instance-culling runtimes are not proven serialized in `_Project` scenes/prefabs/assets
- source counts are volatile during active multi-agent work; exact counts are snapshot data, not permanent truth
- runtime proof remains absent

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `AGENTS.md`

## 2026-05-13 DOC_AUDIT R5 Override

This section supersedes older numeric and bus-model claims in this file.

| Scan | Current Value |
|---|---:|
| first-party C# files under `Assets/_Project` | 1,411 |
| first-party C# physical lines under `Assets/_Project` | 869,871 |
| first-party non-test C# files | 1,401 |
| first-party non-test C# physical lines | 867,132 |
| interface declaration hits under `Assets/_Project` | 215 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 51 |
| authoritative domain ids | 85 |
| `GlobalSignals` typed lanes | 33 |
| `GlobalSignals` `ParallelWriter` lanes | 13 |
| `[StructLayout(... Size = 32/64/128)]` hits | 127 |
| first-party asmdefs under `Assets/_Project` | 24 |

Current architecture authority addenda:

- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`
- `Docs/ARCHITECTURE/BOOT_SEQUENCE_TOPOLOGY.md`
- `Docs/ARCHITECTURE/ARENA_ALLOCATOR_2_0.md`
- `Docs/ARCHITECTURE/GLOBAL_REGISTRY_SERVICE_LOCATOR.md`
- `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/DOC_CHRONOS_ARCHITECTURE_TRUTH_SYNC_2026-05-12.md`

### 85-Domain Matrix

The project is not an 80-domain system. `Docs/Actual Domains of Project.txt` defines ids `1..85`.

| Id | Domain | Current Boundary |
|---:|---|---|
| 1 | BIOS Bootstrapper | boot orchestration |
| 2 | Global EventBus | `GlobalSignals` SPSC/MPSC signal corridor |
| 3 | Data Archivist | FileStream save storage, legacy MMF docs purged |
| 4 | Data Monolith | `.h8bin` static database |
| 5 | Origin Shift | AUP/floating origin and rebase signals |
| 6 | Native Arena Allocator | double-buffered unmanaged scratch |
| 7 | Crash Telemetry | fixed black-box event buffers |
| 8 | Scalability Dictator | hardware tier and math LOD switching |
| 9 | Platform Abstraction Layer | input/platform isolation |
| 10 | Tick Dispatcher & Time Dilation | cadence control |
| 11 | MapMagic Runtime Bridge | third-party terrain isolation |
| 12 | Voxel SDF Pipeline | density field ownership |
| 13 | Marching Cubes Mesher | async voxel polygonization |
| 14 | Voxel Carving | deformation deltas |
| 15 | BRG Scatter Director | GPU/BRG scatter instances |
| 16 | Procedural Wreckage Assembler | deterministic wreck assembly |
| 17 | Geological Node Spawner | ore and resource distribution |
| 18 | Biome Transition Manager | biome blending |
| 19 | Abyssal Flow Fields | current vectors |
| 20 | Thermal Vents & Geysers | heat/impulse danger zones |
| 21 | Ecosystem Director | macro fauna/ecology |
| 22 | Fauna Spatial Hash Grid | neighbor lookup |
| 23 | Swarm Compute Director | GPU boids |
| 24 | Predator Cognition | utility scoring |
| 25 | Predator Steering & Lunge | navigation/attack motion |
| 26 | A* Funnel Smoothing | voxel path smoothing |
| 27 | Leviathan Procedural IK | procedural body/tentacle motion |
| 28 | Flora Procedural Sway | shader-driven vegetation motion |
| 29 | Bioluminescence Sync | glow phase coordination |
| 30 | Fauna Genetics & Mutation | trait masks |
| 31 | Kinematic Character Controller | capsule cast locomotion |
| 32 | Hydrodynamic Drag & Buoyancy | force approximations |
| 33 | Contextual Hand IK | interaction hand placement |
| 34 | Tether & Cable Physics | acceleration-constraint cable path |
| 35 | Equipment Runtime | tools and power use |
| 36 | Scavenging & Harvesting | loot conversion |
| 37 | SOA Inventory System | compact item storage |
| 38 | Crafting Fast-Fail Validator | recipe bitmask checks |
| 39 | VR Somatic Comfort | XR comfort rules |
| 40 | VR Interaction Bridge | hand interaction bridge |
| 41 | Combat Damage Router | force/damage packet lane |
| 42 | Armor Penetration LUT | hit/penetration lookup |
| 43 | Status Effects Engine | mask-based effects |
| 44 | Player Stress & Fear System | stress state |
| 45 | Decompression Sickness | tissue nitrogen approximation |
| 46 | Hypoxia & Gas Toxicity | gas danger state |
| 47 | Crush Depth Integrity | pressure damage |
| 48 | Diet & Metabolism | hunger, hydration, heat |
| 49 | Radiation Scrubber | dose accumulation |
| 50 | Screen-Space Wounds & Decals | visor/body presentation damage |
| 51 | Grid Snapping & Ghost Preview | construction preview |
| 52 | Structural Integrity Math | base strength |
| 53 | Fluid Incursion | flooding propagation |
| 54 | Power Grid | energy relaxation |
| 55 | Pipe & Sump Pump Logistics | liquid/oxygen transfer |
| 56 | Module Deconstruction | refund/rollback |
| 57 | Submarine OS | terminal runtime |
| 58 | Submarine Navigation | pitch/roll stabilization |
| 59 | Drone Fleet Commander | repair/mining bots |
| 60 | Scooter Kinematics | handheld transport |
| 61 | Celestial Orbit Mechanics | cheap orbit presentation |
| 62 | Tide & Seismic Generator | deterministic world events |
| 63 | Weather & Wind Director | surface/weather turbidity |
| 64 | Gas Dynamics | compartment gas state |
| 65 | Thermodynamics | heat diffusion |
| 66 | Marine Snow & Silt Compute | GPU particulate fog |
| 67 | Volumetric Fog & Light Shafts | raymarch/light presentation |
| 68 | Day/Night GI Relay | ocean light state |
| 69 | Zero-GC Subtitles | localization text path |
| 70 | Diegetic Terminals | 3D UI input projection |
| 71 | Visor AR | HUD/stencil presentation |
| 72 | PDA Encyclopedia Streaming | lazy knowledge streaming |
| 73 | AUP Narrative Triggers | large-world POI triggers |
| 74 | Cartography & Fog of War | sonar/map discovery |
| 75 | Frequency Tuning | scanner matching |
| 76 | DSP Acoustic Radar | occlusion/radar audio data |
| 77 | Granular Synthesis | pressure/hull audio |
| 78 | Vocal Warning System | warning queue |
| 79 | Haptic Feedback Director | impact-to-haptics |
| 80 | Camera Juice & Shake | camera impulse presentation |
| 81 | Physics Culling Overseer | rigidbody sleep policy |
| 82 | Integrator | compile medic |
| 83 | Chronicler | documentation/atlas generation |
| 84 | QA Watchdog Bot | long-run validation |
| 85 | Tech Researcher | mandate evolution |

## 2026-05-13 Current-State Boundary

- This stable map is the architecture authority. Dated reports are evidence/counter snapshots only.
- Read `Docs/README.md`, `.agents-skills/README.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`, `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, and then older dated evidence reports before using this map as current project truth.
- This map is source-backed architecture orientation, not Play Mode proof, profiler proof, console certification, or scene/prefab wiring proof.
- Line counts and interface counts are orientation data only; source files and fresh verification logs win.

## 1. Scope

- Audit target: `Assets/_Project/**/*.cs`
- Current first-party `.cs` inventory under `Assets/_Project`: `1411`
- Current first-party `.cs` inventory under `Assets/_Project/Scripts`: `1365`
- Current project physical line count under `Assets/_Project` by .NET `File.ReadLines` count: `869871`
- Current script physical line count under `Assets/_Project/Scripts` by .NET `File.ReadLines` count: `852315`
- Average physical lines per script from the May 13 R4 snapshot: `624.41`
- Scripts still living directly in `Assets/_Project/Scripts` root: `336`

This file replaces the older generated map whose `808`-script snapshot and some interface conclusions are no longer true.

2026-05-13 R4 correction:
- Counts above supersede the April 30 `1038/998/444135`, earlier May 1 `1060/1020/466768`, May 1 `1020/544728`, earlier May 2 `489893`, May 2 `1087/1047/571562/317`, May 6 `651121/552119`, and earlier May 7 `651253/559502`, `651810/560025`, `652238/560372`, `652787/560848`, `655363/563210`, and `667771` snapshots.
- Current full first-party source scan found `215` interface declaration hits. `GlobalRegistryContracts.cs` direct public interface count remains a narrower owner-file metric, not the whole interface surface.
- Older interface coverage ratios in this document are stale orientation only; re-open `GlobalRegistryContracts.cs` and implementors before making interface-completion claims.
- The May 11 green Core dependency build is historical evidence only. The May 12 DOC_CHRONOS compile gate failed with `111` C# errors and `3` warnings in external platform/native/voxel missing-symbol families; current build status is `[BLOCKED BY DEPENDENCY]`.
- Current Unity MCP proof was not run in the May 11 continuation; older MCP readbacks are historical only.
- This architecture map is not a Play Mode, profiler, GCMonitor, memory-retention, or player-build certificate.

## 2. Runtime Topology

Representative structure visible in current source:

1. `Bootstrap/GameBootstrapper.cs` initializes registry-owned services and dependency edges.
2. `SceneBootstrap.cs` separately owns scene warmup, readiness gates, and bootstrap event flow.
3. `Core/GlobalRegistry.cs` exposes shared service slots.
4. `Core/SystemDispatcher.cs` and `GameTickManager.cs` drive central runtime cadence.
5. feature owners in gameplay, world, UI, audio, quest, and submarine layers consume those services through mixed direct references, interfaces, and event lanes.

Meaning:

- the project is not raw Unity chaos
- the project also does not have one uncontested startup sovereign

## 3. Confirmed Contract Owners

Current `GlobalRegistry` service ownership confirmed by source scan:

- `IInputService` -> `InputDispatcher`
- `IPhysicsService` -> `PhysicsApplySystem`
- `IAudioService` -> `SpatialAudioManager`
- `ISceneService` -> `SceneRuntimeService`
- `ISaveService` -> `SaveManager`
- `IUIService` -> `UI/SuitHUDV4CanvasOverlay`
- `IPlayerRuntimeContext` -> `PlayerRuntimeContextService`
- `IPlayerInventoryService` -> `PlayerInventoryManager`
- `IModularEquipmentService` -> `ModularEquipmentEngine`
- `IPlayerSensoryService` -> `PlayerSensoryManager`
- `IEnvironmentRuntimeContext` -> `EnvironmentRuntimeContextService`
- `IWeatherService` -> `GlobalWeatherDirector`
- `IThermodynamicsService` -> `World/AbyssalThermalManager`
- `ILogisticsService` -> `Construction/ConstructionManager`
- `IWorldGenService` -> `WorldProceduralScatterDirector`
- `IEncounterDirectorService` -> `HectonDirectorAI`
- `IQuestSystem` -> `Quest/QuestManager`
- `IHectonOceanKinematicsService` -> `OceanKinematicsRuntimeService`
- `IInteractionSignalService` -> `EquipmentInteractionHandler`
- `IDebrisService` -> `Gameplay/DebrisManager`
- `IEcosystemDirectorService` -> `World/EcosystemDirector`

Additional registry-backed contracts outside that 21-owner service list:

- `IPDALogbookService` -> `PDA/PDALogbookManager`
- `IPowerGridService` -> `PowerGridManager`
- `IFaunaSim` -> `Fauna/FaunaSimulationEngine` when an active `FaunaDirector` exists; bootstrap fallback `DemiurgeFaunaSimulationService` is headless/data-only and does not prove visible fauna or resident slots
- `IFluidSim` -> `Physics/FluidMathCore`
- `ISubmarineRuntimeContext` -> `SubmarineCoreDirector`
- `ISubmarineHullBreachReadModel` -> `SubmarineStructuralGrid`

Current direct rendering contract owners:

- `IRenderable` -> `HectonUnderwaterVisuals`
- `IRenderable` -> `Gameplay/HectonSubmarineOS`
- `IRenderable` -> `Quest/MissionMarkerSystem`

Current direct damage contract owners confirmed by source scan:

- `Hecton8.Core.IDamageReceiver` -> `Gameplay/HabitatIntegrityManager`
- `Hecton8.Core.IDamageReceiver` -> `Gameplay/HectonPlayerHealth`

This is materially different from older documents that claimed `IAudioService` was ghost, `IUIService` was directly multi-owned, or `IDamageReceiver` was an unresolved shadow-conflict.
It also supersedes older interface-count claims. Current direct public interface count is `41`; coverage must be rechecked from source before being used as proof.

## 4. Event Architecture

Current source authority is `Assets/_Project/Scripts/Core/GlobalSignals.cs`.

The old five-artery Mega-Bus classification is historical shorthand only. It is not the current EventBus truth.

Current source-backed facts:

- 33 typed `NativeQueue<T>` lanes
- 33 public signal structs
- 13 `NativeQueue<T>.ParallelWriter` producer lanes
- signal payloads are fixed 32/64-byte unmanaged structs
- `SpscSignalRingBuffer<T>` exists for SPSC-only cases

Cross-domain authority is now documented in `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`.

Current rule:

- Direct `Action`, `Func`, C# `event`, `delegate`, or `UnityEvent` chains are legacy debt when they cross domain boundaries.
- Local owner callbacks and async completion callbacks do not become event architecture authority by appearing in code.
- Source scan found one static `Action`-typed GPU readback callback bridge in `SaveThumbnailSystem.cs`; it is not a first-party cross-domain bus lane.

## 5. Current Editor Readback

Current documentation pass status:

- `Reports/2026-05-13_DOC_AUDIT_XRAY.md` is the latest documentation counter/missing-artifact override
- `Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` is the latest machine-readable active documentation manifest, but its counters are historical where the May 13 X-Ray supersedes them
- `Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` is the latest `.agents-skills` visual-fake doctrine boundary
- `Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` is historical May 11 documentation/build synchronization evidence
- `Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md` is the previous R186 documentation/build synchronization boundary
- `Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` is the latest documentation sorting and authority map, amended by the May 6 synchronization pass
- `Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` is the latest broad documentation/build/guard actuality sweep before the May 6 sync layer
- `Reports/2026-05-04_WARNING_CLEANUP.md` is the latest first-party warning cleanup and post-refresh console-readback addendum
- `Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` is the latest foundation guard repair addendum
- `Reports/2026-05-01_CURRENT_PROJECT_STATE.md` remains the stable conceptual state anchor
- May 11 report text claimed a local full Core dependency build returned `0 Warning(s)` / `0 Error(s)`, but the May 13 DOC_AUDIT filesystem check did not find the cited summary or raw log artifact
- earlier DOTS/PlayModeTests/foundation-guard/MCP readbacks are historical unless refreshed by a newer report
- no fresh Unity MCP, Unity Console, Play Mode, profiler, GCMonitor, player-build, import, scene-wiring, or visual-quality proof was captured in the May 11 documentation continuation or DOC_AUDIT R5

Historical Unity Editor readback from older rechecks:

- Build Settings scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`
- Loaded active scene: `02_HECTON_WORLD`
- Older console readback: `15` errors, but all `15` were package-side MCP `ManageAsset` conversion errors, not first-party compile errors

Latest console slice observed:

- tool/package source: `./Library/PackageCache/com.coplaydev.unity-mcp.../Editor/Tools/ManageAsset.cs`
- repeated message pattern: `Failed to convert -1 to a unsigned 32 bit int`
- affected assets: `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_*`

This older package-side MCP error slice is historical and was not reproduced by the May 4 or May 6 console error queries.
It is not proof of stable play-mode behavior, shipping build health, or zero-GC runtime.

## 6. Concentration Risks

Largest current first-party owners by line count:

- `WorldProceduralScatterDirector.cs` - `11778`
- `HectonPlayerMovement.cs` - `10571`
- `HectonVoxelEngine.cs` - `6751`
- `World/HectonMapMagicVegetationBridge.cs` - `6640`
- `HectonUnderwaterVisuals.cs` - `6553`
- `UI/SuitHUDV4CanvasOverlay.cs` - `6220`
- `Audio/PlayerCriticalProceduralAudioRenderer.cs` - `6047`
- `SaveBinaryStorage.cs` - `5743`
- `World/PersistentWorldRegistry.cs` - `5646`
- `WorldProceduralFieldSampler.cs` - `5100`

These are not cosmetic complaints.
They are risk multipliers because runtime ownership, visuals, simulation, and integration pressure accumulate inside very large files.

## 7. Proven Tensions

- bootstrap ownership is split between `GameBootstrapper` and `SceneBootstrap`
- event architecture is mixed between queue-backed and direct static buses
- root-folder ownership is still noisy with `342` scripts in `Assets/_Project/Scripts`
- codebase health is stronger than some old reports claimed, but file-size concentration and ownership spread remain real

## 8. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime code changed
Cadence: documentation only
Correctness: improved because stale script counts and large-owner line counts were refreshed; editor state remains historical until a future Unity run

## 9. Hot Path Impact

None. Markdown-only pass.

## 10. Failure Modes

- source ownership can drift again without synchronized doc maintenance
- current console cleanliness can change between editor sessions
- large-owner files can hide internal contradictions even when top-level maps remain accurate

## 11. Why This Version Was Kept

Kept because the previous version had become numerically and structurally false.
This version is shorter, but it is anchored to current code and current reachable editor state.

STATUS: PENDING VERIFICATION
