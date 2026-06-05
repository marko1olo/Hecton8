# 2304 Scene Slab Primitive Offender Static Patchpack

Evidence class: STATIC_SOURCE + STATIC_DOC + screenshot file inspection.
Unity status: NOT RUN. Play Mode/build/imports untouched.
Acceptance status: PENDING UNITY OWNER VERIFICATION.

## Evidence Read

- `Docs/Reports/Batch22/2202_UNDERWATER_PLANE_SLAB_OFFENDER_MATRIX.md`
- `Docs/Reports/Batch22/_2202_scene_candidates_raw.csv`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Material assets resolved from suspect GUIDs.
- Screenshots inspected:
  - `Docs/Screenshots/MCP/h8_1472_underwater_0_5m.png`
  - `Docs/Screenshots/MCP/h8_1472_underwater_20_50m_route.png`
  - `Docs/Screenshots/MCP/h8_1473_mainrt_underwater_0_5m.png`

## Screenshot Failure Summary

- `h8_1472_underwater_0_5m.png`: hard horizontal split; lower frame occupied by pale/yellow flat sheet; water/sky remains above. This reads as world-space sheet/slab, not accepted water presentation.
- `h8_1472_underwater_20_50m_route.png`: large pale sheet cuts through route and intersects/occludes rocks; flat seabed/route presentation fails photic and medium-depth visual floor.
- `h8_1473_mainrt_underwater_0_5m.png`: yellow/green slab dominates lower frame, with black/green banding at waterline/top edge. This remains product-facing primitive/slab failure.

## Static Drift Correction

Batch22 prose says `H8_DEPTH_LOW_SHELF_1428` uses beige `MAT_H8_SurfaceLittoralShelf_1430`. Current scene YAML says otherwise:

- `H8_DEPTH_LOW_SHELF_1428` line `9861`, active `1`, renderer `1`, mesh `10202` cube, material GUID `b9e8da6f36ed4d9459efa10020f3397d`.
- That GUID resolves to `Assets/_Project/Art/Materials/MAT_H8WorldAbyssRidge_1428.mat`, `_BaseColor {0.018, 0.075, 0.078, 1}`, empty `_BaseMap/_MainTex`.
- Beige `MAT_H8_SurfaceLittoralShelf_1430` exists at `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_SurfaceLittoralShelf_1430.mat`, GUID `8af264cdc665d554e8d1e7f9f09c0e6d`, `_BaseColor {0.68, 0.64, 0.5, 1}`, bound base/normal textures.
- Current scene references beige GUID only on `H8_SURFACE_LITTORAL_SHALLOW_SHELF_1430`, line `6580`, but that GameObject has `m_IsActive: 0`.

Interpretation: the captured yellow sheet may be from an older capture state, runtime/material override, stale report mapping, or another currently unproven render path. Static report must not claim exact live renderer identity beyond YAML evidence.

## Top Offender Decisions

