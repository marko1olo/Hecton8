Date: 2026-04-16
Status: ARCHIVED

**WARNING: LEGACY DOCUMENT.** This file is archived for historical reference and may contain outdated information or broken links.

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
| family.kelp.tall | a0/g14 | 14 | 14 (authored 0, gen 14) | 0 | 7938 | 62 | 3 | 14/14 | 14/14 | 14/14 | 14/14 |
| family.kelp.patch.dense | a0/g12 | 12 | 12 (authored 0, gen 12) | 0 | 9264 | 2736 | 3 | 12/12 | 12/12 | 12/12 | 12/12 |
| family.kelp.canopy | a0/g13 | 13 | 13 (authored 0, gen 13) | 0 | 9358 | 642 | 3 | 13/13 | 13/13 | 13/13 | 13/13 |
| family.kelp.abyssal | a0/g14 | 14 | 14 (authored 0, gen 14) | 0 | 8944 | 56 | 3 | 14/14 | 14/14 | 14/14 | 14/14 |
| family.coral.low | a0/g6 | 6 | 6 (authored 0, gen 6) | 0 | 6096 | 904 | 3 | 6/6 | 6/6 | 6/6 | 6/6 |
| family.coral.branching | a0/g6 | 6 | 6 (authored 0, gen 6) | 0 | 6580 | 5420 | 3 | 6/6 | 6/6 | 6/6 | 6/6 |
| family.coral.massive | a0/g6 | 6 | 6 (authored 0, gen 6) | 0 | 6296 | 2704 | 3 | 6/6 | 6/6 | 6/6 | 6/6 |
| family.coral.plate | a0/g6 | 6 | 6 (authored 0, gen 6) | 0 | 1352 | 7148 | 3 | 6/6 | 6/6 | 6/6 | 6/6 |
| family.coral.brittle | a0/g10 | 10 | 10 (authored 0, gen 10) | 0 | 7160 | 2340 | 3 | 10/10 | 10/10 | 10/10 | 10/10 |

## family.kelp.tall - Kelp Tall

