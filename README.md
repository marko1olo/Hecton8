<div align="center">

# HECTON-8

**A deep-water survival simulator built on flooded geography, failed industry, and pressure architecture.**

*“The world is not random underwater decoration.”*

<br>

![Unity](https://img.shields.io/badge/Unity-6000.5.0f1-000000?style=for-the-badge&logo=unity&logoColor=white)
![URP](https://img.shields.io/badge/Render_Pipeline-URP-1a7f5a?style=for-the-badge)
![C#](https://img.shields.io/badge/C%23-1.76M_lines-239120?style=for-the-badge&logo=csharp&logoColor=white)
![Files](https://img.shields.io/badge/Scripts-2806_files-3178C6?style=for-the-badge)
![Shaders](https://img.shields.io/badge/Shaders-474_+_78_compute-8a2be2?style=for-the-badge)
![Burst](https://img.shields.io/badge/Jobs_+_Burst-DOD-ff6f00?style=for-the-badge)
![XR](https://img.shields.io/badge/XR-supported_lane-1a7f5a?style=for-the-badge)
![Status](https://img.shields.io/badge/Status-In_Development-yellow?style=for-the-badge)

</div>

---

## Table of contents

- [What this is](#what-this-is)
- [Scale](#scale)
- [Design law](#design-law)
- [Engineering invariants](#engineering-invariants)
- [Core runtime architecture](#core-runtime-architecture)
  - [Service registry](#service-registry)
  - [Tick tiers](#tick-tiers)
  - [Data vault](#data-vault)
  - [Bootstrap](#bootstrap)
- [System map](#system-map)
  - [World generation](#world-generation)
  - [Voxel terrain](#voxel-terrain)
  - [Ecosystem](#ecosystem)
  - [Fauna AI](#fauna-ai)
  - [Reactive flora](#reactive-flora)
  - [Player, physics and vehicles](#player-physics-and-vehicles)
  - [Physiology and survival](#physiology-and-survival)
  - [Atmosphere, gas and thermodynamics](#atmosphere-gas-and-thermodynamics)
  - [Construction, habitat and drones](#construction-habitat-and-drones)
  - [Power and logistics](#power-and-logistics)
  - [Rendering and graphics](#rendering-and-graphics)
  - [Visor and HUD](#visor-and-hud)
  - [Audio](#audio)
  - [Save system](#save-system)
  - [Narrative and quests](#narrative-and-quests)
  - [Modding API](#modding-api)
  - [Optimization and asset lifecycle](#optimization-and-asset-lifecycle)
- [Repository layout](#repository-layout)
- [Getting started](#getting-started)
- [Verification](#verification)
- [Contributing](#contributing)

---

## What this is

HECTON-8 is a systems-first underwater survival simulator. Most of this repository is simulation
rather than set dressing: macro geology shapes the seafloor, the seafloor drives ecology, ecology
drives fauna behaviour, atmosphere and thermodynamics drive survival pressure, and the player's
submarine perturbs all of it.

> [!IMPORTANT]
> **Development status.** This is an in-development project and system maturity varies widely across
> the domains below. The project's own standard is that nothing is called *verified* without runtime
> proof, so this document separates what has been measured from what is merely implemented. Treat it
> as a map, not a release claim.

---

## Scale

| Metric | Value |
|:--|--:|
| C# source | **1,764,002 lines** across **2,806 files** |
| Shaders | **474** `.shader` |
| Compute shaders | **78** `.compute` |
| HLSL includes | **93** `.hlsl` |
| Registered runtime services | **285** |
| Vault buffer identifiers | **3,307** |
| Editor tooling & diagnostics | **561** files |
| Pure testable logic + tests | **404** files |

The largest single subsystems by line count give a fair picture of where the weight sits:

| File | Lines | Domain |
|:--|--:|:--|
| `PlayerCriticalProceduralAudioRenderer.cs` | 14,337 | Audio |
| `H8LocHashes.cs` (generated) | 12,895 | Localization |
| `PersistentWorldRegistry.cs` | 11,139 | World |
| `FloraInteractionManager.cs` | 11,023 | Flora |
| `DestructibleOrganicManager.cs` | 10,737 | World |
| `SargassumMicroFaunaBoids.cs` | 10,504 | Fauna |
| `DroneFleetManager.cs` | 10,044 | Construction |
| `GameBootstrapper.cs` | 8,985 | Bootstrap |
| `GlobalRegistry.cs` | 8,473 | Core |
| `SuitHUDV4CanvasOverlay.cs` | 8,137 | UI |

---

## Design law

The prime world law, quoted from the project's own world bible:

> The world is not random underwater decoration. It is flooded geography, failed industry, salvage
> history, and pressure architecture.

This is an architectural constraint with a testable consequence, not a mood statement. If a biome's
fauna can be predicted without looking at the terrain, the law is being violated — and that specific
failure is guarded by an automated probe. See [Verification](#verification).

Acceptance is three-pillar: **graphics, optimization and gameplay must all pass.** Beautiful but
empty is rejected. Fast but flat is rejected. Rich gameplay that runs badly or looks cheap is
rejected.

---

## Engineering invariants

### 1. Absolute Universe Position (AUP)

World coordinates are 64-bit `double3` (`AbsoluteUniversePosition`) with a **floating origin**.
Systems that care implement `IOriginShiftListener` and rebase when the origin moves.

The consequence is that `float32` on the GPU is *correct by design*, not a compromise: simulation
happens in absolute doubles, then coordinates are rebased into a near-origin local space before ever
reaching a shader.

| System | Absolute side | Rendered side |
|:--|:--|:--|
| `HectonSpatialHash` | `double3` centres, **`Long3` integer cell coords** | CPU only |
| `SargassumMicroFaunaBoids` | grid origin quantised in `double` (`FloorToMultiple64`) | narrowed to `float4` post origin-shift |
| `FloraInteractionManager` | wake source deltas resolved in `double3` | narrowed after subtraction |

> [!WARNING]
> Do not "upgrade" a GPU path to 64-bit because it looks lossy. If the system implements
> `IOriginShiftListener`, the values it uploads are already small and the narrowing is intentional.
> Widening costs performance and fixes nothing.

### 2. Data-oriented hot paths

Hot work runs as Burst-compiled Jobs over `NativeArray` / `NativeQueue`. Steady-state heap
allocation in a hot path is a defect. Where a managed array is unavoidable it is allocated once and
annotated at the declaration:

```csharp
// COLD ALLOC: GeologyBiomeCacheEntry[256] - direct-mapped sector -> biome classification cache so
// the macro geology stack runs once per 1 km sector instead of once per 50 m biomass macro cell
// (400 macro cells per sector) - owner: EcosystemDirector
private GeologyBiomeCacheEntry[] _geologyBiomeCache;
```

### 3. Determinism where it is load-bearing

`FloatMode.Deterministic` is used wherever a result feeds save identity, replay or cross-machine
agreement; `FloatMode.Fast` is acceptable elsewhere. The split is deliberate — determinism forfeits
reassociation and FMA contraction, so it is applied on evidence, not reflex.

Numerical guards expected in new code:

```csharp
float lengthSq = math.lengthsq(value);
bool valid = math.isfinite(lengthSq) & lengthSq > 0.000001f;
```

### 4. Continuous quality scaling

`GlobalQualityWeight` scales presentation continuously from minimum-survival to visual overkill.
It must **not** alter gameplay truth, DTO layout, save identity, authority route or deterministic
state ownership. Quality changes how the world looks, never what is true about it.

### 5. Evidence classes

| Class | Means |
|:--|:--|
| `STATIC_DOC` | Written down. No execution. |
| **Static review** | Read by a human or agent. Not compiled. |
| **Compile-verified** | Builds clean. Says nothing about behaviour. |
| **Runtime proof** | Observed executing, with the log or measurement retained. |

“It compiles” is never reported as “it works”.

---

## Core runtime architecture

Four files carry the runtime spine: `GlobalRegistry.cs` (8.5k lines), `SystemDispatcher.cs` (7.1k),
`GlobalDataVault.cs` (6.8k) and `H8Memory.cs` (6.4k).

### Service registry

`GlobalRegistry` exposes **285 services** through interface-typed slots — terrain bridge, player
context, save service, voxel engine, fauna genetics, hazard zones, tick dispatcher and so on.
Services register at bootstrap and can be **hot-swapped at runtime**: consumers implement
`IGlobalRegistryHotSwapListener` and are notified with the slot, previous and current instance, so
they can rebind rather than hold a stale reference.

```csharp
public void OnGlobalRegistryServiceReplaced(
    GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
```

This is why systems resolve dependencies through the registry instead of `FindObjectOfType`, and why
a null service is expected to degrade gracefully rather than throw.

### Tick tiers

`SystemDispatcher` drives seven distinct tick tiers so each system runs at the cadence it actually
needs. Nothing runs per-frame by default.

| Interface | Cadence | Typical use |
|:--|:--|:--|
| `ITickable` | per frame | input, camera, presentation |
| `IFixedTickable` | fixed step | physics-coupled simulation |
| `IPostFixedTickable` | after fixed step | post-physics reconciliation |
| `ISlowTickable` | 0.1 s / 0.5 s | population, ecology, logistics |
| `IBucketedSlowTickable` | slow, amortised in buckets | large sets spread across ticks |
| `IFrostTickable` | 5 s / 10 s | cold solves, headless ecosystem |
| `ILateFrameTickable` | end of frame | GPU upload, telemetry flush |

Execution order is pinned where it matters via `[DefaultExecutionOrder]` — e.g.
`FloraInteractionManager` at `-105`, `FaunaGeneticsManager` at `-6235`.

### Data vault

`GlobalDataVault` + `H8Memory` provide a central, generation-tracked native memory pool addressed by
**3,307 `BufferID` identifiers**. Systems acquire typed views rather than allocating their own
containers:

- `VaultGenerationHandle<T>` — a handle that becomes invalid if the underlying buffer is
  reallocated, so stale reads are detected instead of silently returning garbage
- **mutation guards** — `TryAcquireMutationGuard(mask)` / `ReleaseMutationGuard(mask)` serialise
  writers against a bitmask of buffer IDs
- **black-box telemetry lanes** — fixed-size ring buffers of packed structs
  (`[StructLayout(LayoutKind.Explicit)]`) that record recent frames of system state for post-mortem
  dumps without allocating

This is the mechanism that makes zero-GC realistic across a project this size: buffers are owned
centrally, sized once, and handed out as views.

### Bootstrap

`GameBootstrapper` (9k lines) sequences service construction, scene loading and readiness gating.
Supporting pieces are deliberately paranoid:

- `BootstrapRegistryCycleValidator` — detects circular service dependencies at boot
- `SceneInstantiationGate` — blocks premature instantiation before dependencies exist
- `BootstrapRouteEnforcer` — keeps the boot route from being bypassed

Entry scene is `Assets/_Project/Scenes/00_BOOTSTRAP.unity`, which transitions into
`02_HECTON_WORLD`.

---

## System map

### World generation

`WorldMacroGeologyFields.Evaluate(x, z, in parameters)` returns a `WorldMacroGeologySample` carrying
masks — `ShelfMask`, `ShelfBreakMask`, `RidgeMask`, `TrenchMask`, `BasinMask`, `FaultMask`,
`SedimentMask`, `SeepMask`, `ReefEligibilityMask`, `CraterMask`, slope, curvature, erosion flow — and
a resolved zone:

```
PhoticShelf · ShelfBreak · FaultRidge · BrineTrench
AbyssalPlain · SedimentFan · ColdSeepField · HadalBasin
```

Geology is the shared source of truth. It is consumed at runtime by terrain height, MapMagic bridge,
splatmaps, vegetation scatter, wreck placement (`ProceduralWreckGenerator`) and the ecosystem.
`PersistentWorldRegistry` (11.1k lines) tracks what the world has become — carved voxels, placed
structures, harvested nodes — so the world is persistent rather than regenerated.

### Voxel terrain

Surface-nets voxel volumes provide true 3D caves and overhangs on top of the 2.5D heightfield.
The collider path is fully asynchronous: classify → bucket → build chunk-local geometry → upload →
`Physics.BakeMesh` on a background thread → deferred assignment to `MeshCollider.sharedMesh`, with
`BoxCollider` proxies covering the window before the real mesh commits. Offline bakers exist for
static cave SDF, hadal arches, hadal trenches and wreckage.

### Ecosystem

The world is partitioned into **1 km sectors** with biomass on a **50 m macro-cell** grid. Sector
populations evolve under a Lotka-Volterra model modulated by food density, carrying capacity,
oxygen, temperature and light. Sector biome is derived from geology into three lanes:

| Lane | Condition | Meaning |
|:--:|:--|:--|
| `2` scarce | `TrenchMask > 0.8` | Abyssal trench. Thin food column, pressure-adapted hunters. `-0.05` capacity. |
| `1` rich | `ShelfMask > 0.5` | Photic shelf. Dense kelp, schooling prey. `+0.08` capacity. |
| `0` neutral | otherwise | No bias. |

Trench wins where both masks overlap — a trench cutting through a shelf is still a trench.

**Whalefall.** Death is conserved, not deleted. `FaunaBrain.Die()` publishes an `EntityDeathSignal`
whose intensity scales with max health, so a leviathan leaves a proportionally larger corpse.
`NutrientDriftRuntime` consumes it into a carrion pool (capacity 5,000) with cold/hot decay
multipliers and nutrient injection, then republishes each active corpse into `WorldSpatialHashGrid`
as a transient resource event — the same channel scavenger AI already queries for food.

```
FaunaBrain.Die()
  └─> SignalBus<EntityDeathSignal>          (also fed by population-balancer culls)
        └─> NutrientDriftRuntime_Carrion    decay · nutrient injection
              └─> WorldSpatialHashGrid      Resource | ChemicalReceiver | Interactable
                    └─> scavenger AI
```

**Genetics.** `FaunaGeneticsManager` derives per-instance traits deterministically from AUP spawn
position, species, biome and the persisted world seed, packing them into a 64-bit genome
(`FaunaGenome64`). Traits then mutate in place under real environmental fields — radiation grid,
AUP-native toxicity hazard zones, brine layer depth — via `FaunaGenome64.MutateGenome`.
`MigrationDirector` moves populations between sectors following food gradients.

### Fauna AI

`FaunaBrain` is split across partials for combat damage, ecosystem coupling and foveated LOD.
Sensing runs through `FaunaSpatialHashRegistry` over `HectonSpatialHash` (AUP-native, `Long3` cells).
`SargassumMicroFaunaBoids` (10.5k lines) drives GPU boid flocking — cohesion, separation, alignment,
obstacle avoidance, panic response, grazing anchors — with a compute-side spatial grid.
`ShinobuEcosystemBalancer` and `ShinobuFloraFaunaSymbiosisSolver` handle population balance and
flora/fauna symbiosis including toxemia, camouflage and pollen transfer.

### Reactive flora

Vegetation reacts to wake sources through `AccumulateFloraForcesJob`, which resolves source-to-sample
deltas in `double3` AUP space, applies an ellipsoidal vertical squash, blends wake and radial
directions, weights by source kind and speed, and caps displacement. A separate **cascade** channel
propagates a bioluminescent pulse through kelp when a disturbance fires.

Both the cascade clock and the per-instance seed are published **epoch-relative**, because the shader
carries them as `half`: absolute simulation seconds quantise to 1.0 s spacing near t=2048 and
overflow at 65504, which would erase a sub-second pulse envelope. Subtracting a shared epoch on both
sides leaves `age = cascadeTime - cascadeSeed` algebraically identical while keeping both operands
resolvable.

Supporting systems: `FloraRegrowthDirector`, `SargassumCutManager`, `SargassumCollapseChunk`,
`SargassumGlobalDragManager`, `DestructibleOrganicManager` (10.7k lines) with deterministic
`EntropyYieldJob` harvest yields, and `InstancedFloraRenderer` for GPU-instanced draw.

### Player, physics and vehicles

`HydrodynamicKccRuntime` (5.2k) is a kinematic character controller written for water — buoyancy,
drag, current advection and contact resolution. `PlayerKinematicsRuntime` (4.6k) owns player motion
state. `AsyncBuoyancyReadbackRuntime` reads GPU water state back asynchronously to avoid stalls.
`AbyssalCavitationRuntime` models cavitation at depth.

Submarine handling: `SubmarineAutoLevelBallastController` (4.6k) for ballast and trim,
`SubmarineAutopilotSdfNavigator` (3k) which navigates against the voxel SDF rather than a navmesh,
`VehicleDockingModule` for docking.

`ContextualPhysicalIkRig` (3.7k) and `PhysicalHandController` (3.8k) drive physical IK and hand
interaction; `VRSomaticProvider` (3.9k) and `VRInteractionKinematicBridge` cover the XR lane with
comfort handling.

### Physiology and survival

The `Shinobu*` physiology stack models the body rather than a health bar:
`ShinobuPhysiologyRuntime`, `ShinobuMetabolismRuntime` (effort, load, energy),
`ShinobuSuitIntegrityRuntime`, `ShinobuPhysiologyJobs` for the Burst side, and
`ShinobuRespawnReconciliationRuntime` for death/respawn state reconciliation. Decompression is real
—`buhlmann_zh16_profiles.csv` and `buhlmann_3tissue_profiles.csv` drive tissue-loading models.
`HazardZoneManager` (3.7k) owns AUP-addressed hazard fields including toxicity.

### Atmosphere, gas and thermodynamics

`GasDynamicsSolver` (3.9k) simulates gas mixing and pressure in enclosed volumes;
`BaseAtmosphereLogisticsRuntime` distributes breathable atmosphere through habitats;
`ToxicOutgassingChemistryRuntime` models contamination. Surface weather is driven by
`HectonSurfaceWeatherDirector` and `ShinobuOceanSurfaceAtmosphereRuntime`, with storm propagation
and `HectonSeismicTideDirector` for tides and seismic events.

Thermodynamics is a separate solver stack: `AbyssalThermodynamicsSolver` with a reactor bridge,
`ReactorThermalGridJobs`, `ThermodynamicsHazardGridRuntime`, and
`SubmarineOsThermalGridRuntime` for on-board heat.

### Construction, habitat and drones

`HabitatGraphManager` (7.1k) maintains the habitat as a graph of modules, with
`HullIntegrityRuntime`, `StructuralIntegrityCalculatorRuntime` and `HabitatDamageBakePipeline`
resolving structural load and damage. `BulkheadContainmentRuntime` handles flooding containment.

`DroneFleetManager` (10k lines) plus `DroneFleetNavigationKernel` run an autonomous drone fleet for
construction and logistics tasks.

### Power and logistics

`LogisticsNetworkGraph` (5.1k) and `ShinobuLogisticsRouter` route resources through a graph
topology; `BatteryChargerLogisticsRuntime` and `PowerGridSolarContracts` handle generation and
storage. Inventory is SoA-backed: `SoaInventoryQueryEngine` with a cargo-sync partial,
`InventoryRoutingNetwork`, and `Shinobu19EconomyLedger` for economy accounting.

### Rendering and graphics

Custom URP render features and GPU-driven paths:

- `GpuScatterLodManager` (2.8k) — GPU-driven scatter LOD
- `HectonIndirectVegetationRenderer` — `BatchRendererGroup`-style indirect vegetation draw with
  per-instance structured buffers
- `AbyssalDeferredCausticsRuntime` — deferred caustics
- `HectonBilateralDrsUpscalerRuntime` + `ThermalDynamicResolutionAdapter` (3k) — dynamic resolution
  driven by *thermal* headroom, not just frame time
- `HectonVolumetricParticulateFogFeature`, `HectonVisorUberPostFeature` — volumetrics and post
- `InteriorGIProbeVolumeRuntime` (3.5k), `HectonGIRelaySystem`, `DynamicPointLightCullingDirector`
- `VisualPressureAgingRuntime` — surfaces visibly age under pressure exposure
- `TBDRPipelineSurgeonTypes` — tile-based deferred pipeline tuning
- `HectonMarineSnowRenderer` (6.1k) and `CarveDebrisComputeRenderer` — particulate and debris
- `GlobalShaderDispatcher` — centralised global shader property publication

### Visor and HUD

The visor is treated as diegetic hardware, not an overlay. `SpectrumSystem` (4.4k) drives
spectral/sonar vision modes; `VisorHUDController` and `SuitHUDV4CanvasOverlay` (8.1k) render the suit
HUD; `DynamicDecalVaultRuntime` (4.1k) manages decals on the lens itself;
`DiegeticVisorLensRuntime` and `DiegeticGlitchSurgeonRuntime` handle lens physicality and glitching,
driven by `visor_properties.csv` and `glitch_profiles.csv`. `TerminalOsRuntime` (5.1k) and
`VehicleSubOsCockpitRuntime` implement in-world computer interfaces;
`PDAEncyclopediaStreamer` (4.4k) streams codex content.

### Audio

`PlayerCriticalProceduralAudioRenderer` is the single largest file in the project at **14,337
lines** — procedural audio synthesis for player-critical cues rather than sample playback.
`DynamicMusicGranularSynthesizer` provides granular synthesis, `HectonMusicDirector` (4.7k)
orchestrates score state, `VocalWarningSystem` (3.4k) handles spoken warnings, and
`AcousticEcholocationRaymarch` raymarches echolocation. Loudness is normalised through a
LUFS calculator in `PureLogic`.

### Save system

Saving is engineered for a mutable persistent world, not a snapshot:

- `EntityDeltaCompressionArchitecture` (4k) and `VoxelDeltaCompressionArchitecture` (2.4k) — delta
  compression for entities and carved voxels
- `H8BinaryWorldPager` (3.7k) — pages world state in and out of binary storage
- `SaveStateMerkleTree` (3k) — Merkle hashing over save state for integrity and cheap diffing
- `ISaveable` participants register with explicit `SavePriority` / `LoadPriority` ordering

### Narrative and quests

`QuestStateManager` and `QuestDagResolverRuntime` resolve quests as a DAG rather than a linear chain.
`HectonNarrativeDirector` drives POI triggers, `AwaitableDropSequenceDirector` sequences set-piece
drops, `LoreDatabaseManager` and `MetaCampaignService` carry lore and meta-campaign state.
Localization is hash-based through generated `H8LocHashes` (12.9k lines) and `LocHash.Compute`.

### Modding API

A sandboxed mod runtime rather than raw script loading: `ModLoader`, `ModCommandDispatcher`,
`ModRuntimeState`, `ModEcosystemRegistry` for ecosystem overlays (e.g.
`FaunaBiomeMutationDefinition` biases fauna genetics per biome), and
`FutureCommandSandboxValidator` (5k lines) validating commands before execution. An SDK lives in
`ModdingSDK/`.

### Optimization and asset lifecycle

`AssetLifecycleGovernor` (5.3k) owns load/unload lifetime; `AssetLoadDispatcher` sequences loads;
`VRAMPressureMonitor` and `VRAMMonitor` track GPU memory pressure and feed quality scaling.
`GlobalPhysicsStateManager` includes a physics-culling partial. Foveated LOD exists for both
rendering and AI (`FaunaBrain.Foveated`, `AI/Foveated`).

---

## Repository layout

```
Assets/_Project/Scripts/
  Core/           registry, dispatcher, vault, memory, contracts, signals
  Bootstrap/      boot sequencing, route enforcement, readiness gates
  World/          geology, voxel, flora, persistence, spatial hash, scatter
  Ecosystem/      populations, genetics, nutrient drift, migration
  AI/             cognition, perception, pathfinding, ecology, foveation
  Fauna/          FaunaBrain and combat/LOD partials
  Gameplay/       player kinematics, submarine, hazards, XR somatics
  Physics/        hydrodynamic KCC, buoyancy readback, cavitation, autopilot
  Physiology/     metabolism, suit integrity, decompression, respawn
  Atmosphere/     gas dynamics, weather, outgassing, storms
  Thermodynamics/ abyssal + reactor thermal solvers
  Construction/   habitat graph, bulkheads, docking, drone fleet
  Power/          logistics graph, routing, batteries, thermal grid
  Inventory/      SoA inventory, routing, economy ledger
  Rendering/      URP features, GPU scatter, caustics, DRS
  Graphics/       material response, pressure aging, TBDR tuning
  Lighting/       GI probe volumes, light culling, day/night relay
  VFX/            marine snow, bioluminescence, debris, camera juice
  Visor/          spectrum modes, HUD, lens decals, diegetic glitch
  UI/             suit HUD, terminal OS, PDA, menus, localization
  Audio/          procedural audio, music director, echolocation
  SaveSystem/     delta compression, world pager, Merkle integrity
  Quest/ Narrative/ Meta/   quest DAG, POI triggers, lore, campaign
  ModdingAPI/     sandboxed mod loader, dispatcher, validator
  Optimization/   asset lifecycle, VRAM pressure
  PureLogic/      dependency-free logic + unit tests
  Editor/         561 files of tooling, diagnostics and probes

Assets/_Project/Art/      shaders, materials, generated art
Assets/_Project/Scenes/   00_BOOTSTRAP is the entry scene
Docs/                     architecture, reports, agent tasks, archive
Tools/BatchTasks/         Unity batchmode task runners
Tools/UnityLaunchers/     editor launch and capture scripts
Data/                     CSV tuning profiles loaded at runtime
ModdingSDK/               mod development kit
```

The lowercase `*.md` files at repo root are **route bibles** — per-domain authoring standards
(`world.md`, `ecosystem.md`, `rendering.md`, `physics.md`, `audio.md`, …). They are addressed by name
from the routing files and are intentionally kept at root rather than tidied into a folder.

---

## Getting started

**Requires Unity `6000.5.0f1`** with URP.

```bash
git clone https://github.com/marko1olo/Hecton8.git
```

Open the folder in Unity Hub and load `Assets/_Project/Scenes/00_BOOTSTRAP.unity`.
First import is long — the project carries substantial procedural, voxel and shader content.

---

## Verification

Two headless probes are checked in. Both exit non-zero on failure, so they work as CI gates.

<details open>
<summary><b>Bootstrap / play-mode probe</b></summary>

<br>

```bash
Unity.exe -batchmode -nographics -projectPath <repo> \
  -logFile Logs/probe.log \
  -executeMethod Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run \
  -h8Scene Assets/_Project/Scenes/00_BOOTSTRAP.unity \
  -h8WarmupFrames 60 -h8MenuSeconds 25 -h8SettleSeconds 25 \
  -h8GameplaySeconds 45 -h8StartGame 1 -h8TimeoutSeconds 400
```

Reports bootstrap readiness, active scene, resolved runtime world seed and registry wiring.

</details>

<details open>
<summary><b>Geology biome lane probe</b></summary>

<br>

Guards a **silent** failure mode. If the geology field never exceeds the lane thresholds, every
sector collapses into one lane, fauna density becomes uniform, and *nothing errors* — the ecosystem
would look exactly as it did when biome was a coordinate hash.

```bash
Unity.exe -batchmode -nographics -projectPath <repo> \
  -logFile Logs/lanes.log \
  -executeMethod Hecton8.EditorTools.Diagnostics.H8_GeologyBiomeLaneProbe.Run \
  -h8SectorRadius 16
```

No play mode required. Measured baseline over 33×33 sectors (1,089 samples, default authoring seed):

```
samples=1089  neutral=437  rich=545  scarce=107  nonFinite=0
maxTrenchMask=1.0000 (threshold 0.8)   maxShelfMask=1.0000 (threshold 0.5)
DISCRIMINATING
```

Both masks reach 1.0, so neither non-neutral lane is unreachable. The resulting shape — ~50 % shelf,
~10 % trench — is the physically expected one: shelves common, trenches rare.

</details>

Beyond these, `Assets/_Project/Scripts/Editor/` carries 561 files of diagnostics: compute-shader
reachability audits, unassigned serialized-reference audits, vegetation pass parity probes, shader
compile gates, terrain/geology atlas dumps and AUP precision scans.

---

## Contributing

Read **[CONTRIBUTING.md](CONTRIBUTING.md)** first. It documents the authority chain, the lock-free
compile gate, multi-agent rules, and several traps in this repository that cost an hour each if you
meet them cold — including a namespace shadowing hazard that has broken the build, and a compile
tool that reports three errors which do not exist.

The two rules that matter most:

1. **Read the authority files before non-trivial work** — `AGENTS.md`, `COMMON_SENSE.md`,
   `PROJECT_BIBLES.md`, and the route bible for your domain.
2. **Commit only your own files.** The tree routinely contains other contributors' in-flight work.
   Never revert or sweep it up silently.

---

<div align="center">

**Danat Games**

</div>
