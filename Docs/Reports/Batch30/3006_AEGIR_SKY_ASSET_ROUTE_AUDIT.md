# Batch30 Worker 3006 - Aegir / Sky Asset Route Audit

Status: STATIC VERIFIED / VISUAL ACCEPTANCE PENDING UNITY  
Date: 2026-06-04  
Scope: Aegir, sky, sun-disc, cloud, and celestial asset route audit. No Unity, no build, no Assets edits.

Write path:
- `Docs/Reports/Batch30/3006_AEGIR_SKY_ASSET_ROUTE_AUDIT.md`

## Evidence Classes

- `STATIC_DOC`: authority docs and prior reports were read.
- `STATIC_SOURCE`: C# and shader source text was inspected.
- `STATIC_YAML`: scene and material YAML was inspected.
- `STATIC_ASSET_METADATA`: asset paths, GUIDs, file sizes, and image dimensions were inspected.
- `PLAYER_CAPTURE_ARTIFACT`: existing screenshot files were visually reviewed as files on disk. This does not prove current runtime state.
- `PENDING_VERIFICATION`: Unity import, Play Mode, console health, Frame Debugger, profiler, texture residency, and final visual acceptance.

## Authority Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `celestial.md`
- `atmosphere.md`
- `rendering.md`
- `shaders.md`
- `quality.md`

Mandates followed:
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

Prior reports inspected:
- `Docs/Reports/Batch25/2503_UNDERWATER_VISUALS_OWNER_PHASE_AUDIT.md`
- `Docs/Reports/Batch27/2705_AEGIR_SKY_OWNER_VISUAL_POLISH_ROUTE_AUDIT.md`
- `Docs/Reports/Batch29/2901_ROUTE_AWARE_SUN_WARNING_PATCH_PLAN.md`

Screenshots inspected:
- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.png` - 1280x720, 1234539 bytes.
- `Docs/Screenshots/MCP/h8_1908_surface_runtime_ui_on.png` - 1008x567, 661304 bytes.
- `Docs/Screenshots/1428_sky_foam_caustics_pass_game.png` - 1008x567, 427586 bytes.

## Static Findings

### 1. Current skybox owner is still `Mat_HectonSky`

Claim: the scene skybox, `HectonUnderwaterVisuals.skyMaterial`, and `HectonCelestialEngine._skyMaterial` point at `Mat_HectonSky.mat` GUID `c94a1beef2372b8458941c2ed9d05d5e`.  
Evidence Class: `STATIC_YAML`  
Artifacts:
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:29`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4652`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:90895`

Material state:
- `Assets/_Project/Art/Materials/Mat_HectonSky.mat` uses shader GUID `6302a783d2378694c9db8d0036358965`.
- `_SunSize`, `_SunDiscColor`, `_SunScatterIntensity`, `_AegirHaloIntensity`, and sky color fields exist in the material.
- `_StarTex` resolves to `Assets/_Project/Art/TEXTURES/Sky/bo2.png` GUID `13a5b68ec75a4bc4b804b409e2ddcfe2`.
- `_BakedStarCubemap`, `_StarTwinkleLUT`, and `_MainCloudTex` are null in `Mat_HectonSky.mat`.
- `_HighCloudTex` GUID `97dacc0c8637b304f9451ecd290acffb` and `_MainCloudAtlas` GUID `161f2ad7f77e8bf408b29aa7e3d29966` did not resolve to asset `.meta` files under `Assets` by static search. That is a sky material state risk, not runtime proof.

Risk: the selected sky owner is correct, but active material texture slots are not proof-clean. Normal surface sky acceptance needs texture residency/readback, not static YAML.

### 2. Current active Aegir route is the impostor material

Claim: the active Aegir scene route uses `MAT_AegirGasGiant_Impostor_1428.mat` GUID `ab7b03af667690149bdc7be9a1ae023c`.  
Evidence Class: `STATIC_YAML`  
Artifact: `Assets/_Project/Scenes/02_HECTON_WORLD.unity:89860-89893`

Static route:
- `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` is active in the scene override.
- Its renderer is enabled.
- It uses material `MAT_AegirGasGiant_Impostor_1428.mat`.
- `H8_AEGIR_SKY_BACKDROP_1428` exists but its `MeshRenderer` is disabled at `02_HECTON_WORLD.unity:94851-94864`.

Active material texture slots:
- `_MainTex`: `Assets/_Project/Art/TEXTURES/clouds0_diff.png`, 4096x2048, GUID `6c173d4e1a858b34ca1b7e5610aae988`.
- `_DetailTex`: `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`, 2048x2048, GUID `e1aefa60ab4517644bb884257440872b`.
- `_StormTex`: `Assets/_Project/Art/TEXTURES/Aegir_storms.png`, 4096x2048, GUID `d9d11072e85a2b54cacd11eaad6614a8`.

