# Project Runtime Topology

Date: 2026-06-02
Status: STATIC_SOURCE_SNAPSHOT / RUNTIME PENDING
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_SOURCE / STATIC_FILESYSTEM

Purpose: current source-backed project wiring map for agents before they edit code.

This is not compile, Unity import, Play Mode, profiler, GC, save/load, player-build, shader, platform, or visual proof.

Detailed per-system source ownership is in `SOURCE_SYSTEMS_REALITY_MAP.md`. This file stays focused on topology, route rules, and proof gaps.

## Authority Boundary

- `AGENTS.md`, `.agents-skills/`, `Docs/PROJECT_BASELINE.md`, and active files in `Docs/ARCHITECTURE` remain doctrine.
- Current source under `Assets/_Project` wins over dated reports and archived prompts.
- This file records static topology: paths, packages, scenes, source owners, and proof gaps.
- Runtime readiness still requires the proof ladder in `PLATFORM_PORTABILITY_PROOF_LADDER.md` and gates in `Docs/QUALITY_GATES.md`.

## Current Project Envelope

| Fact | Current static value | Source |
|---|---|---|
| Unity editor | `6000.4.1f1` | `ProjectSettings/ProjectVersion.txt` |
| Render pipeline package | `com.unity.render-pipelines.universal` `17.4.0` | `Packages/manifest.json` |
| Addressables package | `com.unity.addressables` `2.7.6` | `Packages/manifest.json` |
| Input package | `com.unity.inputsystem` `1.19.0` | `Packages/manifest.json` |
| Memory Profiler package | `com.unity.memoryprofiler` `1.1.12` | `Packages/manifest.json` |
| XR packages | OpenXR `1.17.0`, Meta OpenXR `2.5.0`, XR Management `4.6.0` | `Packages/manifest.json` |
| First-party asmdefs | `171` under `Assets/_Project` | 2026-06-01 static filesystem count |
| First-party script directories | `56` under `Assets/_Project/Scripts` | 2026-06-01 static filesystem count |
| Data Monolith payload | `7,457,664` bytes, mtime 2026-06-07 | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; 2026-08-05 static filesystem measurement |

Package presence is not platform readiness. XR/package fields do not prove provider setup, device launch, comfort, thermal, or frame pacing.

## First-Party Script Surface

Current static directory groups under `Assets/_Project/Scripts`:

| Group | Directories |
|---|---|
| Core/runtime authority | `Bootstrap`, `Core`, `Global`, `Input`, `SaveSystem`, `Data`, `Build`, `BuildTools`, `Compatibility` |
| Player and interaction | `Gameplay`, `Player`, `Interaction`, `Items`, `Inventory`, `Equipment`, `Scavenging`, `Tools`, `Visor`, `PDA`, `UI` |
| World and simulation | `World`, `Environment`, `Atmosphere`, `Thermodynamics`, `Physics`, `Construction`, `Habitat`, `Power`, `Logistics`, `Vehicles` |
| AI, fauna, and progression | `AI`, `Animation`, `Ecosystem`, `Fauna`, `Narrative`, `Quest`, `Progression`, `Economy`, `Prologue` |
| Presentation and rendering | `Audio`, `AudioLog`, `Cartography`, `Graphics`, `Lighting`, `Rendering`, `VFX` |
| Meta and integration | `AtlasSignal`, `Dev`, `Editor`, `Meta`, `ModdingAPI`, `Networking`, `Optimization`, `Plugins`, `QA` |

Shader assets live under `Assets/_Project/Shaders`. This is a source visibility map, not proof that every directory owns an implemented runtime system.

## Loose Root Script Reality

`Assets/_Project/Scripts/*.cs` is not an empty legacy bin. Static source listing on 2026-06-02 shows many active mixed-domain files live directly at script root. Do not infer ownership from subfolders alone.

Root-level implementation surface includes:

