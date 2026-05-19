# HECTON-8 Global Architecture Map

Date: 2026-05-19
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Scope: source-backed architecture map only, not a profiler capture and not a build-certification report

## 2026-05-13 DOC_AUDIT X-Ray Override

Read `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` before trusting older numeric counters in this file.

Current static audit corrections:

- first-party asmdefs under `Assets/_Project`: R27 volatile static snapshot `123`
- previously unindexed current asmdefs include `Hecton8.Core.Memory` and `Hecton8.Physics.Determinism`
- cited May 11 build artifacts `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` and `.log` are absent from the current filesystem
- latest R27 static source spot-check counters: `1818` project C# files, `1761` script C# files, `1797` non-test C# files excluding `Assets/_Project/Tests*`, `1204221` project physical lines, `1184559` script physical lines, `1199376` non-test physical lines, `342` broad interface token hits, `267` direct interface declaration lines, `62` direct public interfaces in `GlobalRegistryContracts.cs`, and `123` first-party asmdefs. Evidence class: `STATIC_SOURCE`; volatile under concurrent agents; rerun before exact use.
- latest R5/R6 package/config scan: Unity pin `6000.4.1f1`; URP `17.4.0`; Addressables `2.7.6`; Input System `1.19.0`; AI Navigation `2.0.11`; forbidden UPM IDs absent; physical Astar/Easy Save/Demigiant/DarkTonic folders still present; live `DOTWEEN` and heavy Standalone vendor scripting defines remain contamination; embedded Crest/MicroSplat/ShaderGraph package drift needs import/build proof
- R5 URP mapping correction: Low quality uses `URP_Low` with `Mobile_Renderer`, not `PC_Renderer`
- R7 authority correction: `AGENTS.md` and `.codexrules/AGENTS.md` now carry the same Low mapping and no-new-ES3 wording
- R8 world/scatter correction: world runtime code and data are substantial, but `GameBootstrapper` only creates `PersistentWorldRegistry`; scatter/field/chunk/MapMagic/vegetation/streaming managers still require scene/editor-authoring proof and runtime validation
- R9 root/atlas correction: root authority is only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`; root `PROJECT_ATLAS.md` is a compatibility mirror and atlas data is asmdef graph evidence only, not package/config/runtime proof
- R14/R19/R20/R21/R23/R26/R27/R28 gameplay/economy correction: item/catalog/recipe/inventory/fabricator/scarcity/logistics code is real, but resource acquisition is not runtime-proven; R14 found `23 / 27` resource-node harvest items lacking `worldPrefab` and duplicate copper authority, R19 reduced the primary-harvest `worldPrefab` gap to `16 / 27`, R20/R21 extended `ContentSanityValidator` and reduced current resource-node primary-harvest gaps to `0 / 27` missing `worldPrefab` and `0 / 27` non-catalog, R23 added duplicate `ItemData.PersistentId` / catalog ambiguity validation, R26 added quest item/prerequisite route validation, R27 added recipe/result/ingredient/catalog plus craft-quest recipe-output validation, and R28 added scan-gate warnings for scan-locked recipes without known generic/prefab unlock routes while runtime pickup/craft/quest proof remains pending
- R15 AI/Fauna correction: fauna archetype/template/biome/proxy data is real, but static scans did not prove production-scene `FaunaDirector` or `WorldFaunaSpawnRegistry` wiring; `GameBootstrapper` can register `DemiurgeFaunaSimulationService.Shared` as a ready `IFaunaSim` fallback with `ResidentSlotCapacity = 0`, so `IFaunaSim.IsReady` alone is not visible-fauna proof
- R16/R18/R22/R24/R25 tools/PDA/first-hour correction: tool/scan/interaction architecture is real (`12` tool items, `12` held prefabs, `12` world prefabs, non-null tool worldPrefab refs, real scanner/scan-log/interaction/quest paths); R16 found enabled `ToolLoadoutProvisioner` startup grants on `Player.prefab`, R18 disabled/gated that dev provisioning, R22 made headless `PlayerPDA.Open()` fail closed, R24 added editor validation for active tool metadata -> held prefab -> ItemData/catalog/worldPrefab routes, and R25 added a player-prefab dev-provisioner startup flag gate; visible `DiegeticPDAController` bridge placement remains unproven
- R17 rendering/visor/shader correction: active renderer assets contain real custom post/noir/SSDO/shaft features and `21` visor renderer-feature files implement `RecordRenderGraph`, but GPU Resident Drawer/GPU occlusion are disabled in scanned URP tier assets, `16` visor feature files still use `AddUnsafePass`, and source-ready GPR/light-shaft/instance-culling runtimes are not proven serialized in `_Project` scenes/prefabs/assets
- R29/R30/R31/R32/R33/R36/R37/R38 compile/persistence correction: DOC_AUDIT reported Unity `6000.4.1f1` batchmode import/script compilation at `Library/Codex_DOC_AUDIT_UnityBatchCompile.log`, but the R10 filesystem check did not find that path. R37 added local Unity Bee/Roslyn temp-output probes for `Hecton8.Core.Memory` and `Hecton8.Core`, both exit code `0`; R38 demoted that full-Core success as stale under then-current churn. R43 later superseded the compile-blocked note with a clean external root `Hecton8*.csproj` no-restore CLI recheck. Unity MCP Console is currently unavailable, so this is compile/source boundary only; runtime save/load, profiler, player build, and PlayMode remain unproven. `SaveManager` / `H8BinaryWorldPager` now fail-close locked `world_data.h8bin` page persistence instead of throwing through bootstrap; R30/R31/R32/R33/R36/R37/R38 tighten it to single-writer file sharing, joinable pager worker shutdown, no stale pending counters after unexpected pager command faults, no false sparse-sector corruption, no per-chunk global voxel snapshot write, lazy pager file open outside `InitializeNativeBuffers()`, first-use allocation for large save buffers, pager-fault staging guard, Core.Memory asmdef boundary hygiene, WFC outpost MacroDB bitmask persist/restore contract coverage, and no orphaned chunk-load voxel prefetch.
- R39/R40/R41/R42/R43/R45 generated-project correction: R39 found `Hecton8.Core.asmdef` references `23` first-party assemblies absent from generated `Hecton8.Core.csproj`, and `HectonComplianceValidator` now emits `CSPROJ001` for this mismatch. R40 attempted non-destructive Unity batchmode project refresh, found root generated projects still stale, then added a source-backed `Directory.Build.targets` bridge instead of editing generated `.csproj` files. R43 rechecked all eight root projects as single-project no-restore builds: `Hecton8.Core`, `Hecton8.Editor`, `Hecton8.PlayModeTests`, `Hecton8.World.Contracts`, `Hecton8.World.Dots`, `Hecton8.Bootstrap.Contracts`, `Hecton8.Input.Generated`, and `Hecton8.Input` each returned `0 Warning(s)` / `0 Error(s)` with `LASTEXITCODE=0`. Missing `Temp\obj` restore assets can still cause `NETSDK1004`, missing referenced `Temp\bin\Debug` DLLs can stop no-restore Editor/Core checks, and shared `Temp\obj` locks can create transient `CS2012` evidence noise until restore/build and build-server cleanup are rerun. Full restore graphs still carry vendor/package warnings outside the isolated root no-restore surface. R45 reapplied this boundary after stale R41/R38 wording reappeared in active docs. This is CLI compile evidence only; Unity Console, Play Mode, profiler, GCMonitor, player build, and scene wiring remain unproven.
- 2026-05-15 current-disk correction: latest observed archived artifact `Docs/Archive/Batch007/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_224641_CurrentDisk53.log` exits `0` for `Hecton8.Core.csproj` with `Build succeeded`, `0 Warning(s)`, and `0 Error(s)`; earlier same-session failed or stale build artifacts are superseded by the later clean CurrentDisk53 artifact for archived Core CLI status only.
- 2026-05-19 HFI/H-Phi correction: `Docs/Reports/2026-05-19_HFI_AUDIT_H_PHI_AND_PROJECT_RISK.md` and `Docs/Archive/Batch009/AgentLogs/HPhi_SHINOBU_02_current2.json` are the latest static numeric orientation, with `DataSovereignty=0.203977518`, `MemoryAlignment=0.586269524`, `HPhiStaticNarrow=0.119585803`, and `HPhiStaticRisk=0.009214659`; runtime proof remains absent.
- 2026-05-15 H-Phi correction: archived artifact `Docs/Archive/Batch007/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.json` exits `0`; static scores include `DataSovereignty=0.021306032`, `MemoryAlignment=0.506309148`, `RuntimeHPhiRisk=0.000636091`, `GlobalRegistrySurface=5060/5060`, `ManagedFormatSurface=534/534`, `PrimaryManagedRuntimeRisk=147/147`, `DuplicateSignalNames=0`, `UnityUpdateMethods=0`, and Core graph debt `25/10/14/8/6`. This supersedes earlier same-day MemoryAlignment failure artifacts, the interim R47 `GlobalRegistrySurface=5076 > 5075` failure, R49, R52, R53, and R54 only for archived static H-Phi status.
- 2026-05-15 DOC_HONEST_ANALYSIS R3 correction: archived static H-Phi summary artifact `Docs/Archive/Batch006/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CurrentStaticSummary.json` found Core asmdef debt had drifted to `26` because `Hecton8.World.GPR` appeared as an unused Core reference candidate in transient workspace/index state. The archived post-prune artifact `Docs/Archive/Batch006/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CoreGraphAfterGprPrune.json` exits `0`, returned Core graph debt to `25/10/14/8/6`, and reported no unused Core asmdef candidates. Follow-up archived Core CLI compile `Docs/Archive/Batch006/AgentLogs/Build_DOC_HONEST_ANALYSIS_R3_20260515_AfterGprAsmdefPrune_Hecton8Core.log` exits `0` with `0 Warning(s)` and `0 Error(s)`.
- R35 world-streaming/PDA correction: `WorldChunkResidencyManager` exposes a separate HLOD point/read-model version through `IStreamingBackpressureService.ActiveImpostorVersion`, and `PDAMapTab` uses it to skip unchanged fixed HLOD POI GPU uploads while keeping renderer matrix dirty state separate. Runtime PDA map/profiler proof remains unproven.
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
| first-party C# files under `Assets/_Project` | 1,818 |
| first-party C# physical lines under `Assets/_Project` | 1,204,221 |
| first-party non-test C# files excluding `Assets/_Project/Tests*` | 1,797 |
| first-party non-test C# physical lines | 1,199,376 |
| broad `interface` token hits under `Assets/_Project` | 342 |
| direct interface declaration lines under `Assets/_Project` | 267 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 62 |
| authoritative domain ids | 85 |
| `GlobalSignals` typed lanes | 133 |
| `GlobalSignals` direct queue slots | 73 |
| `[StructLayout(... Size = 32/64/128)]` hits | 127 |
| first-party asmdefs under `Assets/_Project` | 123 |

Current architecture authority addenda:

- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `Docs/ARCHITECTURE/BOOT_SEQUENCE_TOPOLOGY.md`
- `Docs/ARCHITECTURE/ARENA_ALLOCATOR_2_0.md`
- `Docs/ARCHITECTURE/GLOBAL_REGISTRY_SERVICE_LOCATOR.md`
- `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md`
- `Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md`
- `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/DOC_CHRONOS_ARCHITECTURE_TRUTH_SYNC_2026-05-12.md`

### 85-Domain Matrix

The project is not an 80-domain system. `Docs/Actual Domains of Project.txt` defines ids `1..85`.

### ASCII Domain Backbone

This is the visual domain map for the encyclopedia pass. It follows the authoritative `85` domains. The older "80 domains" wording is stale.

```text
HECTON-8 DOMAIN BACKBONE