Material/shader risk:
- `MAT_AegirGasGiant_Impostor_1428.mat` has `_HorizonVeilStrength: 0.76`, `_HorizonVeilStart: -0.025`, `_RimStrength: 0.58`, `_RimTint.b: 1.3`, `_DetailStrength: 1.08`, `_StormStrength: 0.62`.
- `H8_AegirGasGiantImpostor_1428.shader` samples three textures and blends authored bands with controlled tint, rim, storm, phase, and horizon veil.
- The shader is texture-driven, not pure sine stripes, but the current material/texture/parameter/crop route still produces muddy/sticker risk in available captures.
- Static search found no direct `GlobalQualityWeight` hook in this current impostor shader. Continuous scaling exists elsewhere for firmament and in `Sky/Hecton_AegirSky.shader`, but the active Aegir impostor route needs an explicit continuous fidelity/residency path before acceptance.

### 3. Screenshot review rejects current Aegir acceptance

Claim: the inspected screenshot artifacts do not prove an accepted Aegir/sky route.  
Evidence Class: `PLAYER_CAPTURE_ARTIFACT`  
Artifacts:
- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.png`
- `Docs/Screenshots/MCP/h8_1908_surface_runtime_ui_on.png`
- `Docs/Screenshots/1428_sky_foam_caustics_pass_game.png`

Observed issues:
- `h8_1912_surface_edit_main.png`: Aegir reads as a huge translucent sphere with black/green muddy cloud masses, pale washed lower atmosphere, weak horizon integration, and dark foreground terrain/water. Not Subnautica-level surface/celestial beauty.
- `h8_1908_surface_runtime_ui_on.png`: water has repetitive dark green banding, terrain is near-black, and Aegir still reads blotchy/sticker-like rather than premium authored gas giant.
- `1428_sky_foam_caustics_pass_game.png`: older capture has better blue band/storm readability and surface lighting, but it is not current runtime proof and still needs crop inspection for seam, rim, veil, and horizon integration.

Verdict: Aegir remains `PENDING VERIFICATION`. Static texture presence and prior screenshots cannot accept it.

### 4. Raw-enabling old sun disc is not correct

Claim: `SURFACE_LOW_SUN_DISC_1428` must not be enabled as a quick fix.  
Evidence Class: `STATIC_YAML`, `STATIC_SOURCE`

Scene/material facts:
- `HectonUnderwaterVisuals.sunVisualTransform` points to fileID `1985271341` at `02_HECTON_WORLD.unity:4632`.
- `HectonCelestialEngine.sunVisualTransform` is `{fileID: 0}` at `02_HECTON_WORLD.unity:91163`.
- `SURFACE_LOW_SUN_DISC_1428` is inactive at `02_HECTON_WORLD.unity:95891-95896`.
- Its `MeshRenderer` is disabled at `02_HECTON_WORLD.unity:95912-95919`.
- Its material `Assets/_Project/Art/Materials/World/MAT_SurfaceSunDisc_1428.mat` has no `_BaseMap` or `_MainTex`, only a flat brown/orange base color.

Source route facts:
- `HectonCelestialEngine.ApplySunOcclusion()` sets `skyOwnsPrimarySunDisc = _atmosphereManager != null` and only toggles the mesh sun when sky ownership is false.
- `HectonCelestialEngine.RestoreSunDefaults()` hides `sunVisualTransform` when `_atmosphereManager != null`.
- `HectonUnderwaterVisuals.ApplySunVisualState()` hides the mesh sun when `_cachedAtmoManager != null`.
- Batch29's planned predicates `RequiresMeshSunVisual()` and `SkyMaterialOwnsPrimarySunDisc()` are not present in current source by static search.

Conclusion: the sky-material sun disc is the correct primary route, but it is still implicit. Raw-enabling the old mesh disc would create a second visual owner, fight existing source behavior, and use a flat untextured material below the visual floor.

### 5. Available asset candidates exist, but none are accepted yet

Claim: the project contains candidate authored/generated gas giant and sky assets that can replace or improve the muddy active impostor route.  
Evidence Class: `STATIC_ASSET_METADATA`, `STATIC_YAML`

Candidate gas giant sources:
- `Assets/_Project/Art/TEXTURES/clouds0_diff.png` - 4096x2048, GUID `6c173d4e1a858b34ca1b7e5610aae988`; active `_MainTex`.
- `Assets/_Project/Art/TEXTURES/clouds.png` - 4096x2048, GUID `cd47cc9e2fe0ec3448654aae6eaf7824`; not active in current impostor material.
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png` - 4096x2048, GUID `d9d11072e85a2b54cacd11eaad6614a8`; active `_StormTex`.
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png` - 2048x2048, GUID `e1b1feb9b4e2dee44a023824a82e7199`; used by `MAT_H8SurfaceGasGiantDisc_1428.mat`.
- `Assets/_Project/Art/TEXTURES/TX_H8SurfaceGasGiantDisc_1428.asset` - 11185863 bytes, GUID `0fdfe0cfeaf72244e8089c93cc9ce2a6`; static Texture2D asset candidate.
- `Assets/_Project/Art/TEXTURES/TX_H8SurfaceGasGiantBands_1428.asset` - 1049627 bytes, GUID `ac3a3a0c994dd894ea51da0eb8f8d958`; static Texture2D asset candidate.
- `Assets/_Project/Art/TEXTURES/TX_H8SurfaceGasGiantStormBands_1428.asset` - 5593468 bytes, GUID `5ff7f940682b3e648a0bcd478b1d4f89`; used by `MAT_SurfaceGasGiant_1428.mat`.
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png` - 4096x2048, GUID `58d380f7ee3056c40bc2868f906c3e86`; used by `MAT_H8AegirGasGiantReal_1428.mat`.

