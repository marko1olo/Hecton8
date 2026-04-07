# Procedural Flora Final Status Report

- Root: `Assets/_Project/Prefabs/Nature/Flora/Baked`
- Generated: `GEN_` prefabs are starter finals only.
- Coverage metric: `aX/gY` = authored prefab count / generated prefab count under baked root.
- Linked metric: counts from `WorldPrefabFamilyProfile.variants` with `finalReady=true` and `proxyOnly=false`.

## Summary

| Family | Coverage | Expected Linked | Actual Linked | Linked Placeholder | Max Budget Triangles | Triangle Headroom | Max Renderers | LOD Prefabs | Material Ready | LOD Cascade |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| family.kelp.tall | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 584 | 7416 | 4 | 3/3 | 3/3 | 3/3 |
| family.kelp.patch.dense | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 496 | 11504 | 4 | 3/3 | 3/3 | 3/3 |
| family.kelp.canopy | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 684 | 9316 | 4 | 3/3 | 3/3 | 3/3 |
| family.coral.low | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 3840 | 3160 | 2 | 3/3 | 3/3 | 3/3 |
| family.coral.branching | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 4320 | 7680 | 2 | 3/3 | 3/3 | 3/3 |
| family.coral.massive | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 3840 | 5160 | 2 | 3/3 | 3/3 | 3/3 |
| family.coral.plate | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 400 | 8100 | 2 | 3/3 | 3/3 | 3/3 |

## family.kelp.tall - Kelp Tall

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `584`
- Triangle budget limit: `8000`
- Triangle headroom: `7416`
- Max renderer count: `4`
- Renderer budget limit: `12`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs:
  - `GEN_family_kelp_tall__lean` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lean` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=424 | weight=2 | scale=90-108 | lodTriangles=424/192/84/26 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lean.prefab`
  - `GEN_family_kelp_tall__ribbon` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__ribbon` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=540 | weight=2 | scale=90-108 | lodTriangles=540/268/108/48 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__ribbon.prefab`
  - `GEN_family_kelp_tall__stalk` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__stalk` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=584 | weight=2 | scale=90-108 | lodTriangles=584/302/128/50 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__stalk.prefab`

## family.kelp.patch.dense - Kelp Patch Dense

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `496`
- Triangle budget limit: `12000`
- Triangle headroom: `11504`
- Max renderer count: `4`
- Renderer budget limit: `18`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs:
  - `GEN_family_kelp_patch_dense__patch` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=414 | weight=1 | scale=94-108 | lodTriangles=414/184/78/24 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch.prefab`
  - `GEN_family_kelp_patch_dense__patch_tall` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch_tall` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=496 | weight=1 | scale=94-108 | lodTriangles=496/246/120/40 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch_tall.prefab`
  - `GEN_family_kelp_patch_dense__ring` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__ring` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=386 | weight=1 | scale=94-108 | lodTriangles=386/222/102/32 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__ring.prefab`

## family.kelp.canopy - Kelp Canopy

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `684`
- Triangle budget limit: `10000`
- Triangle headroom: `9316`
- Max renderer count: `4`
- Renderer budget limit: `14`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs:
  - `GEN_family_kelp_canopy__crown` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__crown` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=684 | weight=1 | scale=94-106 | lodTriangles=684/380/184/82 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__crown.prefab`
  - `GEN_family_kelp_canopy__fan` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__fan` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=590 | weight=1 | scale=94-106 | lodTriangles=590/320/174/76 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__fan.prefab`
  - `GEN_family_kelp_canopy__frond` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__frond` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=474 | weight=1 | scale=94-106 | lodTriangles=474/230/110/40 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__frond.prefab`

## family.coral.low - Coral Low

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `3840`
- Triangle budget limit: `7000`
- Triangle headroom: `3160`
- Max renderer count: `2`
- Renderer budget limit: `10`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs:
  - `GEN_family_coral_low__bed` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__bed` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=2304 | weight=2 | scale=92-106 | lodTriangles=2304/1536 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__bed.prefab`
  - `GEN_family_coral_low__knoll` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__knoll` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=3840 | weight=2 | scale=92-106 | lodTriangles=3840/2304 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__knoll.prefab`
  - `GEN_family_coral_low__plate` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__plate` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=2384 | weight=2 | scale=92-106 | lodTriangles=2384/1536 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__plate.prefab`

## family.coral.branching - Coral Branching

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `4320`
- Triangle budget limit: `12000`
- Triangle headroom: `7680`
- Max renderer count: `2`
- Renderer budget limit: `16`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs:
  - `GEN_family_coral_branching__branch` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__branch` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=2624 | weight=1 | scale=94-108 | lodTriangles=2624/240 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__branch.prefab`
  - `GEN_family_coral_branching__fan` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__fan` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=4320 | weight=1 | scale=94-108 | lodTriangles=4320/240 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__fan.prefab`
  - `GEN_family_coral_branching__mass` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__mass` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=3392 | weight=1 | scale=94-108 | lodTriangles=3392/928 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__mass.prefab`

## family.coral.massive - Coral Massive

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `3840`
- Triangle budget limit: `9000`
- Triangle headroom: `5160`
- Max renderer count: `2`
- Renderer budget limit: `12`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs:
  - `GEN_family_coral_massive__boulder` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__boulder` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=3840 | weight=1 | scale=95-105 | lodTriangles=3840/2304 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__boulder.prefab`
  - `GEN_family_coral_massive__head` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__head` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=2304 | weight=1 | scale=95-105 | lodTriangles=2304/1536 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__head.prefab`
  - `GEN_family_coral_massive__porous` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__porous` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=2328 | weight=1 | scale=95-105 | lodTriangles=2328/2304 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__porous.prefab`

## family.coral.plate - Coral Plate

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `400`
- Triangle budget limit: `8500`
- Triangle headroom: `8100`
- Max renderer count: `2`
- Renderer budget limit: `12`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs:
  - `GEN_family_coral_plate__ledge` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__ledge` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=240 | weight=1 | scale=96-104 | lodTriangles=240/160 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__ledge.prefab`
  - `GEN_family_coral_plate__shelf` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__shelf` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=320 | weight=1 | scale=96-104 | lodTriangles=320/160 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__shelf.prefab`
  - `GEN_family_coral_plate__stack` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__stack` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=400 | weight=1 | scale=96-104 | lodTriangles=400/240 | material=ok | renderState=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__stack.prefab`