| Priority | Object | Classification | Current static state | Decision | Why |
|---:|---|---|---|---|---|
| 1 | `H8_DEPTH_LOW_SHELF_1428` | shoreline art candidate / visible slab offender | Active `1`, renderer `1`, layer `0`, cube `10202`, pos `{0,-0.9,30}`, scale `{58,1.15,8}`, material `MAT_H8WorldAbyssRidge_1428` | Disable renderer for first proof pass; no deletion. If required, move to proof-hidden layer. | Geometry is the best static match for the huge horizontal sheet. Current material conflicts with Batch22 beige claim, so proof disable beats blind material edit. |
| 2 | `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428` | waterline service mesh / occlusion cheat candidate | All active `1`, renderer `1`, layer `0`, cube `10202`, y `-0.07`, material `MAT_WorldShell_1428` | Disable or layer-filter as one group after priority 1 proof. No deletion until replacement. | Likely source of hard black/green waterline strips and route slicing. Names imply service, but they are visible layer-0 rendered cubes. |
| 3 | `H8_DEPTH_CEILING_OCCLUSION_1428` | false ceiling / occlusion cheat candidate | Active `1`, renderer `1`, cube `10202`, pos `{-4,7.8,25}`, scale `{70,1,8}`, material `MAT_H8WorldDeepAbyss_1428` | Move to invisible occlusion-only layer if service; otherwise disable renderer. | Large active overhead slab can produce false ceiling/hard slicing from underwater cameras. |
| 4 | `NOIR_UPPER_PRESSURE_LID` | false ceiling / vignette cheat | Active `1`, renderer `1`, cube `10202`, pos `{0,8.6,5}`, scale `{38,0.25,30}`, transparent `MAT_H8WorldPressureVignette_1428` alpha `0.36` | Disable renderer test before retune; replace with post/fog/shader route if effect needed. | Transparent horizontal lid can sort as a visible slab and contribute to black/green waterline banding. |
| 5 | `NOIR_LEFT_VIGNETTE_SLAB`, `NOIR_RIGHT_VIGNETTE_SLAB` | debug/vignette side slab cheat | Active `1`, renderer `1`, cube `10202`, layer `0`, transparent pressure-vignette material | Move to underwater-camera-excluded/proof-hidden layer or replace with full-screen/post route. | Product-facing side geometry curtains are not acceptable final composition tools. |
| 6 | `H8_SURFACE_LITTORAL_SHALLOW_SHELF_1430` | shoreline art candidate / inactive beige risk | Inactive `0`, renderer `1`, authored mesh `47b9...`, beige `MAT_H8_SurfaceLittoralShelf_1430` | Keep inactive; if reactivated, inspect first. | Only current scene reference to the beige material that visually matches the yellow sheet, but static state says inactive. |
| 7 | `Water_Mass_Far_1428`, `Water_Mass_Mid_1428` | waterline service mesh / veil risk | Inactive `0`, renderer `1`, cube `10202`, huge vertical scales, transparent water veil material | Keep inactive; replace with authored fog/water volume if needed. | Dangerous if reactivated; not a current active source from static YAML. |
| 8 | `BLACK_WATER_PLANE`, `BASALT_SEABED` | old plane/proxy debt | Inactive `0`, renderer `0`, built-in plane `10209` | Leave inactive; never reactivate as final geometry. | Flat planes explain prior placeholder risk but are not active current source. |
| 9 | `ABYSS_SURFACE_CEILING`, `ABYSS_BLACKWATER_CEILING_1428` | false ceiling watchlist | Inactive `0`, renderer `0`, cube `10202` | Keep inactive unless a masked presentation route is proven. | Explicit ceiling planes are forbidden as visible surface/photic hide geometry. |
| 10 | `NOIR_FAR_WATER_CURTAIN_A/B` | curtain/haze watchlist | Inactive `0`, renderer `0`, cube `10202` | Keep inactive; replace with fog/post fake if needed. | Vertical curtain geometry can create visible slice edges if re-enabled. |

## Most Likely Sources By Artifact

Pale/yellow overhead or route sheet:

1. `H8_DEPTH_LOW_SHELF_1428`: first-disable geometry match; current material is dark, so Unity owner must inspect actual live binding/runtime override.
2. `H8_SURFACE_LITTORAL_SHALLOW_SHELF_1430`: best material/color match, but current `m_IsActive: 0`; do not blame without Unity proof.
3. `H8_UnderwaterSurfaceSheet_1455`: sheet-specific material, but current `m_IsActive: 0` and renderer `0`; watch only.
4. `Water_Mass_Far_1428` / `Water_Mass_Mid_1428`: huge sheet/veil geometry, but inactive.

Surface/coast black/green banding:

1. `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428`: active low waterline strips at y `-0.07`; dark opaque `MAT_WorldShell_1428`.
2. `H8_DEPTH_CEILING_OCCLUSION_1428`: active dark ceiling slab.
3. `NOIR_UPPER_PRESSURE_LID`: active transparent dark lid, queue `3000`, `ZWrite 0`.
4. `NOIR_LEFT_VIGNETTE_SLAB` / `NOIR_RIGHT_VIGNETTE_SLAB`: active transparent side slabs.

## Never Accept As Final Product-Facing Geometry

- Any active rendered built-in cube/plane slab named `*_SHELF_*`, `*_OCCLUSION_*`, `*_CEILING_*`, `*_LID`, `*_VIGNETTE_SLAB`, `*_CURTAIN_*`, `BLACK_WATER_PLANE`, or `BASALT_SEABED` when visible to gameplay cameras.
- Built-in cube `10202` or plane `10209` may exist only as hidden service/debug/proxy with renderer disabled or isolated from product cameras.
- If a visual substitute is needed, use authored terrain/water meshes, shader-space fog/waterline fakes, post/camera-space vignette, or masked volume presentation. Do not use exposed rectangular geometry.

## Service Geometry Caution

Do not delete these blindly:

- `H8_WORLD_LOW_WATER_OCCLUSION_*`
- `H8_DEPTH_CEILING_OCCLUSION_1428`
- `NOIR_UPPER_PRESSURE_LID`
- `NOIR_*_VIGNETTE_SLAB`
- `Water_Mass_*`

Reason: names and placement imply service/culling/presentation roles. Correct first action is renderer/layer isolation with rollback, not deletion. If they are needed, the Unity owner must convert them to hidden service layer or non-geometry camera/post/fog route.

## Staged Removal / Proof Order

1. Baseline: capture the exact rejected repro positions before touching anything:
   - `h8_1472_underwater_0_5m`
   - `h8_1472_underwater_20_50m_route`
   - `h8_1473_mainrt_underwater_0_5m`
2. Disable only `H8_DEPTH_LOW_SHELF_1428` MeshRenderer or move it to a proof-hidden layer. Capture all three repros. Roll back before next group if sheet persists or route void appears.
3. Disable/layer-filter `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428` as a group. Capture all three repros. If waterline banding improves but service gap appears, re-enable and replace with hidden/camera-excluded route.
4. Disable/layer-filter `H8_DEPTH_CEILING_OCCLUSION_1428`. Capture all three repros. If it was hiding a void, restore and replace with invisible occlusion/culling support, not visible geometry.
5. Disable `NOIR_UPPER_PRESSURE_LID`. Capture from shallow underwater and route view. Replace with post/fog/shader vignette if effect is still required.
6. Disable `NOIR_LEFT_VIGNETTE_SLAB` and `NOIR_RIGHT_VIGNETTE_SLAB`. Capture wide/oblique underwater route. Replace with full-screen/post route if needed.
7. Only after active sources are proven, inspect inactive watchlist before any reactivation.

## Rollback Data