Candidate sky/cloud sources:
- `Assets/_Project/Art/TEXTURES/Sky/clod1.png` - 2048x2048, GUID `d1e0a899aafb21d4eb46607799c9bfbb`; used by `MAT_SurfaceCloudPanorama_1428.mat`.
- `Assets/_Project/Art/TEXTURES/Sky/clod2.png` - 2048x2048, GUID `ade59f8348cb0b74e97f6b73d58380b1`; used by `MAT_SurfaceCloudPanorama_1428.mat`.
- `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png` - GUID `0457f161a38fbb1489e989696048ed6c`; used by `Mat_HectonSky_CloudOverlay.mat` as `_MainCloudTex`.
- `Assets/_Project/Art/TEXTURES/Sky/bo2.png` - 2048x2048, GUID `13a5b68ec75a4bc4b804b409e2ddcfe2`; active `_StarTex` in `Mat_HectonSky`.
- `Assets/_Project/Art/Skyboxes/panorama_den.png`, `panorama_shtorm.png`, and `panorama_noch.png` exist as panorama candidates, but are old skybox material sources until proven route-safe.

Rejected as normal surface default:
- `Assets/_Project/Art/Materials/MAT_SurfaceNoirProceduralSkybox_1428.mat` uses built-in procedural skybox, `_Exposure: 0.42`, dark ground/sky tints, and no texture slots. It is not a normal bright surface route.
- `Assets/_Project/Art/Materials/World/MAT_SurfaceSunDisc_1428.mat` is flat and untextured. It is not a primary sun route.

## Owner-Correct Visual Polish Route

Recommended route:
1. Keep `PrimarySunDiscOwner=SkyMaterial`.
2. Implement the Batch29 route-aware predicates in a future source task:
   - `HectonUnderwaterVisuals.RequiresMeshSunVisual()`
   - `HectonCelestialEngine.SkyMaterialOwnsPrimarySunDisc()`
3. Make proof snapshots record `PrimarySunDiscOwner=SkyMaterial`, `MeshSunVisualRequired=false`, active skybox material GUID, active sky shader GUID, sun material values, Aegir material GUID, Aegir shader GUID, texture residency, and clean-console state.
4. Repair `Mat_HectonSky.mat` texture slots before visual acceptance:
   - resolve or replace missing `_HighCloudTex` and `_MainCloudAtlas` GUIDs;
   - assign a deliberate `_MainCloudTex` or prove shader procedural/cloud fallback is intentional;
   - do not promote `MAT_SurfaceNoirProceduralSkybox_1428.mat` as normal surface default.
5. Keep one active Aegir owner. Do not activate `H8_AEGIR_SKY_BACKDROP_1428` or old alternate discs as parallel truth.
6. Replace the muddy Aegir output through controlled texture authoring:
   - use the existing 4K gas giant cloud/storm sources or the 2K baked disc as inputs;
   - generate or author a reviewed equirectangular/baked Aegir texture offline;
   - feed it into the current impostor material or migrate to `MAT_H8AegirGasGiantReal_1428` / `MAT_H8SurfaceGasGiantDisc_1428` only after crop proof;
   - keep import/compression/residency inside texture budget and prove Unity import state later.