- persistence and save: `SaveManager`, `SaveBinaryStorage`, payload codecs, sidecar storage, thumbnails, slot audit/repair/metadata;
- world/simulation: `HectonWorldGenerator`, `HectonVoxelEngine`, `VoxelDeltaProcessor`, `HectonFluidEngine`, `WorldProceduralScatterDirector*`, `WorldStreamingDirector`, zone/slice/content/population directors;
- player/tools/gameplay: `PlayerInventory`, `PlayerToolManager`, `ScannerTool`, `RepairTool`, `LaserCutter`, `BuilderTool`, `CraftingSystem`, `Fabricator`, `HectonSurvivalSystem`, `TetherManager`;
- narrative/content: `NarrativeDiscovery`, `ScannableTarget`, `MessageTerminal`, `ScannerDataMiningRouter`, terminal OS runtime, QuestDAG, PDA encyclopedia streamer, and audio log system bridge baked AppliedLore packets into scanner/PDA/terminal/audio routes;
- environment/presentation: `HectonCelestialEngine`, `HectonAtmosphereManager`, `SpatialAudioManager`, HUD/PDA root bridges, localization managers, acoustic/current/biome helpers;
- diagnostics/smoke tests: save, scan, tool, UI, visual, voxel, thermal, fauna, builder, fabrication, runtime performance smoke/profiler helpers.

Interpretation: folder-level domain anchors are routing hints. Actual source ownership still requires opening the concrete file and checking owner phase, registry route, signal lane, DataVault handle, and proof artifact.

## Wave 2 Source Family Topology Overlay

Status: STATIC_SOURCE only. These topology overlays route under-described source families to existing owner docs or architecture rows. They do not prove compile, Unity import, Play Mode, profiler, GC, save/load, shader import, visual quality, player build, or platform readiness.

