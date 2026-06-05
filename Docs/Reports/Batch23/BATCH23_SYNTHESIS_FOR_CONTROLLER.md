# Batch23 Synthesis For Controller

Date: 2026-06-04
Scope: post-1473 visual route recovery. Static/no-Unity workers plus orchestrator observation.

## Current Verdict

1473 is rejected. No current packet proves Subnautica-level surface/shoreline/photic shallows.

Observed live/editor state after 1474 compile:
- Unity compiled successfully after `HectonCelestialEngine` MPB hardening.
- `02_HECTON_WORLD` is loaded.
- MCP HTTP bridge is active.
- Visual screen still reads green/flat with dark weak shoreline and dirty Aegir/atmosphere blending.
- No `1474+` screenshot packet exists yet under `Docs/Screenshots/MCP`.

## Hard Reject Reasons To Preserve

- `FALSE_LABEL`: 1473 underwater filenames do not prove actual underwater camera/state.
- `STALE_LOG`: 1473 has no clean same-session post-capture runtime tail.
- `RUNTIME_FAULT`: old packet logs contained repeated `HectonCelestialEngine.UpdateAegirMaterial()` `ArgumentNullException`; source is now hardened but Play/GameView clean proof is still required.
- `PALE_SLAB` / `FLAT_TINT_PLANE`: underwater diagnostics show slab/plane-like mass, not photic shallows.
- `DEBUG_FOAM`: 1473 foam variants read as rectangular/pixel-grid sheets.
- `ACID_GREEN`: surface/ocean remains green-heavy; brightness must not become neon or posterized.

## Batch23 Findings

### 2301 Atmosphere/Fog

- Fog ownership is split.
- `02_HECTON_WORLD` scene default fog is green/teal and dense enough to contaminate visuals.
- `_useAutoUnderwaterDetection: 0` can leave atmosphere in surface state unless another owner explicitly forces underwater.
- `HectonUnderwaterVisuals` can write/affect fog independently and must be part of proof.
- `HectonCelestialEngine` may polish readable surface fog but must not become underwater truth owner.

Controller consequence:
- Future screenshots need ordered writer logs: atmosphere, underwater visuals, celestial, final render settings.

### 2302 Proof Harness

- Existing screenshot tools can write images but not enough metadata.
- `Assets/Screenshots` must remain rejected as proof output.
- Required packet: six views, metadata manifest, checksums, clean log tail.

Controller consequence:
- No image is accepted without camera/depth/state/profile/fog/Crest metadata.

### 2303 Foam/Caustics

Safe first routes:
- Crest foam via `MAT_H8_SurfaceCrestOcean_1428` + `H8_CREST_FOAM_INPUT_PASS_1464`.
- `H8_FloorCausticSoft_1443` only if it reads as subtle caustic lace, not as a sheet.
- One narrow authored shoreline contact candidate after Crest proof: `H8_OFFSHORE_FOAM_BREAK_1428_0`, `H8_VisibleWaveFoam_1438`, or live-proven Photic1469 fine foam.

Forbidden as-is:
- `H8_SurfaceFoamLace_1453`
- `MAT_H8_SurfaceFoamBlob_1447` route as shown in 1473
- `H8_VisibleFoamUnlit_1436`
- `H8_VisibleBrokenFoam_1435`
- `SURFACE_FOAM_RIBBON_1428_2`
- `WATER_CAUSTIC_RIB_*`

Controller consequence:
- One foam/caustic route per test. Capture before/after. Roll back if it becomes sheet/grid/noise.

### 2304 Scene Slabs

First static offender targets:
- `H8_DEPTH_LOW_SHELF_1428`
- `H8_WORLD_LOW_WATER_OCCLUSION_00_1428`
- `H8_WORLD_LOW_WATER_OCCLUSION_01_1428`
- `H8_WORLD_LOW_WATER_OCCLUSION_02_1428`
- `H8_WORLD_LOW_WATER_OCCLUSION_03_1428`

Live owner note:
- Unity owner observed `H8_FloorCausticSoft_1443` may currently be the visible yellow/white sheet. Live evidence overrides static guess, but the object must be retuned/replaced into subtle caustic breakup, not deleted into empty water.

Controller consequence:
- Disable/isolate renderer in stages. Do not blindly delete service geometry.

### 2305 Visual Rubric

Future packet must include:
- `surface_coast_aegir_ui_off`
- `shoreline_close_1m`
- `underwater_0_5m`
- `underwater_20_50m_route`
- `aegir_celestial_long`
- `regression_low_oblique`
- metadata/checksums
- clean post-capture log tail

Controller consequence:
- Missing view, false underwater label, or stale log rejects whole packet.

### 2306 Texture Intake

- Current Gemini/source candidates remain source-only/rejected.
- Do not bind current sand/shell/wet-basalt candidates as final material textures.

Next useful generation priorities:
1. `WetBasaltShoreline_AlbedoSource`
2. `PhoticShellSand_AlbedoSource`
3. `ShoreFoamSalt_RGBAMaskSource`

Controller consequence:
- Spend Gemini generations only against a target material and audit checklist.

## Active Unity Owner Instruction Already Sent

Sent to `Продолжить работу по логам`:
- `Docs/Orchestration/UNITY_OWNER_STEER_20260604_BATCH23_1474_OPERATIONAL.md`

## Next Controller Action

Wait for a new `1474+` packet or runtime log tail. Then:
1. Check complete six-view packet exists.
2. Check metadata manifest/checksums exist.
3. Inspect surface/shore/underwater images against reject codes.
4. Check log tail after final screenshot for no exceptions, no forced-load exit, no screenshot import loop.
5. If rejected, steer with only the exact failing codes and exact next object/route to test.