7. Tune rim, veil, phase, detail, storm, and horizon integration against long view and crop view. Static values are suspicious because current captures show sticker/veil/mud risk.
8. Produce fresh proof:
   - surface coast/Aegir long view, UI off;
   - surface sky/sun view;
   - Aegir crop showing bands/rim/veil/seam;
   - shoreline close view;
   - 0-5 m underwater view;
   - 20-50 m photic route view;
   - clean Unity console log;
   - Frame Debugger/RenderGraph proof if render path changes;
   - profiler/GC/VRAM proof if runtime code, texture residency, or render features change.

## Continuous Quality Scaling Consequences

Compact / low:
- Same sky-material sun ownership.
- Same single Aegir owner.
- Preserve readable Aegir, sky, clouds, ocean color, shoreline, and route silhouettes.
- Reduce optional cloud layers, star/baked cubemap readiness, reflection resolution, diagnostic capture cadence, and texture mip residency before reducing surface readability.
- No bloom dependency, no normal surface noir fallback, no flat mesh sun, no disabled Aegir textures on surface.

Middle:
- Baseline target for acceptance.
- Use `Mat_HectonSky` plus a repaired Aegir material with resident cloud/storm textures.
- Surface and photic captures must look good without hiding behind fog, darkness, or crop selection.

High:
- Spend saved budget on richer cloud depth, cleaner Aegir halo/veil, better shoreline reflection/contact, longer texture residency, and higher crop stability.
- Do not change sun ownership, celestial phase truth, or route authority.

Ultra:
- Visual overkill through higher-resolution authored/baked Aegir textures, denser atmospheric layering, richer sky/cloud detail, stronger controlled scattering, and high-resolution proof captures.
- No second sun owner and no gameplay truth change.

Gap: the active `H8_AegirGasGiantImpostor_1428.shader` has no direct `GlobalQualityWeight` input by static search. Future source work should add a continuous visual/residency control path or route through a shader/material owner that already consumes `GlobalQualityWeight`.

## Regression Model

CPU:
- Report-only task. No runtime CPU change.
- Future route-aware source patch must keep predicates pure, read-only, no scene search, no allocation, no signal publish, and no hot `GlobalRegistry` lookup.

GC:
- Report-only task. No runtime GC change.
- Future proof snapshot formatting must run outside hot paths. Managed strings stay in editor/capture harness, not in unmanaged signal payloads.

Memory / VRAM:
- Report-only task. No asset import or residency change.
- Future texture swaps must prove compressed import, mip/streaming behavior, and compact VRAM budget. Existing candidate files are static presence only.

Cadence:
- Report-only task. No update cadence change.
- Future Aegir/sky quality scaling must use continuous `GlobalQualityWeight`, not binary low/high switches.

Correctness:
- Do not create two primary sun visual owners.
- Do not let deep/performance celestial texture detachment affect surface proof.
- Do not accept broken sky material GUIDs or null cloud slots as final surface proof without runtime material readback and captures.

## Hot Path Impact

No code or asset changes were made. Hot path impact is none for this task.

## Failure Modes

- Missing sky cloud GUIDs leave `Mat_HectonSky` visually dependent on procedural/fallback behavior.
- Active Aegir impostor can remain muddy, sticker-like, or seam-visible despite 4K source textures.
- Old mesh sun can be accidentally reactivated and create duplicate sun truth.
- `HectonUnderwaterVisuals` can keep route-agnostic sunVisual resolution/warning behavior until Batch29's plan is implemented.
- Deep texture detachment can null Aegir/sky textures outside surface proof if proof capture does not record residency state.
- Static reports can be misread as runtime acceptance. They are not.

## Why Kept / Rejected

Kept:
- `Mat_HectonSky.mat` as primary sky/sun route because source and scene already route sky/sun state through it.
- `MAT_AegirGasGiant_Impostor_1428.mat` as current active Aegir owner only until an authored/baked replacement is proven. It is an owner route, not a quality acceptance.
- Existing 4K/2K Aegir and cloud textures as candidate inputs.

Rejected:
- Raw enabling `SURFACE_LOW_SUN_DISC_1428`.
- Promoting `MAT_SurfaceNoirProceduralSkybox_1428.mat` as normal surface sky.
- Accepting current Aegir from static texture presence.
- Accepting any Aegir route without long/crop screenshot proof, texture residency proof, and clean log binding.

## First-20-Minutes Route Relevance

This removes a surface/photic-shallow visual proof blocker. The first exit must be bright, beautiful, readable, alien, and uneasy. Current Aegir/sky evidence is not accepted until the sky material slots, Aegir texture route, sun owner metadata, and fresh capture packet are proven.

## Proof Boundary

This report proves only static docs/source/YAML/assets and existing screenshot review. It does not prove Unity import, Play Mode behavior, console health, render feature state, material runtime values, texture residency, profiler cost, GC behavior, or final visual quality.