+-- ECHELON 1: CORE AND MEMORY INFRASTRUCTURE (01-10)
|   01 BIOS Bootstrapper
|   02 Global EventBus / Signal Corridor
|   03 Data Archivist / Save Codec
|   04 Data Monolith / Static DB
|   05 Origin Shift / AUP Manager
|   06 Native Arena Allocator
|   07 Crash Telemetry / Black Box
|   08 Scalability Dictator / Hardware
|   09 Platform Abstraction Layer
|   10 Tick Dispatcher / Time Dilation
|
+-- ECHELON 2: WORLD GENERATION AND TERRAIN (11-20)
|   11 MapMagic Runtime Bridge
|   12 Voxel SDF Pipeline
|   13 Marching Cubes Mesher
|   14 Voxel Carving / Deformation
|   15 BRG Scatter Director
|   16 Procedural Wreckage Assembler
|   17 Geological Node Spawner
|   18 Biome Transition Manager
|   19 Abyssal Flow Fields
|   20 Thermal Vents and Geysers
|
+-- ECHELON 3: FLORA, FAUNA, AND BIOTA (21-30)
|   21 Ecosystem Director
|   22 Fauna Spatial Hash Grid
|   23 Swarm Compute Director
|   24 Predator Cognition
|   25 Predator Steering and Lunge
|   26 A* Funnel Smoothing
|   27 Leviathan Procedural IK
|   28 Flora Procedural Sway
|   29 Bioluminescence Sync
|   30 Fauna Genetics and Mutation
|
+-- ECHELON 4: PLAYER, KINEMATICS, AND TOOLS (31-40)
|   31 Kinematic Character Controller
|   32 Hydrodynamic Drag and Buoyancy
|   33 Contextual Hand IK
|   34 Tether and Cable Physics
|   35 Equipment Runtime
|   36 Scavenging and Harvesting
|   37 SOA Inventory System
|   38 Crafting Fast-Fail Validator
|   39 VR Somatic Comfort
|   40 VR Interaction Bridge
|
+-- ECHELON 5: COMBAT AND SURVIVAL PHYSIOLOGY (41-50)
|   41 Combat Damage Router
|   42 Armor Penetration LUT
|   43 Status Effects Engine
|   44 Player Stress and Fear System
|   45 Decompression Sickness
|   46 Hypoxia and Gas Toxicity
|   47 Crush Depth Integrity
|   48 Diet and Metabolism
|   49 Radiation Scrubber
|   50 Screen-Space Wounds and Decals
|
+-- ECHELON 6: HABITAT AND VEHICLES (51-60)
|   51 Grid Snapping and Ghost Preview
|   52 Structural Integrity Math
|   53 Fluid Incursion
|   54 Power Grid
|   55 Pipe and Sump Pump Logistics
|   56 Module Deconstruction
|   57 Submarine OS
|   58 Submarine Navigation
|   59 Drone Fleet Commander
|   60 Scooter Kinematics
|
+-- ECHELON 7: ATMOSPHERE AND CELESTIAL (61-68)
|   61 Celestial Orbit Mechanics
|   62 Tide and Seismic Generator
|   63 Weather and Wind Director
|   64 Gas Dynamics
|   65 Thermodynamics
|   66 Marine Snow and Silt Compute
|   67 Volumetric Fog and Light Shafts
|   68 Day/Night GI Relay
|
+-- ECHELON 8: PRESENTATION AND UX (69-78)
|   69 Zero-GC Subtitles
|   70 Diegetic Terminals
|   71 Visor AR
|   72 PDA Encyclopedia Streaming
|   73 AUP Narrative Triggers
|   74 Cartography and Fog of War
|   75 Frequency Tuning
|   76 DSP Acoustic Radar
|   77 Granular Synthesis
|   78 Vocal Warning System
|
+-- ECHELON 9: META, POLISH, AND INTEGRATION (79-85)
    79 Haptic Feedback Director
    80 Camera Juice and Shake
    81 Physics Culling Overseer
    82 Integrator
    83 Chronicler
    84 QA Watchdog Bot
    85 Tech Researcher

