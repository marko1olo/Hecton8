# 1870 Resource Pickup Visual Source Package

Agent ID: 1870
Evidence class: STATIC_SOURCE, STATIC_DOC
Unity/build/import/bake/PlayMode/screenshot/profiler execution: NOT RUN
Mutation boundary: docs/report outputs only. No source, prefab, asset, scene, `.meta`, binary, Unity menu, importer, bake, profiler, build, or Data Monolith action was run.

## Scope

This packet defines the source package needed to replace primitive product-face resource pickups:

- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_FiberKelp.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_HydrocarbonResin.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_MembraneTissue.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilicaShards.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilverOre.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SulfurClumps.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab`
- `Assets/_Project/Prefabs/Item_Titanium.prefab`

Detailed per-resource matrix: `Docs/Reports/Batch18/1870_RESOURCE_PICKUP_VISUAL_SOURCE_MATRIX.csv`.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `inventory.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `world.md`
- `Docs/Reports/Batch18/1864_PRODUCT_FACE_PRIMITIVE_REPLACEMENT_QUEUE.md`
- `Docs/Reports/Batch18/1864_PRODUCT_FACE_REPLACEMENT_MATRIX.csv`
- `Docs/Reports/Batch18/1866_POWER_RESOURCE_REAL_SOURCE_MESH_REQUIREMENTS.md`
- `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `Docs/Reports/Batch18/1868_PRODUCT_FACE_UNITY_VALIDATOR_GATE.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

Blocked authority:

- `resources.md` is requested by the task but is missing at `C:\hades\Hecton8\resources.md`. No replacement root bible was invented. `inventory.md` and item/resource data assets were used for the resource identity boundary.

## Static Findings

All nine target prefabs contain visible Unity built-in primitive mesh references:

| Resource | Prefab | Primitive evidence | Current material path | Data owner path |
|---|---|---|---|---|
| CopperOre | `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab` | cube `fileID 10202`, built-in primitive guid `0000000000000000e000000000000000`; `BoxCollider` 1/1/1 | `Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat` | `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` |
| FiberKelp | `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_FiberKelp.prefab` | plane `fileID 10208`, built-in primitive guid; `CapsuleCollider` radius 0.5 height 2 | `Assets/_Project/Art/Materials/Resources/Mat_Resource_Fiber.mat` | `Assets/_Project/Data/Items/Resources/Raw/Data_FiberKelp.asset` |
| HydrocarbonResin | `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_HydrocarbonResin.prefab` | plane `fileID 10208`, built-in primitive guid; `CapsuleCollider` radius 0.5 height 2 | `Assets/_Project/Art/Materials/Resources/Mat_Resource_Resin.mat` | `Assets/_Project/Data/Items/Resources/Raw/Data_HydrocarbonResin.asset` |
| MembraneTissue | `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_MembraneTissue.prefab` | sphere/capsule-class primitive `fileID 10207`, built-in primitive guid | `Assets/_Project/Art/Materials/Resources/Mat_Resource_Membrane.mat` | `Assets/_Project/Data/Items/Resources/Raw/Data_MembraneTissue.asset` |
| SilicaShards | `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilicaShards.prefab` | sphere/capsule-class primitive `fileID 10207`, built-in primitive guid | `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silica.mat` | `Assets/_Project/Data/Items/Resources/Raw/Data_SilicaShards.asset` |
| SilverOre | `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilverOre.prefab` | cube `fileID 10202`, built-in primitive guid; `BoxCollider` 1/1/1 | `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silver.mat` | `Assets/_Project/Data/Items/Resources/Raw/Data_SilverOre.asset` |
| SulfurClumps | `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SulfurClumps.prefab` | sphere/capsule-class primitive `fileID 10207`, built-in primitive guid | `Assets/_Project/Art/Materials/Resources/Mat_Resource_Sulfur.mat` | `Assets/_Project/Data/Items/Resources/Raw/Data_SulfurClumps.asset` |
| TitaniumScrap | `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab` | cube `fileID 10202`, built-in primitive guid; `BoxCollider` 1/1/1 | `Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat` | `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset` |
| Item_Titanium | `Assets/_Project/Prefabs/Item_Titanium.prefab` | cube `fileID 10202`, built-in primitive guid; `BoxCollider` 1/1/1 | unresolved material guid `31321ba15b8f8eb4c954353edc038b1d` | `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset` |