| Source family | Static exemplar anchors | Owner doc / topology route | Evidence class | Failure mode / proof artifact class |
|---|---|---|---|---|
| Editor authoring, bakers, validators, tuners | `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureControlMapBaker.cs`; `Assets/_Project/Scripts/World/OfflineHadalTrenchBaker/Editor/HadalTrenchBakePipeline.cs`; `Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs` | Meta and integration lane; `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 9 plus `Docs/ARCHITECTURE/TECH_ART_PBR_SURFACE_DOCTRINE.md`, `Docs/ARCHITECTURE/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`, `Docs/QUALITY_GATES.md` | STATIC_SOURCE only | Failure mode: editor source treated as runtime/import proof. Proof class: tool report, importer/validator artifact, CI/player-build artifact. |
| Loose root mixed-domain scripts | `Assets/_Project/Scripts/HectonWorldGenerator.cs`; `Assets/_Project/Scripts/WorldGeneratedPrimitiveFactory.cs`; `Assets/_Project/Scripts/SaveSidecarStorage.cs`; `Assets/_Project/Scripts/LocalizationManager.cs` | `Loose Root Script Reality` plus matching domain lane by concrete owner | STATIC_SOURCE only | Failure mode: folder-only topology skips root owners. Proof class: exact owner read plus domain-specific compile/import/runtime artifact. |
| Physiology runtime cluster | `Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs`; `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`; `Assets/_Project/Scripts/Physiology/ShinobuSuitIntegrityRuntime.cs` | Player/gameplay/survival topology through `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 5, `survival.md`, `physics.md` | STATIC_SOURCE only | Failure mode: physiology hidden under combat/presentation. Proof class: survival/pressure run, DataVault/SignalBus layout audit, black-box/profiler/GC artifact. |
| Plugins bridge / quarantine | `Assets/_Project/Scripts/Plugins/Crest/OceanKinematics/OceanKinematicsVaultRuntime.cs`; `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs`; `Assets/_Project/Scripts/Plugins/Steam/SteamManager.cs` | Meta/integration lane through `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md`; `water.md` and `terrain.md` only through approved bridge routes | STATIC_SOURCE only | Failure mode: bridge code mistaken for approved direct third-party dependency usage. Proof class: quarantine audit, bridge review, package/import/runtime/platform artifact. |
| UI navigation and instruments | `Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs`; `Assets/_Project/Scripts/UI/SubmarineSonarHoloMapRenderer.cs`; `Assets/_Project/Scripts/UI/AcousticRadarSphereRenderer.cs` | Presentation/UI lane through `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` Echelon 8, `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`, `ui.md`, `sonar.md` | STATIC_SOURCE only | Failure mode: instrument UI owns gameplay truth or allocates in HUD path. Proof class: UI GC, route capture, profiler/frame artifact. |
| Visor render features | `Assets/_Project/Scripts/Visor/HectonVisorFluidDistortionFeature.cs`; `Assets/_Project/Scripts/Visor/HectonAbyssalSsdoFeature.cs`; `Assets/_Project/Scripts/Visor/HectonStochasticSsrFeature.cs` | Presentation/rendering lane through `Docs/ARCHITECTURE/VISOR_AR_STENCIL_RENDERER.md`, `rendering.md`, `shaders.md` | STATIC_SOURCE only | Failure mode: URP feature source treated as shader/import/visual proof. Proof class: shader import, Frame Debugger/RenderGraph, visual/VRAM/frame artifact. |
| Audio propagation, echolocation, synthesis | `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs`; `Assets/_Project/Scripts/Audio/Echolocation/AcousticEcholocationRaymarch.cs`; `Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` | Presentation/audio lane through `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`, `Docs/ARCHITECTURE/ADAPTIVE_STEM_AUDIO_MIXER.md`, `Docs/ARCHITECTURE/VOCAL_WARNING_QUEUE_SHINOBU_352.md`, `audio.md` | STATIC_SOURCE only | Failure mode: broad audio topology hides DSP/thread/native proof. Proof class: audio profiler/device capture, DSP queue audit, GC/profiler artifact. |
| Lighting / GI / light shafts | `Assets/_Project/Scripts/Lighting/HectonLightingRuntime_DayNightRelay.cs`; `Assets/_Project/Scripts/Lighting/InteriorGIProbeVolumeRuntime.cs`; `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs` | Presentation/rendering and atmosphere-celestial lanes through `lighting.md`, `rendering.md`, `Docs/ARCHITECTURE/SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md` | STATIC_SOURCE only | Failure mode: light source treated as baked/probe/visual readiness. Proof class: Frame Debugger, probe/lightmap artifact, visual capture, profiler/frame artifact. |
| Graphics material response and caustics | `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs`; `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs`; `Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs` | Presentation/rendering lane through `Docs/ARCHITECTURE/TECH_ART_PBR_SURFACE_DOCTRINE.md`, `rendering.md`, `shaders.md` | STATIC_SOURCE only | Failure mode: material response source treated as shader/import/runtime visual proof. Proof class: material/shader import audit, Frame Debugger, capture, VRAM/frame artifact. |
| Core bridge, diagnostics, replay | `Assets/_Project/Scripts/Core/Bridge/H8BridgeFacadeRuntime.cs`; `Assets/_Project/Scripts/Core/DodReplayRecorder.cs`; `Assets/_Project/Scripts/Core/Diagnostics/AsynchronousTelemetryExporter.cs` | Core Source Spine plus `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`, `Docs/ARCHITECTURE/ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md` | STATIC_SOURCE only | Failure mode: diagnostics become hidden authority or proof inflation. Proof class: replay hash, telemetry export, black-box dump, compile/import/runtime artifact. |
| Save sidecars, thumbnails, maintenance | `Assets/_Project/Scripts/SaveSidecarStorage.cs`; `Assets/_Project/Scripts/SaveThumbnailSystem.cs`; `Assets/_Project/Scripts/SaveSlotMaintenanceRecord.cs` | Data And Persistence plus `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` and Echelon 1 | STATIC_SOURCE only | Failure mode: support source treated as save/load correctness. Proof class: save/load roundtrip, corruption recovery, thumbnail/sidecar artifact. |
| World anomaly, sargassum, readability | `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs`; `Assets/_Project/Scripts/World/SargassumCutManager.cs`; `Assets/_Project/Scripts/World/WorldReadabilityDirector.cs` | World/terrain/voxel lane through `Docs/ARCHITECTURE/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`, `world.md`, `terrain.md` | STATIC_SOURCE only | Failure mode: anomaly/readability code treated as route readability or visual proof. Proof class: route capture, gameplay readability artifact, profiler/GC/frame artifact. |
| QA and headless harnesses | `Assets/_Project/Scripts/QA/Headless/HeadlessSimulationRunner.cs`; `Assets/_Project/Scripts/Editor/DocumentationAuthoritySmokeTester.cs`; `Assets/_Project/Scripts/AutomationSmokeTester.cs` | Meta and integration lane through `Docs/QUALITY_GATES.md` and QA/headless row | STATIC_SOURCE only | Failure mode: harness source treated as executed validation. Proof class: fresh headless/QA CSV, black-box artifact, CI log. |
| Settings, localization, subtitles | `Assets/_Project/Scripts/UI/SettingsManager.cs`; `Assets/_Project/Scripts/LocalizationManager.cs`; `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs` | Presentation/UI lane through `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`, `settings.md`, `localization.md`, `ui.md` | STATIC_SOURCE only | Failure mode: source treated as persistence/font/subtitle/UI-GC proof. Proof class: settings roundtrip, locale/font/subtitle capture, UI GC/profiler artifact. |

