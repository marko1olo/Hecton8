# SHINOBU_258 Data Monolith Source Coverage

Date: 2026-05-21
Evidence: static filesystem/source inspection only. Unity bake, import, Play Mode, profiler, and player proof remain pending.

## Active Source Roots

- `Data/Balance`
- `Assets/_SourceData/DataMonolith`

`Data/Balance/Baked` and `Data/Balance/Schemas` are excluded by `H8DataMonolithCompiler.IsGeneratedBalancePath`.

## Current Authored Runtime Rows

| Table | File | Authored rows |
| --- | --- | ---: |
| Items | `Data/Balance/Items.csv` | 4 |
| Creatures/Fauna | `Data/Balance/Fauna.csv` | 3 |
| Biomes | `Data/Balance/Biomes.csv` | 2 |
| Recipes | `Data/Balance/Recipes.csv` | 3 |
| LootCdf source rows | `Data/Balance/Loot.csv` | 4 |
| VoxelMaterials | `Data/Balance/VoxelMaterials.csv` | 2 |
| AudioClipRegistry | `Data/Balance/AudioRegistry.csv` | 3 |
| VfxScalars | `Data/Balance/VfxScalars.csv` | 2 |
| ToolHeatCapacity | `Data/Balance/ToolHeat.csv` | 2 |
| SubmarineHullConstants | `Data/Balance/SubmarineHull.csv` | 2 |
| PhysicsMaterials | `Data/Balance/PhysicsMaterials.csv` | 3 |
| GhostModules | `Data/Balance/GhostModules.csv` | 2 |
| SpawnCreditCosts | `Data/Balance/SpawnCredits.csv` | 3 |
| SopErrors | `Data/Balance/SopErrors.csv` | 2 |
| HudLayouts | `Data/Balance/HudLayout.csv` | 2 |
| SectorPageDirectory | `Data/Balance/SectorPages.csv` | 2 |
| Economy | `Data/Balance/Economy.csv` | 3 |
| PhysicsConstants | `Data/Balance/Physics.csv` | 3 |

`Assets/_SourceData/DataMonolith` currently has no authored CSV/JSON source files.

## Generated During Compiler Finalization

- `DepthPressureCurve`: 256 generated samples when no source curve exists.
- `LightAttenuationCurve`: 256 generated samples when no source curve exists.
- `BiomeHeatmap`: normalized to 65,536 cells; without authored biomes the fallback biome hash is `0`.

## Static Coverage State

Current static source inspection now finds authored rows for every non-generated production section required by `H8DataMonolithCompiler.ValidateProductionSectionCoverage`.

This is not payload readiness. Remaining blockers:

- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
- Unity batchmode/editor bake has not been run in this pass.
- The generated output blob has not been validated by Unity import, player boot, profiler, GCMonitor, Memory Profiler, or player build.
- `SectorPages.csv` currently provides directory coverage rows with `byte_count=0`; real world-page byte payload routing remains pending.

## Semantic Bake Gate

`H8DataMonolithCompiler.ValidateCrossReferences` now rejects semantic garbage rows after table finalization:

- CSV data rows must have the exact same column count as their header; silent truncation is rejected.
- Required record IDs and required references must not resolve to hash `0`.
- Duplicate production identity hashes are rejected for VoxelMaterials, AudioClipRegistry, VfxScalars, ToolHeatCapacity, SubmarineHullConstants, PhysicsMaterials, GhostModules, SpawnCreditCosts, SopErrors, HudLayouts, and SectorPageDirectory.
- Production numeric fields must be finite; positive-only quantities such as item stack/mass/volume/quality, recipe craft seconds, heat capacity, crush depth, and spawn cost must be greater than `0`.
- `AudioClipRegistry.BankHash` must be nonzero.
- Depth ranges must have `max >= min`.
- Sector AUP coordinates must remain inside `[-100000, 100000]`.
- `Biomes.SurfaceId` must resolve to `VoxelMaterials.VoxelHash`.
- `VoxelMaterials.YieldHash` must resolve to `Items.HashId`.
- `VoxelMaterials.SurfaceId` must resolve to `PhysicsMaterials.SurfaceHash`.
- `GhostModules.RecipeHash` must resolve to `Recipes.OutputHash`.
- `SpawnCreditCosts.EntityHash` must resolve to `Creatures.SpeciesHash`.
- `SectorPageDirectory.BiomeHash` and normalized `BiomeHeatmap.BiomeHash` must resolve to `Biomes.BiomeHash`.

`HudLayout.csv` is intentionally limited to the 64-byte `H8HudLayoutRecord` fields (`m00..m31`). It does not author `m32` or `m33`; adding those cells without a schema/DTO migration is a bake error.

This gate is source-code static reviewed here. Runtime/editor execution proof remains pending until a Unity bake is allowed.

## Schema Templates

Materialized excluded authoring templates now exist under `Data/Balance/Schemas`. These files are headers only and are not runtime payload proof.

Do not copy templates into active source roots as empty files. Add reviewed production rows, then bake and validate `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
