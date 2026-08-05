# HECTON-8 Global Architecture Map

Date: 2026-05-28
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: stable architecture orientation for source owners, domains, and global authority routes. Current proof snapshots live in `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`; current scene/source topology lives in `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`; domain coverage lives in `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`.

## Authority Boundary

- Active only where it agrees with `AGENTS.md`, `.agents-skills/`, `Docs/PROJECT_BASELINE.md`, `Docs/ARCHITECTURE`, and current source.
- This map is not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, shader, platform, or visual proof.
- Exact source counters are snapshot data. Recapture before using counts as gates.

## Current Static Topology

Static source/filesystem check on 2026-06-01:

- Unity editor: `6000.4.1f1`.
- First-party root: `Assets/_Project`.
- Enabled scene spine: `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.
- Load-game resume may enter `02_HECTON_WORLD` directly from `01_MAIN_MENU`.
- First-party asmdefs under `Assets/_Project`: `171`.
- Data Monolith payload exists at `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, `7,457,664` bytes, mtime 2026-06-07, measured 2026-08-05 (supersedes 2026-06-01 check: `1,804,864` bytes).

Do not treat these facts as route proof. They only state what current project files expose.

Scene authority drift remains open: `AGENTS.md` still contains older no-orbit handoff wording. `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` records the current static conflict and proof boundary.

## Runtime Authority Flow

```text
Bootstrap
  -> owner-local setup
  -> GlobalRegistry cold service identity
  -> dispatcher phases
  -> DataVault/native owner buffers
  -> SignalBus hot packets
  -> presentation/audio/UI sync
```

Rules:

- One fact has one owner and one route.
- Owners publish snapshots from their owner phase.
- Consumers read immutable snapshots, cached interfaces, or typed packets.
- Read APIs do not publish, allocate, search the scene, complete jobs, or mutate global state.

## Domain Backbone

`Docs/PROJECT_ATLAS.md` carries the current domain index. This map summarizes domain ids `1..85`.

| Range | Echelon | Scope |
|---:|---|---|
| 01-10 | Core and memory infrastructure | bootstrap, signal lanes, save codec, Data Monolith, AUP, arena/native memory, telemetry, scalability, platform, dispatcher |
| 11-20 | World generation and terrain | MapMagic bridge, voxel/SDF, meshing, carving, scatter, wreckage, geology, biomes, flows, vents |
| 21-30 | Flora, fauna, and biota | ecosystem, fauna spatial lookup, swarm compute, predator cognition/motion, IK, flora sway, bioluminescence, genetics |
| 31-40 | Player, kinematics, tools | locomotion, buoyancy, IK, tether, equipment, harvesting, inventory, crafting, XR comfort, interaction |
| 41-50 | Combat and physiology | damage, armor LUT, status effects, stress, decompression, hypoxia, crush depth, metabolism, radiation, wounds |
| 51-60 | Habitat and vehicles | construction, structural integrity, flooding, power, pipes, deconstruction, submarine OS/nav, drones, scooter |
| 61-68 | Atmosphere and celestial | orbits, tide/seismic, weather, gas, thermodynamics, marine snow, fog/light shafts, day/night GI |
| 69-78 | Presentation and UX | subtitles, terminals, visor AR, PDA, narrative triggers, cartography, frequency tuning, acoustic radar, audio warnings |
| 79-85 | Meta, polish, integration | haptics, camera, physics culling, integration, documentation, QA, technical research |

## Core Owners

Registry-backed service owners visible in source must remain cold identity routes:

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

Additional source-backed contracts include:

- `IPDALogbookService` -> `PDA/PDALogbookManager`
- `IPowerGridService` -> `PowerGridManager`
- `IFaunaSim` -> active fauna simulation owner; bootstrap fallback does not prove visible fauna
- `IFluidSim` -> `Physics/FluidMathCore`
- `ISubmarineRuntimeContext` -> `SubmarineCoreDirector`
- `ISubmarineHullBreachReadModel` -> `SubmarineStructuralGrid`

## Global Communication

- `SignalBus<T>` is the first-party hot broadcast route.
- `GlobalSignals` direct queues are legacy/documented bridge lanes.
- `HectonEventBus` is managed mod/API/cold isolation.
- New routes require `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` and `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`.
- New hot paths must not call generic `GlobalRegistry.Get<T>` / `TryGet<T>` as polling.

## Native Memory

- `GlobalDataVault` owns cross-domain persistent/job-visible native buffers.
- `H8Memory` owns approved direct allocation helpers.
- Owner-local native scratch is allowed only with explicit lifetime and disposal.
- Persistent native fields in `MonoBehaviour` types are debt unless proven owner-local and disposed.
- Remaining debt and latest hotspot numbers are tracked in the actuality ledger.

## Runtime System Groups

World:

- Keep one world/scatter runtime owner.
- DOTS is shadow/prototype until total-frame profiler gain and semantic parity exist.
- Voxel/geology/streaming remain hybrid unless owner and profiler proof justify a narrower migration.

Player and tools:

- Player, equipment, inventory, interaction, and scanner routes must keep source-of-truth ownership explicit.
- UI and presentation read snapshots; they do not own simulation truth.

Save and static data:

- Save protocol and Data Monolith contracts live under `Docs/ARCHITECTURE`.
- Data Monolith readiness requires file, import, bake, boot, checksum, and player proof.

Presentation:

- Premium approximation first.
- Quality scales continuously through `GlobalQualityWeight`.
- Rendering proof requires shader/import/render artifacts, not just source references.

## Verification Gaps

See `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

This map deliberately does not duplicate current build logs, scanner counters, report chains, or task-loop history.

STATUS: PENDING VERIFICATION
