# Unity Owner Handoff 2202 - Underwater Slabs

Evidence class: STATIC VERIFIED. Runtime result: PENDING VERIFICATION.

Do not delete assets. Do not broad-clean the scene. One suspect group per capture pass.

## Top 5 Actionable Suspects

1. `H8_DEPTH_LOW_SHELF_1428`
   - Static proof: active GameObject, active renderer, built-in cube `10202`, pos `x:0 y:-0.9 z:30`, scale `x:58 y:1.15 z:8`, beige opaque `MAT_H8_SurfaceLittoralShelf_1430`.
   - Action: disable renderer only or move to underwater-excluded debug layer.
   - Proof: `0.5m` and `20-50m_route` captures must remove pale/yellow sheet without losing route readability.

2. `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428`
   - Static proof: all active/rendered built-in cubes on layer 0; y `-0.07`; material `MAT_WorldShell_1428` has empty BaseMap/MainTex.
   - Action: isolate one by one or as a named group after suspect 1 proof.
   - Proof: sliced waterline/black band must disappear or remain unchanged; no gameplay truth changes.

3. `H8_DEPTH_CEILING_OCCLUSION_1428`
   - Static proof: active/rendered built-in cube, pos `x:-4 y:7.8 z:25`, scale `x:70 y:1 z:8`, dark abyss material with empty textures.
   - Action: if needed for occlusion, move renderer to invisible/occlusion-only layer; otherwise disable renderer.
   - Proof: overhead/horizon hard slice removed; route silhouettes remain readable.

4. `NOIR_UPPER_PRESSURE_LID`
   - Static proof: active/rendered horizontal lid, scale `x:38 y:0.25 z:30`, transparent queue 3000, alpha `0.36`.
   - Action: verify camera mask/sorting; disable renderer test before retune.
   - Proof: no visible lid or sorting plane in underwater captures.

5. `NOIR_LEFT_VIGNETTE_SLAB` and `NOIR_RIGHT_VIGNETTE_SLAB`
   - Static proof: active/rendered side primitive slabs, transparent vignette material, Batch21 primitive refs.
   - Action: move away from world geometry into post/camera route or underwater-excluded layer.
   - Proof: no side slab intersections in wide/oblique underwater captures.

## Rollback Rule

Before changing anything, record original `m_IsActive`, renderer `m_Enabled`, layer, material GUID, and transform. Roll back immediately if disabling exposes voids, breaks route cues, or changes gameplay/collision truth.

## Required Capture Set

- Before current: `h8_1472_underwater_0_5m.png`, `h8_1472_underwater_20_50m_route.png`.
- After each suspect group: same camera routes, UI off if possible.
- Optional debug: scene hierarchy selection and camera culling mask proof.

## Rejection Boundary

Passing proof is not "looks less bad." Passing proof is: pale/yellow sheet gone, sliced waterline gone or materially reduced, rocks no longer visibly intersect a flat sheet, seabed no longer reads as a crude plane, and compact readability is preserved.