Copper note: the visual pickup route uses `CopperOre`, but the canonical data asset is `Data_Copper.asset`. Do not invent `Data_CopperOre.asset`.

`Item_Titanium.prefab` is a duplicate/legacy-looking root pickup that points to `Data_TitaniumScrap.asset` and has a `ScannableTarget` entry `resource.titanium_fragment`. It should be quarantined unless production-reference proof says this root path is still canonical.

## Candidate Source Routes

Real candidates:

- `Assets/_Project/Art/Generated/Flora/BioForge/Shallows/Kelp/GEN_Shallows_Kelp_*_LOD0/1/2.asset`: usable seed language for `FiberKelp` bundle only. Prior Batch 18 packets classify it as candidate-only because manifests, named proof, and captures are missing.
- `Assets/_Project/Art/Meshes/WorldProceduralGeology/RockSmallFloor`, `RockClusterMedium`, and related `*_LOD0/1/2.asset` plus `*_COL.asset`: usable host-rock/geology seed language for copper, silver, silica, sulfur, and ore-adjacent chunks only. Prior packets classify them as source-only, not accepted pickup source packages.
- `Assets/ScifiFacility/Models` from 1866 is a hard-surface kitbash candidate for manufactured scrap/power/interior work only after sanitization and repackaging. It is not a direct pickup source.

Rejected as final visible sources:

