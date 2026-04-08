# Procedural Flora Final Status Report

- Root: `Assets/_Project/Prefabs/Nature/Flora/Baked`
- Generated: `GEN_` prefabs are starter finals only.
- Coverage metric: `aX/gY` = authored prefab count / generated prefab count under baked root.
- Linked metric: counts from `WorldPrefabFamilyProfile.variants` with `finalReady=true` and `proxyOnly=false`.

## Summary

| Family | Coverage | Expected Linked | Actual Linked | Linked Placeholder | Max Budget Triangles | Triangle Headroom | Max Renderers | LOD Prefabs | Material Ready | LOD Cascade | Fidelity Floor |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| family.kelp.tall | a0/g7 | 7 | 7 (authored 0, gen 7) | 0 | 5544 | 2456 | 4 | 7/7 | 7/7 | 7/7 | 7/7 |
| family.kelp.patch.dense | a0/g7 | 7 | 7 (authored 0, gen 7) | 0 | 9688 | 2312 | 4 | 7/7 | 7/7 | 7/7 | 7/7 |
| family.kelp.canopy | a0/g7 | 7 | 7 (authored 0, gen 7) | 0 | 4356 | 5644 | 4 | 7/7 | 7/7 | 7/7 | 7/7 |
| family.kelp.abyssal | a0/g7 | 7 | 7 (authored 0, gen 7) | 0 | 6096 | 2904 | 4 | 7/7 | 7/7 | 7/7 | 7/7 |
| family.coral.low | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 1658 | 5342 | 2 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.branching | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 1486 | 10514 | 2 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.massive | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 2028 | 6972 | 2 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.plate | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 560 | 7940 | 2 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.brittle | a0/g7 | 7 | 7 (authored 0, gen 7) | 0 | 1714 | 7786 | 2 | 7/7 | 7/7 | 7/7 | 7/7 |

## family.kelp.tall - Kelp Tall

- Coverage: `a0/g7`
- Expected linked real finals: `7`
- Linked final-ready: `7`
- Linked real finals: `7`
- Linked placeholders: `0`
- Max budget triangles: `5544`
- Triangle budget limit: `8000`
- Triangle headroom: `2456`
- Minimum recommended triangles: `360`
- Max renderer count: `4`
- Renderer budget limit: `12`
- Material-ready prefabs: `7/7`
- Strict LOD cascade prefabs: `7/7`
- Prefabs meeting fidelity floor: `7/7`
- Prefabs:
  - `GEN_family_kelp_tall__banner` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__banner` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=5446 | weight=2 | scale=90-108 | lodTriangles=5446/2486/920/534 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__banner.prefab`
  - `GEN_family_kelp_tall__lamina` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lamina` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=5060 | weight=2 | scale=90-108 | lodTriangles=5060/2298/852/482 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lamina.prefab`
  - `GEN_family_kelp_tall__lance` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lance` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4578 | weight=2 | scale=90-108 | lodTriangles=4578/2024/712/384 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lance.prefab`
  - `GEN_family_kelp_tall__lean` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lean` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4188 | weight=2 | scale=90-108 | lodTriangles=4188/1786/620/306 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lean.prefab`
  - `GEN_family_kelp_tall__ribbon` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__ribbon` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=5544 | weight=2 | scale=90-108 | lodTriangles=5544/2504/932/556 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__ribbon.prefab`
  - `GEN_family_kelp_tall__rope` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__rope` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4480 | weight=2 | scale=90-108 | lodTriangles=4480/1896/688/368 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__rope.prefab`
  - `GEN_family_kelp_tall__stalk` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__stalk` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=5376 | weight=2 | scale=90-108 | lodTriangles=5376/2416/900/500 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__stalk.prefab`

## family.kelp.patch.dense - Kelp Patch Dense

