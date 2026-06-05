# STEER_BATCH26_SYNTHESIS

Target: `Продолжить работу по логам` Unity owner.
Date: 2026-06-04 21:07 +04:00.
Source: `Docs/Reports/Batch26/BATCH26_SYNTHESIS_FOR_UNITY_OWNER.md`.

## Verdict

Do not claim visual acceptance. `1474` remains rejected and there is no `1475` packet or manifest.

The current problem is not one missing screenshot. It is a broken proof chain plus unresolved runtime/owner/material/art blockers.

## Required Order

1. Finish the current Unity session settling window. Do not capture during compile/import/domain reload/ILPP/shader compile/MCP startup noise.
2. Clear runtime proof health first:
   - no `GlobalRegistry` ready-lock rejection for `HectonUnderwaterVisuals`;
   - no `SeamGapDitherRenderer.EnsureBuffers()` persistent `GraphicsBuffer` leak after fresh reload/play-exit;
   - no stale `WeatherEvents` persistent leak after cleanup;
   - no MCP transport error storm inside the accepted proof window.
3. Resolve celestial ownership:
   - either sky material/atmosphere owns the primary sun disc and the inactive `SURFACE_LOW_SUN_DISC_1428` is not part of acceptance;
   - or the scene mesh sun route is assigned to `HectonCelestialEngine.sunVisualTransform`, active, renderer-enabled, material-upgraded, and not hidden by the atmosphere-present path.
4. Resolve underwater visual owner state:
   - static `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` is not enough;
   - prove registered runtime owner, actual underwater state, real depth bands, volume/detail refs or replacement route, caustics/fog/turbidity state, and route cue.
5. Fix water/material route through owners, not random asset edits:
   - `Ocean-Underwater.mat` has `_CausticsStrength: 0`;
   - `Crest.UnderwaterRenderer` has `_volumeGeometry: {fileID: 0}` and copies material params each frame;
   - do not raw-enable `H8_UnderwaterHazeCurtain_1454`, slabs, pressure lid, shelf, or curtain materials without owner gating and low-oblique proof.
6. Fix shoreline art route before close proof:
   - active `H8_PhoticRouteTerrain_1464` uses rejected `TX_H8_WetBasaltShoreline_Albedo_1428` as broad terrain input;
   - no current Gemini wet basalt, shell/sand, foam/salt, caustic, or algae/biofilm source is ready for Unity import;
   - do not import more sources into `Assets/**` until they pass static intake, 2x2/3x3 visual review, and material-family planning.
7. Only then produce `1475`:
   - same-session distinct route-correct screenshots;
   - owned manifest with SHA256, timestamps, camera/depth/route/quality/material/toggle/log fields;
   - clean log tail newer than final screenshot and stable for at least 60 seconds after it.

## Minimum `1475` Views

- surface/coast/water/Aegir with UI state declared;
- surface/coast/water/Aegir UI off;
- shoreline close 1 m with waterline, wet contact, organic foam, material breakup, scale cue;
- underwater 0-5 m with camera and player/cockpit depth proof;
- underwater 20-50 m route with near/mid/far volume and route structure;
- Aegir/celestial long plus crop or lens metadata;
- low-oblique regression for slabs/planes/white ocean artifacts.

## Reject Immediately If

- any underwater filename is surface-looking again;
- shoreline is a medium/distant coast shot again;
- there is no manifest;
- log path is stale or dirty;
- any screenshot is under `Assets`;
- foam/caustics/volume are only material-number claims;
- surface/photic/mid-depth/Aegir is dark, muddy, flat, primitive, hidden by fog/haze, or below the Subnautica-level floor.
