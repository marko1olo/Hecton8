# 2202 Underwater Plane/Slab Offender Matrix

Evidence class: STATIC VERIFIED. Visual acceptance: PENDING VERIFICATION.

No Unity slot was taken. No Play Mode, no scene save, no build, no deletion.

## Visible Offender Classes

- `h8_1472_underwater_0_5m.png`: lower half is filled by a pale/yellow horizontal slab. Water surface/sky remain above it, so the offender reads as a world-space sheet/terrain shelf, not only post-processing.
- `h8_1472_underwater_20_50m_route.png`: a huge pale sheet cuts across the route, intersects rocks, and creates a hard sliced waterline. Seabed beneath reads flat and primitive.
- Intersecting rocks and flat seabed indicate mixed offender classes: horizontal shelf/slab, active occlusion strips, visible false ceiling/curtain candidates, and placeholder terrain/seabed shells.
- Static image scan of 1469-1472 underwater captures found the pale rows persist:
  - `1469_underwater_0_5m`: pale rows y 429-719.
  - `1470_underwater_0_5m`: pale rows y 429-719.
  - `1471_underwater_0_5m`: pale rows y 429-719.
  - `1472_underwater_0_5m`: pale rows y 429-719.
  - `1469-1472_underwater_20_50m_route`: pale rows persist around y 201-328.

This is not accepted as fixed by repeated screenshots.

## Governing Static Facts

- Scene grep found all explicit names requested by task except no fabricated extras were added.
- `H8_UnderwaterSurfaceSheet_1455`, `BLACK_WATER_PLANE`, `BASALT_SEABED`, `ABYSS_SURFACE_CEILING`, `ABYSS_BLACKWATER_CEILING_1428`, `Water_Mass_Far_1428`, and `Water_Mass_Mid_1428` exist but are inactive in YAML.
- Active top offenders are built-in primitive slabs/cubes on layer 0 with enabled renderers.
- Batch21 validator independently flags the same candidate class as `BUILTIN_PRIMITIVE_MESH_REF`, plus empty base texture slots on multiple water/veil/depth materials.
- Static source proof does not prove which renderer drew the captured pixels. Selective Unity-owner renderer/layer isolation is required.

## Suspect Table

| Rank | Suspect | Static Evidence | Why It Can Cause 1472 Artifact | First-Pass Action |
| ---: | --- | --- | --- | --- |
| 1 | `H8_DEPTH_LOW_SHELF_1428` | Active `1`, renderer `1`, built-in cube `10202`, pos `x:0 y:-0.9 z:30`, scale `x:58 y:1.15 z:8`, material `MAT_H8_SurfaceLittoralShelf_1430` with opaque beige `_BaseColor {0.68,0.64,0.5,1}`. Batch21 line 1631 flags primitive mesh. | Best match for pale/yellow horizontal sheet and route-slicing slab. It is active and large enough. | Disable renderer or move to underwater-excluded/debug layer for one proof pass. No deletion. |
| 2 | `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428` | All active/rendered built-in cubes on layer 0 at y `-0.07`, scale `x:3.2 y:0.06 z:0.4`, material `MAT_WorldShell_1428` with empty base textures. Batch21 flags all four primitive refs. | Less likely to create pale color, but likely to create black slicing/occlusion bands near waterline and route geometry. | Disable or layer-filter one by one; verify waterline/slice improvement. |
| 3 | `H8_DEPTH_CEILING_OCCLUSION_1428` | Active/rendered built-in cube, pos `x:-4 y:7.8 z:25`, scale `x:70 y:1 z:8`, material `MAT_H8WorldDeepAbyss_1428` with empty base textures. | Large active false ceiling can create hard upper/lower slices from underwater cameras. | Move to invisible occlusion-only layer if it is culling support; otherwise disable renderer. |
| 4 | `NOIR_UPPER_PRESSURE_LID` | Active/rendered built-in cube, pos `x:0 y:8.6 z:5`, scale `x:38 y:0.25 z:30`, transparent `MAT_H8WorldPressureVignette_1428`, alpha `0.36`, empty textures. | Horizontal transparent lid can sort badly and produce visible waterline/lid artifacts. | Verify layer mask and sorting; disable renderer proof before retune. |
| 5 | `NOIR_LEFT_VIGNETTE_SLAB` / `NOIR_RIGHT_VIGNETTE_SLAB` | Active/rendered built-in cubes, side curtain scales around `x:0.3 y:7-7.5 z:15`, same transparent vignette material. Batch21 flags primitive refs. | Side curtains can visibly intersect composition and read as slab geometry instead of post/volume. | Move to camera/post route or underwater-excluded layer. |
| 6 | `Water_Mass_Far_1428` / `Water_Mass_Mid_1428` | Inactive GameObjects but renderers enabled in component YAML; built-in cubes scaled `150x34x0.25` and `135x28x0.25`; transparent `MAT_WorldReadableWaterVeil_1428` has empty BaseMap/MainTex. | If reactivated, these are enormous flat water veils; dangerous but not active from static YAML. | Keep inactive; replace with fog/volume fake only with proof. |
| 7 | `H8_UnderwaterSurfaceSheet_1455` | Exists but inactive and renderer disabled. Material is custom sheet with `_Opacity 0.42`, bright/foam colors, sea level `14.02`. | Name/material match sheet risk, but static source says not currently active. | Do not enable. If used, require underwater-only camera fade proof. |
| 8 | `BLACK_WATER_PLANE` | Exists but inactive and renderer disabled. Built-in plane `10209`, pos `y:-0.23 z:45`, scale `26x1x26`, dark teal material. Batch21 flags primitive plane. | Old plane/proxy debt, not active source. | Leave inactive; do not reactivate. |
| 9 | `BASALT_SEABED` | Exists but inactive and renderer disabled. Built-in plane `10209`, pos `y:-1.42 z:52`, scale `32x1x25`, dark basalt material. Batch21 flags primitive plane. | Explains flat seabed debt if reactivated; not active source. | Replace with authored terrain shell if needed. |
| 10 | `ABYSS_SURFACE_CEILING` / `ABYSS_BLACKWATER_CEILING_1428` | Exist but inactive and renderer disabled. Built-in cube ceiling class; teal/blackglass shell materials. Batch21 flags primitive refs. | False ceiling risk if reactivated. | Keep inactive unless Unity owner proves masked presentation route. |
| 11 | `NOIR_FAR_WATER_CURTAIN_A/B` | Batch21 flags built-in primitive refs. Material `MAT_H8WorldDepthCurtain_1428` is transparent alpha `0.34` with empty textures. | Far vertical curtain can cause visible rectangular haze/slice edges. | Unity owner must inspect active state and camera mask, then replace visible geometry with fog/post fake if visible. |
| 12 | `DOCK_MAIN_DECK` | Active GameObject, renderer disabled, built-in cube, scale `x:12.2 y:0.3 z:7.2`. Batch21 flags primitive ref. | Not current pale sheet due renderer disabled; production primitive debt if re-enabled. | Keep disabled until authored dock mesh/material exists. |