FLOW OF AUTHORITY

AGENTS.md
  -> .agents-skills/*
  -> stable Docs/*
  -> current source
  -> fresh verification artifacts
  -> dated reports

RUNTIME DATA FLOW

Bootstrap
  -> Registry service slots
  -> Dependency injection
  -> Dispatcher phases
  -> DataVault buffers
  -> Signal lanes
  -> Visual/audio/UI sync
```

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
- Read `Docs/README.md`, `.agents-skills/README.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`, `Docs/Reports/2026-05-19_DOCUMENTATION_R29_ROOT_ARCHITECTURE_STALE_GATE_GLOBAL_AUTHORITY_LOCAL.md`, `Docs/Reports/2026-05-19_DOCUMENTATION_R28_ROOT_ARCHITECTURE_INTERIOR_BOUNDARY_LOCAL.md`, `Docs/Reports/2026-05-19_DOCUMENTATION_R27_ROOT_ARCHITECTURE_INDEX_COUNTER_LOCAL.md`, `Docs/Reports/2026-05-19_DOCUMENTATION_R26_ROOT_ARCHITECTURE_HFI_PROMOTION_LOCAL.md`, `Docs/Reports/2026-05-19_DOCUMENTATION_R25_ROOT_ARCHITECTURE_LONGTAIL_LOCAL.md`, `Docs/Reports/2026-05-19_DOCUMENTATION_R24_ROOT_ARCHITECTURE_ACTUALITY_LOCAL.md`, `Docs/Reports/2026-05-19_HFI_AUDIT_H_PHI_AND_PROJECT_RISK.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_R23_SUBAGENT_RESIDUE_AND_STATUS_JSON_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_R22_COUNTER_DRIFT_AND_VALIDATION_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_R21_COUNTERS_REPORTS_MARKETING_BOUNDARIES_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ARCHIVARIUS_DESIGN_PROOF_RESIDUE_R20_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_MARKETING_EVENTBUS_COUNTERS_R19_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_R4_ARCHIVARIUS_FORENSIC_LONGTAIL_R18_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_REPORT_VAULT_AND_NAVIGATION_R17_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_R15_NAVIGATION_SUPERSESSION_R16_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_ENTRYPOINT_NAVIGATION_R15_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_BATCH008_BINARY_HYGIENE_R14_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_GENERIC_REPORT_BOUNDARIES_R13_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_REMAINDER_R11_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_LONGTAIL_INTERIOR_R10_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`, `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, and then older dated evidence reports before using this map as current architecture orientation.
- This map is source-backed architecture orientation, not Play Mode proof, profiler proof, console certification, or scene/prefab wiring proof.
- Line counts and interface counts are orientation data only; source files and fresh verification logs win.

## 1. Scope

- Audit target: `Assets/_Project/**/*.cs`
- R27 first-party `.cs` inventory under `Assets/_Project`: `1818`
- R27 first-party `.cs` inventory under `Assets/_Project/Scripts`: `1761`
- R27 project physical line count under `Assets/_Project`: `1204221`
- R27 script physical line count under `Assets/_Project/Scripts`: `1184559`
- Average physical lines per script from the May 19 R27 snapshot: `672.66`
- Scripts still living directly in `Assets/_Project/Scripts` root: `337`

This file replaces the older generated map whose `808`-script snapshot and some interface conclusions are no longer true.

2026-05-17 R8 correction:
- Counts above supersede the April 30 `1038/998/444135`, earlier May 1 `1060/1020/466768`, May 1 `1020/544728`, earlier May 2 `489893`, May 2 `1087/1047/571562/317`, May 6 `651121/552119`, and earlier May 7 `651253/559502`, `651810/560025`, `652238/560372`, `652787/560848`, `655363/563210`, and `667771` snapshots.
- Counts above also supersede the May 13 R4 `1411 / 1365 / 1401 / 869871 / 852315 / 867132 / 215 / 51 / 24` source-scale snapshot and the May 18 R10 `1739 / 1686 / 1722 / 1134113 / 1115245 / 1130062 / 296 / 63 / 107` source-scale snapshot where exact counts differ.
- R27 broad first-party source scan found `342` broad `interface` token hits and `267` direct interface declaration lines. `GlobalRegistryContracts.cs` direct public interface count is now `62` and remains a narrower owner-file metric, not the whole interface surface.
- Older interface coverage ratios in this document are stale orientation only; re-open `GlobalRegistryContracts.cs` and implementors before making interface-completion claims.
- The May 11 green Core dependency build is historical evidence only. The May 12 DOC_CHRONOS compile gate failed with `111` C# errors and `3` warnings in external platform/native/voxel missing-symbol families; that blocker is historical unless a current compile rerun reproduces it.
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
It also supersedes older interface-count claims. Current direct public interface count is `62`; coverage must be rechecked from source before being used as proof.

## 4. Event Architecture

Current source authority is `Assets/_Project/Scripts/Core/GlobalSignals.cs`.

The old five-bucket signal classification is historical shorthand only. It is not the current EventBus truth.

Current source-backed facts:

- R27 static source scan of `GlobalSignals.InitializeAllQueues()` sees `73` direct `CreateQueue(...)` native queue slots
- R27 static source scan of `GlobalSignals.cs` sees `133` `SignalBus<T>.EnsureInitialized()` typed lanes, including `ConfigureDebugSignalLane()` for `DebugSignal`
- Current Mod API static validator now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`); source-level `ISignal` struct counts are schema/input-surface evidence and static validator proof only, not runtime-lane proof.
- signal payloads are unmanaged structs; sizes are source-validated by `ValidateSignalSize`/`ValidateSignalPayload` calls, not profiler proof
- `SpscSignalRingBuffer<T>` exists for SPSC-only cases

