# Batch26 Controller Synthesis - Interim Static Blockers

Date: 2026-06-04 20:58 +04:00.
Controller: local orchestrator.
Status: interim synthesis while Batch26 worker reports are still pending.

## Current Front

Unity visual/runtime proof remains blocked after rejected packet `1474`.

No newer screenshot packet exists under `Docs/Screenshots/MCP` after:
- `h8_1474_surface_coast_aegir_ui_off.png`
- `h8_1474_shoreline_close_1m.png`
- `h8_1474_underwater_0_5m.png`
- `h8_1474_underwater_20_50m_route.png`
- `h8_1474_aegir_celestial_long.png`
- `h8_1474_regression_low_oblique.png`

`1474` stays `REJECTED`: false underwater/shoreline labels, no manifest/checksums/camera/depth/quality/toggles/log path, no convincing foam, no visible caustics, no underwater volume/particles/depth route, weak shoreline/terrain, weak Aegir/celestial.

## Fresh Process / Log State

Latest process sample shows Unity active:
- `Unity` PID `11440`, started `20:50:59`.
- `Unity.ILPP.Runner` PID `5820`.
- `UnityShaderCompiler` PID `10920`.
- `mcp-for-unity` PID `11060`.

No `dotnet build` was launched by controller. Do not start builds or new Unity work while this import/compiler window is active.

Latest inspected log remains dirty:
- `Asset Pipeline Refresh` and `Begin MonoManager ReloadAssembly` repeated.
- MCP WebSocket warnings continue even though HTTP bridge is present.
- `CriticalBootException: [GlobalRegistry] Ready-locked registry rejected registration: HectonUnderwaterVisuals` at `UnityEditor_visual_audit_restart_1474b.log:2621`.
- New persistent leak owner appears in the same log: `SeamGapDitherRenderer.EnsureBuffers()` allocating `GraphicsBuffer` during reload, with stacks at `SeamGapDitherRenderer.cs:455`, `456`, `466`, `467`, `476`, `481`.

## Static Scene / Material Blockers

Underwater/celestial:
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4608` has one `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`.
- Same owner has `sunVisualTransform: {fileID: 1985271341}` but `underwaterSuspendedMotes`, `underwaterMarineSnow`, `underwaterExhaleBubbles`, and `shallowSunBeamLight` are all `{fileID: 0}` at lines `4637-4640`.
- Runtime still rejects `HectonUnderwaterVisuals` registration after ready lock, so static presence is not runtime proof.
- `HectonCelestialEngine.sunVisualTransform` remains `{fileID: 0}` in the scene at line `91163`.
- Candidate `SURFACE_LOW_SUN_DISC_1428` is inactive and renderer-disabled at lines `95890-95918`.

Water/caustics/volume:
- `Ocean.mat` has `_ClipSurface: 0`, `_ClipUnderTerrain: 0`, `_Transparency: 0`, `_Underwater: 1`, `_WaveFoamStrength: 1.25`.
- `Ocean-Underwater.mat` has clip/transparency keywords enabled, but `_CausticsStrength: 0`.
- `Ocean_UnderwaterCurtain.mat` has `_CAUSTICS_ON`, `_CausticsStrength: 10`, `_ClipSurface: 0`, `_LightIntensityMultiplier: 5.31`, green foam bubble color, and no observed clip/transparency keyword in the grep.
- `MAT_H8_SurfaceCrestOcean_1428.mat` enables clip/transparency/caustics, but uses high/acid-prone values: `_WaveFoamStrength: 3.45`, `_CausticsStrength: 1.45`, `_SubSurface: {r: 0.34, g: 0.86, b: 0.92}`, `_FoamBubbleColor: {r: 0.76, g: 1, b: 0.88}`.
- `H8_FloorCausticSoft_1443` is active and renderer-enabled at `02_HECTON_WORLD.unity:64133-64161`, so lack of caustics in proof is not solved by object existence alone.
- `H8_UnderwaterHazeCurtain_1454` is inactive and renderer-disabled at `93776-93804`.
- `H8_UnderwaterSuspendedSpecks_1446` GameObject is inactive at `73620-73648`, so its enabled renderer is not visible.

Capture/proof:
- Existing dev screenshot menu writes generic `screenshot-yyyyMMdd-HHmmss.png` to `Docs/Screenshots`, not a route-correct six-view packet with manifest: `Assets/_Project/Editor/HectonDevToolsMenu.cs:189-215`.
- `Docs/Screenshots/MCP` contains no `*manifest*` file for `1474`.
- `Assets/Screenshots` directory still exists and remains an import-loop risk if used by any capture route.

Generated assets:
- `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260604.md` states no inspected source is currently `READY_FOR_DERIVATION`.
- Current inspected wet basalt and photic substrate sources are `REJECT` / `STATIC_REJECTED`; do not import them directly into active terrain/materials.

## Controller Verdict

Do not accept the current Unity lane and do not ask for another visual packet until:
1. Unity import/ILPP/shader compiler quiets.
2. `HectonUnderwaterVisuals` registers before ready lock or through the proper runtime publication gate.
3. The new `SeamGapDitherRenderer` persistent `GraphicsBuffer` leak is either fixed or cleanly proven gone after reload/play-exit.
4. Celestial sun route is decided: wire and activate `SURFACE_LOW_SUN_DISC_1428`, or document that the sky material owns sun disc and remove stale `sunVisualTransform` expectation.
5. Underwater volume owners are wired or replaced by a real premium volume path; inactive scene specks/haze curtain do not prove underwater richness.
6. A real packet harness writes six route-correct views plus manifest/checksums/camera/depth/quality/toggles/log path and clean log tail newer than the final screenshot.

