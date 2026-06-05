# 1901 Shallow Proof Artifact Priority Runbook

ID: 1901  
Mode: REPORT_ONLY_STATIC_VISUAL_PROOF_RUNBOOK  
Evidence class: STATIC_DOC  
Runtime/editor/render/profiler proof: PENDING UNITY

## Scope

This runbook prioritizes future proof artifacts for clearing the 338 `SURFACE_SHALLOW_VISUAL_PROOF_PENDING` warnings recorded by Batch18 generated-asset audits. It does not create manifests, screenshots, renders, Unity imports, profiler captures, or warning clearance.

Owned output matrix: `Docs/Reports/Batch18/1901_SHALLOW_PROOF_ARTIFACT_PRIORITY_MATRIX.csv`.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `world.md`
- `water.md` as water/ocean authority because root `ocean.md` was absent and `PROJECT_BIBLES.md` routes ocean/water presentation through `water.md`
- `terrain.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
- `Docs/Reports/Batch18/1858_GENERATED_FLORA_GEOLOGY_MANIFEST_PROOF_PACKET.md`
- `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md`
- `Docs/Reports/Batch18/1802_SURFACE_SHALLOW_ASSET_INVENTORY.md`
- `Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_PACKET.md`

Mandates read:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`

## Static Evidence Boundary

Static reports prove path inventory, audit warning categories, route intent, and proof-file naming rules only. They do not prove beauty, material response, route readability, compact-tier acceptance, collider safety, import health, GC, frame time, or Unity scene binding.

Text manifests can clear only text/file-presence warnings after a future audit recognizes them. `SURFACE_SHALLOW_VISUAL_PROOF_PENDING` requires real named screenshots/renders containing the full asset stem and cannot be cleared by this runbook.

## Audit Starting Point

Batch18 1851/1858 established:

- 338 `MISSING_MANIFEST`
- 338 `MISSING_NAMED_PROOF`
- 338 `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`
- 249 `SOURCE_ONLY_PACKAGE`
- 41 to 83 error-class primitive/final-link issues depending on audit scope

Priority order follows 1858, not alphabetical order:

1. Coral carriers: `coral_branching`, `coral_brittle`, `coral_low`, `coral_massive`, `coral_plate`.
2. Kelp carriers: `kelp_canopy`, `kelp_tall`, `kelp_patch_dense`.
3. Geology route anchors: `CaveEntrance`, `LandmarkSpire`, `RockArch`, `RockShelf`, `RockCluster`, `RockFloor`.
4. Medium-depth hero continuity: abyssal kelp, cave entrances, spires, arches, shelves.

## Proof Waves

First proof wave: 30 asset stems.  
Purpose: surface/photic-shallow first-route beauty, reef density, waterline/route silhouettes, and high false-completion risk.

Second proof wave: 16 asset stems.  
Purpose: medium-depth hero continuity and lower photic route anchors after the first surface/shallow wave has a proof pattern.

The full asset list, required manifest paths, required proof filenames, views, rejection conditions, performance rejection conditions, and status are in the CSV matrix.

## Required Manifest Paths

Future manifests must be local to the package family and include the full asset stem:

- Baked flora: `Assets/_Project/Prefabs/Nature/Flora/Baked/MANIFEST_<asset_stem>.md`
- World procedural geology: `Assets/_Project/Art/Meshes/WorldProceduralGeology/MANIFEST_<asset_stem>.md`

Minimum manifest fields:

- `assetStem`
- `family`
- `sourceRoot`
- `runtimePrefabPath`
- `lodMeshes`
- `colliderProxy`
- `materials`
- `textures`
- `uvOrProjection`
- `vertexColorContract`
- `generation`
- `biomeRoute`
- `lodPolicy`
- `collisionPolicy`
- `streamingPolicy`
- `proofArtifacts`
- `auditState`
- `rejectionReview`
- `createdBy`
- `reviewedBy`
- `dateUtc`

## Required Proof Filenames

Each asset stem needs exact filename inclusion. A family-only or folder-only proof is invalid.

For every row in the CSV:

- `Docs/Reports/GeneratedAssets/PROOF_<asset_stem>_validation.md`
- `Docs/Screenshots/GeneratedAssets/<asset_stem>__flat_clay__editor.png`
- `Docs/Screenshots/GeneratedAssets/<asset_stem>__final_material__compact.png`
- `Docs/Screenshots/GeneratedAssets/<asset_stem>__final_material__high.png`
- `Docs/Screenshots/GeneratedAssets/<asset_stem>__wire_lod_collider__editor.png`
- `Docs/Screenshots/GeneratedAssets/<asset_stem>__gameplay_distance__compact.png`
- `Docs/Screenshots/GeneratedAssets/<asset_stem>__gameplay_distance__high.png`
- `Docs/Screenshots/GeneratedAssets/<asset_stem>__route_composition__surface_shallow.png`

## Required Views

Every asset row requires:

- flat/clay: neutral clay or flat material view proving silhouette before texture detail
- final material: compact and high captures proving texture/material response
- wire/LOD/collider: editor debug view proving LOD chain, wire silhouette, and collider/proxy status
- compact gameplay distance: expected route distance on compact quality; must remain attractive and readable
- high gameplay distance: same route distance on high quality; must add sensory density without changing truth
- route composition: asset in route context with water, terrain, landmark/return path, and nearby ecology or industrial cue

