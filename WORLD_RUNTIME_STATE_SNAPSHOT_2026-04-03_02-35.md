# WORLD RUNTIME STATE SNAPSHOT — 2026-04-03 02:35

Trace:
- `C:/Users/danat/AppData/LocalLow/Danat Games/Submerge/Diagnostics/Hecton8_runtime_2026-04-03_02-32-06.log`

What is now confirmed:
- The previous voxel runtime thrash is no longer the main repeating source in this run.
- In this trace there is only one late voxel request:
  - `key=-8605169640`
  - `grid=32`
  - `voxel=1.10`
  - `collider=False`
  - one complete build only
- The run no longer shows the earlier pattern where the same voxel runtime key is rebuilt multiple times in a short span with different grid/collider signatures.

Startup state:
- Startup scatter is still heavy:
  - `rebuild=233.91ms`
  - `sample=151.38ms`
  - `reconcile=58.45ms`
  - `spawn=52.73ms`
- Startup GC is still bad:
  - `window=1 gc=14280474B`
- Startup trace still proves exact startup warmup is creating a large chunk of that pressure:
  - `family.coral.low prefab=PFB_family_coral_low count=120`
  - several additional startup warmups for `egg.cluster`, `pocket.safe`, `coral.branching`, `landmark.spire`, `cave.entrance`, `creature.spawn.passive`

What the latest live movement trace proved:
- The remaining large runtime GC spike in this run is not the old voxel rebuild loop.
- The big movement spike is now directly correlated with scatter pool misses / expansions:
  - `[ObjectPoolManager] 'PFB_family_pocket_safe_Placeholder': Pool exhausted, expanding by 4`
  - `[ObjectPoolManager] 'PFB_family_coral_low_Placeholder': Pool created on-demand`
  - `[ObjectPoolManager] 'PFB_family_coral_low_Placeholder': Pool exhausted, expanding by 4`
  - `[ObjectPoolManager] 'PFB_family_creature_spawn_passive__stalk': Pool exhausted, expanding by 4`
- This spike lands in:
  - `window=6 gc=4814800B`
  - same interval also has scatter rebuild:
    - `rebuild=80.45ms`
    - `sample=65.01ms`
    - `spawn=10.53ms`

Interpretation:
- The main residual runtime GC source has shifted.
- Before this pass, repeated voxel refresh/signature churn was a strong suspect.
- After the hysteresis pass, the cleanest live evidence points at scatter companion/proxy prefab pool misses during movement.
- The current remaining problem is:
  1. huge startup exact warmup
  2. insufficient companion/proxy warmup for hot families during later movement

Code changes now in place before the next validation:
- `WorldGenerativeGeologyVoxelBridgeDirector.cs`
  - added stable voxel detail banding with hysteresis
  - added collider hysteresis
  - runtime now stores `resolvedResolution` and `detailBand`
  - request signature now uses stabilized build settings
- `WorldProceduralScatterDirector.cs`
  - added `startupVariantWarmupReserve`
  - startup warmup can now prewarm a small reserve for hot companion/proxy variants instead of only exact active runtime prefabs

Next validation target:
- Check whether the new small startup companion reserve removes:
  - `PFB_family_coral_low_Placeholder` on-demand creation
  - `PFB_family_pocket_safe_Placeholder` expansion
  - `PFB_family_creature_spawn_passive__stalk` expansion
- Compare next trace against:
  - `window=6 gc=4814800B`
  - pool miss lines above

