# Generated Flora/Geology Manifest Proof Packet 1858

Evidence class: STATIC_SOURCE  
Runtime proof: PENDING VERIFICATION  
Unity/import/render/profiler proof: not run by task rule.

## Authority Read

- Root/project: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`.
- Domain bibles: `terrain.md`, `world.md`, `water.md`, `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `PROCEDURAL_ASSET_PIPELINE.md`, `3DMODEL_FLORA_CORAL.md`, `3DMODEL_GEOLOGY_ROCKS.md`.
- Mandates: `QA_Evidence_Text_Filter_Audit.txt`, `REND_Instanced_Flora_Physics.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`, `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `REND_Terrain_VirtualTexturing.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`.
- Static audit: `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`, `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.json`, `Tools/GeneratedAssetProductionAudit.py`.

## Audit State Reconfirmed

Source: existing `1851_GENERATED_ASSET_PRODUCTION_AUDIT.json`, generated `2026-06-04T04:42:08.883838+00:00`.

- Packages scanned: 392.
- Fatal issues: 0.
- Error issues: 41.
- Warning issues: 1281.

Issue-code distribution:

- `MISSING_MANIFEST`: 338.
- `MISSING_NAMED_PROOF`: 338.
- `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`: 338.
- `SOURCE_ONLY_PACKAGE`: 249.
- `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH`: 21.
- `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH`: 20.
- `FAMILY_NO_REAL_FINAL_LINKS`: 18.

Family distribution:

- `baked_flora_prefabs`: 89 packages, 267 warnings.
- `bioforge_shallow_source_meshes`: 200 packages, 800 warnings.
- `world_procedural_geology_meshes`: 49 packages, 196 warnings.
- `final_prefab_roots`: 21 packages, 21 errors.
- `procedural_family_links`: 33 packages, 20 errors, 18 warnings.

Do not clear `SURFACE_SHALLOW_VISUAL_PROOF_PENDING` with text. It needs named render/screenshot artifacts.

## Static Folder Findings

### Baked Flora Prefabs

Path: `Assets/_Project/Prefabs/Nature/Flora/Baked`.

Static inventory:

- 89 `.prefab` files.
- 267 `.asset` mesh files.
- 366 `.meta` files.
- One local `.md`.

Representative package samples:

- `GEN_family_coral_branching__bouquet`: LOD0/1/2 present, runtime prefab present, shared material `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`, no manifest, no named proof.
- `GEN_family_coral_low__bed`: LOD0/1/2 present, runtime prefab present, shared material `MAT_family_coral_low.mat`, no manifest, no named proof.
- `GEN_family_coral_massive__boulder`: LOD0/1/2 present, runtime prefab present, shared material `MAT_family_coral_massive.mat`, no manifest, no named proof.
- `GEN_family_coral_plate__shelf`: LOD0/1/2 present, runtime prefab present, shared material `MAT_family_coral_plate.mat`, no manifest, no named proof.
- `GEN_family_kelp_canopy__tapestry__s160-240`: LOD0/1/2 present, runtime prefab present, shared material `MAT_family_kelp_canopy.mat`, no manifest, no named proof.
- `GEN_family_kelp_patch_dense__patch`: LOD0/1/2 present, runtime prefab present, shared material `MAT_family_kelp_patch_dense.mat`, no manifest, no named proof.
- `GEN_family_kelp_tall__colossus__s160-240`: LOD0/1/2 present, runtime prefab present, shared material `MAT_family_kelp_tall.mat`, no manifest, no named proof.

Primitive risk: representative static prefab samples did not expose Unity built-in primitive mesh GUID hits. This does not prove visual quality. It only reduces one false-completion risk.

### BioForge Shallow Source Meshes

Path: `Assets/_Project/Art/Generated/Flora/BioForge/Shallows`.

Static inventory:

- 600 `.asset` files.
- 603 `.meta` files.
- 200 audit packages.
- Grouping: `Kelp` 100 packages, `TubeCoral` 50 packages, `PorousRock` 50 packages.

Representative package samples:

- `GEN_Shallows_Kelp_000_Flora_5A110101`: LOD0/1/2 source assets present. Matching prefab/material references exist in audit, but the family is still marked `SOURCE_ONLY_PACKAGE`.
- `GEN_Shallows_TubeCoral_000_Flora_5A110001`: LOD0/1/2 source assets present. Matching prefab has LODGroup/material refs. No manifest, no named proof.
- `GEN_Shallows_PorousRock_000_Rock_ABFE290C`: LOD0/1/2 source assets present. Matching prefab sample includes a `MeshCollider` line and LODGroup/material refs, but no static proof that collider is budget-safe or not LOD0-derived.

Classification: production candidates only. They are not accepted until final package manifests, render proof, compact/high screenshots, material proof, and collider/proxy proof exist.

### WorldProceduralGeology Meshes

Path: `Assets/_Project/Art/Meshes/WorldProceduralGeology`.

Static inventory:

- 196 `.asset` files.
- 202 `.meta` files.
- 49 audit packages.
- Grouping: `RockFloor` 10, `RockCluster` 10, `RockShelf` 8, `CaveEntrance` 7, `LandmarkSpire` 7, `RockArch` 7.

Representative package samples:

- `CaveEntrance_00`: LOD0/1/2 plus `CaveEntrance_00_COL.asset`; no prefab, no material route, no manifest, no named proof.
- `LandmarkSpire_00`: LOD0/1/2 plus `LandmarkSpire_00_COL.asset`; no prefab, no material route, no manifest, no named proof.
- `RockArch_00`: LOD0/1/2 plus `RockArch_00_COL.asset`; no prefab, no material route, no manifest, no named proof.
- `RockCluster_00`: LOD0/1/2 plus `RockCluster_00_COL.asset`; no prefab, no material route, no manifest, no named proof.
- `RockShelf_00`: LOD0/1/2 plus `RockShelf_00_COL.asset`; no prefab, no material route, no manifest, no named proof.
- `RockFloor_00`: LOD0/1/2 plus `RockFloor_00_COL.asset`; no prefab, no material route, no manifest, no named proof.

Classification: source/library geology pieces with collider proxy evidence by filename only. Collider validity, triangle budget, navigation fit, material truth, silhouette quality, and route readability remain pending.

## Top Families By Visual/Game Value

Priority is based on surface/shallow route readability, first-exit visual impact, route landmark value, ecology density, and false-completion risk.

1. Coral carriers: `coral_branching`, `coral_brittle`, `coral_low`, `coral_massive`, `coral_plate`.
2. Kelp carriers: `kelp_canopy`, `kelp_tall`, `kelp_patch_dense`.
3. BioForge shallows: `Kelp`, `TubeCoral`, `PorousRock`.
4. Geology route anchors: `CaveEntrance`, `LandmarkSpire`, `RockArch`, `RockShelf`, `RockCluster`, `RockFloor`.
5. Medium-depth hero continuity: `kelp_abyssal`, cave entrances, spires, arches, shelves.

Alphabetical order is rejected as a proof sequence because it does not track player-facing route value.

## Required Manifest Schema

Every generated visual package needs a local manifest named against the asset stem.

Required fields:

- `assetStem`
- `family`
- `sourceRoot`
- `runtimePrefabPath`
- `lodMeshes`: `lod0`, `lod1`, `lod2`, optional `hlod`
- `colliderProxy`: path, type, primitive count or convex triangle count, explicit `none` with reason for non-colliding flora
- `materials`: shared material paths, shader family, material slots, SRP Batcher/instancing note
- `textures`: albedo, normal, MRAO, emission/detail, import role, channel packing, mip/streaming policy
- `uvOrProjection`: unwrap route or triplanar route, texel density, stretch, atlas padding, edge bleed
- `vertexColorContract`: flora R/G/B/A or geology R/G/B/A semantics
- `generation`: generator name/version, deterministic seed, `GlobalQualityWeight` bake value/range, source references
- `biomeRoute`: depth band, surface/shallow/medium/deep, route use, substrate/current/light logic
- `lodPolicy`: tri counts, decimation method, preserved silhouette/anchor/ore/vent flags, hysteresis/dither note
- `collisionPolicy`: visual mesh not used as production LOD0 collision; proxy route named
- `streamingPolicy`: Addressables group target, residency class, HLOD/impostor note
- `proofArtifacts`: validation report, flat-material render, final-material render, gameplay-distance capture, compact/high captures
- `auditState`: issue codes cleared by manifest, issue codes still pending
- `rejectionReview`: blurry/muddy/flat/primitive/low-poly/crayon/missing-material/LOD/collider checks
- `createdBy`, `reviewedBy`, `dateUtc`

Suggested manifest filename:

`MANIFEST_<assetStem>.md` or `MANIFEST_<assetStem>.json`.

## Named Proof Convention

`GeneratedAssetProductionAudit.py` currently finds named proof by scanning `Docs/Reports`, `Docs/Screenshots`, `Docs/AgentLogs`, and `Docs/Orchestration` for filenames containing the asset stem.

Use exact stem inclusion:

- `Docs/Reports/GeneratedAssets/PROOF_<assetStem>_validation.md`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__flat_material__compact.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__final_material__compact.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__final_material__high.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__gameplay_distance__normal.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__route_composition__normal.png`
- `Docs/Screenshots/GeneratedAssets/<assetStem>__wire_lod_collider__editor.png`

