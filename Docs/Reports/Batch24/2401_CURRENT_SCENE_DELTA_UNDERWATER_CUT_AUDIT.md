# 2401 Current Scene Delta Underwater Cut Audit

Evidence class: STATIC_SOURCE + STATIC_DOC + STATIC_SCREENSHOT.
Unity status: NOT RUN by 2401. No builds, imports, scene edits, material edits, or code edits.
Staleness risk: HIGH. Unity owner is actively editing; current YAML/diff can drift after this audit.

## Scope

Target screenshot: `Docs/Screenshots/MCP/h8_1474_diag_underwater_route_from_mcp.png`.

Observed static screenshot failure:
- hard horizontal dark shelf across the middle distance;
- clean rectangular blue wall on the right horizon;
- broad flat seabed foreground;
- isolated rocks/organic lumps read pasted, with weak substrate integration.

Authorities read:
- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `terrain.md`
- `water.md`
- `world.md`
- `Docs/Reports/Batch23/BATCH23_SYNTHESIS_FOR_CONTROLLER.md`
- `Docs/Reports/Batch23/2304_SCENE_SLAB_PATCHPACK.md`

Mandates read:
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Current Diff Factors

`git diff -- Assets/_Project/Scenes/02_HECTON_WORLD.unity` shows:
- scene fog changed from light surface fog `{0.68, 0.78, 0.86}` density `0.00105` to teal/dark fog `{0.035, 0.3, 0.34}` density `0.01`;
- many photic/flora/coral/rock prefabs were added or repositioned;
- previous slab suspects are still active/rendered in current YAML;
- multiple old/new inactive water curtains and surface sheets remain dangerous if reactivated.

Static interpretation: fog can tint and flatten the image, but the screenshot's clean geometric right-edge wall and hard horizontal cut match rendered geometry more strongly than fog alone.

## Ranked Suspect Table

