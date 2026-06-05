# 3105 Aegir / Sky / Celestial Owner

Status: STATIC VERIFIED / UNITY VISUAL ACCEPTANCE PENDING
Date: 2026-06-05

Evidence classes: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_YAML`, `PENDING_VERIFICATION`.

## Authority Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `quality.md`
- `celestial.md`
- `atmosphere.md`
- `rendering.md`
- `shaders.md`
- `Docs/Reports/Batch30/3006_AEGIR_SKY_ASSET_ROUTE_AUDIT.md`
- `Docs/Reports/Batch31/SKY_TEXTURE_SLOT_RESOLUTION_20260605.md`
- `Docs/Reports/Batch31/MATERIAL_TEXTURE_CRITICALS_20260605.md`

Mandates followed:

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`

## Verdict

`Mat_HectonSky` remains the correct active skybox and sky-material sun owner. The old flat mesh sun remains rejected. The only high-confidence cloud bind candidate for the active sky shader is `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png` into `_MainCloudTex`, but this pass did not bind it because Unity material/shader readback was not executed.

`_HighCloudTex` and `_MainCloudAtlas` are stale serialized rows by static shader/source evidence. Do not bind them from a guessed texture map.

## Static Route Facts

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:29` uses `Mat_HectonSky` as `m_SkyboxMaterial`.
- `Assets/_Project/Scenes/00_BOOTSTRAP.unity:29` uses `Mat_HectonSky` as `m_SkyboxMaterial`.
- `02_HECTON_WORLD` references the same sky material through `HectonUnderwaterVisuals.skyMaterial`, `HectonCelestialEngine._skyMaterial`, and `blendedSkyboxMaterial`.
- `Assets/_Project/Art/Materials/Mat_HectonSky.mat` uses shader GUID `6302a783d2378694c9db8d0036358965`.
- `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader` declares `_MainCloudTex`, `_StarTwinkleLUT`, `_BakedStarCubemap`, sun disc properties, Aegir halo, and Aegir lensing.
- Static shader search found no `_HighCloudTex` or `_MainCloudAtlas` declaration.

## Sky Texture Slot Map

| Material | Slot | Static state | Decision |
|---|---|---|---|
| `Mat_HectonSky.mat` | `_MainCloudTex` | null | Effective shader property. Bind `Sky/oblaka!.png` only after Unity readback confirms still null. |
| `Mat_HectonSky.mat` | `_HighCloudTex` | stale GUID `97dacc0c8637b304f9451ecd290acffb` | Do not bind by guess. Static shader search does not prove property exists. |
| `Mat_HectonSky.mat` | `_MainCloudAtlas` | stale GUID `161f2ad7f77e8bf408b29aa7e3d29966` | Do not bind by guess. Static shader search does not prove property exists. |
| `Mat_HectonSky.mat` | `_StarTex` | `Sky/bo2.png` | Keep unless Unity shader readback proves ignored/wrong. |
| `Mat_HectonSky.mat` | `_StarTwinkleLUT` | null | No confident candidate. Do not guess. |
| `Mat_HectonSky.mat` | `_BakedStarCubemap` | null | Needs correct 2DArray/cubemap asset, not a PNG guess. |
| `Mat_HectonSky_CloudOverlay.mat` | `_MainCloudTex` | `Sky/oblaka!.png` | Keep. |
| `MAT_H8SurfaceCloudDeck_1428.mat` | `_BaseMap` | `Sky/oblaka!.png` | Keep. |

## Aegir / Cloud Stack

Current active Aegir material route:

- `MAT_AegirGasGiant_Impostor_1428.mat`
- `_MainTex`: `Assets/_Project/Art/TEXTURES/clouds0_diff.png`, GUID `6c173d4e1a858b34ca1b7e5610aae988`
- `_DetailTex`: `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`, GUID `e1aefa60ab4517644bb884257440872b`
- `_StormTex`: `Assets/_Project/Art/TEXTURES/Aegir_storms.png`, GUID `d9d11072e85a2b54cacd11eaad6614a8`

Other resolved stack members:

- `MAT_AegirSky_Master.mat` binds `_AegirBandTex` to `Aegir_storms.png`.
- `MAT_SurfaceCloudPanorama_1428.mat` binds `_CloudTexA` to `clod1.png` and `_CloudTexB` to `clod2.png`.
- `MAT_AtmosphericCloudSheet_1428.mat` binds `_MainTex` to `oblakajip.png`.

Keep these references unless fresh visual proof rejects them. Static presence is not visual acceptance.

## Sun Ownership

Primary route: `PrimarySunDiscOwner=SkyMaterial`.

Rejected route: raw-enabling `SURFACE_LOW_SUN_DISC_1428`.

Basis:

- `HectonUnderwaterVisuals.ApplySunVisualState()` hides `sunVisualTransform` when `_cachedAtmoManager != null`.
- `HectonCelestialEngine.ApplySunOcclusion()` only toggles mesh sun when `skyOwnsPrimarySunDisc` is false.
- `HectonCelestialEngine.RestoreSunDefaults()` hides the mesh sun when `_atmosphereManager != null`.
- Old mesh sun material route is flat/untextured by prior static report and would duplicate visual authority.

## Unity Owner Pass Required

1. Open `Mat_HectonSky` in Unity.
2. Confirm effective shader properties through material/shader readback.
3. If `_MainCloudTex` is effective and null, bind `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`.
4. Leave `_HighCloudTex` and `_MainCloudAtlas` alone unless Unity proves they are effective.
5. Keep current Aegir/cloud stack unless screenshot proof rejects it.
6. Do not enable duplicate mesh sun.
7. Capture proof packet:
   - active skybox material GUID;
   - active sky shader GUID;
   - resolved texture slot manifest;
   - `PrimarySunDiscOwner=SkyMaterial`;
   - Aegir long view;
   - Aegir crop;
   - surface sun/sky view;
   - clean Unity console;
   - texture memory/residency note.

## Continuous GlobalQualityWeight Consequences

- Low: same sky-material sun owner, one Aegir owner, essential resident cloud map, no bloom dependency, no dark/fog concealment.
- Middle: repaired `_MainCloudTex` route, readable Aegir/clouds/sky in normal surface capture.
- High: richer cloud detail, halo/veil tuning, longer texture residency, stronger reflection/atmosphere without changing route truth.
- Ultra: higher-resolution proof captures, richer sky/cloud/Aegir overkill, no second sun owner and no gameplay truth change.

## Regression Model

CPU: no runtime code changed. Future material binding should not add per-frame CPU work.

GC: no hot-path allocation changed. Future proof strings stay editor/capture-only.

Memory/VRAM: no assets imported or rebound in this pass. Future `_MainCloudTex` binding must prove import compression, mip/streaming behavior, and compact residency.

Cadence: no update cadence changed. Future sky/Aegir feature richness must scale continuously through `GlobalQualityWeight`.

Correctness: primary sun visual authority remains single-owner. Stale texture rows remain unaccepted until Unity confirms them.

## Hot Path Impact

None. Report-only/static verification pass.

## Failure Modes

- `_MainCloudTex` remains null until Unity binding is performed.
- Stale serialized rows can be mistaken for active shader slots if a future pass ignores shader readback.
- Aegir can remain muddy/sticker-like despite valid texture GUIDs.
- Duplicate sun can reappear if old mesh sun is enabled by scene edit.
- Static reports can be misused as runtime acceptance. This report forbids that upgrade.

## Proof Boundary

No Unity Editor, Play Mode, Frame Debugger, profiler, GCMonitor, or screenshot proof was produced. Current disposition remains `PENDING VERIFICATION` for visual acceptance.