| Object | Rollback active | Rollback renderer | Rollback layer | Rollback material | Rollback transform |
|---|---:|---:|---:|---|---|
| `H8_DEPTH_LOW_SHELF_1428` | `1` | `1` | `0` | `b9e8da6f36ed4d9459efa10020f3397d` / `MAT_H8WorldAbyssRidge_1428` | pos `{0,-0.9,30}`, scale `{58,1.15,8}` |
| `H8_WORLD_LOW_WATER_OCCLUSION_00_1428` | `1` | `1` | `0` | `09da3dc87b1df5945a9996378de36940` / `MAT_WorldShell_1428` | pos `{-7,-0.07,18.8}`, scale `{3.2,0.06,0.4}` |
| `H8_WORLD_LOW_WATER_OCCLUSION_01_1428` | `1` | `1` | `0` | `09da3dc87b1df5945a9996378de36940` / `MAT_WorldShell_1428` | pos `{-2.4,-0.07,21.199999}`, scale `{3.2,0.06,0.4}` |
| `H8_WORLD_LOW_WATER_OCCLUSION_02_1428` | `1` | `1` | `0` | `09da3dc87b1df5945a9996378de36940` / `MAT_WorldShell_1428` | pos `{2.1999998,-0.07,23.599998}`, scale `{3.2,0.06,0.4}` |
| `H8_WORLD_LOW_WATER_OCCLUSION_03_1428` | `1` | `1` | `0` | `09da3dc87b1df5945a9996378de36940` / `MAT_WorldShell_1428` | pos `{6.7999997,-0.07,26}`, scale `{3.2,0.06,0.4}` |
| `H8_DEPTH_CEILING_OCCLUSION_1428` | `1` | `1` | `0` | `f3e2d325400cdbb408f84e6acc9de027` / `MAT_H8WorldDeepAbyss_1428` | pos `{-4,7.8,25}`, scale `{70,1,8}` |
| `NOIR_UPPER_PRESSURE_LID` | `1` | `1` | `0` | `1763ea6867c15774ea09e5c90cc8675b` / `MAT_H8WorldPressureVignette_1428` | pos `{0,8.6,5}`, scale `{38,0.25,30}` |
| `NOIR_LEFT_VIGNETTE_SLAB` | `1` | `1` | `0` | `1763ea6867c15774ea09e5c90cc8675b` / `MAT_H8WorldPressureVignette_1428` | pos `{-15.2,3.2,-2}`, scale `{0.3,7.5,15}` |
| `NOIR_RIGHT_VIGNETTE_SLAB` | `1` | `1` | `0` | `1763ea6867c15774ea09e5c90cc8675b` / `MAT_H8WorldPressureVignette_1428` | pos `{12.2,3,-3}`, scale `{0.3,7,15}` |

## Validator Proposal

Add a static scene/prefab gate for product-facing primitive slabs:

- Scan `.unity` and `.prefab` YAML for active GameObjects with MeshRenderer enabled and mesh fileID `10202` cube or `10209` plane on gameplay-visible layers.
- Flag if object name contains `SHELF`, `OCCLUSION`, `CEILING`, `LID`, `CURTAIN`, `VIGNETTE`, `PLANE`, `SEABED`, `WATER`, `SHELL`, or scale exceeds slab thresholds such as one dimension over `8m` with another under `0.35m`.
- Whitelist only hidden service/debug objects with renderer disabled, camera-excluded layer, or explicit `Docs/Reports/...` proof reference.
- Severity: CRITICAL for active rendered primitive in surface/photic/medium-depth product route; HIGH for inactive but dangerous reactivation candidate; MEDIUM for material with empty base texture on visible route geometry.

## Quality Consequences If Substitute Is Needed

- Minimum/Low: remove visible primitive sheet/band artifacts; preserve route silhouettes, water color, return path, and instrument readability through camera/fog/shader fakes.
- Middle: replace slabs with authored terrain/waterline masks, richer material breakup, and controlled turbidity.
- High: spend recovered overdraw/primitive budget on wet rock response, shallow silt structure, caustic hints, and richer waterline specular/foam.
- Ultra: add sensory density only after no visible sheet, false ceiling, hard band, or flat seabed remains.

## Unity Owner First Five Objects

1. `H8_DEPTH_LOW_SHELF_1428` - first disable target; huge active cube shelf, best geometry match for route-cutting sheet.
2. `H8_WORLD_LOW_WATER_OCCLUSION_00_1428`
3. `H8_WORLD_LOW_WATER_OCCLUSION_01_1428`
4. `H8_WORLD_LOW_WATER_OCCLUSION_02_1428`
5. `H8_WORLD_LOW_WATER_OCCLUSION_03_1428`

Reason for 2-5: active rendered waterline cube strips on layer 0; most direct static match for hard black/green waterline bands. Inspect/disable as a group after object 1, with rollback.
