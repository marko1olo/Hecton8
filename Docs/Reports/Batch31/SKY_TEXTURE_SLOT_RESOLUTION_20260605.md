# Sky Texture Slot Resolution - 2026-06-05

Status: STATIC INTEGRATION / NO UNITY ACCEPTANCE

Evidence class: `STATIC_SOURCE`, `SUBAGENT_STATIC_REPORT`.

## Verdict

`Assets/_Project/Art/Materials/Mat_HectonSky.mat` is the active serialized skybox path in `02_HECTON_WORLD` and `00_BOOTSTRAP`.

Two cloud slots in `Mat_HectonSky.mat` and `Mat_HectonSky_CloudOverlay.mat` are stale/deleted GUID references, not moved files:

- `_HighCloudTex` -> `97dacc0c8637b304f9451ecd290acffb`
- `_MainCloudAtlas` -> `161f2ad7f77e8bf408b29aa7e3d29966`

Do not fix these by guessing in raw YAML. Unity owner must inspect shader property names and material effective slots.

## Current Static Facts

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:29` uses `Mat_HectonSky` as `m_SkyboxMaterial`.
- `Assets/_Project/Scenes/00_BOOTSTRAP.unity:29` also references `Mat_HectonSky`.
- Missing cloud GUIDs appear only in the two materials and old reports/audits.
- `Mat_HectonSky_CloudOverlay.mat` already binds `_MainCloudTex` to `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`.

## Texture GUID Map

| Texture | GUID | Static size |
|---|---|---|
| `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png` | `0457f161a38fbb1489e989696048ed6c` | 2048x2048 |
| `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png` | `e1aefa60ab4517644bb884257440872b` | 2048x2048 |
| `Assets/_Project/Art/TEXTURES/Sky/clod1.png` | `d1e0a899aafb21d4eb46607799c9bfbb` | 2048x2048 |
| `Assets/_Project/Art/TEXTURES/Sky/clod2.png` | `ade59f8348cb0b74e97f6b73d58380b1` | 2048x2048 |
| `Assets/_Project/Art/TEXTURES/Sky/bo2.png` | `13a5b68ec75a4bc4b804b409e2ddcfe2` | 2048x2048 |
| `Assets/_Project/Art/TEXTURES/Sky/bo3.png` | `b49287f86c4ea7347a3aac351c07ced3` | 2048x2048 |
| `Assets/_Project/Art/TEXTURES/Sky/eb2.png` | `284c70c300e1f884bbd3b39b8efb49b1` | 2048x2048 |
| `Assets/_Project/Art/TEXTURES/clouds.png` | `cd47cc9e2fe0ec3448654aae6eaf7824` | 4096x2048 |
| `Assets/_Project/Art/TEXTURES/clouds0_diff.png` | `6c173d4e1a858b34ca1b7e5610aae988` | 4096x2048 |
| `Assets/_Project/Art/TEXTURES/Aegir_storms.png` | `d9d11072e85a2b54cacd11eaad6614a8` | 4096x2048 |
| `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png` | `e1b1feb9b4e2dee44a023824a82e7199` | 2048x2048 |

## Slot Resolution

| Material | Slot | Status | Candidate / Action |
|---|---|---|---|
| `Mat_HectonSky.mat` | `_MainCloudTex` | null | Candidate: `Sky/oblaka!.png`. High confidence because overlay already uses it and shader expects main cloud texture. |
| `Mat_HectonSky.mat` | `_HighCloudTex` | missing stale GUID | Likely legacy/stale if shader does not declare it. Candidate only if Unity/shader readback proves use: `Sky/oblakajip.png` or `Sky/clod1.png`. |
| `Mat_HectonSky.mat` | `_MainCloudAtlas` | missing stale GUID | Likely legacy/stale if shader declares `_MainCloudTex` instead. Do not bind blindly. |
| `Mat_HectonSky.mat` | `_StarTex` | `Sky/bo2.png` | Keep unless shader readback proves ignored or wrong. |
| `Mat_HectonSky.mat` | `_StarTwinkleLUT` | null | No obvious LUT found. `bo3.png`/`eb2.png` are low-confidence candidates only. |
| `Mat_HectonSky.mat` | `_BakedStarCubemap` | null | Needs actual 2DArray/cubemap asset, not a PNG guess. |
| `Mat_HectonSky_CloudOverlay.mat` | `_MainCloudTex` | `Sky/oblaka!.png` | Keep. |
| `MAT_AegirSky_Master.mat` | `_AegirBandTex` | `Aegir_storms.png` | Keep. |
| `MAT_SurfaceCloudPanorama_1428.mat` | `_CloudTexA`, `_CloudTexB` | `clod1.png`, `clod2.png` | Keep. |
| `MAT_AtmosphericCloudSheet_1428.mat` | `_MainTex` | `oblakajip.png` | Keep. |
| `MAT_H8SurfaceCloudDeck_1428.mat` | `_BaseMap` | `oblaka!.png` | Keep. |

## Required Unity Owner Pass

1. Read this report and `MATERIAL_TEXTURE_CRITICALS_20260605.md`.
2. Inspect active skybox material and shader properties in Unity.
3. Confirm whether `_HighCloudTex` and `_MainCloudAtlas` are effective shader properties or stale serialized rows.
4. If `_MainCloudTex` is null and effective, bind `Sky/oblaka!.png`.
5. Do not change cloud panorama/sheet/Aegir materials that already resolve unless visual proof fails.
6. Capture active skybox material GUID, shader GUID, Aegir material/shader GUID, resolved texture slots, and surface screenshot in the proof packet.

## Rejection Gates

- No dark/fog fallback.
- No duplicate sun mesh quick fix.
- No raw YAML edit.
- No claim that stale slots are fixed until Unity material readback and screenshot proof exist.

## Current Disposition

`PENDING VERIFICATION`.
