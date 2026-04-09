# Procedural Flora Final Status Report

- Root: `Assets/_Project/Prefabs/Nature/Flora/Baked`
- Generated: `GEN_` prefabs are starter finals only.
- Texture proof: procedural editor-generated `.asset` textures do not count as authored photoreal final proof.
- Shader proof: material contract requires `_QUALITY_MX350`, no `_QUALITY_HIGH`, and positive triplanar/normal/fresnel/parallax properties.
- Coverage metric: `aX/gY` = authored prefab count / generated prefab count under baked root.
- Linked metric: counts from `WorldPrefabFamilyProfile.variants` with `finalReady=true` and `proxyOnly=false`.

## Summary

| Family | Coverage | Expected Linked | Actual Linked | Linked Placeholder | Max Budget Triangles | Triangle Headroom | Max Renderers | LOD Prefabs | Material Contract | LOD Contract | Fidelity Floor |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| family.kelp.tall | a0/g14 | 14 | 14 (authored 0, gen 14) | 0 | 7250 | 750 | 3 | 14/14 | 14/14 | 14/14 | 14/14 |
| family.kelp.patch.dense | a0/g12 | 12 | 12 (authored 0, gen 12) | 0 | 9016 | 2984 | 3 | 12/12 | 12/12 | 12/12 | 12/12 |
| family.kelp.canopy | a0/g13 | 13 | 13 (authored 0, gen 13) | 0 | 9402 | 598 | 3 | 13/13 | 13/13 | 13/13 | 13/13 |
| family.kelp.abyssal | a0/g14 | 14 | 14 (authored 0, gen 14) | 0 | 8536 | 464 | 3 | 14/14 | 14/14 | 14/14 | 14/14 |
| family.coral.low | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 1658 | 5342 | 3 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.branching | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 1486 | 10514 | 3 | 3/3 | 3/3 | 3/3 | 3/3 |
| family.coral.massive | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 2028 | 6972 | 3 | 3/3 | 0/3 | 3/3 | 3/3 |
| family.coral.plate | a0/g3 | 3 | 3 (authored 0, gen 3) | 0 | 560 | 7940 | 3 | 3/3 | 0/3 | 3/3 | 3/3 |
| family.coral.brittle | a0/g7 | 7 | 7 (authored 0, gen 7) | 0 | 1714 | 7786 | 3 | 7/7 | 0/7 | 7/7 | 7/7 |

## family.kelp.tall - Kelp Tall

- Coverage: `a0/g14`
- Expected linked real finals: `14`
- Linked final-ready: `14`
- Linked real finals: `14`
- Linked placeholders: `0`
- Max budget triangles: `7250`
- Triangle budget limit: `8000`
- Triangle headroom: `750`
- Minimum recommended triangles: `360`
- Max renderer count: `3`
- Renderer budget limit: `12`
- Material-contract prefabs: `14/14`
- Exact LOD contract prefabs: `14/14`
- Prefabs meeting fidelity floor: `14/14`
- Prefabs:
  - `GEN_family_kelp_tall__banner` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__banner` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4054 | weight=2 | scale=90-108 | lodTriangles=4054/1526/920 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__banner.prefab`
  - `GEN_family_kelp_tall__broadleaf__s110-170` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__broadleaf` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6828 | weight=2 | scale=110-170* | lodTriangles=6828/1808/732 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__broadleaf__s110-170.prefab`
  - `GEN_family_kelp_tall__colossus__s160-240` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__colossus` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6268 | weight=2 | scale=160-240* | lodTriangles=6268/2028/1286 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__colossus__s160-240.prefab`
  - `GEN_family_kelp_tall__frondcrest__s105-165` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__frondcrest` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4622 | weight=2 | scale=105-165* | lodTriangles=4622/1450/932 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__frondcrest__s105-165.prefab`
  - `GEN_family_kelp_tall__lamina` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lamina` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5764 | weight=2 | scale=90-108 | lodTriangles=5764/2298/852 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lamina.prefab`
  - `GEN_family_kelp_tall__lance` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lance` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4578 | weight=2 | scale=90-108 | lodTriangles=4578/2024/712 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lance.prefab`
  - `GEN_family_kelp_tall__lean` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lean` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4188 | weight=2 | scale=90-108 | lodTriangles=4188/1786/620 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lean.prefab`
  - `GEN_family_kelp_tall__paddle__s90-150` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__paddle` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6168 | weight=2 | scale=90-150* | lodTriangles=6168/1712/1084 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__paddle__s90-150.prefab`
  - `GEN_family_kelp_tall__ribbon` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__ribbon` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5720 | weight=2 | scale=90-108 | lodTriangles=5720/2504/932 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__ribbon.prefab`
  - `GEN_family_kelp_tall__rope` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__rope` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4480 | weight=2 | scale=90-108 | lodTriangles=4480/1896/688 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__rope.prefab`
  - `GEN_family_kelp_tall__sail__s115-175` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__sail` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5076 | weight=2 | scale=115-175* | lodTriangles=5076/908/502 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__sail__s115-175.prefab`
  - `GEN_family_kelp_tall__seedling__s55-90` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__seedling` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1666 | weight=2 | scale=55-90* | lodTriangles=1666/676/164 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__seedling__s55-90.prefab`
  - `GEN_family_kelp_tall__stalk` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__stalk` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5376 | weight=2 | scale=90-108 | lodTriangles=5376/2416/900 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__stalk.prefab`
  - `GEN_family_kelp_tall__tower__s130-185` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__tower` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=7250 | weight=2 | scale=130-185* | lodTriangles=7250/2294/1484 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__tower__s130-185.prefab`

