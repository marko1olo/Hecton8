<div align="center">

# HECTON-8

**A deep-water survival simulator built on flooded geography, failed industry, and pressure architecture.**

*The world is not random underwater decoration.*

<br>

![Unity](https://img.shields.io/badge/Unity-6000.5.0f1-000000?style=for-the-badge&logo=unity&logoColor=white)
![URP](https://img.shields.io/badge/Render_Pipeline-URP-1a7f5a?style=for-the-badge)
![C#](https://img.shields.io/badge/C%23-2805_files-239120?style=for-the-badge&logo=csharp&logoColor=white)
![Shaders](https://img.shields.io/badge/Shaders-474-8a2be2?style=for-the-badge)
![Burst](https://img.shields.io/badge/Jobs_+_Burst-DOD-ff6f00?style=for-the-badge)
![Status](https://img.shields.io/badge/Status-In_Development-yellow?style=for-the-badge)

</div>

---

## What this is

HECTON-8 is a systems-first underwater survival simulator. The design law it is held to is that
the ocean must behave like a place with a history — flooded geography, salvage, pressure — rather
than a backdrop with pickups scattered on it.

The engineering consequence of that law is that most of this repository is simulation, not set
dressing: macro geology drives terrain, terrain drives ecology, ecology drives fauna behaviour, and
the player's submarine perturbs all three.

> **Development status.** This is an in-development project. Individual systems are at very
> different maturity levels, and the project's own standard is that nothing is called verified
> without runtime proof. Treat the table below as a map, not a release claim.

---

## Architecture at a glance

| Layer | What it does |
|:--|:--|
| 🌍 **Macro Geology** | Procedural seafloor: shelf, shelf-break, fault ridge, brine trench, abyssal plain, sediment fan, cold-seep field, hadal basin. Drives terrain height *and* downstream ecology. |
| 🕳️ **Voxel Terrain** | Surface-nets voxel volumes for true 3D caves and overhangs, with async collider baking. |
| 🌊 **Ocean & Fluids** | Crest-based ocean surface, underwater volumetrics, flow fields, wake and propwash physics. |
| 🌿 **Reactive Flora** | GPU-instanced vegetation that bends to submarine wake, currents and impacts, with a bioluminescent cascade that propagates through kelp. |
| 🐟 **Ecosystem** | Lotka-Volterra sector populations, migration, carrion/whalefall nutrient drift, and fauna genetics that mutate under radiation, toxicity and brine. |
| 🧠 **Fauna AI** | Boid flocking, spatial-hash sensing, predator/prey behaviour, symbiosis solving. |
| 🛠️ **Survival Loop** | Oxygen, pressure, salvage, repair chains, construction, hazard response. |

### Engineering constraints the codebase actually holds itself to

- **Absolute Universe Position (AUP)** — world coordinates are 64-bit `double3` with a floating
  origin, so the simulation stays exact at multi-hundred-kilometre range while the GPU still
  receives small float32 values.
- **Data-Oriented Design** — hot paths run as Burst-compiled Jobs over `NativeArray`, with
  `FloatMode.Deterministic` wherever determinism is load-bearing.
- **Zero GC in hot paths** — steady-state allocation is treated as a defect, not a tuning problem.
- **Proof separation** — static review, compile verification and runtime proof are recorded as
  distinct evidence classes. "It compiles" is never reported as "it works".

---

## Repository layout

```
Assets/_Project/Scripts/    Runtime systems (Ecosystem, AI, World, VFX, Core, ...)
Assets/_Project/Art/        Shaders, materials, generated art
Docs/                       Architecture, reports, agent tasks, archive
Tools/                      Batch tasks, Unity launchers, capture utilities
Data/                       Tuning profiles and CSV-driven data
Packages/ ProjectSettings/  Unity project configuration
```

The many `*.md` files at the repository root are **route bibles** — per-domain authoring standards
(`world.md`, `ecosystem.md`, `rendering.md`, …). They are addressed by name from the project's
routing files and are intentionally kept at root.

---

## Getting started

**Requires Unity `6000.5.0f1`** (URP).

```bash
git clone https://github.com/marko1olo/Hecton8.git
```

Open the folder with Unity Hub, then load `Assets/_Project/Scenes/00_BOOTSTRAP.unity`.
First import is long — the project carries substantial procedural and voxel content.

<details>
<summary><b>Headless verification (no editor window)</b></summary>

<br>

Play-mode bootstrap probe:

```bash
Unity.exe -batchmode -nographics -projectPath <repo> \
  -logFile Logs/probe.log \
  -executeMethod Hecton8.EditorTools.Diagnostics.H8_HeadlessPlayModeProbe.Run \
  -h8Scene Assets/_Project/Scenes/00_BOOTSTRAP.unity -h8StartGame 1
```

Ecosystem geology-lane distribution check — verifies that biome classification actually
discriminates instead of silently collapsing to a single lane:

```bash
Unity.exe -batchmode -nographics -projectPath <repo> \
  -logFile Logs/lanes.log \
  -executeMethod Hecton8.EditorTools.Diagnostics.H8_GeologyBiomeLaneProbe.Run \
  -h8SectorRadius 16
```

Both exit non-zero on failure, so they work as gates.

</details>

---

## Contributing

This repository is worked by multiple agents and contributors in parallel. Two rules matter most:

1. **Read the authority files before non-trivial work** — `AGENTS.md`, `COMMON_SENSE.md`,
   `PROJECT_BIBLES.md`, and the route bible for your domain.
2. **Commit only your own files.** The tree frequently contains other contributors' in-flight work;
   never revert or sweep it up without saying so.

---

<div align="center">

**Danat Games**

</div>