- Coverage: `a0/g14`
- Expected linked real finals: `14`
- Linked final-ready: `14`
- Linked real finals: `14`
- Linked placeholders: `0`
- Max budget triangles: `7938`
- Triangle budget limit: `8000`
- Triangle headroom: `62`
- Minimum recommended triangles: `360`
- Max renderer count: `3`
- Renderer budget limit: `12`
- Material-contract prefabs: `14/14`
- Exact LOD contract prefabs: `14/14`
- Prefabs meeting fidelity floor: `14/14`
- Prefabs:
  - `GEN_family_kelp_tall__banner` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__banner` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4422 | weight=2 | scale=90-108 | lodTriangles=4422/1614/920 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__banner.prefab`
  - `GEN_family_kelp_tall__broadleaf__s110-170` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__broadleaf` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=7676 | weight=2 | scale=110-170* | lodTriangles=7676/2168/732 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__broadleaf__s110-170.prefab`
  - `GEN_family_kelp_tall__colossus__s160-240` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__colossus` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6980 | weight=2 | scale=160-240* | lodTriangles=6980/2148/1286 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__colossus__s160-240.prefab`
  - `GEN_family_kelp_tall__frondcrest__s105-165` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__frondcrest` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4622 | weight=2 | scale=105-165* | lodTriangles=4622/1450/932 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__frondcrest__s105-165.prefab`
  - `GEN_family_kelp_tall__lamina` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lamina` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6408 | weight=2 | scale=90-108 | lodTriangles=6408/2298/852 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lamina.prefab`
  - `GEN_family_kelp_tall__lance` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lance` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4578 | weight=2 | scale=90-108 | lodTriangles=4578/2024/712 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lance.prefab`
  - `GEN_family_kelp_tall__lean` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__lean` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4188 | weight=2 | scale=90-108 | lodTriangles=4188/1786/620 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__lean.prefab`
  - `GEN_family_kelp_tall__paddle__s90-150` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__paddle` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6684 | weight=2 | scale=90-150* | lodTriangles=6684/1808/1084 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__paddle__s90-150.prefab`
  - `GEN_family_kelp_tall__ribbon` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__ribbon` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5720 | weight=2 | scale=90-108 | lodTriangles=5720/2504/932 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__ribbon.prefab`
  - `GEN_family_kelp_tall__rope` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__rope` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4480 | weight=2 | scale=90-108 | lodTriangles=4480/1896/688 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__rope.prefab`
  - `GEN_family_kelp_tall__sail__s115-175` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__sail` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5464 | weight=2 | scale=115-175* | lodTriangles=5464/1060/502 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__sail__s115-175.prefab`
  - `GEN_family_kelp_tall__seedling__s55-90` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__seedling` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1666 | weight=2 | scale=55-90* | lodTriangles=1666/676/164 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__seedling__s55-90.prefab`
  - `GEN_family_kelp_tall__stalk` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__stalk` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5376 | weight=2 | scale=90-108 | lodTriangles=5376/2416/900 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__stalk.prefab`
  - `GEN_family_kelp_tall__tower__s130-185` generated | variantId=`family.kelp.tall.final.flora.gen_family_kelp_tall__tower` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=7938 | weight=2 | scale=130-185* | lodTriangles=7938/2414/1484 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_tall/GEN_family_kelp_tall__tower__s130-185.prefab`

## family.kelp.patch.dense - Kelp Patch Dense

- Coverage: `a0/g12`
- Expected linked real finals: `12`
- Linked final-ready: `12`
- Linked real finals: `12`
- Linked placeholders: `0`
- Max budget triangles: `9264`
- Triangle budget limit: `12000`
- Triangle headroom: `2736`
- Minimum recommended triangles: `320`
- Max renderer count: `3`
- Renderer budget limit: `18`
- Material-contract prefabs: `12/12`
- Exact LOD contract prefabs: `12/12`
- Prefabs meeting fidelity floor: `12/12`
- Prefabs:
  - `GEN_family_kelp_patch_dense__bladder__s80-135` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__bladder` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6552 | weight=1 | scale=80-135* | lodTriangles=6552/3056/1180 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__bladder__s80-135.prefab`
  - `GEN_family_kelp_patch_dense__brush` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__brush` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8688 | weight=1 | scale=94-108 | lodTriangles=8688/4388/1296 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__brush.prefab`
  - `GEN_family_kelp_patch_dense__drape` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__drape` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=7116 | weight=1 | scale=94-108 | lodTriangles=7116/3732/1008 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__drape.prefab`
  - `GEN_family_kelp_patch_dense__frilltuft__s75-125` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__frilltuft` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8648 | weight=1 | scale=75-125* | lodTriangles=8648/4412/1512 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__frilltuft__s75-125.prefab`
  - `GEN_family_kelp_patch_dense__nest__s65-105` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__nest` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8388 | weight=1 | scale=65-105* | lodTriangles=8388/3772/1256 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__nest__s65-105.prefab`
  - `GEN_family_kelp_patch_dense__paddlespray__s70-120` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__paddlespray` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6752 | weight=1 | scale=70-120* | lodTriangles=6752/3164/1344 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__paddlespray__s70-120.prefab`
  - `GEN_family_kelp_patch_dense__patch` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5712 | weight=1 | scale=94-108 | lodTriangles=5712/3000/792 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch.prefab`
  - `GEN_family_kelp_patch_dense__patch_tall` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__patch_tall` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8808 | weight=1 | scale=94-108 | lodTriangles=8808/4532/1572 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__patch_tall.prefab`
  - `GEN_family_kelp_patch_dense__ring` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__ring` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8144 | weight=1 | scale=94-108 | lodTriangles=8144/4412/1332 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__ring.prefab`
  - `GEN_family_kelp_patch_dense__sheet` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__sheet` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6498 | weight=1 | scale=94-108 | lodTriangles=6498/3312/1008 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__sheet.prefab`
  - `GEN_family_kelp_patch_dense__sheetwall__s120-185` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__sheetwall` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=9264 | weight=1 | scale=120-185* | lodTriangles=9264/4532/1572 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__sheetwall__s120-185.prefab`
  - `GEN_family_kelp_patch_dense__tuft` generated | variantId=`family.kelp.patch.dense.final.flora.gen_family_kelp_patch_dense__tuft` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8704 | weight=1 | scale=94-108 | lodTriangles=8704/4720/1460 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_patch_dense/GEN_family_kelp_patch_dense__tuft.prefab`

## family.kelp.canopy - Kelp Canopy

- Coverage: `a0/g13`
- Expected linked real finals: `13`
- Linked final-ready: `13`
- Linked real finals: `13`
- Linked placeholders: `0`
- Max budget triangles: `9358`
- Triangle budget limit: `10000`
- Triangle headroom: `642`
- Minimum recommended triangles: `460`
- Max renderer count: `3`
- Renderer budget limit: `14`
- Material-contract prefabs: `13/13`
- Exact LOD contract prefabs: `13/13`
- Prefabs meeting fidelity floor: `13/13`
- Prefabs:
  - `GEN_family_kelp_canopy__crown` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__crown` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3708 | weight=1 | scale=94-106 | lodTriangles=3708/1908/1104 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__crown.prefab`
  - `GEN_family_kelp_canopy__fan` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__fan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=2972 | weight=1 | scale=94-106 | lodTriangles=2972/1768/928 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__fan.prefab`
  - `GEN_family_kelp_canopy__featherfan__s120-200` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__featherfan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6462 | weight=1 | scale=120-200* | lodTriangles=6462/1596/1056 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__featherfan__s120-200.prefab`
  - `GEN_family_kelp_canopy__frond` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__frond` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=2604 | weight=1 | scale=94-106 | lodTriangles=2604/1224/558 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__frond.prefab`
  - `GEN_family_kelp_canopy__laminaria__s105-165` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__laminaria` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4214 | weight=1 | scale=105-165* | lodTriangles=4214/1684/972 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__laminaria__s105-165.prefab`
  - `GEN_family_kelp_canopy__mantle` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__mantle` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4234 | weight=1 | scale=94-106 | lodTriangles=4234/1924/1120 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__mantle.prefab`
  - `GEN_family_kelp_canopy__oar__s110-180` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__oar` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6570 | weight=1 | scale=110-180* | lodTriangles=6570/1900/1056 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__oar__s110-180.prefab`
  - `GEN_family_kelp_canopy__paddlefan__s120-190` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__paddlefan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=9358 | weight=1 | scale=120-190* | lodTriangles=9358/2012/968 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__paddlefan__s120-190.prefab`
  - `GEN_family_kelp_canopy__rosette` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__rosette` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4334 | weight=1 | scale=94-106 | lodTriangles=4334/2696/1046 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__rosette.prefab`
  - `GEN_family_kelp_canopy__sheetwall__s150-230` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__sheetwall` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6712 | weight=1 | scale=150-230* | lodTriangles=6712/2074/1180 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__sheetwall__s150-230.prefab`
  - `GEN_family_kelp_canopy__splay` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__splay` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3556 | weight=1 | scale=94-106 | lodTriangles=3556/2330/844 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__splay.prefab`
  - `GEN_family_kelp_canopy__tapestry__s160-240` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__tapestry` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8424 | weight=1 | scale=160-240* | lodTriangles=8424/2496/1000 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__tapestry__s160-240.prefab`
  - `GEN_family_kelp_canopy__veil` generated | variantId=`family.kelp.canopy.final.flora.gen_family_kelp_canopy__veil` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4406 | weight=1 | scale=94-106 | lodTriangles=4406/1912/1112 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_canopy/GEN_family_kelp_canopy__veil.prefab`

## family.kelp.abyssal - Kelp Abyssal

- Coverage: `a0/g14`
- Expected linked real finals: `14`
- Linked final-ready: `14`
- Linked real finals: `14`
- Linked placeholders: `0`
- Max budget triangles: `8944`
- Triangle budget limit: `9000`
- Triangle headroom: `56`
- Minimum recommended triangles: `380`
- Max renderer count: `3`
- Renderer budget limit: `14`
- Material-contract prefabs: `14/14`
- Exact LOD contract prefabs: `14/14`
- Prefabs meeting fidelity floor: `14/14`
- Prefabs:
  - `GEN_family_kelp_abyssal__braid` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__braid` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6080 | weight=1 | scale=96-108 | lodTriangles=6080/2796/1072 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__braid.prefab`
  - `GEN_family_kelp_abyssal__cathedral__s140-240` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__cathedral` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8944 | weight=1 | scale=140-240* | lodTriangles=8944/3768/1288 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__cathedral__s140-240.prefab`
  - `GEN_family_kelp_abyssal__cowl__s110-180` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__cowl` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=8888 | weight=1 | scale=110-180* | lodTriangles=8888/1742/1012 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__cowl__s110-180.prefab`
  - `GEN_family_kelp_abyssal__lantern__s100-180` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__lantern` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=7216 | weight=1 | scale=100-180* | lodTriangles=7216/1854/1036 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__lantern__s100-180.prefab`
  - `GEN_family_kelp_abyssal__mantle` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__mantle` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4062 | weight=1 | scale=96-108 | lodTriangles=4062/1694/1012 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__mantle.prefab`
  - `GEN_family_kelp_abyssal__nodule` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__nodule` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4546 | weight=1 | scale=96-108 | lodTriangles=4546/1968/708 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__nodule.prefab`
  - `GEN_family_kelp_abyssal__pennant` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__pennant` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4924 | weight=1 | scale=96-108 | lodTriangles=4924/1750/1096 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__pennant.prefab`
  - `GEN_family_kelp_abyssal__petal__s100-170` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__petal` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6898 | weight=1 | scale=100-170* | lodTriangles=6898/1702/920 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__petal__s100-170.prefab`
  - `GEN_family_kelp_abyssal__reed__s80-140` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__reed` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=2924 | weight=1 | scale=80-140* | lodTriangles=2924/1248/402 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__reed__s80-140.prefab`
  - `GEN_family_kelp_abyssal__shroud` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__shroud` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3554 | weight=1 | scale=96-108 | lodTriangles=3554/1408/838 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__shroud.prefab`
  - `GEN_family_kelp_abyssal__strap` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__strap` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4262 | weight=1 | scale=96-108 | lodTriangles=4262/1896/668 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__strap.prefab`
  - `GEN_family_kelp_abyssal__tatterveil__s110-185` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__tatterveil` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4912 | weight=1 | scale=110-185* | lodTriangles=4912/1598/1012 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__tatterveil__s110-185.prefab`
  - `GEN_family_kelp_abyssal__veilwall__s150-240` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__veilwall` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5576 | weight=1 | scale=150-240* | lodTriangles=5576/1048/494 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__veilwall__s150-240.prefab`
  - `GEN_family_kelp_abyssal__whip` generated | variantId=`family.kelp.abyssal.final.flora.gen_family_kelp_abyssal__whip` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3980 | weight=1 | scale=96-108 | lodTriangles=3980/1848/672 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_kelp_abyssal/GEN_family_kelp_abyssal__whip.prefab`