Filename must include the full `assetStem`. A generic folder name or family-only proof is not enough.

## Screenshot/Render Proof Requirements

Minimum by use case:

- Close inspection: flat material, final material, wireframe/LOD/collider overlay, neutral lighting, scale reference.
- Gameplay distance: player-height or vehicle-height shot at expected route distance, normal tier and compact tier.
- Route composition: asset inside route context with water, terrain, landmark, return path, and nearby ecology/industry cue.
- Low/Compact: strong silhouette, material identity, no alpha-blend field failure, no mud, no black-screen hiding.
- Middle: richer density and material response without route truth changes.
- High: richer near-field detail, better wetness/cavity/biolum response, longer LOD residency.
- Ultra: visual overkill only: denser scars, pores, strata, shafts, reflection, silt, flora sway. No new gameplay truth.

Proof cannot be a beauty shot that hides the mesh. It must show the asset can survive inspection.

## Warning Clearance Rules

Can be cleared by static manifest/proof-file creation:

- `MISSING_MANIFEST`: cleared only by a local manifest matching the asset stem and schema.
- `MISSING_NAMED_PROOF`: cleared only by named proof files whose filenames contain the full asset stem.

Requires Unity/render proof:

- `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`: cannot be cleared by text. Needs named screenshot/render proof against `TASTE.md`.

