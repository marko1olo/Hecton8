# Flora Transfer Master Status

Status: `IN PROGRESS`
Verification: `PENDING VERIFICATION`

## Source Files

- Coral raw: `работа с кораллами.md`
- Coral optimized: `Coralli.md`
- Seaweed raw: `работа с водорослями.md`
- Seaweed optimized: `Vodorosli.md`

Detailed ledgers:

- `CORALLI_TRANSFER_LEDGER.md`
- `VODOROSLI_TRANSFER_LEDGER.md`

## Transfer Policy

- Claude monolithic systems are not copied into runtime as-is.
- Optimized docs are the primary wording source.
- Raw docs are used as completeness cross-checks.
- HECTON-8 runtime ownership stays in the existing stack:
  - `WorldProceduralProxyAuthoring`
  - `WorldProceduralPlaceholderAuthoring`
  - `BiomeMatrixBootstrapAuthoring`
  - `WorldProceduralScatterDirector`
  - world runtime stack assets rebuilt from authoring
- New files are created only when the project needs a missing family/rule/placeholder/runtime data asset.
- If the project already has an owner system, concepts are adapted there instead of creating a parallel Claude subsystem.

## What Was Actually Created

- New world families:
  - `family.kelp.canopy`
  - `family.coral.massive`
  - `family.coral.plate`
- New placement rules:
  - `rule.kelp.canopy`
  - `rule.coral.massive`
  - `rule.coral.plate`
- New placeholder/runtime assets generated from existing authoring flow for those families.

## What Was Adapted Instead Of Copied

- Coral/seaweed morphology -> family silhouettes and placeholder recipes
- Seaweed anatomical detail -> kelp proxy prefab builders with stipe/base/frond composition
- Reef ecology and depth logic -> biome matrix preferred families and scatter weighting
- Route readability and shelter concepts -> structure/cluster role mapping
- Shallow-water richness -> fertile/reef/corridor weighting in scatter selection

## Current Stage

- Stage 1 complete: new families/rules/assets exist and are in the live runtime stack.
- Stage 2 complete: fertile and reef slices now use coral/kelp families in live reports, not only in data.
- Stage 3 complete: kelp creation-side proxy anatomy moved beyond bare cylinder proxies.
- Stage 4 in progress: corridor and edge-case biome tuning.

Current verified readback from `PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md`:

- `FertileShallows / Fossil Reef Context`: top and dominant structure `Coral Plate`
- `FertileShallows / Mesa Plateaus`: top and dominant structure `Kelp Canopy`
- `ReefNavigation / Fossil Reef Context`: top and dominant structure `Coral Plate`
- `ReefNavigation / Littoral Karst Context`: top and dominant structure `Kelp Canopy`
- `LandmarkCorridor / Fossil Reef Context`: dominant structure `Coral Plate`, structure role mix `bio 7 / cave 3`, but top structure still `Cave Entrance Marker`
- Kelp proxy assets were regenerated with richer anatomy on `2026-04-07 15:17`:
  - `PFB_family_kelp_tall__stalk.prefab`
  - `PFB_family_kelp_patch_dense__grove.prefab`
  - `PFB_family_kelp_canopy__crown.prefab`

## Current Blocker

- Soft-water corridor tuning is partially fixed, not fully closed.
- The fossil-reef corridor slice is no longer cave-dominant, but its single highest-scoring structure is still `Cave Entrance Marker`.
- Hard corridor slices remain intentionally unchanged:
  - `Granite Escarpment` stays landmark-heavy
  - `Rift Spine` stays cave-heavy

## Next Pass

- Finish the remaining `LandmarkCorridor / Fossil Reef Context` top-structure shift without destabilizing granite/rift corridors.
- After that, move from selection tuning into broader in-world beauty/readability verification.
- Perf remains `PENDING VERIFICATION` until GC/build numbers exist.