- `Assets/_Project/Prefabs/WorldProceduralProxy/*`.
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders/*`.
- Existing `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_*` primitive prefabs.
- Collision-only `*_COL.asset` meshes as visual sources.
- Data assets, recipes, item catalogs, biome resource plans, resource channels, and node templates.
- Generic rock replacement for all resources. Each resource needs physical identity, not recolor.

## Required Source Package

Required root: `Assets/_Project/Prefabs/Resources/Sources` as defined by 1866.

Each resource source package needs:

- `PFB_Resource_<Name>_Source.prefab` or equivalent explicit source prefab.
- Persistent `MESH_*_LOD0`, `MESH_*_LOD1`, `MESH_*_LOD2`, plus HLOD/impostor where fields need distance rendering.
- Shared `MAT_*` material assets, never runtime clones.
- `TX_*_Albedo`, `TX_*_Normal`, `TX_*_MRAO`, and optional `TX_*_Emission`, `TX_*_Detail`, or alpha/translucency masks.
- Manifest-owned MRAO channel semantics. Do not guess whether G is roughness or smoothness for a shader.
- `VIS_*` render children and `COL_*` collider/proxy children.
- Pickup trigger expectations separated from visual collision.
- LODGroup or documented HLOD/impostor route with dithered cross-fade and hysteresis.
- Static primitive scan proof showing no visible Unity built-in primitive mesh references.
- Screenshot/player-capture proof before any visual acceptance claim.

## Physical Identity Requirements

Resource silhouettes:

- CopperOre: copper-bearing fractured host-rock chunk with oxide streaks.
- FiberKelp: harvested kelp bundle, folded strips/fronds with thickness and ragged cuts.
- HydrocarbonResin: sticky amber/dark resin clump or pod fragment with sagging lobes and grit.
- MembraneTissue: torn wet membrane sheet with veins, folds, and cut edges.
- SilicaShards: angular milky/glassy shard cluster with fracture planes.
- SilverOre: darker host-rock ore chunk with silver veins, visually distinct from copper by seam shape and material response.
- SulfurClumps: brittle sulfur nodule/crystal cluster with vent residue.
- TitaniumScrap: bent/cut manufactured metal shard with bolt holes, paint remnants, salt, oil, and torn edges.
- Item_Titanium: either canonical TitaniumScrap visual if retained, or quarantine as legacy duplicate.

Material identity:

- Ore resources require host rock, mineral streaks/veins, fractured normals, cavity AO, wetness where underwater exposure demands it.
- Biological resources require wet tissue/kelp, vein or fiber normals, alpha/translucency only through dithered clip or proven shader routes, not alpha-blend spam.
- Resin requires oily/translucent material logic, embedded grit, wet response, and source pod residue.
- Scrap requires cut metal, paint, labels/stamps, scratches, salt, oil grime, bent plate normals, and exposed edges.

## Collider, Pickup, And Data Boundary

Resource pickups are dumb world proxies of item data. `DATA_Inventory_Resources_Items_SOA_Layout` requires world item proxies to resolve item truth by numeric item/template data, not smart object behavior or runtime string identity.

Required split:

- Visual mesh: `VIS_*`, material-rich, LOD-owned, no gameplay truth.
- Pickup trigger/collider: `COL_*`, coarse box/capsule/sphere/convex hull, no LOD0 visual MeshCollider.
- Data owner: `Data_*` item assets and future baked registry. Runtime must not mutate ScriptableObjects.
- Interaction owner: pickup/highlight components keep authority only as collection proxy, not item identity owner.

Current colliders are blockout-sized primitive colliders. The replacement source must preserve easy pickup readability without making visual triangles part of collision truth.

## LOD/HLOD And Continuous Quality

Single pickups:

- LOD0: near pickup silhouette and material identity.
- LOD1: preserved identity, reduced shards/fronds/veins.
- LOD2: coarse but still identifiable shape.

Resource fields:

- Use instancing/GPU Resident Drawer-friendly shared meshes/materials where possible.
- HLOD/impostor for dense fields beyond interaction range.
- Dithered cross-fade with hysteresis. No alpha-blend density fields on compact hardware.

Continuous `GlobalQualityWeight` consequences:

- Low/compact (`0.0` checkpoint): no ugly mode; resource silhouette, material family, pickup readability, shared material, cheap collider proxy, and compact readability are preserved.
- Middle: stronger normal/mask detail, decals/labels/residue where physical, longer LOD1 residency.
- High: richer wetness, mineral veins, fracture normals, glass/translucency response, longer LOD0.
- Ultra: micro chips, secondary fronds/folds, small bolts, scratches, micro bubbles, richer masks, and longer HLOD residency only. No item ID, recipe truth, collider truth, save identity, or authority route changes.

## First-20-Minute Priority

Priority resources:

1. `PFB_Resource_TitaniumScrap` and `Item_Titanium`: early fabrication and salvage identity. Current cube directly damages first route believability. `Item_Titanium` should be canonicalized or quarantined before it leaks duplicate primitive pickup logic.
2. `PFB_Resource_CopperOre`: early wiring, battery, signal, and tool recipes. Must keep `Data_Copper.asset` alias truth.
3. `PFB_Resource_FiberKelp`: early organic mesh/binding/suit work and shallow-zone beauty. Current plane contradicts harvested fiber.
4. `PFB_Resource_SilicaShards`: early optics/glass/viewports. Sphere primitive contradicts shard identity.
5. `PFB_Resource_HydrocarbonResin`: sealant/lubricant/pressure survival path. Needs resin pod/clump source.
6. `PFB_Resource_MembraneTissue`, `PFB_Resource_SilverOre`, `PFB_Resource_SulfurClumps`: progression resources with higher-route value; still product-face debt but after the immediate tool/repair loop.

Reason: the first 20 minutes must prove salvage, oxygen/pressure preparation, tool repair/fabrication, and route planning. Abstract colored primitives turn resources into currency and fail the three-pillar gate.

## Factory Blockers

1866 keeps `ResourceWorldBootstrapAuthoring` blocked until `Assets/_Project/Prefabs/Resources/Sources` has real source packages. Existing pickup prefabs under `Assets/_Project/Prefabs/Resources/Pickups` are not source proof because they are primitive product-face prefabs.

Blocked by missing source assets:

- No accepted ore chunk source package for CopperOre/SilverOre.
- No accepted silica shard source package.
- No accepted sulfur nodule source package.
- No accepted titanium scrap source package.
- No accepted resin clump/source pod pickup package.
- No accepted membrane tissue pickup source package.
- Kelp generated assets exist but are candidate-only until manifest/proof capture exists.

Unblocked only by:

- real source prefab path;
- mesh LOD path;
- material and texture role paths;
- collider/proxy split;
- manifest/proof;
- screenshots/captures when Unity is allowed later.

## Unresolved Material Or Texture Roles

- `Item_Titanium.prefab` material guid `31321ba15b8f8eb4c954353edc038b1d` did not resolve to a material `.meta` path in the static `Assets` meta search. It appears in unrelated prefabs/settings and must be resolved by Unity-side audit or replaced through canonical `Mat_Resource_Scrap.mat` if the prefab is retained.
- All resolved `Mat_Resource_*` paths are material-path proof only. They do not prove texture roles, MRAO channel semantics, import settings, normal maps, or visual quality.
- No source package manifest currently declares albedo/normal/MRAO/emission/detail/alpha roles for these pickups.

## Quarantine Recommendation

`Assets/_Project/Prefabs/Item_Titanium.prefab` should be quarantined as legacy unless production-reference proof shows it is required. If retained, it must become a canonical titanium scrap visual and share `Data_TitaniumScrap.asset` with the resource pickup. It must not keep a separate visual/data/material truth route.

No other listed pickup should be quarantined by default; they are canonical `Data_*` world prefab routes and need replacement source packages.

## Proof Ladder

Static source proof required before relink:

1. Source prefab path under `Assets/_Project/Prefabs/Resources/Sources`.
2. Mesh LOD0/LOD1/LOD2/HLOD or explicit no-HLOD reason.
3. Material paths and texture role manifest.
4. Collider/proxy child list.
5. Data owner path and stable ID.
6. Static primitive scan showing no visible built-in primitive mesh references.

Unity/player proof required before acceptance:

1. Prefab YAML after replacement.
2. Mesh/material/texture import report.
3. Collider/proxy proof.
4. Compact pickup screenshot.
5. Normal-tier/player capture at pickup distance.
6. Field/cluster capture for dense resource placements.
7. Profiler/Frame Debugger only if render path, shader, HLOD, instancing, or material variant route changes.

This packet does not claim visual acceptance, Unity acceptance, runtime proof, or profiler proof.

## Verification

Claim: all target prefabs still use Unity built-in primitive mesh references.
Evidence Class: STATIC_SOURCE.
Artifact: target prefab YAML.
Command or tool: `Select-String` / `rg` static text reads for `m_Mesh` and GUID `0000000000000000e000000000000000`.
Date: 2026-06-04.
Residual risk: static YAML only; Unity import/runtime state not proven.

Claim: material GUIDs were resolved where possible.
Evidence Class: STATIC_SOURCE.
Artifact: material `.meta` search under `Assets`.
Command or tool: `rg -l "guid: <guid>" Assets`.
Date: 2026-06-04.
Residual risk: `Item_Titanium` material guid unresolved; material import settings/texture roles not proven.

Claim: data owner paths were resolved.
Evidence Class: STATIC_SOURCE.
Artifact: `Assets/_Project/Data/Items/Resources/Raw/Data_*.asset`.
Command or tool: `Get-Content` static reads and GUID searches.
Date: 2026-06-04.
Residual risk: runtime registry/bake/Data Monolith state not proven.

Claim: reusable candidate source routes exist only as candidates, not accepted replacements.
Evidence Class: STATIC_DOC, STATIC_SOURCE.
Artifact: 1866 packet plus static file listing of BioForge kelp and WorldProceduralGeology assets.
Command or tool: `Get-ChildItem`, `rg --files`.
Date: 2026-06-04.
Residual risk: Unity visual quality and import validity not proven.

## Result

What was wrong: resource pickups are primitive product-face props. Current materials do not compensate for cube/plane/sphere silhouettes, and there is no accepted source package under the required resource source route.

What I did: wrote a static source package packet and matrix defining exact primitive evidence, material/data owners, candidate source routes, required silhouette/material identity, collider/proxy split, LOD/HLOD expectations, continuous quality consequences, first-20-minute priority, blockers, and proof ladder.

In-game result: PENDING VERIFICATION. Unity, screenshots, PlayMode, profiler, build, import, bake, and source relinks were forbidden.

What was verified: static YAML/docs/data/material-path evidence only.