| Rank | Object | YAML evidence | Material | Transform / scale | Why it matches screenshot | Safe Unity-owner test | Rollback |
|---:|---|---|---|---|---|---|---|
| 1 | `H8_DEPTH_LOW_SHELF_1428` | line `9873`, active `1`, renderer `1`, layer `0`, built-in cube `10202` | `MAT_H8WorldAbyssRidge_1428`, GUID `b9e8da6f36ed4d9459efa10020f3397d`, opaque, no base texture, color `{0.018,0.075,0.078,1}` | pos `{0,-0.9,30}`, scale `{58,1.15,8}` | Huge horizontal slab at route distance. Best match for dark shelf and route cut. It can also hide terrain behind it, making the seabed read empty. | Disable only its MeshRenderer, capture same camera before/after. If the dark horizontal shelf disappears, replace with authored terrain/waterline mask, not a cube. | Re-enable renderer; active `1`, layer `0`, material GUID `b9e8...`, transform above. |
| 2 | `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428` | lines `7810`, `43603`, `16983`, `57101`, all active `1`, renderer `1`, layer `0`, built-in cube `10202` | `MAT_WorldShell_1428`, GUID `09da3dc87b1df5945a9996378de36940`, opaque, no base texture, color `{0.035,0.07,0.078,1}` | four strips at y `-0.07`, z `18.8..26`, each scale `{3.2,0.06,0.4}` | Series of rendered waterline occlusion cubes can form hard horizontal banding and expose rectangular blue gaps between/behind strips. They are named service geometry but render on visible layer 0. | Disable the four MeshRenderers as one group after rank 1 test. Capture route view. If blue wall/hard band changes, convert to camera-excluded service or shader/post fake. | Re-enable all four renderers; restore listed transforms/material. |
| 3 | `H8_DEPTH_CEILING_OCCLUSION_1428` | line `75112`, active `1`, renderer `1`, layer `0`, built-in cube `10202` | `MAT_H8WorldDeepAbyss_1428`, GUID `f3e2d325400cdbb408f84e6acc9de027`, opaque, no base texture, color `{0.005,0.03,0.032,1}` | pos `{-4,7.8,25}`, scale `{70,1,8}` | Massive overhead slab can create a false horizontal ceiling/occlusion edge in shallow underwater camera views and contribute to the hard dark upper cut. | Disable only this MeshRenderer after ranks 1-2. Capture shallow and route views. If it was hiding voids, restore and replace with non-rendered occlusion support. | Re-enable renderer; active `1`, material GUID `f3e2...`, transform above. |
| 4 | `NOIR_UPPER_PRESSURE_LID` | line `5040`, active `1`, renderer `1`, layer `0`, built-in cube `10202` | `MAT_H8WorldPressureVignette_1428`, GUID `1763ea6867c15774ea09e5c90cc8675b`, transparent queue `3000`, alpha `0.36`, no base texture | pos `{0,8.6,5}`, scale `{38,0.25,30}` | Transparent physical lid can sort as a visible slab, especially against water/sky. It is a geometry vignette, not a controlled post/fog route. | Disable MeshRenderer and capture the same underwater route. If contrast/cut improves, replace with post/fog/LUT route. | Re-enable renderer; material GUID `1763...`, transform above. |
| 5 | `H8_FloorCausticSoft_1443` | line `63830`, active `1`, renderer `1`, layer `0`, authored mesh `f715884a162ee6c4fbc2846cf6f8eac9` | `MAT_H8_FloorCausticSoft_1443`, GUID `dfaebc7c2bdb3ec44b4523487f34ce44` | pos `{0,0,0}`, scale `{1,1,1}` | Batch23 live note said this may be the visible yellow/white sheet. Current screenshot is less yellow than 1472/1473, but foreground bright planar caustic/seabed read still makes it a live test candidate. | Disable renderer after slab group tests. If the bright flat foreground loses sheet read, retune into subtle caustic breakup; do not delete without replacement. | Re-enable renderer; material GUID `dfa...`, mesh GUID `f715...`. |
| 6 | `H8_PhoticRouteTerrain_1464` | line `57558`, active `1`, renderer `1`, layer `0`, authored mesh `8b19b392692115a42b7888b54f1c3c7b` | `MAT_H8_PhoticRouteTerrain_1464`, GUID `bdbb2649ef167e74c9bc048ac189dd2c` | pos `{0,0,0}`, scale `{1,1,1}` | Main active photic terrain candidate for broad flat seabed. If its mesh/material lacks local relief or breakup, it explains the empty foreground once slab occluders are removed. | Capture wire/scene isolate or temporarily solo terrain with rocks off. Do not delete. Check whether mesh has route relief, substrate variation, and material breakup. | Restore original renderer/material. |
| 7 | `H8_PhoticBackRidge_1436` | line `1385`, active `1`, renderer `1`, layer `0`, authored mesh `b25462f2a6ad3364c9399b0f62075e43` | `MAT_H8_PhoticReefBasaltSand_1435`, GUID `7cd6ded339d18b4488af0c9f0ad9f50d` | pos `{0,0,0}`, scale `{1,1,1}` | Can be part of the horizon terrain mass. If too level, it reinforces the long flat dark horizontal read behind the foreground. | Isolate with rank 1/2 disabled to determine whether it is valid ridge or another flat band. | Restore renderer/material. |
| 8 | `H8_OrganicRubbleMounds_1444` / `H8_OrganicRubbleCaps_1445` | lines `18580`, `23919`, active `1`, renderer `1`, layer `0` | `MAT_H8_OrganicRubbleWet_1444` GUID `de276...`; `MAT_H8_RubbleStrata_1448` GUID `079f...` | parented under inactive/active pass roots; local pos `{0,0,0}`, scale `{1,1,1}` | Current screenshot rocks/lumps read pasted because contact shadow, substrate blending, density, and scale witnesses are weak. These meshes may be fine in isolation but fail composition against flat floor. | Disable/solo organic rubble after slab/terrain test. Capture with contact shadow/material debug. If pasted read vanishes, re-place with substrate masks/debris fans, not random isolated lumps. | Restore renderers/materials. |
| 9 | `H8_FAUNA_SHADOW_TAIL_*_1428` | multiple active rendered built-in cube `10202` tails, layer `0`, material `MAT_H8WorldFaunaSilhouette_1428` GUID `279fc1a8d3bb0c547ab1a0721c080e60` | dark silhouette material | small scales around y `2.2..4.5`, z `17.966..51.9572` | Unlikely source of the large wall, but active cube silhouette props can read as primitive pasted black shapes in the distance. | Group-disable only after primary slab tests. Verify whether any dark shapes remain useful as fauna silhouettes. | Re-enable renderers. |
| 10 | Inactive sheet/curtain watchlist: `H8_UnderwaterHorizonHaze_1437`, `H8_UnderwaterHazeCurtain_1454`, `H8_UnderwaterSurfaceSheet_1455`, `H8_PHOTIC_VOLUME_HAZE_CURTAINS_1429`, `H8_PHOTIC_CEILING_CAUSTIC_RIBBONS_1429` | current active `0` and/or renderer `0` | varied haze/sheet materials | mostly root pos `{0,0,0}` | Not current active offender from static YAML. Dangerous if a runtime script or Unity owner reactivates them. | Confirm not runtime-enabled during capture metadata/log. Do not blame without live proof. | Keep inactive unless a proof route exists. |