- Coverage: `a0/g7`
- Expected linked real finals: `7`
- Linked final-ready: `7`
- Linked real finals: `7`
- Linked placeholders: `0`
- Max budget triangles: `9688`
- Triangle budget limit: `12000`
- Triangle headroom: `2312`
- Minimum recommended triangles: `320`
- Max renderer count: `4`
- Renderer budget limit: `18`
- Material-ready prefabs: `7/7`
- Strict LOD cascade prefabs: `7/7`
- Prefabs meeting fidelity floor: `7/7`
- Prefabs:
  - `GEN_family_kelp_patch_dense__brush` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__brush` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=8720 | weight=1 | scale=94-108 | lodTriangles=8720/4196/1260/716 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__brush.prefab`
  - `GEN_family_kelp_patch_dense__drape` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__drape` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=7764 | weight=1 | scale=94-108 | lodTriangles=7764/3732/1008/592 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__drape.prefab`
  - `GEN_family_kelp_patch_dense__patch` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=6096 | weight=1 | scale=94-108 | lodTriangles=6096/3000/792/424 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch.prefab`
  - `GEN_family_kelp_patch_dense__patch_tall` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch_tall` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=9384 | weight=1 | scale=94-108 | lodTriangles=9384/4532/1572/794 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch_tall.prefab`
  - `GEN_family_kelp_patch_dense__ring` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__ring` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=9600 | weight=1 | scale=94-108 | lodTriangles=9600/4492/1344/716 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__ring.prefab`
  - `GEN_family_kelp_patch_dense__sheet` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__sheet` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=6930 | weight=1 | scale=94-108 | lodTriangles=6930/3312/1008/520 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__sheet.prefab`
  - `GEN_family_kelp_patch_dense__tuft` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__tuft` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=9688 | weight=1 | scale=94-108 | lodTriangles=9688/4208/1400/808 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__tuft.prefab`

## family.kelp.canopy - Kelp Canopy

- Coverage: `a0/g7`
- Expected linked real finals: `7`
- Linked final-ready: `7`
- Linked real finals: `7`
- Linked placeholders: `0`
- Max budget triangles: `4356`
- Triangle budget limit: `10000`
- Triangle headroom: `5644`
- Minimum recommended triangles: `460`
- Max renderer count: `4`
- Renderer budget limit: `14`
- Material-ready prefabs: `7/7`
- Strict LOD cascade prefabs: `7/7`
- Prefabs meeting fidelity floor: `7/7`
- Prefabs:
  - `GEN_family_kelp_canopy__crown` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__crown` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4080 | weight=1 | scale=94-106 | lodTriangles=4080/2660/1104/628 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__crown.prefab`
  - `GEN_family_kelp_canopy__fan` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__fan` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=3844 | weight=1 | scale=94-106 | lodTriangles=3844/2448/928/532 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__fan.prefab`
  - `GEN_family_kelp_canopy__frond` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__frond` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=2724 | weight=1 | scale=94-106 | lodTriangles=2724/1624/558/284 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__frond.prefab`
  - `GEN_family_kelp_canopy__mantle` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__mantle` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4356 | weight=1 | scale=94-106 | lodTriangles=4356/2764/1120/680 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__mantle.prefab`
  - `GEN_family_kelp_canopy__rosette` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__rosette` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4334 | weight=1 | scale=94-106 | lodTriangles=4334/2696/1046/604 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__rosette.prefab`
  - `GEN_family_kelp_canopy__splay` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__splay` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=3556 | weight=1 | scale=94-106 | lodTriangles=3556/2330/844/478 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__splay.prefab`
  - `GEN_family_kelp_canopy__veil` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__veil` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4340 | weight=1 | scale=94-106 | lodTriangles=4340/2752/1112/680 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__veil.prefab`

## family.kelp.abyssal - Kelp Abyssal

- Coverage: `a0/g7`
- Expected linked real finals: `7`
- Linked final-ready: `7`
- Linked real finals: `7`
- Linked placeholders: `0`
- Max budget triangles: `6096`
- Triangle budget limit: `9000`
- Triangle headroom: `2904`
- Minimum recommended triangles: `380`
- Max renderer count: `4`
- Renderer budget limit: `14`
- Material-ready prefabs: `7/7`
- Strict LOD cascade prefabs: `7/7`
- Prefabs meeting fidelity floor: `7/7`
- Prefabs:
  - `GEN_family_kelp_abyssal__braid` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__braid` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=5880 | weight=1 | scale=96-108 | lodTriangles=5880/2796/1072/668 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__braid.prefab`
  - `GEN_family_kelp_abyssal__mantle` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__mantle` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=5724 | weight=1 | scale=96-108 | lodTriangles=5724/2742/1012/638 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__mantle.prefab`
  - `GEN_family_kelp_abyssal__nodule` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__nodule` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4546 | weight=1 | scale=96-108 | lodTriangles=4546/1968/708/390 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__nodule.prefab`
  - `GEN_family_kelp_abyssal__pennant` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__pennant` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=6096 | weight=1 | scale=96-108 | lodTriangles=6096/2894/1096/698 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__pennant.prefab`
  - `GEN_family_kelp_abyssal__shroud` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__shroud` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4954 | weight=1 | scale=96-108 | lodTriangles=4954/2280/838/500 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__shroud.prefab`
  - `GEN_family_kelp_abyssal__strap` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__strap` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=4262 | weight=1 | scale=96-108 | lodTriangles=4262/1896/668/378 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__strap.prefab`
  - `GEN_family_kelp_abyssal__whip` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__whip` | renderers=4 | lodGroups=1 | lodLevels=4 | budgetTriangles=3980 | weight=1 | scale=96-108 | lodTriangles=3980/1848/672/396 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__whip.prefab`

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
- Max budget triangles: `2028`
- Triangle budget limit: `9000`
- Triangle headroom: `6972`
- Minimum recommended triangles: `1100`
- Max renderer count: `2`
- Renderer budget limit: `12`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_massive__boulder` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__boulder` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=2028 | weight=1 | scale=95-105 | lodTriangles=2028/1204 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__boulder.prefab`
  - `GEN_family_coral_massive__head` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__head` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1496 | weight=1 | scale=95-105 | lodTriangles=1496/840 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__head.prefab`
  - `GEN_family_coral_massive__porous` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__porous` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1712 | weight=1 | scale=95-105 | lodTriangles=1712/1032 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__porous.prefab`