## family.coral.low - Coral Low

- Coverage: `a0/g6`
- Expected linked real finals: `6`
- Linked final-ready: `6`
- Linked real finals: `6`
- Linked placeholders: `0`
- Max budget triangles: `6096`
- Triangle budget limit: `7000`
- Triangle headroom: `904`
- Minimum recommended triangles: `900`
- Max renderer count: `3`
- Renderer budget limit: `10`
- Material-contract prefabs: `6/6`
- Exact LOD contract prefabs: `6/6`
- Prefabs meeting fidelity floor: `6/6`
- Prefabs:
  - `GEN_family_coral_low__bed` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__bed` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4660 | weight=2 | scale=92-106 | lodTriangles=4660/2364/1312 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__bed.prefab`
  - `GEN_family_coral_low__knoll` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__knoll` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4888 | weight=2 | scale=92-106 | lodTriangles=4888/2168/1192 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__knoll.prefab`
  - `GEN_family_coral_low__mound` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__mound` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5932 | weight=2 | scale=92-106 | lodTriangles=5932/2808/1620 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__mound.prefab`
  - `GEN_family_coral_low__plate` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__plate` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3136 | weight=2 | scale=92-106 | lodTriangles=3136/1600/800 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__plate.prefab`
  - `GEN_family_coral_low__saucer` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__saucer` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3856 | weight=2 | scale=92-106 | lodTriangles=3856/2128/1128 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__saucer.prefab`
  - `GEN_family_coral_low__spread` generated | variantId=`family.coral.low.final.flora.gen_family_coral_low__spread` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6096 | weight=2 | scale=92-106 | lodTriangles=6096/3416/2044 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_low/GEN_family_coral_low__spread.prefab`

## family.coral.branching - Coral Branching

- Coverage: `a0/g6`
- Expected linked real finals: `6`
- Linked final-ready: `6`
- Linked real finals: `6`
- Linked placeholders: `0`
- Max budget triangles: `6580`
- Triangle budget limit: `12000`
- Triangle headroom: `5420`
- Minimum recommended triangles: `800`
- Max renderer count: `3`
- Renderer budget limit: `16`
- Material-contract prefabs: `6/6`
- Exact LOD contract prefabs: `6/6`
- Prefabs meeting fidelity floor: `6/6`
- Prefabs:
  - `GEN_family_coral_branching__bouquet` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__bouquet` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5856 | weight=1 | scale=94-108 | lodTriangles=5856/2176/764 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__bouquet.prefab`
  - `GEN_family_coral_branching__branch` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__branch` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4394 | weight=1 | scale=94-108 | lodTriangles=4394/1446/500 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__branch.prefab`
  - `GEN_family_coral_branching__crest` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__crest` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6580 | weight=1 | scale=94-108 | lodTriangles=6580/2036/748 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__crest.prefab`
  - `GEN_family_coral_branching__fan` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__fan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5866 | weight=1 | scale=94-108 | lodTriangles=5866/1734/648 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__fan.prefab`
  - `GEN_family_coral_branching__mass` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__mass` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=2794 | weight=1 | scale=94-108 | lodTriangles=2794/1054/620 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__mass.prefab`
  - `GEN_family_coral_branching__thicket` generated | variantId=`family.coral.branching.final.flora.gen_family_coral_branching__thicket` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3500 | weight=1 | scale=94-108 | lodTriangles=3500/1416/796 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_branching/GEN_family_coral_branching__thicket.prefab`

## family.coral.massive - Coral Massive

- Coverage: `a0/g6`
- Expected linked real finals: `6`
- Linked final-ready: `6`
- Linked real finals: `6`
- Linked placeholders: `0`
- Max budget triangles: `6296`
- Triangle budget limit: `9000`
- Triangle headroom: `2704`
- Minimum recommended triangles: `1100`
- Max renderer count: `3`
- Renderer budget limit: `12`
- Material-contract prefabs: `6/6`
- Exact LOD contract prefabs: `6/6`
- Prefabs meeting fidelity floor: `6/6`
- Prefabs:
  - `GEN_family_coral_massive__boulder` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__boulder` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5336 | weight=1 | scale=95-105 | lodTriangles=5336/2768/1484 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__boulder.prefab`
  - `GEN_family_coral_massive__buttress` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__buttress` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6296 | weight=1 | scale=95-105 | lodTriangles=6296/3488/1976 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__buttress.prefab`
  - `GEN_family_coral_massive__dome` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__dome` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4936 | weight=1 | scale=95-105 | lodTriangles=4936/2656/1400 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__dome.prefab`
  - `GEN_family_coral_massive__head` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__head` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4184 | weight=1 | scale=95-105 | lodTriangles=4184/2096/1040 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__head.prefab`
  - `GEN_family_coral_massive__lobed` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__lobed` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5096 | weight=1 | scale=95-105 | lodTriangles=5096/2816/1480 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__lobed.prefab`
  - `GEN_family_coral_massive__porous` generated | variantId=`family.coral.massive.final.flora.gen_family_coral_massive__porous` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4600 | weight=1 | scale=95-105 | lodTriangles=4600/2448/1244 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_massive/GEN_family_coral_massive__porous.prefab`