## family.kelp.patch.dense - Kelp Patch Dense

- Coverage: `a0/g12`
- Expected linked real finals: `12`
- Linked final-ready: `12`
- Linked real finals: `12`
- Linked placeholders: `0`
- Max budget triangles: `9016`
- Triangle budget limit: `12000`
- Triangle headroom: `2984`
- Minimum recommended triangles: `320`
- Max renderer count: `3`
- Renderer budget limit: `18`
- Material-contract prefabs: `12/12`
- Exact LOD contract prefabs: `12/12`
- Prefabs meeting fidelity floor: `12/12`
- Prefabs:
  - `GEN_family_kelp_patch_dense__bladder__s80-135` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__bladder` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6232 | weight=1 | scale=80-135* | lodTriangles=6232/2864/1180 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__bladder__s80-135.prefab`
  - `GEN_family_kelp_patch_dense__brush` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__brush` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8048 | weight=1 | scale=94-108 | lodTriangles=8048/4196/1260 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__brush.prefab`
  - `GEN_family_kelp_patch_dense__drape` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__drape` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=7116 | weight=1 | scale=94-108 | lodTriangles=7116/3732/1008 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__drape.prefab`
  - `GEN_family_kelp_patch_dense__frilltuft__s75-125` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__frilltuft` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8648 | weight=1 | scale=75-125* | lodTriangles=8648/4412/1512 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__frilltuft__s75-125.prefab`
  - `GEN_family_kelp_patch_dense__nest__s65-105` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__nest` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8388 | weight=1 | scale=65-105* | lodTriangles=8388/3772/1256 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__nest__s65-105.prefab`
  - `GEN_family_kelp_patch_dense__paddlespray__s70-120` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__paddlespray` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6432 | weight=1 | scale=70-120* | lodTriangles=6432/2940/1344 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__paddlespray__s70-120.prefab`
  - `GEN_family_kelp_patch_dense__patch` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5712 | weight=1 | scale=94-108 | lodTriangles=5712/3000/792 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch.prefab`
  - `GEN_family_kelp_patch_dense__patch_tall` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch_tall` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8808 | weight=1 | scale=94-108 | lodTriangles=8808/4532/1572 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch_tall.prefab`
  - `GEN_family_kelp_patch_dense__ring` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__ring` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8832 | weight=1 | scale=94-108 | lodTriangles=8832/4492/1344 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__ring.prefab`
  - `GEN_family_kelp_patch_dense__sheet` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__sheet` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6498 | weight=1 | scale=94-108 | lodTriangles=6498/3312/1008 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__sheet.prefab`
  - `GEN_family_kelp_patch_dense__sheetwall__s120-185` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__sheetwall` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8808 | weight=1 | scale=120-185* | lodTriangles=8808/4532/1572 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__sheetwall__s120-185.prefab`
  - `GEN_family_kelp_patch_dense__tuft` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__tuft` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=9016 | weight=1 | scale=94-108 | lodTriangles=9016/4208/1400 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__tuft.prefab`

## family.kelp.canopy - Kelp Canopy

- Coverage: `a0/g13`
- Expected linked real finals: `13`
- Linked final-ready: `13`
- Linked real finals: `13`
- Linked placeholders: `0`
- Max budget triangles: `9402`
- Triangle budget limit: `10000`
- Triangle headroom: `598`
- Minimum recommended triangles: `460`
- Max renderer count: `3`
- Renderer budget limit: `14`
- Material-contract prefabs: `13/13`
- Exact LOD contract prefabs: `13/13`
- Prefabs meeting fidelity floor: `13/13`
- Prefabs:
  - `GEN_family_kelp_canopy__crown` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__crown` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3184 | weight=1 | scale=94-106 | lodTriangles=3184/1700/1104 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__crown.prefab`
  - `GEN_family_kelp_canopy__fan` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__fan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=2836 | weight=1 | scale=94-106 | lodTriangles=2836/1568/928 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__fan.prefab`
  - `GEN_family_kelp_canopy__featherfan__s120-200` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__featherfan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6462 | weight=1 | scale=120-200* | lodTriangles=6462/1596/1056 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__featherfan__s120-200.prefab`
  - `GEN_family_kelp_canopy__frond` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__frond` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=2168 | weight=1 | scale=94-106 | lodTriangles=2168/1064/558 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__frond.prefab`
  - `GEN_family_kelp_canopy__laminaria__s105-165` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__laminaria` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3642 | weight=1 | scale=105-165* | lodTriangles=3642/1484/972 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__laminaria__s105-165.prefab`
  - `GEN_family_kelp_canopy__mantle` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__mantle` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3678 | weight=1 | scale=94-106 | lodTriangles=3678/1708/1120 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__mantle.prefab`
  - `GEN_family_kelp_canopy__oar__s110-180` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__oar` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6230 | weight=1 | scale=110-180* | lodTriangles=6230/1692/1056 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__oar__s110-180.prefab`
  - `GEN_family_kelp_canopy__paddlefan__s120-190` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__paddlefan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8130 | weight=1 | scale=120-190* | lodTriangles=8130/1596/968 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__paddlefan__s120-190.prefab`
  - `GEN_family_kelp_canopy__rosette` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__rosette` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4334 | weight=1 | scale=94-106 | lodTriangles=4334/2696/1046 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__rosette.prefab`
  - `GEN_family_kelp_canopy__sheetwall__s150-230` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__sheetwall` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6372 | weight=1 | scale=150-230* | lodTriangles=6372/1858/1180 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__sheetwall__s150-230.prefab`
  - `GEN_family_kelp_canopy__splay` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__splay` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3556 | weight=1 | scale=94-106 | lodTriangles=3556/2330/844 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__splay.prefab`
  - `GEN_family_kelp_canopy__tapestry__s160-240` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__tapestry` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=9402 | weight=1 | scale=160-240* | lodTriangles=9402/1874/1264 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__tapestry__s160-240.prefab`
  - `GEN_family_kelp_canopy__veil` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__veil` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3838 | weight=1 | scale=94-106 | lodTriangles=3838/1696/1112 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__veil.prefab`