Full CSV: `Docs/Reports/Batch22/2202_UNDERWATER_PLANE_SLAB_OFFENDER_MATRIX.csv`.

## Material Risk Classes

- Pale/yellow/offending material class: `MAT_H8_SurfaceLittoralShelf_1430`, opaque, beige base color, bound base/normal textures, applied to active huge shelf. Primary suspect.
- Empty-texture shell class: `MAT_WorldShell_1428`, `MAT_H8WorldDeepAbyss_1428`, `MAT_H8WorldPressureVignette_1428`, `MAT_WorldReadableWaterVeil_1428`, `MAT_H8WorldDepthCurtain_1428`. These can still be intentional flat color fakes, but without Unity capture they are visual debt, not accepted production water/terrain.
- Transparent queue class: pressure/vignette/water/depth curtains use queue `3000`, alpha `0.11-0.36`, `ZWrite 0`. Wrong camera/layer/sorting can produce visible slabs.

## Disable / Retune / Occlusion Buckets

- Disable renderer proof first: `H8_DEPTH_LOW_SHELF_1428`, `H8_WORLD_LOW_WATER_OCCLUSION_*`, `H8_DEPTH_CEILING_OCCLUSION_1428`, `NOIR_UPPER_PRESSURE_LID`, `NOIR_*_VIGNETTE_SLAB`.
- Keep inactive / do not reactivate: `H8_UnderwaterSurfaceSheet_1455`, `BLACK_WATER_PLANE`, `BASALT_SEABED`, `ABYSS_SURFACE_CEILING`, `ABYSS_BLACKWATER_CEILING_1428`, `Water_Mass_Far_1428`, `Water_Mass_Mid_1428`.
- Material retune candidates after renderer proof: `MAT_H8_SurfaceLittoralShelf_1430` beige albedo/opaque class; transparent curtain materials alpha/render queue/ZWrite class; empty texture shell materials if visible.
- Occlusion/culling support candidates: low-water occlusion strips and depth ceiling occlusion may be intended for staging/culling. If needed, they must be invisible to gameplay cameras or layer-filtered. Gameplay truth must not depend on visible primitive renderers.

## No-Deletion First-Pass Plan

1. Unity owner isolates `H8_DEPTH_LOW_SHELF_1428`: disable renderer only or move to underwater-excluded debug layer, capture `0.5m` and `20-50m_route`, then restore/commit only after proof decision.
2. If pale sheet persists, isolate `H8_WORLD_LOW_WATER_OCCLUSION_*`, `H8_DEPTH_CEILING_OCCLUSION_1428`, and `NOIR_UPPER_PRESSURE_LID` one group at a time. Do not batch-disable without proof separation.
3. If geometry slicing is gone but color/material debt remains, retune or replace the offending material route with authored terrain/water fake. Proof requires compact and high captures. No broad scene cleanup.

## Low / Middle / High / Ultra Consequences

- Low: remove visible primitive slab artifacts while keeping route silhouettes and instrument readability. Layer filtering must not change collision, resource, route, or save truth.
- Middle: replace surviving visible flat fakes with authored water/terrain masks and better material breakup.
- High: spend saved overdraw and primitive clutter budget on wet terrain, silt structure, caustic hints, and richer water response.
- Ultra: add sensory density only after low/middle prove no pale sheet, sliced waterline, or flat seabed.

## Required Unity-Owner Proof

- Hierarchy inspector screenshots or structured log of each suspect before modification.
- One suspect group changed at a time.
- Before/after screenshots for `h8_1472_underwater_0_5m` and `h8_1472_underwater_20_50m_route` repro positions.
- Rollback note: exact renderer/layer/material property changed and original value.
- Confirm no gameplay truth route changed: no collider, save, spawn, navigation, resource, or pressure authority edits.

Until then: PENDING VERIFICATION.