## family.coral.plate - Coral Plate

- Coverage: `a0/g6`
- Expected linked real finals: `6`
- Linked final-ready: `6`
- Linked real finals: `6`
- Linked placeholders: `0`
- Max budget triangles: `1352`
- Triangle budget limit: `8500`
- Triangle headroom: `7148`
- Minimum recommended triangles: `220`
- Max renderer count: `3`
- Renderer budget limit: `12`
- Material-contract prefabs: `6/6`
- Exact LOD contract prefabs: `6/6`
- Prefabs meeting fidelity floor: `6/6`
- Prefabs:
  - `GEN_family_coral_plate__bastion` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__bastion` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1352 | weight=1 | scale=96-104 | lodTriangles=1352/892/516 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__bastion.prefab`
  - `GEN_family_coral_plate__canopy` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__canopy` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=992 | weight=1 | scale=96-104 | lodTriangles=992/708/596 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__canopy.prefab`
  - `GEN_family_coral_plate__ledge` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__ledge` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=822 | weight=1 | scale=96-104 | lodTriangles=822/598/398 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__ledge.prefab`
  - `GEN_family_coral_plate__shelf` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__shelf` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1014 | weight=1 | scale=96-104 | lodTriangles=1014/646/446 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__shelf.prefab`
  - `GEN_family_coral_plate__stack` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__stack` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=964 | weight=1 | scale=96-104 | lodTriangles=964/620/360 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__stack.prefab`
  - `GEN_family_coral_plate__terrace` generated | variantId=`family.coral.plate.final.flora.gen_family_coral_plate__terrace` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=1352 | weight=1 | scale=96-104 | lodTriangles=1352/892/516 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_plate/GEN_family_coral_plate__terrace.prefab`

## family.coral.brittle - Coral Brittle

- Coverage: `a0/g10`
- Expected linked real finals: `10`
- Linked final-ready: `10`
- Linked real finals: `10`
- Linked placeholders: `0`
- Max budget triangles: `7160`
- Triangle budget limit: `9500`
- Triangle headroom: `2340`
- Minimum recommended triangles: `720`
- Max renderer count: `3`
- Renderer budget limit: `14`
- Material-contract prefabs: `10/10`
- Exact LOD contract prefabs: `10/10`
- Prefabs meeting fidelity floor: `10/10`
- Prefabs:
  - `GEN_family_coral_brittle__candelabra` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__candelabra` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6542 | weight=1 | scale=94-108 | lodTriangles=6542/2490/934 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__candelabra.prefab`
  - `GEN_family_coral_brittle__cathedral` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__cathedral` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6966 | weight=1 | scale=94-108 | lodTriangles=6966/2722/1030 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__cathedral.prefab`
  - `GEN_family_coral_brittle__crown` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__crown` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5444 | weight=1 | scale=94-108 | lodTriangles=5444/1952/676 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__crown.prefab`
  - `GEN_family_coral_brittle__fan` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__fan` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5298 | weight=1 | scale=94-108 | lodTriangles=5298/1506/548 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__fan.prefab`
  - `GEN_family_coral_brittle__halo` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__halo` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=6000 | weight=1 | scale=94-108 | lodTriangles=6000/1800/648 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__halo.prefab`
  - `GEN_family_coral_brittle__lace` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__lace` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5866 | weight=1 | scale=94-108 | lodTriangles=5866/1734/648 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__lace.prefab`
  - `GEN_family_coral_brittle__spire` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__spire` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=4928 | weight=1 | scale=94-108 | lodTriangles=4928/1728/588 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__spire.prefab`
  - `GEN_family_coral_brittle__sprig` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__sprig` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=3890 | weight=1 | scale=94-108 | lodTriangles=3890/1230/412 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__sprig.prefab`
  - `GEN_family_coral_brittle__thicket` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__thicket` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=5444 | weight=1 | scale=94-108 | lodTriangles=5444/1952/676 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__thicket.prefab`
  - `GEN_family_coral_brittle__wreath` generated | variantId=`family.coral.brittle.final.flora.gen_family_coral_brittle__wreath` | renderers=3 | lodGroups=1 | lodLevels=3 | budgetTriangles=7160 | weight=1 | scale=94-108 | lodTriangles=7160/2272/848 | material=ok | lodContract=ok | renderState=ok | fidelity=ok | path=`Assets/_Project/Prefabs/Nature/Flora/Baked/family_coral_brittle/GEN_family_coral_brittle__wreath.prefab`
