<div align="center">

# HECTON-8

**A deep-water survival simulator built on flooded geography, failed industry, and pressure architecture.**

*“The world is not random underwater decoration.”*

<br>

![Unity](https://img.shields.io/badge/Unity-6000.5.0f1-000000?style=for-the-badge&logo=unity&logoColor=white)
![URP](https://img.shields.io/badge/Render_Pipeline-URP-1a7f5a?style=for-the-badge)
![C#](https://img.shields.io/badge/C%23-2805_files-239120?style=for-the-badge&logo=csharp&logoColor=white)
![Shaders](https://img.shields.io/badge/Shaders-474-8a2be2?style=for-the-badge)
![Burst](https://img.shields.io/badge/Jobs_+_Burst-DOD-ff6f00?style=for-the-badge)
![Status](https://img.shields.io/badge/Status-In_Development-yellow?style=for-the-badge)

</div>

---

## Table of contents

- [What this is](#what-this-is)
- [Design law](#design-law)
- [Engineering invariants](#engineering-invariants)
  - [Absolute Universe Position](#1-absolute-universe-position-aup)
  - [Data-oriented hot paths](#2-data-oriented-hot-paths)
  - [Determinism where it is load-bearing](#3-determinism-where-it-is-load-bearing)
  - [Evidence classes](#4-evidence-classes)
- [System map](#system-map)
  - [World generation](#world-generation)
  - [Ecosystem](#ecosystem)
  - [Reactive flora](#reactive-flora)
- [Repository layout](#repository-layout)
- [Getting started](#getting-started)
- [Verification](#verification)
- [Contributing](#contributing)

---

## What this is

HECTON-8 is a systems-first underwater survival simulator. Most of this repository is simulation
rather than set dressing: macro geology shapes the seafloor, the seafloor drives ecology, ecology
drives fauna behaviour, and the player's submarine perturbs all three.

> [!IMPORTANT]
> **Development status.** This is an in-development project and system maturity varies widely.
> The project's own standard is that nothing is called *verified* without runtime proof, so this
> document distinguishes what is measured from what is merely implemented. Treat the tables below
> as a map, not a release claim.

---

## Design law

The prime world law, quoted from the project's own world bible:

> The world is not random underwater decoration. It is flooded geography, failed industry, salvage
> history, and pressure architecture.

This is not a mood statement; it is an architectural constraint with a testable consequence. If a
biome's fauna can be predicted without looking at the terrain, the law is being violated. That
specific failure is guarded by an automated probe — see [Verification](#verification).

---

## Engineering invariants

These four rules are the ones the codebase is actually held to. Violating any of them is treated as
a defect rather than a tuning problem.

### 1. Absolute Universe Position (AUP)

World coordinates are 64-bit `double3` (`AbsoluteUniversePosition`) with a **floating origin**.
Systems that care implement `IOriginShiftListener` and rebase when the origin moves.

The important consequence is that `float32` on the GPU is *correct by design*, not a compromise:
simulation happens in absolute doubles, then coordinates are rebased to a near-origin local space
before ever reaching a shader. Two examples in-tree:

| System | Absolute side | Rendered side |
|:--|:--|:--|
| `HectonSpatialHash` | `double3` centres, **`Long3` integer cell coords** | n/a — CPU only |
| `SargassumMicroFaunaBoids` | grid origin quantised in `double` via `FloorToMultiple64` | narrowed to `float4` after origin shift |

> [!WARNING]
> Do not "upgrade" a GPU path to 64-bit because it looks lossy. If the system implements
> `IOriginShiftListener`, the float32 values it uploads are already small and the narrowing is
> intentional. Widening it costs performance and fixes nothing.

### 2. Data-oriented hot paths

Hot work runs as Burst-compiled Jobs over `NativeArray`/`NativeQueue`. Steady-state heap allocation
in a hot path is a defect. Where a managed array is unavoidable it is allocated once and annotated
at the declaration:

```csharp
// COLD ALLOC: GeologyBiomeCacheEntry[256] - direct-mapped sector -> biome classification cache so
// the macro geology stack runs once per 1 km sector instead of once per 50 m biomass macro cell
// (400 macro cells per sector) - owner: EcosystemDirector
private GeologyBiomeCacheEntry[] _geologyBiomeCache;
```

### 3. Determinism where it is load-bearing

`FloatMode.Deterministic` is used wherever a result feeds save identity, replay, or cross-machine
agreement; `FloatMode.Fast` is acceptable elsewhere. The distinction is deliberate — determinism
forfeits reassociation and FMA contraction, so it is applied on evidence rather than by reflex.

Numerical guards that appear throughout, and are expected in new code:

```csharp
// Every normalisation is guarded on both finiteness and magnitude.
float lengthSq = math.lengthsq(value);
bool valid = math.isfinite(lengthSq) & lengthSq > 0.000001f;
```

### 4. Evidence classes

Claims are separated by how they were established. This vocabulary appears in commit messages and
in docs, and it is enforced socially:

| Class | Means |
|:--|:--|
| `STATIC_DOC` | Written down. No execution. |
| **Static review** | Read by a human or agent. Not compiled. |
| **Compile-verified** | Builds clean. Says nothing about behaviour. |
| **Runtime proof** | Observed executing, with the log or measurement retained. |

“It compiles” is never reported as “it works”.

---

## System map

| Layer | What it does | Key types |
|:--|:--|:--|
| 🌍 **Macro geology** | Procedural seafloor structure, and the source of truth for where things live | `WorldMacroGeologyFields` |
| 🕳️ **Voxel terrain** | Surface-nets volumes for true 3D caves and overhangs, async collider baking | `HectonVoxelEngine` |
| 🌊 **Ocean & fluids** | Crest ocean surface, underwater volumetrics, flow fields, wake and propwash | `FloraInteractionManager`, wake sources |
| 🌿 **Reactive flora** | GPU-instanced vegetation reacting to wake, current and impact | `FloraInteractionManager` |
| 🐟 **Ecosystem** | Sector populations, migration, carrion nutrient drift, fauna genetics | `EcosystemDirector`, `NutrientDriftRuntime` |
| 🧠 **Fauna AI** | Boids, spatial-hash sensing, predator/prey, symbiosis | `FaunaBrain`, `HectonSpatialHash` |
| 🛠️ **Survival loop** | Oxygen, pressure, salvage, repair chains, construction | — |

### World generation

`WorldMacroGeologyFields.Evaluate(x, z, in parameters)` returns a `WorldMacroGeologySample`
carrying masks (`ShelfMask`, `TrenchMask`, `ReefEligibilityMask`, `SeepMask`, slope, curvature, …)
and a resolved `WorldMacroGeologyZone`:

```
PhoticShelf · ShelfBreak · FaultRidge · BrineTrench
AbyssalPlain · SedimentFan · ColdSeepField · HadalBasin
```

Geology is consumed at runtime by terrain, vegetation scatter, splatmaps, wreck placement — and by
the ecosystem.

### Ecosystem

The world is partitioned into **1 km sectors**, with biomass tracked on a **50 m macro-cell** grid.
Sector populations evolve under a Lotka-Volterra model modulated by food density and carrying
capacity.

Sector biome classification is derived from geology, mapping onto three lanes:

| Lane | Condition | Meaning |
|:--:|:--|:--|
| `2` scarce | `TrenchMask > 0.8` | Abyssal trench. Thin food column, pressure-adapted hunters. `-0.05` carrying capacity. |
| `1` rich | `ShelfMask > 0.5` | Photic shelf. Dense kelp, schooling prey. `+0.08` carrying capacity. |
| `0` neutral | otherwise | No bias. |

Trench wins where both masks overlap — a trench cutting through a shelf is still a trench.

**Whalefall.** Death is conserved rather than deleted. `FaunaBrain.Die()` publishes an
`EntityDeathSignal` whose intensity scales with the creature's max health, so a leviathan produces
a proportionally larger corpse. `NutrientDriftRuntime` consumes that signal into a carrion pool
with decay and nutrient injection, then republishes each active corpse into `WorldSpatialHashGrid`
as a transient resource event — which is the same channel scavenger AI already queries for food.

```
FaunaBrain.Die()
  └─> SignalBus<EntityDeathSignal>          (also fed by population-balancer culls)
        └─> NutrientDriftRuntime_Carrion    decay · nutrient injection
              └─> WorldSpatialHashGrid      Resource | ChemicalReceiver | Interactable
                    └─> scavenger AI
```

**Genetics.** `FaunaGeneticsManager` derives per-instance traits deterministically from AUP spawn
position, species, biome and the persisted world seed. Traits then mutate in place under real
environmental fields — radiation grid, AUP-native toxicity hazard zones, and brine layer depth —
via `FaunaGenome64.MutateGenome`.

### Reactive flora

Vegetation reacts to wake sources (submarine, apex predators) through
`AccumulateFloraForcesJob`, which resolves source-to-sample deltas in `double3` AUP space before
narrowing, applies an ellipsoidal vertical squash, blends wake and radial directions, and caps
displacement.

A separate **cascade** channel propagates a bioluminescent pulse outward through kelp when a
disturbance event fires. Both the cascade clock and the per-instance activation seed are published
**epoch-relative**, because the shader carries them as `half`: absolute simulation seconds quantise
to 1.0 s spacing near t=2048 and overflow at 65504, which would erase a sub-second pulse envelope.
Subtracting a shared epoch on both sides leaves `age = cascadeTime - cascadeSeed` algebraically
identical while keeping both operands resolvable.

---

## Repository layout

```
Assets/_Project/Scripts/    Runtime systems — Ecosystem, AI, World, VFX, Core, Fauna, ...
Assets/_Project/Art/        Shaders, materials, generated art
Assets/_Project/Scenes/     00_BOOTSTRAP is the entry scene
Docs/                       Architecture, reports, agent tasks, archive
Tools/BatchTasks/           Unity batchmode task runners
Tools/UnityLaunchers/       Editor launch and screen-capture scripts
Data/                       CSV tuning profiles loaded at runtime
Packages/ ProjectSettings/  Unity project configuration
```

The many lowercase `*.md` files at the repository root are **route bibles** — per-domain authoring
standards (`world.md`, `ecosystem.md`, `rendering.md`, `physics.md`, …). They are addressed by name
from the project's routing files and are intentionally kept at root rather than tidied into a
folder.

---

## Getting started

**Requires Unity `6000.5.0f1`** with URP.

```bash
git clone https://github.com/marko1olo/Hecton8.git
```

Open the folder in Unity Hub and load `Assets/_Project/Scenes/00_BOOTSTRAP.unity`.
The first import is long — the project carries substantial procedural, voxel and shader content.

---

## Verification

Two headless probes are checked in. Both exit non-zero on failure, so they work as CI gates.

<details open>
<summary><b>Bootstrap / play-mode probe</b></summary>

<br>

Boots the game headless, enters play mode, and reports service readiness.

```bash
Unity.exe -batchmode -nographics -projectPath <repo> \
  -logFile Logs/probe.log \
  -executeMethod Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run \
  -h8Scene Assets/_Project/Scenes/00_BOOTSTRAP.unity \
  -h8WarmupFrames 60 -h8MenuSeconds 25 -h8SettleSeconds 25 \
  -h8GameplaySeconds 45 -h8StartGame 1 -h8TimeoutSeconds 400
```

Reports bootstrap readiness, active scene, the resolved runtime world seed, and registry wiring.

</details>

<details open>
<summary><b>Geology biome lane probe</b></summary>

<br>

Guards a **silent** failure mode. If the geology field never exceeds the lane thresholds, every
sector collapses into one lane, fauna density becomes uniform, and *nothing errors* — the ecosystem
would look exactly as it did when biome was a coordinate hash. This probe makes that observable.

```bash
Unity.exe -batchmode -nographics -projectPath <repo> \
  -logFile Logs/lanes.log \
  -executeMethod Hecton8.EditorTools.Diagnostics.H8_GeologyBiomeLaneProbe.Run \
  -h8SectorRadius 16
```

No play mode required, so it is cheap to re-run after any geology or threshold change.
Measured baseline over 33×33 sectors (1089 samples, default authoring seed):

```
samples=1089  neutral=437  rich=545  scarce=107  nonFinite=0
maxTrenchMask=1.0000 (threshold 0.8)   maxShelfMask=1.0000 (threshold 0.5)
DISCRIMINATING
```

Both masks reach 1.0, so neither non-neutral lane is unreachable. The resulting shape — ~50 % shelf,
~10 % trench — is the physically expected one: shelves common, trenches rare.

</details>

---

## Contributing

Read **[CONTRIBUTING.md](CONTRIBUTING.md)** before non-trivial work. It documents the authority
chain, the lock-free compile gate, the multi-agent rules, and several traps in this repository that
will cost you an hour each if you meet them cold.

The two rules that matter most:

1. **Read the authority files first** — `AGENTS.md`, `COMMON_SENSE.md`, `PROJECT_BIBLES.md`, and the
   route bible for your domain.
2. **Commit only your own files.** The tree routinely contains other contributors' in-flight work.
   Never revert or sweep it up silently.

---

<div align="center">

**Danat Games**

</div>