## Source-Backed Runtime Reality

Static source scan on 2026-06-02. This proves code presence and declared routes only. It does not prove compile, Unity import, Play Mode, frame cost, GC, save/load, or platform behavior.

| Spine | Implementation reality | Source |
|---|---|---|
| Bootstrap | `GameBootstrapper` is a real phased bootstrapper, not a doc-only concept. It drives `HardwareCheck`, `MemoryPreWarm`, `CoreServices`, `Environment`, `Player`, `UI`, and `SceneActivate`; has explicit scene constants for `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, and `02_HECTON_WORLD`; retries Data Monolith bootstrap up to `3` times; prewarms Addressables dependency chains/UI prefabs/tier labels; guards scene activation through root-budget checks. | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` |
| Registry | `GlobalRegistry` has a broad concrete service surface: bootstrap, dispatcher, render, physics, input, audio, scene, save, UI, player context/inventory/equipment, environment/weather/ocean, power, interaction, debris, ecosystem/fauna, thermodynamics/fluid, logistics, worldgen, encounter, quest, PDA, localization, crash telemetry, asset lifecycle, VRAM, floating origin, and more. Doctrine says cold identity/DI only; the code surface is wide and must be treated as debt-sensitive. | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` |
| Dispatcher | `SystemDispatcher` is the central phase owner and also still carries legacy/priority tick lanes. Master phases include pre-simulation, simulation, post-simulation, visual sync, and fixed-simulation bridge. Cadences exist for fast 60 Hz, slow 10 Hz, thermal-critical slow 5 Hz, cold 1 Hz, and frost 5 s. Dispatcher black box is a 300-frame DataVault-backed ring. | `Assets/_Project/Scripts/Core/SystemDispatcher.cs`, `SystemDispatcherContracts.cs` |
| Hot signals | `SignalBus<T>` is a bounded frame-snapshot transport with expected capacity, max-frame policy, lane hashes, finite guards, deterministic ordering policy, overflow faulting, and coalescing/load-shed routes for acoustic/impact/combat style payloads. It is not a generic managed event bus. | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` |
| Native data | `IDataVault` / `GlobalDataVault` own generation handles, read/write handles, writer locks, buffer locks, release routes, macro-database cache ownership, and frost defrag. `TryGetLatestCreated()` exists, but only fits bootstrap/editor/diagnostic/crash escape use unless a route card says otherwise. | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`, `H8Memory.cs` |
| Save | `SaveManager` is an async persistence service plus tick/heartbeat/shutdown participant. `SaveBinaryStorage` has writer version `0x000B`, current header `56`, legacy header `44`, LZ4 paths, XXH3 checksum paths, `.tmp`/`.bak` handling, migration, sector/entity-state packing, and indexed jobs. Save/load readiness still needs route proof. | `Assets/_Project/Scripts/SaveManager.cs`, `SaveBinaryStorage.cs` |
| Static data | `H8StaticDataArena` loads `static_data.h8bin` into a NativeArray/DataVault-backed arena. `H8DataMonolithTypes` declares layout constants: schema hash `0x33313332`, header `64`, directory `64`, explicit header/directory/section/telemetry structs, and layout audits. Runtime boot/checksum proof remains separate. | `Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs`, `H8DataMonolithTypes.cs` |

## Domain Code Surface Reality

Static source scan grouped by direct script domain. File/type counts are orientation only; content and ownership matter more than exact totals.

| Domain group | Implemented surface visible in code | Current read |
|---|---|---|
| Core / Bootstrap / Data | Bootstrapper, registry, dispatcher, service contracts, runtime contexts, input, scene, native memory, DataVault, save, crash/runtime watchdogs, GC monitor, scalability/homeostasis, Data Monolith arena. | Real central runtime spine exists. Broad `GlobalRegistry` and mixed dispatcher lanes are the main governance risks. |
| World / Terrain / Voxel / Scatter / Streaming | Chunk residency/backpressure, terrain pager, predictive streaming DTO/jobs, voxel streaming bridge, active voxel MC pipeline, voxel delta save participant, geology seam/voxel bridges, dynamic nav grid, procedural scatter with classic/GPUI/DOTS backend surfaces, vegetation residency/indirect renderer, persistent world registry, wreckage/resource/regrowth routes, biome SDF/transition managers, HLOD/impostor/LOD/culling services. | World systems are source-present and DataVault-heavy. Scene wiring, Addressables/hydration, voxel carve save/load, terrain seam visuals, scatter backend parity, vegetation/wreck visual proof, profiler/GC, VRAM, and frame hitch proof remain pending. |
| Player / KCC / Input / Physical Interaction / VR | `InputDispatcher` owns deterministic input, XR buffers, haptic request consumption, and input black box. `HydrodynamicKccRuntime` owns KCC states/inputs/environment/collision/wake/telemetry DataVault lanes and publishes `KccVelocitySignal`. `PlayerKinematicsRuntime` publishes player state/stress/acoustic/haptic routes and hand probes. `PlayerInteraction`, `PhysicalInteractionHandler`, `PhysicalHandController`, `VRInteractionKinematicBridge`, `VRSomaticProvider`, and `EquipmentInteractionHandler` cover look target, physical grab/snap, VR hand bridge, somatic comfort, and interaction signal queues. | Source-present only. Controller/device proof, KCC collision correctness, environment provider wiring, grab/force ownership, XR route, queued tool-surface route, First 20 Minutes movement/interact proof, profiler/GC remain pending. |
| Gameplay / Inventory / Equipment / Economy / Scavenging | Airlocks, reactors, hazards, data archaeology, beacons, eclipse/ending systems, first-hour and mission services, auxiliary equipment router, native SOA inventory plus service manager, powered fabricator/recipe jobs/physical output, survival saveable slow/late tick, loot magnet, recycler buffer, tool/equipment/scavenging routes. | Gameplay/economy systems are source-present, not just planned docs. Copper-wire route, boot-to-craft-to-save/load, inventory/fabricator UI feedback, loot pickup route, profiler/GC proof remain pending. |
| Narrative / Quest / PDA Lore / AudioLog | AppliedLore DataMonolith facade, generated packet hashes, QuestDAG resolver/loading, PDA encyclopedia streaming, scanner/terminal unlock routes, audio log save/playback/encrypted fragments, narrative progression bridge. | Real content runtime route exists for AppliedLore in source. Scene bindings, UI route proof, and save/load continuity are still pending. |
| Physics / Forces / Buoyancy / Tethers / Cavitation | Buoyancy object/read model, buoyancy displacement jobs, analytical Gerstner wave runtime, async buoyancy readback, PhysicsApplySystem buoyancy queue, abyssal cavitation runtime, cable solver/service, tether AUP/Verlet solvers, harpoon tension solver, physics event/tension/snap signal routes. | Source owners exist, but force application ownership, collision correctness, same-frame job completion/readback audit, tether/cable gameplay route, frame time, GC, and player-build proof remain pending. |
| Vehicles / Submarine / Seaglide / Exosuit | Submarine dynamics and gyro/ballast jobs, docking autopilot spline service, seaglide hydrodynamics, exosuit kinematics/SDF route, vehicle motor wake generation, vehicle component damage, hull dent shader bridge. | Vehicle physics is source-present and signal/DataVault-heavy. Controller scene proof, ballast/docking gameplay, damage-to-hull visual proof, audio/haptic feedback, physics stability, profiler/GC, and device proof remain pending. |
| Construction / Habitat / Power / Logistics | Construction manager as logistics/habitat/deconstruction/save participant, base modules, habitat CSR graph, module catalog, deconstruction refund/loot kernel, structural integrity, fluid incursion, fluid pipe pressure graph, sump pumps, bulkhead/hatch containment, power grid/logistics graph, relays, RTG, battery charger logistics, Shinobu logistics router, drone/repair routes. | These systems are source-present and DataVault/SignalBus-heavy. Base-building, module save/load, flood authority, pipe/bulkhead interaction, power distribution, brownout feedback, battery charge inventory route, drone/repair gameplay, visual proof, profiler/GC, and player-build proof remain pending. |
| AI / Fauna / Pathfinding / Ecosystem | Fauna simulation engine, multi-lane fauna brain, predator cognition/acoustic SDF, stress-driven spawn director, fauna kinematics/IK/tentacles/damage route, utility cognition vaults/jobs, apex/alpha leviathan cognition, voxel A*/path funnel runtime, acoustic echo black box, ecosystem director, boid/flocking balancer, flora-fauna symbiosis, macro ecosystem migration/nutrient routes. | These systems are source-present and DataVault/SignalBus-heavy. Scene spawn integration, gameplay request route, SDF/navgrid wiring, deterministic ordering, swarm visual proof, profiler/GC, and runtime fault dump proof remain pending. |
| Atmosphere / Weather / Ocean / Thermodynamics | Base atmosphere engine/logistics, gas dynamics solver, toxic outgassing chemistry, ocean surface atmosphere runtime, global weather director, storm propagation contracts, abyssal thermodynamics solver, thermodynamics hazard grid, reactor thermal jobs. | Source owners exist for gas, weather, ocean surface, thermal hazards, and reactor heat/radiation signals. Base room wiring, gas/player survival route, weather/ocean visual proof, reactor coupling, continuous quality load-shed, profiler/GC, device, and player-build proof remain pending. |
| UI / PDA / Visor / Audio / VFX / Graphics | PDA DataVault/typewriter streamer, diegetic PDA controller, subtitle/font/loading managers, visor HUD projection and URP render features, foveated render commander, thermal DRS adapter, culling/TBDR services, player-critical procedural audio renderer, vocal warning system, music director/adaptive stem mixer, marine snow/propwash/plasma/fog VFX buffers. | Presentation layer is large, tick-heavy, and GPU/audio-thread sensitive. It consumes snapshots/signals and does not own gameplay truth. Must be proven through UI GC, audio-thread profiler/device, Frame Debugger/RenderGraph, shader import, visual capture, and VRAM/DRS artifacts. |
| Optimization / Asset Lifecycle / VRAM / RT | Asset lifecycle governor, asset load dispatcher, Addressables handle records, VRAM monitor/pressure/enforcer, RT lifecycle tracker/pool, camera/UI/visor/post-FX RT managers, thermal dynamic resolution, TBDR/culling telemetry. | Source-present runtime support. Addressables release proof, RT leak proof, Memory Profiler/VRAM capture, DRS visual proof, profiler/GC/frame proof remain pending. |
| Networking / Rollback | Network mode wrapper plus rollback runtime/contracts: DataVault snapshots, Merkle nodes/descriptors, input prediction journals, mock jitter packets, rollback signal, snapshot/restore/hash/mismatch jobs, quality-scaled rollback budget math. | Source-present only. Loopback/device proof, authoritative transport, packet serialization, desync recovery, rollback correctness, profiler/GC proof remain pending. |
| ModdingAPI / Sandbox / Persistence | Envelope-only `HectonAPI`, `HectonEventBus` managed isolation, mod event projection, command dispatcher, future command sandbox/quarantine, loader/runtime info, resource proxy, settings/menu state, mod save payload store, mod-world spawn persistence. | Source-present only. Mod envelope runtime playbook, external starter kit validator proof, mod load/play proof, command budget/security proof, save roundtrip proof remain pending. |
| QA / Headless / Watchdogs | Command-line QA watchdog, endurance watchdog, GC allocation fuzzer, DataVault metric/blackbox rings, profiler recorders, CSV export, headless simulation/stress/Jacobi runners. | Source-present harnesses. Fresh QA/headless artifacts, current CSV/blackbox reports, profiler/GC execution proof, and CI wiring proof remain pending. |
| Editor Build / Platform / SDK Tooling | Prebuild/postbuild validators and scanners for XR/OpenXR, Quest Vulkan/URP, graphics API, native plugins, shader precision/portability, thread affinity, machine code purity, GUID/case sensitivity, log scrubbing, build playtest log, mod sandbox/kernel editor tooling. | Editor tooling exists in source. Player build, platform/device, CI validator output, Quest/PCVR build, and SDK tool run proof remain pending. |

## Reality Gaps From Source Scan

- Static source proves implementation surface, not correctness.
- `GlobalRegistry` is much wider than the doctrine summary; every new hot caller must prove it is not polling registry state.
- `SystemDispatcher` has both master phase ownership and legacy tick lanes; docs that imply a fully pure four-phase system are ahead of code.
- Data Monolith and save code are substantial, but runtime boot/checksum/save-load route proof is still missing here.
- UI/world/gameplay domains contain many tick participants; presentation readiness needs profiler/GC, audio-thread/device, Frame Debugger/RenderGraph, shader import, visual capture, and VRAM/DRS evidence.
- Runtime support tooling is source-present only until separate optimization, networking, modding, QA/headless, player-build/platform, profiler/GC/VRAM artifacts exist.
- Mod signal inventory is currently `175 / 2 / 173` by `rg --pcre2` source scan; schema/spec/README must match that split before static mod validation can pass.

## Active Scene Spine

Enabled scenes in `ProjectSettings/EditorBuildSettings.asset`:

1. `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
2. `Assets/_Project/Scenes/01_MAIN_MENU.unity`
3. `Assets/_Project/Scenes/01_ORBIT.unity`
4. `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

Enabled scene list includes `01_ORBIT`, but enabled list is not production handoff proof. Current first-20 proof follows the root production handoff:

```text
00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD
```

Load-game resume may still enter `02_HECTON_WORLD` directly from `01_MAIN_MENU`. `01_ORBIT` remains an enabled standalone/YELLOW prologue route; it is not mandatory first-20 acceptance until its route card is GREEN and root scene-flow authority is updated. Sandbox scenes exist under `Assets/_Project/Scenes`, but they are not enabled build-spine proof.

Authority drift note:

- `ProjectSettings/EditorBuildSettings.asset` still enables `01_ORBIT`.
- `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md` remains `YELLOW / STATIC_SOURCE_ONLY`.
- Treat any `01_ORBIT` main-handoff claim as unresolved until that route card is GREEN and root scene-flow authority is updated.
- Do not claim Play Mode route proof from this static state.

## Core Source Spine

| Runtime area | Source anchor | Route rule |
|---|---|---|
| Bootstrap | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | cold owner setup, Kahn order, no scene-search dependency loops |
| Registry/DI | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` | cold identity and dependency injection only |
| Hot first-party signals | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` | typed bounded payloads, owner/phase/capacity required |
| Legacy signal bridge | `Assets/_Project/Scripts/Core/GlobalSignals.cs` | documented bridge lanes only |
| Native ownership | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` and `H8Memory.cs` | cross-domain persistent/job-visible buffers use generation-checked handles |
| Dispatcher | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` and `SystemDispatcherContracts.cs` | `PRE_SIMULATION`, `SIMULATION`, `POST_SIMULATION`, `VISUAL_SYNC` owner windows |
| Scalability | `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs` | continuous `GlobalQualityWeight`, no binary quality switch |
| Save | `Assets/_Project/Scripts/SaveBinaryStorage.cs` and `SaveManager.cs` | writer `0x000B`, header `56` bytes, proof needs route save/load |
| Scene service | `Assets/_Project/Scripts/Core/SceneRuntimeService.cs` | scene activation gate, cached service route |
| Player context | `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs` | owner publishes player runtime truth |
| Environment context | `Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs` | owner publishes environment runtime truth |
| Input | `Assets/_Project/Scripts/Core/InputDispatcher.cs` | service route; no hot singleton polling |
| Physics application | `Assets/_Project/Scripts/PhysicsApplySystem.cs` | dispatcher-owned fixed/post-fixed packet windows |
| Audio | `Assets/_Project/Scripts/SpatialAudioManager.cs` | presentation/audio consumes snapshots and owned signal lanes |
| World/scatter | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` | one world/scatter owner path until profiler proof says otherwise |
| Encounter pacing | `Assets/_Project/Scripts/HectonDirectorAI.cs` | director route must not become a second owner for world truth |
| HUD/UI | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` | presentation reads snapshots; UI does not own simulation truth |

## Runtime Route Map

```text
Bootstrap
  -> owner-local setup
  -> GlobalRegistry cold service identity
  -> SystemDispatcher phase ownership
  -> GlobalDataVault native snapshots and handles
  -> SignalBus<T> hot unmanaged packets
  -> presentation/audio/UI visual sync