## Capture Contexts From Prior Route Reports

Use these future Unity contexts where assets are present or being placed:

- Surface coast first read: player-eye view with ocean, coastline, Aegir, clouds/moons, route cue, and readable HUD/instruments.
- Waterline close-up: water color, wave normals, specular, foam, wet basalt, entry/exit line, and return landmark.
- Under-surface 5-20 m: bright colorful photic entry, `Starter_ReefField`, oxygen state, return direction, authored biota.
- 30-100 m route: forward and return views with lower photic silhouettes and instruments; no fog/dark masking.
- Aegir horizon: active skybox/Aegir/cloud/moon path in one frame; disabled/noir candidates are not proof.
- Industrial/resource/fabricator close set: machinery, copper, scrap, and fabricator must read physically.
- Quality comparison: same camera for compact/middle/high/ultra; compact remains attractive and readable.

## Warning Clearance Rule

Can be cleared by static text/file artifacts after future audit support:

- `MISSING_MANIFEST`: only by the required local manifest matching the full asset stem and schema.
- `MISSING_NAMED_PROOF`: only by named proof files whose filenames contain the full asset stem.

Requires real Unity/render screenshot proof:

- `SURFACE_SHALLOW_VISUAL_PROOF_PENDING`: must stay open until named screenshots/renders exist and pass `TASTE.md`, `quality.md`, `world.md`, `water.md`, `terrain.md`, `3dmodel.md`, and the family bibles. Text manifests do not clear it.

Cannot be solved by this report-only runbook:

- `SOURCE_ONLY_PACKAGE`
- `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH`
- `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH`
- `FAMILY_NO_REAL_FINAL_LINKS`
- `PRODUCT_FACE_PREFAB_BUILTIN_PRIMITIVE_MESH`

## Visual Rejection Gates

Reject future proof if any capture shows:

- muddy, blurry, flat, primitive, low-poly, crayon-like, placeholder, or below-Subnautica surface/shallow readability
- texture detail hiding a primitive silhouette
- bad alpha, dense alpha blend fields on compact, or card-like flora failure
- bad material channels, baked-light albedo, wrong MRAO semantics, wrong normal import, missing mip proof, or no material identity
- no LOD chain, LOD pop without hysteresis/dither, or LODs destroying anchor/route/ore/vent readability
- LOD0 visual mesh used as production collision
- rocks reading as smooth blobs without strata/fractures/wetness/mineral process
- coral/flora with no anchor, no biological taper, root vertices swaying like tips, or missing vertex color semantics
- dark/fog/silt/noir masking used to hide weak surface/shallow art
- route composition with no water, terrain, landmark, return path, ecology, industry, or player decision

## Performance Rejection Gates

Reject future proof if:

- compact capture is missing
- high capture adds gameplay truth instead of sensory density
- a runtime feature over 0.1 ms lacks profiler/load-shed proof
- Frame Debugger/RenderGraph proof is missing for changed water, material, instancing, VFX, or render passes
- GC proof is absent for runtime scatter, material updates, VFX triggers, or proof-route hot paths
- dense flora uses alpha blend instead of dithered clip/fade
- materials break SRP Batcher/instancing by clones or per-object mutation without proof
- collider/proxy evidence is missing or decorative LOD0 meshes drive collision
- texture/VRAM impact is claimed without memory/VRAM artifact

## Compact Middle High Ultra Consequences

Compact:

- Strong silhouettes, material identity, route cues, atlas/shared material use, LOD2/HLOD, simple collision or no collision for flora.
- No black-screen cover, flat water cover, muddy terrain, ugly fallback, or UI-only route readability.

Middle:

- More density, better material masks, richer scatter, stronger route composition, and cheap caustic/wetness cues.
- No new gameplay truth and no binary quality switch.

High:

- Longer LOD residency, better wetness/cavity/biolum response, richer geology/frond detail, stronger water and route composition.
- Profiler and Frame Debugger proof required for any runtime rendering change.

Ultra:

- Visual overkill only: stronger bakes, scars, pores, strata, shafts, reflection, silt, flora sway, and richer near-field composition.
- Same collision identity, harvest anchors, ore/vent identity, save identity, route ownership, DTO layout, and authority route as compact.

`GlobalQualityWeight` is continuous. It may scale density, texture size, LOD distance, bake precision, capture depth, and optional detail. It must not alter gameplay truth.

## Future Unity Capture Sequence

Do not run this during this report-only task. Future Unity owner sequence:

1. Confirm Unity slot is uncontested and no build/import/profiler conflict exists.
2. Open `02_HECTON_WORLD` through the project route only when allowed.
3. Capture baseline console and scene state before edits.
4. For each asset row, verify or place candidate without mutating third-party packages or unrelated assets.
5. Capture flat/clay, final material compact, final material high, wire/LOD/collider, compact gameplay distance, high gameplay distance, and route composition using exact filenames from the matrix.
6. Run Frame Debugger/RenderGraph and profiler/GC proof only for changed runtime/rendering routes.
7. Run a static audit only when the owner owns the output paths.
8. Keep any failed or missing visual proof as `PENDING UNITY`.

## Final State

STATIC RUNBOOK COMPLETE. No warnings cleared. `SURFACE_SHALLOW_VISUAL_PROOF_PENDING` remains open until future named screenshots/renders exist and pass the visual and performance rejection gates.