Cross-domain authority is now documented in `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`.

Current rule:

- Direct `Action`, `Func`, C# `event`, `delegate`, or `UnityEvent` chains are legacy debt when they cross domain boundaries.
- Local owner callbacks and async completion callbacks do not become event architecture authority by appearing in code.
- Source scan found one static `Action`-typed GPU readback callback bridge in `SaveThumbnailSystem.cs`; it is not a first-party cross-domain bus lane.

## 5. Current Editor Readback

Current documentation pass status:

- Current DOC_GLOBAL documentation boundary starts at `Reports/2026-05-19_DOCUMENTATION_R29_ROOT_ARCHITECTURE_STALE_GATE_GLOBAL_AUTHORITY_LOCAL.md`; R28 remains the prior root/architecture interior-boundary correction, R27 remains the latest source-counter/index boundary, R26, R25, and R24 remain prior root/architecture boundaries, `Reports/2026-05-19_HFI_AUDIT_H_PHI_AND_PROJECT_RISK.md` is the current static HFI/H-Phi/global-authority risk snapshot, R23 remains the proof-language/navigation residue boundary, R22 remains a prior source-counter/validation boundary, then R21, R20, R19, R18, R17, R16, R15, R14, R13, R12, R11, R10, and R9 remain subordinate correction layers.
- `Reports/2026-05-13_DOC_AUDIT_XRAY.md` is a historical documentation counter/missing-artifact override
- `Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json` is a historical machine-readable active documentation manifest; its counters are superseded where newer DOC_GLOBAL or DOC_AUDIT passes reran them
- `Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` is a historical `.agents-skills` visual-fake doctrine boundary; stable doctrine now lives in `AGENTS.md` and `.agents-skills`
- `Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` is historical May 11 documentation/build synchronization evidence
- `Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md` is the previous R186 documentation/build synchronization boundary
- `Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` is a historical documentation sorting and authority map, amended by the May 6 synchronization pass
- `Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` is a historical broad documentation/build/guard actuality sweep before the May 6 sync layer
- `Reports/2026-05-04_WARNING_CLEANUP.md` is a historical first-party warning cleanup and post-refresh console-readback addendum
- `Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` is a historical foundation guard repair addendum
- `Reports/2026-05-01_CURRENT_PROJECT_STATE.md` remains the stable conceptual state anchor
- May 11 report text claimed a local full Core dependency build returned `0 Warning(s)` / `0 Error(s)`, but the May 13 DOC_AUDIT filesystem check did not find the cited summary or raw log artifact
- DOC_AUDIT R43 historically rechecked external root-project CLI compile output through the R40 source-backed `Directory.Build.targets` bridge after Unity batchmode project refresh failed to regenerate stale root `.csproj` files: all eight root `Hecton8*.csproj` projects reported `0 Warning(s)` / `0 Error(s)` with single-project no-restore `-m:1 /nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -v:minimal -clp:Summary` and `LASTEXITCODE=0`; current proof requires rerun artifact path, full command, timestamp, environment, and active-workspace output
- DOC_AUDIT/Integrator 2026-05-15 current-disk evidence added clean Core/H-Phi slices; after live file-write races, the archived top slice is `Docs/Archive/Batch007/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_224641_CurrentDisk53.log` (`Hecton8.Core.csproj`, `EXIT=0`, `0 Warning(s)`, `0 Error(s)`) and `Docs/Archive/Batch007/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_224426_CurrentDiskBudgetGate22.json` (`EXIT=0`, `MemoryAlignment=0.506309148`, `RuntimeHPhiRisk=0.000636091`, `GlobalRegistrySurface=5060/5060`). DOC_HONEST_ANALYSIS R3 remains the historical same-day transient `Hecton8.World.GPR` Core asmdef drift cleanup slice.
- earlier foundation-guard/MCP readbacks are historical unless refreshed by a newer report
- no fresh Unity MCP, Unity Console, Play Mode, profiler, GCMonitor, player-build, import, scene-wiring, or visual-quality proof was captured in the May 11 documentation continuation or DOC_AUDIT R43/R45

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
- `HectonPlayerMovement.cs` - `13240`
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
This version is shorter, but it is anchored to current static source/docs plus explicitly named historical editor readbacks. Fresh Unity Console, Play Mode, profiler, GCMonitor, player-build, scene-wiring, save/load, and visual proof remain absent.

STATUS: PENDING VERIFICATION