```

Route rules:

- One gameplay fact has one owner, one route, and one proof artifact.
- Runtime owners publish once from their owner phase.
- Consumers read immutable snapshots, cached service interfaces, generation-checked handles, or typed signal payloads.
- `Get*`, `TryGet*`, `Resolve*`, and `Read*` accessors are pure.
- Read accessors must not allocate, grow buffers, publish, search scenes, sync transforms, complete jobs, or mutate global state.
- `GlobalRegistry` is not a hot polling bus.
- `HectonEventBus` is mod/API/cold managed isolation, not first-party gameplay flow.

## Data And Persistence

- Data Monolith target: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Current static payload exists and is `7,457,664` bytes, mtime 2026-06-07, in the 2026-08-05 static filesystem measurement; the 2026-06-01 check recorded `1,804,864` bytes.
- Scoped Python validator recheck on 2026-05-28 passed for current StreamingAssets `.h8bin` payloads with narrowed Data Monolith source/runtime roots: `Docs/Reports/DOC_ROOT_ARCH_AUDIT_h8bin_validator_narrow_20260528.json`.
- Readiness is still `PENDING VERIFICATION` without import, bake, boot, checksum, player, save/load, and memory proof.
- Save writer version: `0x000B`.
- Current save header size: `56` bytes.
- AUP/blit layout: `48` bytes.

## First Route To Prove

The product spine is not "all systems compile". It is the spectacular semi-open shallow first-20 route defined by `FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md` and `FIRST_20_MINUTES_ROUTE_BRIEF.md`.

Copper/Copper Wire remains a useful candidate starter chain inside that route, not the whole V0 identity:

```text
boot -> world load -> semi-open beautiful shallow exit -> swim -> oxygen/depth/pressure
-> find copper -> collect Data_Copper -> quest_copper_sample
-> craft Recipe_CopperWire or stronger verified route improvement -> save -> load -> return to same state
```

Read before product/runtime work:

- `FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- `FIRST_20_MINUTES_ROUTE_BRIEF.md`
- `BOOT_SEQUENCE_TOPOLOGY.md`
- `DISPATCH_PIPELINE.md`
- `PLATFORM_PORTABILITY_PROOF_LADDER.md`

## Verification Gaps

Current static topology does not prove:

- full-solution compile health;
- Unity import or clean Console;
- Play Mode or player launch;
- route completion;
- profiler, GC, Memory Profiler, or VRAM budget;
- Data Monolith runtime load/checksum;
- save/load roundtrip;
- non-zero scene/prefab AppliedLore hash assignments;
- shader/import/render correctness;
- XR, Steam Deck, Linux, macOS, Quest, PICO, or console readiness.

Use `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` for latest proof snapshots and cite fresh artifacts before changing status.
