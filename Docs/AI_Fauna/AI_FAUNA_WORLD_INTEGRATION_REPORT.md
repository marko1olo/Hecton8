# AI Fauna World Integration Report

Date: 2026-05-07
Status: PENDING VERIFICATION
Verification: PENDING VERIFICATION

## 2026-05-11 Current-State Override

- Current data boundary: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Current manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current visual-realistic-fake doctrine: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R41 current external root `Hecton8*.csproj` no-restore CLI compile surface is `0 Warning(s)` / `0 Error(s)` after restore assets exist; full restore graphs still carry vendor/package warnings. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- May 13 DOC_AUDIT R15 fauna override: this report's biome coverage is still useful orientation, but it is not evidence that active scenes run visible fauna. Current static inventory found `22` recursive creature archetype assets, `22` fauna data templates, `108` fauna biome datasets, `13` fauna family profiles, `432` `possibleCreatures` entries with non-null prefab references, `17` large-threat macro-zone archetype refs, and `6` generated proxy prefabs.
- R15 runtime boundary: `EcosystemRuntimeInstaller` creates genetics/health/migration ecosystem managers only. It does not instantiate `FaunaDirector` or `WorldFaunaSpawnRegistry`. `GameBootstrapper.EnsureFaunaSimulationRegistered()` uses active `FaunaDirector` if present, otherwise registers `DemiurgeFaunaSimulationService.Shared`, a ready headless sentinel with `ResidentSlotCapacity = 0`.
- R15 scene/proof boundary: static script-GUID search did not find serialized `FaunaDirector`, `WorldFaunaSpawnRegistry`, `FaunaRuntimeSmokeTester`, or `EcosystemRuntimeInstaller` in current `Assets` scenes/prefabs/assets. `WorldRuntimeBootstrapAuthoring` can add/configure `WorldFaunaSpawnRegistry`, but `ConfigureFaunaDirector()` returns when no `FaunaDirector` already exists; this is editor authoring support, not production-scene runtime proof.
- R15 smoke boundary: `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` exits with return code `1` and contains `.codex-artifacts is not a valid directory name`; do not cite it as PASS.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## 2026-05-04 Current-State Boundary

- This report is biome/fauna coverage reference, not runtime spawn, prefab, profiler, or scene wiring proof.
- Counts below are orientation data. Re-open current source/assets before changing spawn tables, fauna registries, or biome placement.
- Current project truth starts at `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, then `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, then `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`.

## What Exists

- Recursive creature archetype assets: `22`
- Fauna data templates: `22`
- Fauna datasets by biome: `108`
- `possibleCreatures` entries with non-null prefab refs: `432`
- `possibleCreatures` entries with null prefab refs: `0`
- Large-threat macro-zone archetype refs: `17`
- Generated fauna proxy prefabs: `6`

## Biomes Without Passive Life

- None.

## Biomes Without Threats

- None.

## Large Water Areas With Major Threats

- The Granite Spine - Furnace Maw Leviathan / sentinel pressure / zone 998m
- Sea-Stack Forest - Halo Crown Leviathan / presence circle / zone 998m
- The Ash-Wastes - Black Choir Leviathan / presence circle / zone 806m
- The Rift-Gates - Rift Lancer Leviathan / ambush burst / zone 806m
- Pressure-Slabs - Armor Breaker / sentinel pressure / zone 691m
- Iron Shards - Armor Breaker / sentinel pressure / zone 691m
- Magma Pools - Furnace Maw Leviathan / sentinel pressure / zone 806m
- The Shattered Spine - Rift Lancer Leviathan / ambush burst / zone 806m
- The Glass Plains - Halo Crown Leviathan / presence circle / zone 806m
- The Shivering Slabs - Gate Warden Leviathan / sentinel pressure / zone 806m
- The Pillow-Lava Hives - Furnace Maw Leviathan / sentinel pressure / zone 806m
- The Rift-Maw - Rift Lancer Leviathan / ambush burst / zone 922m
- The Basalt Flux - Black Choir Leviathan / presence circle / zone 806m
- The Iron Peak - Armor Breaker / sentinel pressure / zone 691m
- The Lava Seam - Furnace Maw Leviathan / sentinel pressure / zone 806m
- The Heart of the Rift - Black Choir Leviathan / presence circle / zone 922m
- The Static Matrix - Void Ribbon Leviathan / ambush burst / zone 922m