## Top Current Active Candidates For Horizontal Cut / Blue Wall

1. `H8_DEPTH_LOW_SHELF_1428`: largest active rendered cube slab at the right route depth.
2. `H8_WORLD_LOW_WATER_OCCLUSION_00..03_1428`: active rendered waterline strip group at the same z band.
3. `H8_DEPTH_CEILING_OCCLUSION_1428`: active rendered ceiling slab that can create a hard upper cut.
4. `NOIR_UPPER_PRESSURE_LID`: active transparent cube lid that can sort visibly.
5. `H8_FloorCausticSoft_1443`: active caustic/floor sheet candidate; prior live owner note keeps it in the first proof packet.

## Top Current Active Candidates For Empty Flat Seabed

1. `H8_PhoticRouteTerrain_1464`: main active photic route terrain.
2. `H8_PhoticBackRidge_1436`: active background terrain/ridge.
3. `H8_DEPTH_LOW_SHELF_1428`: can cover/hide route relief and make the visible floor read as a plane.
4. `H8_FloorCausticSoft_1443`: can visually flatten substrate if its caustic/pass mesh reads as a sheet.
5. `H8_OrganicRubbleMounds_1444` / `H8_OrganicRubbleCaps_1445`: isolated mounds make flatness more obvious when not blended into sediment/debris fans.

## Pasted / Primitive Rock And Biota Candidates

- `H8_OrganicRubbleMounds_1444` and `H8_OrganicRubbleCaps_1445`: active and likely visible as isolated foreground lumps.
- `Rock_Runtime`: active GPUInstancer manager with many rock prototypes, but `HectonRockManager` on the same object is disabled in static YAML. Static YAML does not prove which prototypes rendered in the screenshot.
- `H8_FAUNA_SHADOW_TAIL_*_1428`: active built-in cube silhouette props; small but primitive if visible.
- Added photic/coral prefab instances in the diff are less likely to create the blue wall, but may worsen pasted composition if placed without substrate/contact integration.

## Exact Unity-Owner Test Order

1. Baseline capture from the exact `h8_1474_diag_underwater_route_from_mcp.png` camera. Save metadata: active scene, camera transform, fog color/density, render queue/layer masks, enabled renderers for the suspect objects.
2. Disable only `H8_DEPTH_LOW_SHELF_1428` MeshRenderer. Capture before/after. Roll back before the next test unless it is proven.
3. Disable `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428` MeshRenderers as one group. Capture before/after. Roll back or convert to camera-excluded service if needed.
4. Disable `H8_DEPTH_CEILING_OCCLUSION_1428`. Capture shallow and route views.
5. Disable `NOIR_UPPER_PRESSURE_LID`, then `NOIR_LEFT_VIGNETTE_SLAB` and `NOIR_RIGHT_VIGNETTE_SLAB` as a separate test.
6. Disable `H8_FloorCausticSoft_1443`. If floor sheet read improves, retune into subtle caustics rather than deleting into empty seabed.
7. With slab suspects disabled, isolate `H8_PhoticRouteTerrain_1464` and `H8_PhoticBackRidge_1436` to inspect flatness. If terrain remains flat, it is a terrain authoring failure, not only an occlusion failure.
8. Toggle `H8_OrganicRubbleMounds_1444`, `H8_OrganicRubbleCaps_1445`, and rock prototypes to prove pasted-read contribution.

## Reject Boundaries

- Do not delete suspects blindly.
- Do not accept visible rendered primitive cubes/planes as product-facing waterline, ceiling, vignette, shelf, or occlusion art.
- Do not use darker fog as a fix. Current diff already darkened fog and the screenshot still shows geometric cuts.
- Do not treat this static audit as runtime proof.

## Quality Consequences

- Low: remove visible slabs while preserving route silhouettes, return path, ocean color, and material identity.
- Middle: replace slabs with authored terrain/water masks, sediment breakup, and controlled turbidity.
- High: spend recovered overdraw/primitive budget on wet rock response, local silt structure, caustic hints, and route landmarks.
- Ultra: add sensory density only after the hard wall, false ceiling, and flat seabed are gone.

## Verdict

The most likely current YAML offender is still visible active service/cheat geometry, not a shader-only defect. `H8_DEPTH_LOW_SHELF_1428` is the first disable target; `H8_WORLD_LOW_WATER_OCCLUSION_00..03_1428` is the strongest candidate for the rectangular blue wall/hard waterline strip group. `H8_PhoticRouteTerrain_1464` and active rubble/rock scatter explain the empty flat seabed and pasted-prop read after the wall is addressed.
