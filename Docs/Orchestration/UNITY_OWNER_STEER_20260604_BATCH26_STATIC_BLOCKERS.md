# STEER_BATCH26_STATIC_BLOCKERS

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04.
Source:
- `Docs/Reports/Batch26/BATCH26_CONTROLLER_SYNTHESIS_INTERIM.md`

Use after the current Unity import / ILPP / shader compiler window is quiet. Do not interrupt a live capture or script reload.

## Verdict

Do not claim visual progress from the current lane yet. `1474` remains rejected and there is no newer proof packet.

## New Hard Runtime Blocker

Latest inspected log still has dirty reload/import/MCP noise and now shows a persistent leak from:
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs:455`
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs:456`
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs:466`
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs:467`
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs:476`
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs:481`

Stack owner is `SeamGapDitherRenderer.EnsureBuffers()` allocating `GraphicsBuffer` during reload. This is a separate current leak from the earlier WeatherEvents lane. It blocks clean proof until fixed or proven gone after reload/play-exit.

The log also still contains:
- `CriticalBootException: [GlobalRegistry] Ready-locked registry rejected registration: HectonUnderwaterVisuals`
- repeated Asset Pipeline Refresh / Domain Reload
- MCP WebSocket warnings

## Static Scene Blockers

Underwater owner:
- One scene owner exists: `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`.
- It has `sunVisualTransform: {fileID: 1985271341}`.
- Its volume/detail refs are still missing:
  - `underwaterSuspendedMotes: {fileID: 0}`
  - `underwaterMarineSnow: {fileID: 0}`
  - `underwaterExhaleBubbles: {fileID: 0}`
  - `shallowSunBeamLight: {fileID: 0}`
- Runtime registration is still failing after ready lock, so static owner presence is not runtime proof.

Celestial:
- `HectonCelestialEngine.sunVisualTransform` remains `{fileID: 0}` in `02_HECTON_WORLD.unity`.
- Candidate `SURFACE_LOW_SUN_DISC_1428` transform exists, but GameObject is inactive and MeshRenderer disabled.

Underwater volume scene objects:
- `H8_FloorCausticSoft_1443` is active/renderer-enabled.
- `H8_UnderwaterHazeCurtain_1454` is inactive/renderer-disabled.
- `H8_UnderwaterSuspendedSpecks_1446` GameObject is inactive.

Water/material risks:
- `Ocean.mat`: `_ClipSurface 0`, `_ClipUnderTerrain 0`, `_Transparency 0`, `_Underwater 1`.
- `Ocean-Underwater.mat`: underwater clip/transparency keywords are present, but `_CausticsStrength 0`.
- `Ocean_UnderwaterCurtain.mat`: `_CAUSTICS_ON`, `_CausticsStrength 10`, `_ClipSurface 0`, high light multiplier, green foam bubble color, no observed clip/transparency keyword.
- `MAT_H8_SurfaceCrestOcean_1428`: clip/transparency/caustics enabled, but high teal/green foam/subsurface values remain acid/flat-water risk.

## Required Order

1. Let Unity import/ILPP/shader compiler settle.
2. Fix clean runtime route first:
   - no ready-lock rejection for `HectonUnderwaterVisuals`;
   - no `SeamGapDitherRenderer` persistent `GraphicsBuffer` leak after reload/play-exit;
   - no active compile/import/domain reload during capture.
3. Decide celestial sun ownership:
   - either wire and activate `SURFACE_LOW_SUN_DISC_1428` into `HectonCelestialEngine.sunVisualTransform`;
   - or explicitly document sky-material sun ownership and remove the stale scene expectation.
4. Wire or replace underwater volume/detail owners before claiming underwater visual richness.
5. Recheck water material ownership before capture:
   - do not solve flat/green water by adding darkness/fog/haze;
   - restore correct clip/transparency route where required;
   - prove foam/caustics visually, not by numeric material claims.
6. Produce a new packet only when the harness writes:
   - six distinct route-correct views;
   - manifest with checksums/timestamps/camera position/rotation/depth/quality/toggles/log path;
   - clean log tail newer than final screenshot.

## Acceptance Reminder

Surface, sky, Aegir, coastline, ocean surface, photic shallows, and medium-depth hero routes must be bright, readable, detailed, and Subnautica-floor or better. More dark green haze, false underwater labels, inactive scene props, or material-number claims are rejected.