## Biomes With Leviathans

- The Granite Spine (1) - Furnace Maw Leviathan / sentinel pressure
- Sea-Stack Forest (1) - Halo Crown Leviathan / presence circle
- The Ash-Wastes (1) - Black Choir Leviathan / presence circle
- The Rift-Gates (1) - Rift Lancer Leviathan / ambush burst
- Magma Pools (1) - Furnace Maw Leviathan / sentinel pressure
- The Shattered Spine (1) - Rift Lancer Leviathan / ambush burst
- The Glass Plains (1) - Halo Crown Leviathan / presence circle
- The Shivering Slabs (1) - Gate Warden Leviathan / sentinel pressure
- The Pillow-Lava Hives (1) - Furnace Maw Leviathan / sentinel pressure
- The Rift-Maw (1) - Rift Lancer Leviathan / ambush burst
- The Basalt Flux (1) - Black Choir Leviathan / presence circle
- The Lava Seam (1) - Furnace Maw Leviathan / sentinel pressure
- The Heart of the Rift (1) - Black Choir Leviathan / presence circle
- The Static Matrix (1) - Void Ribbon Leviathan / ambush burst

## Biomes Using Heavy Hunters Instead Of Leviathans

- Pressure-Slabs
- Iron Shards
- The Iron Peak

## Reserve Biomes With Leviathans

- None.

## Shallow And Mid-Depth Biomes With Leviathans

- The Granite Spine
- Sea-Stack Forest

## Reef And Littoral Flora Biomes

- Archipelago Needles - family `biome.family.littoral_karst` / fauna `Littoral Passive` (`fauna.family.littoral_passive`) / passive `3` / threat `2` / hunter `1` / leviathan `0` / entries `Shore Skimmer [Ambient] | Kelp Raylet [Ambient] | Brine Siphoner [Ambient] | Nursery Shellguard [Territorial] | Needle Hunter [Hunter]`
- Mesa Plateaus - family `biome.family.littoral_karst` / fauna `Littoral Passive` (`fauna.family.littoral_passive`) / passive `3` / threat `1` / hunter `0` / leviathan `0` / entries `Shore Skimmer [Ambient] | Kelp Raylet [Ambient] | Brine Siphoner [Ambient] | Nursery Shellguard [Territorial]`
- Sea-Stack Forest - family `biome.family.fossil_reef` / fauna `Reef Ambush` (`fauna.family.reef_ambush`) / passive `3` / threat `3` / hunter `1` / leviathan `1` / entries `Shore Skimmer [Ambient] | Kelp Raylet [Ambient] | Brine Siphoner [Ambient] | Nursery Shellguard [Territorial] | Pocket Ambusher [Hunter] | Halo Crown Leviathan [Leviathan]`
- White Alabaster Pools - family `biome.family.crystal_growth` / fauna `Crystal Skittish` (`fauna.family.crystal_skittish`) / passive `3` / threat `1` / hunter `0` / leviathan `0` / entries `Kelp Raylet [Ambient] | Brine Siphoner [Ambient] | Lantern Sifter [Ambient] | Archway Sentinel [Territorial]`
- Coral-Porous Walls - family `biome.family.fossil_reef` / fauna `Reef Ambush` (`fauna.family.reef_ambush`) / passive `3` / threat `1` / hunter `0` / leviathan `0` / entries `Shore Skimmer [Ambient] | Kelp Raylet [Ambient] | Brine Siphoner [Ambient] | Nursery Shellguard [Territorial]`
- Crystalline Ridges - family `biome.family.crystal_growth` / fauna `Crystal Skittish` (`fauna.family.crystal_skittish`) / passive `3` / threat `1` / hunter `0` / leviathan `0` / entries `Kelp Raylet [Ambient] | Brine Siphoner [Ambient] | Lantern Sifter [Ambient] | Archway Sentinel [Territorial]`
- Fossil Gallows - family `biome.family.fossil_reef` / fauna `Reef Ambush` (`fauna.family.reef_ambush`) / passive `3` / threat `2` / hunter `1` / leviathan `0` / entries `Shore Skimmer [Ambient] | Kelp Raylet [Ambient] | Brine Siphoner [Ambient] | Nursery Shellguard [Territorial] | Pocket Ambusher [Hunter]`

## Reef And Littoral Flora Warnings

- None.

## Skew Warnings

- None.