## family.coral.plate - Coral Plate

- Coverage: `a0/g3`
- Expected linked real finals: `3`
- Linked final-ready: `3`
- Linked real finals: `3`
- Linked placeholders: `0`
- Max budget triangles: `560`
- Triangle budget limit: `8500`
- Triangle headroom: `7940`
- Minimum recommended triangles: `220`
- Max renderer count: `2`
- Renderer budget limit: `12`
- Material-ready prefabs: `3/3`
- Strict LOD cascade prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_plate__ledge` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__ledge` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=340 | weight=1 | scale=96-104 | lodTriangles=340/292 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__ledge.prefab`
  - `GEN_family_coral_plate__shelf` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__shelf` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=560 | weight=1 | scale=96-104 | lodTriangles=560/400 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__shelf.prefab`
  - `GEN_family_coral_plate__stack` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__stack` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=312 | weight=1 | scale=96-104 | lodTriangles=312/248 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__stack.prefab`

## family.coral.brittle - Coral Brittle

- Coverage: `a0/g7`
- Expected linked real finals: `7`
- Linked final-ready: `7`
- Linked real finals: `7`
- Linked placeholders: `0`
- Max budget triangles: `1714`
- Triangle budget limit: `9500`
- Triangle headroom: `7786`
- Minimum recommended triangles: `720`
- Max renderer count: `2`
- Renderer budget limit: `14`
- Material-ready prefabs: `7/7`
- Strict LOD cascade prefabs: `7/7`
- Prefabs meeting fidelity floor: `7/7`
- Prefabs:
  - `GEN_family_coral_brittle__crown` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__crown` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1648 | weight=1 | scale=94-108 | lodTriangles=1648/516 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__crown.prefab`
  - `GEN_family_coral_brittle__fan` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__fan` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1246 | weight=1 | scale=94-108 | lodTriangles=1246/364 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__fan.prefab`
  - `GEN_family_coral_brittle__halo` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__halo` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1544 | weight=1 | scale=94-108 | lodTriangles=1544/486 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__halo.prefab`
  - `GEN_family_coral_brittle__lace` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__lace` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1486 | weight=1 | scale=94-108 | lodTriangles=1486/448 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__lace.prefab`
  - `GEN_family_coral_brittle__spire` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__spire` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1424 | weight=1 | scale=94-108 | lodTriangles=1424/438 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__spire.prefab`
  - `GEN_family_coral_brittle__sprig` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__sprig` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=934 | weight=1 | scale=94-108 | lodTriangles=934/256 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__sprig.prefab`
  - `GEN_family_coral_brittle__thicket` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__thicket` | renderers=2 | lodGroups=1 | lodLevels=2 | budgetTriangles=1714 | weight=1 | scale=94-108 | lodTriangles=1714/560 | material=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__thicket.prefab`