Cannot be solved by this packet:

- `SOURCE_ONLY_PACKAGE`: needs assembled production package or documented source-only status that future audit recognizes.
- `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH`: needs prefab/asset replacement; not a docs-only fix.
- `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH`: needs procedural family final links fixed.
- `FAMILY_NO_REAL_FINAL_LINKS`: needs real final-ready non-proxy prefab links.

## First Priority Proof Set

Future pass should handle 30 assets first:

- `GEN_family_coral_branching__bouquet`
- `GEN_family_coral_branching__fan`
- `GEN_family_coral_branching__thicket`
- `GEN_family_coral_brittle__candelabra`
- `GEN_family_coral_brittle__cathedral`
- `GEN_family_coral_brittle__fan`
- `GEN_family_coral_brittle__thicket`
- `GEN_family_coral_low__bed`
- `GEN_family_coral_low__mound`
- `GEN_family_coral_low__plate`
- `GEN_family_coral_massive__boulder`
- `GEN_family_coral_massive__buttress`
- `GEN_family_coral_massive__porous`
- `GEN_family_coral_plate__canopy`
- `GEN_family_coral_plate__shelf`
- `GEN_family_coral_plate__terrace`
- `GEN_family_kelp_canopy__laminaria__s105-165`
- `GEN_family_kelp_canopy__sheetwall__s150-230`
- `GEN_family_kelp_canopy__tapestry__s160-240`
- `GEN_family_kelp_canopy__windrow__s145-230`
- `GEN_family_kelp_patch_dense__frilltuft__s75-125`
- `GEN_family_kelp_patch_dense__patch`
- `GEN_family_kelp_patch_dense__patch_tall`
- `GEN_family_kelp_tall__colossus__s160-240`
- `GEN_family_kelp_tall__seedling__s55-90`
- `GEN_family_kelp_tall__tower__s130-185`
- `CaveEntrance_00`
- `LandmarkSpire_00`
- `RockArch_00`
- `RockShelf_00`

## Second Priority Medium-Depth Set

Medium-depth hero continuity should follow:

- `GEN_family_kelp_abyssal__cathedral__s140-240`
- `GEN_family_kelp_abyssal__cowl__s110-180`
- `GEN_family_kelp_abyssal__lantern__s100-180`
- `GEN_family_kelp_abyssal__petal__s100-170`
- `GEN_family_kelp_abyssal__tatterveil__s110-185`
- `GEN_family_kelp_abyssal__veilwall__s150-240`
- `CaveEntrance_01`
- `CaveEntrance_02`
- `LandmarkSpire_01`
- `LandmarkSpire_02`
- `RockArch_01`
- `RockArch_02`
- `RockCluster_00`
- `RockCluster_01`
- `RockShelf_01`
- `RockShelf_02`

## Rejection Criteria

Reject any future proof pass if it finds:

- blurry, muddy, flat, primitive, low-poly, crayon-like, or placeholder-looking output;
- texture detail hiding a primitive silhouette;
- albedo with baked lighting or fake PBR channels;
- missing material identity, wrong MRAO semantics, wrong normal import, no mip proof;
- missing LOD chain, LOD pop without hysteresis/dither, or LODs that destroy anchor/route/ore/vent readability;
- LOD0 visual mesh used as production collision;
- dense flora alpha blend on compact lane;
- root/anchor vertices swaying like tips;
- rocks reading as smooth blobs without strata/fractures/wetness/mineral process;
- no compact-tier readability;
- no route composition proof;
- manifest claims exceeding evidence class.

## Audit Update Rules

Future agents should:

1. Add manifests/proof artifacts first.
2. Run no-Unity static checks listed below.
3. Re-run `GeneratedAssetProductionAudit.py` only when they own its output paths or are explicitly assigned audit regeneration.
4. Keep `SURFACE_SHALLOW_VISUAL_PROOF_PENDING` until named captures exist.
5. Do not downgrade visual gates to reduce warning count.
6. Never mark package accepted because `.asset`, `.prefab`, or `.mat` files exist.

## Performance And Scaling Preservation

Required future package consequences:

- Compact: preserve silhouette, material identity, route cue, atlas/shared material use, LOD2/HLOD, simple collision or no collision for flora, no alpha blend fields.
- Middle: add density, better material masks, richer scatter, stronger route composition, no new gameplay truth.
- High: longer LOD residency, better wetness/cavity/biolum response, richer geology/frond detail.
- Ultra: offline visual overkill: stronger bakes, scars, pores, strata, richer render response, denser near-field composition.

Systems must use continuous `GlobalQualityWeight` for density, texture size, LOD distance, bake precision, proof depth, and optional detail. It must not alter collision identity, harvest anchors, ore/vent identity, save identity, route ownership, or vertex color semantics.

Dense static flora and geology should prefer shared meshes/materials, GPU Resident Drawer/BRG-compatible renderer ownership, HLOD/impostors, dithered LOD fades, texture arrays or atlases, and Addressables residency policy. Any runtime cost claim needs profiler proof later.

## Minimal No-Unity Static Checks

After adding manifests/proof files, future agents can run read-only/static checks:

- Check manifest existence by stem under local package root.
- Check proof filenames include full `assetStem`.
- Check manifest contains required schema keys.
- Check prefab YAML contains `LODGroup` and shared material refs where a prefab exists.
- Check geology package has `_COL.asset` plus LOD0/1/2 assets.
- Check no proof file claims `PLAYMODE`, `PROFILER`, `PLAYER_BUILD`, or `UNITY_CONSOLE` without artifact path.
- Check no `0000000000000000e000000000000000` built-in primitive GUID in production Final prefabs.
- Check no `placeholder`, `debug`, `mock`, `temp`, `todo`, `crayon`, `flatcolor`, `lowpoly` markers in final asset/material names.

These checks prove text/package presence only. They do not prove beauty, runtime, import, material correctness, or performance.

## Packet Result

State: PACKET_COMPLETE_STATIC_SOURCE.

Missing evidence by design:

- Unity import proof.
- Render/screenshot proof.
- Profiler/GC/Frame Debugger proof.
- Collider runtime behavior proof.
- Material import/render proof.
- Compact/high visual acceptance proof.

Future acceptance remains blocked on named visual proof and, for source packages, assembled production manifests/prefabs where applicable.
