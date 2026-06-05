# UNITY OWNER STEER - Batch23 Operational Handoff

Date: 2026-06-04
Target thread: `Продолжить работу по логам`
Status: 1473 rejected; 1474 proof not yet accepted.

## Non-Negotiable Current Verdict

Do not claim visual acceptance from 1473. Batch23 confirms:
- `FALSE_LABEL`: original `h8_1473_underwater_0_5m.png` and `h8_1473_underwater_20_50m_route.png` do not prove underwater route.
- One 1473 renderer on/off pair is byte-identical, so the toggle proved no visual state change.
- `PALE_SLAB` / `FLAT_TINT_PLANE`: main underwater diagnostic still shows slab/flat mass, not photic shallows.
- Foam variants shown in 1473 are debug/sheet/grid-like, not acceptable shoreline foam.
- No complete clean post-capture runtime log tail exists for accepted proof.

## Compile State

`HectonCelestialEngine` was source-hardened for `MaterialPropertyBlock` null safety in Aegir, sun disc, and moon paths.

Current log evidence:
- `UnityEditor_visual_audit_restart_1474b.log`: `*** Tundra build success (192.15 seconds - 0:03:12)`
- `02_HECTON_WORLD` loaded after compile.

Still required:
- Play/GameView proof that no `Renderer.GetPropertyBlock(null)`, `ArgumentNullException`, forced-load exit, or exception spam occurs across the capture window.

## First Scene Offenders To Inspect

Stage one object/group at a time. Capture before/after. Keep rollback state.

1. `H8_DEPTH_LOW_SHELF_1428`
   - Static evidence: active, rendered, built-in cube, scale roughly `58 x 1.15 x 8`, position `x:0 y:-0.9 z:30`.
   - Most likely pale/yellow horizontal sheet / route-slicing slab.
   - Inspect live material binding first; Batch22/current YAML disagree on material.

2. `H8_WORLD_LOW_WATER_OCCLUSION_00_1428`
3. `H8_WORLD_LOW_WATER_OCCLUSION_01_1428`
4. `H8_WORLD_LOW_WATER_OCCLUSION_02_1428`
5. `H8_WORLD_LOW_WATER_OCCLUSION_03_1428`
   - Static evidence: active rendered waterline cube strips around `y:-0.07`.
   - Likely black/green banding source.

Do not blindly delete service-risk objects. Disable renderer or isolate layer first, capture proof, rollback if needed.

## Fog / Underwater State Proof

1474+ underwater proof must log ordered writer state. For each capture row include:
- active scene
- camera name and position
- player/cockpit root position
- water surface Y
- camera depth and player depth
- `HectonAtmosphereManager` current state
- `_useAutoUnderwaterDetection`
- external underwater flag / movement mode if available
- `HectonUnderwaterVisuals` active visual underwater state
- active atmosphere/biome profile name
- final `RenderSettings.fog`, `fogColor`, `fogDensity`
- shader fog/water globals
- Crest underwater renderer state
- ocean material fog/foam/caustic key params

Reject underwater capture if atmosphere remains `SURFACE_*` without an explicit logged transition reason.

## Foam / Caustics Route Rules

Safe first candidates:
- Crest route: `MAT_H8_SurfaceCrestOcean_1428` + `H8_CREST_FOAM_INPUT_PASS_1464`
- Caustic receiver: `H8_FloorCausticSoft_1443` + `MAT_H8_FloorCausticSoft_1443`
- One controlled authored foam test only after Crest proof: `H8_OFFSHORE_FOAM_BREAK_1428_0` or `H8_VisibleWaveFoam_1438`

Forbidden as-is:
- `H8_SurfaceFoamLace_1453`
- `MAT_H8_SurfaceFoamBlob_1447` route as represented by 1473 sheet/grid captures
- `H8_VisibleFoamUnlit_1436` until empty `_BaseMap` / `_MainTex` slots are fixed
- `H8_VisibleBrokenFoam_1435`
- `SURFACE_FOAM_RIBBON_1428_2`
- `WATER_CAUSTIC_RIB_*`

Do not enable all foam helpers. One route, one proof capture, rollback on grid/sheet/rectangle.

## Required 1474+ Packet

All views must come from one session:
- `surface_coast_aegir_ui_off`
- `shoreline_close_1m`
- `underwater_0_5m`
- `underwater_20_50m_route`
- `aegir_celestial_long`
- `regression_low_oblique`
- metadata manifest/checksums per image
- clean post-capture log tail after final screenshot

Immediate reject codes:
- `FALSE_LABEL`
- `MISSING_VIEW`
- `STALE_LOG`
- `RUNTIME_FAULT`
- `PALE_SLAB`
- `FLAT_TINT_PLANE`
- `DEBUG_FOAM`
- `ACID_GREEN`
- `DARKNESS_HIDE`

## Texture State

Do not bind current Gemini/source candidates as final textures. Batch23 says current sand/shell and wet basalt candidates are still source-only/rejected.

Next useful generations when budget is spent:
1. `WetBasaltShoreline_AlbedoSource`
2. `PhoticShellSand_AlbedoSource`
3. `ShoreFoamSalt_RGBAMaskSource`

No blind Gemini spending and no Unity material binding without audit/manifest.

## Reference Reports

- `Docs/Reports/Batch23/2301_ATMOSPHERE_FOG_WRITER_MATRIX.md`
- `Docs/Reports/Batch23/2302_UNDERWATER_PROOF_HARNESS_AUDIT.md`
- `Docs/Reports/Batch23/2303_FOAM_CAUSTIC_PATCH_PLAN.md`
- `Docs/Reports/Batch23/2304_SCENE_SLAB_PATCHPACK.md`
- `Docs/Reports/Batch23/2305_VISUAL_ACCEPTANCE_RUBRIC.md`
- `Docs/Reports/Batch23/2306_PHOTIC_TEXTURE_BINDING_PLAN.md`
