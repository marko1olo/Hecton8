# Procedural Flora Final Status Report

- Root: `Assets/_Project/Prefabs/Nature/Flora/Baked`
- Generated: `GEN_` prefabs are starter finals only.
- Coverage metric: `aX/gY` = authored prefab count / generated prefab count under baked root.
- Linked metric: counts from `WorldPrefabFamilyProfile.variants` with `finalReady=true` and `proxyOnly=false`.

## Summary

| Family | Coverage | Expected Linked | Actual Linked | Linked Placeholder | Max Budget Triangles | Triangle Headroom | Max Renderers | LOD Prefabs | Material Ready | LOD Cascade | Fidelity Floor |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| family.kelp.tall | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 584 | 7416 | 4 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.kelp.patch.dense | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 496 | 11504 | 4 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.kelp.canopy | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 684 | 9316 | 4 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.low | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 1658 | 5342 | 2 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.branching | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 1486 | 10514 | 2 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.massive | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 1668 | 7332 | 2 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.plate | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 340 | 8160 | 2 | 3/3 | 3/3 | 3/3 | 3/3 |

## family.kelp.tall - Kelp Tall

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `584`
- Triangle budget limit: `8000`
- Triangle headroom: `7416`
- Minimum recommended triangles: `360`
- Max renderer count: `4`
- Renderer budget limit: `12`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_kelp_tall__lean` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lean` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=424 | weight=2 | scale=90-108 | lodTriangles=424/192/84/26 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lean.prefab`
  - `GEN_family_kelp_tall__ribbon` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__ribbon` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=540 | weight=2 | scale=90-108 | lodTriangles=540/268/108/48 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__ribbon.prefab`
  - `GEN_family_kelp_tall__stalk` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__stalk` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=584 | weight=2 | scale=90-108 | lodTriangles=584/302/128/50 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__stalk.prefab`

## family.kelp.patch.dense - Kelp Patch Dense

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `496`
- Triangle budget limit: `12000`
- Triangle headroom: `11504`
- Minimum recommended triangles: `320`
- Max renderer count: `4`
- Renderer budget limit: `18`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_kelp_patch_dense__patch` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=414 | weight=1 | scale=94-108 | lodTriangles=414/184/78/24 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch.prefab`
  - `GEN_family_kelp_patch_dense__patch_tall` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch_tall` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=496 | weight=1 | scale=94-108 | lodTriangles=496/246/120/40 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch_tall.prefab`
  - `GEN_family_kelp_patch_dense__ring` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__ring` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=386 | weight=1 | scale=94-108 | lodTriangles=386/222/102/32 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__ring.prefab`

## family.kelp.canopy - Kelp Canopy

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `684`
- Triangle budget limit: `10000`
- Triangle headroom: `9316`
- Minimum recommended triangles: `460`
- Max renderer count: `4`
- Renderer budget limit: `14`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_kelp_canopy__crown` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__crown` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=684 | weight=1 | scale=94-106 | lodTriangles=684/380/184/82 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__crown.prefab`
  - `GEN_family_kelp_canopy__fan` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__fan` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=590 | weight=1 | scale=94-106 | lodTriangles=590/320/174/76 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__fan.prefab`
  - `GEN_family_kelp_canopy__frond` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__frond` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=474 | weight=1 | scale=94-106 | lodTriangles=474/230/110/40 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__frond.prefab`

## family.coral.low - Coral Low

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `1658`
- Triangle budget limit: `7000`
- Triangle headroom: `5342`
- Minimum recommended triangles: `900`
- Max renderer count: `2`
- Renderer budget limit: `10`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_low__bed` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__bed` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1658 | weight=2 | scale=92-106 | lodTriangles=1658/1006 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__bed.prefab`
  - `GEN_family_coral_low__knoll` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__knoll` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1488 | weight=2 | scale=92-106 | lodTriangles=1488/928 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__knoll.prefab`
  - `GEN_family_coral_low__plate` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__plate` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1008 | weight=2 | scale=92-106 | lodTriangles=1008/616 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__plate.prefab`

## family.coral.branching - Coral Branching

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `1486`
- Triangle budget limit: `12000`
- Triangle headroom: `10514`
- Minimum recommended triangles: `800`
- Max renderer count: `2`
- Renderer budget limit: `16`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_branching__branch` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__branch` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1150 | weight=1 | scale=94-108 | lodTriangles=1150/328 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__branch.prefab`
  - `GEN_family_coral_branching__fan` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__fan` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1486 | weight=1 | scale=94-108 | lodTriangles=1486/448 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__fan.prefab`
  - `GEN_family_coral_branching__mass` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__mass` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=910 | weight=1 | scale=94-108 | lodTriangles=910/448 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__mass.prefab`

## family.coral.massive - Coral Massive

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `1668`
- Triangle budget limit: `9000`
- Triangle headroom: `7332`
- Minimum recommended triangles: `1100`
- Max renderer count: `2`
- Renderer budget limit: `12`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_massive__boulder` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__boulder` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1668 | weight=1 | scale=95-105 | lodTriangles=1668/1076 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__boulder.prefab`
  - `GEN_family_coral_massive__head` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__head` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1136 | weight=1 | scale=95-105 | lodTriangles=1136/712 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__head.prefab`
  - `GEN_family_coral_massive__porous` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__porous` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1352 | weight=1 | scale=95-105 | lodTriangles=1352/904 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__porous.prefab`

## family.coral.plate - Coral Plate

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `340`
- Triangle budget limit: `8500`
- Triangle headroom: `8160`
- Minimum recommended triangles: `220`
- Max renderer count: `2`
- Renderer budget limit: `12`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_plate__ledge` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__ledge` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=340 | weight=1 | scale=96-104 | lodTriangles=340/292 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__ledge.prefab`
  - `GEN_family_coral_plate__shelf` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__shelf` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=232 | weight=1 | scale=96-104 | lodTriangles=232/184 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__shelf.prefab`
  - `GEN_family_coral_plate__stack` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__stack` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=312 | weight=1 | scale=96-104 | lodTriangles=312/248 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__stack.prefab`