## family.kelp.abyssal - Kelp Abyssal

- Coverage: `a0/g14`
- Expected linked real finals: `14`
- Linked final-ready: `14`
- Linked real finals: `14`
- Linked placeholders: `0`
- Max budget triangles: `8536`
- Triangle budget limit: `9000`
- Triangle headroom: `464`
- Minimum recommended triangles: `380`
- Max renderer count: `3`
- Renderer budget limit: `14`
- Material-contract prefabs: `14/14`
- Exact LOD contract prefabs: `14/14`
- Prefabs meeting fidelity floor: `14/14`
- Prefabs:
  - `GEN_family_kelp_abyssal__braid` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__braid` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6080 | weight=1 | scale=96-108 | lodTriangles=6080/2796/1072 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__braid.prefab`
  - `GEN_family_kelp_abyssal__cathedral__s140-240` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__cathedral` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8536 | weight=1 | scale=140-240* | lodTriangles=8536/3768/1288 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__cathedral__s140-240.prefab`
  - `GEN_family_kelp_abyssal__cowl__s110-180` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__cowl` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8392 | weight=1 | scale=110-180* | lodTriangles=8392/1534/1012 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__cowl__s110-180.prefab`
  - `GEN_family_kelp_abyssal__lantern__s100-180` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__lantern` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5992 | weight=1 | scale=100-180* | lodTriangles=5992/1678/1036 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__lantern__s100-180.prefab`
  - `GEN_family_kelp_abyssal__mantle` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__mantle` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3734 | weight=1 | scale=96-108 | lodTriangles=3734/1598/1012 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__mantle.prefab`
  - `GEN_family_kelp_abyssal__nodule` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__nodule` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4546 | weight=1 | scale=96-108 | lodTriangles=4546/1968/708 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__nodule.prefab`
  - `GEN_family_kelp_abyssal__pennant` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__pennant` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4536 | weight=1 | scale=96-108 | lodTriangles=4536/1646/1096 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__pennant.prefab`
  - `GEN_family_kelp_abyssal__petal__s100-170` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__petal` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6202 | weight=1 | scale=100-170* | lodTriangles=6202/1526/920 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__petal__s100-170.prefab`
  - `GEN_family_kelp_abyssal__reed__s80-140` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__reed` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=2924 | weight=1 | scale=80-140* | lodTriangles=2924/1248/402 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__reed__s80-140.prefab`
  - `GEN_family_kelp_abyssal__shroud` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__shroud` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3210 | weight=1 | scale=96-108 | lodTriangles=3210/1320/838 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__shroud.prefab`
  - `GEN_family_kelp_abyssal__strap` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__strap` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4262 | weight=1 | scale=96-108 | lodTriangles=4262/1896/668 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__strap.prefab`
  - `GEN_family_kelp_abyssal__tatterveil__s110-185` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__tatterveil` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4912 | weight=1 | scale=110-185* | lodTriangles=4912/1598/1012 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__tatterveil__s110-185.prefab`
  - `GEN_family_kelp_abyssal__veilwall__s150-240` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__veilwall` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5084 | weight=1 | scale=150-240* | lodTriangles=5084/896/494 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__veilwall__s150-240.prefab`
  - `GEN_family_kelp_abyssal__whip` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__whip` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3980 | weight=1 | scale=96-108 | lodTriangles=3980/1848/672 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__whip.prefab`

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
- Max renderer count: `3`
- Renderer budget limit: `10`
- Material-contract prefabs: `3/3`
- Exact LOD contract prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_low__bed` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__bed` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1658 | weight=2 | scale=92-106 | lodTriangles=1658/1006/514 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__bed.prefab`
  - `GEN_family_coral_low__knoll` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__knoll` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1488 | weight=2 | scale=92-106 | lodTriangles=1488/928/496 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__knoll.prefab`
  - `GEN_family_coral_low__plate` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__plate` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1008 | weight=2 | scale=92-106 | lodTriangles=1008/616/320 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__plate.prefab`

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
- Max renderer count: `3`
- Renderer budget limit: `16`
- Material-contract prefabs: `3/3`
- Exact LOD contract prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_branching__branch` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__branch` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1150 | weight=1 | scale=94-108 | lodTriangles=1150/328/256 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__branch.prefab`
  - `GEN_family_coral_branching__fan` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__fan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1486 | weight=1 | scale=94-108 | lodTriangles=1486/448/364 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__fan.prefab`
  - `GEN_family_coral_branching__mass` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__mass` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=910 | weight=1 | scale=94-108 | lodTriangles=910/448/376 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__mass.prefab`

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
- Max renderer count: `3`
- Renderer budget limit: `12`
- Material-contract prefabs: `0/3`
- Exact LOD contract prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_massive__boulder` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__boulder` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=2028 | weight=1 | scale=95-105 | lodTriangles=2028/1204/740 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__boulder.prefab`
  - `GEN_family_coral_massive__head` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__head` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1496 | weight=1 | scale=95-105 | lodTriangles=1496/840/512 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__head.prefab`
  - `GEN_family_coral_massive__porous` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__porous` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1712 | weight=1 | scale=95-105 | lodTriangles=1712/1032/680 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__porous.prefab`

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
- Max renderer count: `3`
- Renderer budget limit: `12`
- Material-contract prefabs: `0/3`
- Exact LOD contract prefabs: `3/3`
- Prefabs meeting fidelity floor: `3/3`
- Prefabs:
  - `GEN_family_coral_plate__ledge` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__ledge` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=340 | weight=1 | scale=96-104 | lodTriangles=340/292/244 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__ledge.prefab`
  - `GEN_family_coral_plate__shelf` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__shelf` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=560 | weight=1 | scale=96-104 | lodTriangles=560/400/360 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__shelf.prefab`
  - `GEN_family_coral_plate__stack` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__stack` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=312 | weight=1 | scale=96-104 | lodTriangles=312/248/184 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__stack.prefab`

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
- Max renderer count: `3`
- Renderer budget limit: `14`
- Material-contract prefabs: `0/7`
- Exact LOD contract prefabs: `7/7`
- Prefabs meeting fidelity floor: `7/7`
- Prefabs:
  - `GEN_family_coral_brittle__crown` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__crown` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1648 | weight=1 | scale=94-108 | lodTriangles=1648/516/400 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__crown.prefab`
  - `GEN_family_coral_brittle__fan` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__fan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1246 | weight=1 | scale=94-108 | lodTriangles=1246/364/280 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__fan.prefab`
  - `GEN_family_coral_brittle__halo` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__halo` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1544 | weight=1 | scale=94-108 | lodTriangles=1544/486/364 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__halo.prefab`
  - `GEN_family_coral_brittle__lace` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__lace` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1486 | weight=1 | scale=94-108 | lodTriangles=1486/448/364 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__lace.prefab`
  - `GEN_family_coral_brittle__spire` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__spire` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1424 | weight=1 | scale=94-108 | lodTriangles=1424/438/328 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__spire.prefab`
  - `GEN_family_coral_brittle__sprig` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__sprig` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=934 | weight=1 | scale=94-108 | lodTriangles=934/256/184 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__sprig.prefab`
  - `GEN_family_coral_brittle__thicket` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__thicket` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1714 | weight=1 | scale=94-108 | lodTriangles=1714/560/400 | material=starter-generated-textures | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__thicket.prefab`
