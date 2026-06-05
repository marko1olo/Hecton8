# 2602 Foam / Caustics / Crest Material Audit

Worker: Batch26 2602 - Foam / Caustics / Crest Material Auditor  
Date: 2026-06-04  
Evidence class: STATIC VERIFIED only. No Unity Editor, Play Mode, build, process kill, profiler capture, or screenshot capture was run by task constraint.  
Authority: `AGENTS.md`, `VISION_LOCKS.md`, `water.md`, `rendering.md`, `shaders.md`, `quality.md`, `TASTE.md`, Batch25 synthesis, relevant REND/OPT mandates.

## Verdict

Acceptance remains rejected until runtime proof exists. Static evidence explains the Batch25/1474 failures without claiming a runtime result.

1. Active surface Crest route uses `Ocean.mat`. The Batch25 hard clip blocker on this material is repaired in YAML, but the base diffuse is still dark blue/green and final brightness/foam readability is unproven.
2. Active underwater owner references `Ocean-Underwater.mat`, which serializes caustics off and hard clip/transparency keywords on. That can explain no visible caustics and slab/curtain-looking underwater output if runtime owner state does not override it correctly.
3. Crest foam infrastructure exists in scene, and the scene overrides the OceanRenderer foam sim on. That proves route wiring, not final visible shoreline foam.
4. First-party floor caustic fake `H8_FloorCausticSoft_1443` is active and renderer-enabled, but shader/material evidence shows a transparent additive sine fake with no intrinsic depth/light/shadow gate. It needs route owner gating and image proof.
5. `H8_UnderwaterHazeCurtain_1454`, low-water occlusion slabs, pressure lid, depth shelf, and depth ceiling occlusion are serialized renderer-disabled or inactive. They are not the current serialized visible cause, but raw-enabling them would create false volume/slab risk.

First-20-minutes route impact: this audit narrows the likely blocker to active underwater material state, final camera/light route proof, and foam/caustic visibility verification around surface exit, shoreline, and photic shallows.

## Material YAML Evidence

### `Assets/Crest/Crest/Materials/Ocean.mat`

Active scene surface material through `Ocean_Crest.prefab` override.

- `m_Name: Ocean` line 10.
- Valid keywords include `_CAUSTICS_ON` line 17, `_FOAM_ON` line 18, `_UNDERWATER_ON` line 23.
- `_Caustics: 1` line 102; `_CausticsStrength: 0.56` line 109.
- `_ClipSurface: 0` line 112; `_ClipUnderTerrain: 0` line 113. Static clip blocker is repaired for active surface material.
- `_Foam: 1` line 128; `_FoamScale: 0.044` line 131; `_ShorelineFoamMinDepth: 0.82` line 156.
- `_Transparency: 0` line 177; `_Underwater: 1` line 178.
- `_WaveFoamBubblesCoverage: 0.42` line 181; `_WaveFoamStrength: 1.25` line 193.
- `_DepthFogDensity: {r: 0.025, g: 0.032, b: 0.04}` line 195.
- `_Diffuse: {r: 0.012, g: 0.076, b: 0.132}` line 196. Dark green/blue base remains a visual risk unless sun/sky/specular/shallow color lifts the final image.
- `_DiffuseGrazing: {r: 0.105, g: 0.285, b: 0.435}` line 197.
- `_FoamBubbleColor` and `_FoamWhiteColor` are bright cyan/white at lines 199-200.

Risk: surface clip is fixed, but material YAML alone does not prove bright premium water, readable shoreline foam, or photic shallows. Runtime captures must show this material under route lighting, not isolated inspector values.

### `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`

Active underwater material reference on `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`.

- Valid keywords include `_CLIPSURFACE_ON` line 16, `_CLIPUNDERTERRAIN_ON` line 17, `_FOAM_ON` line 19, `_TRANSPARENCY_ON` line 24, `_UNDERWATER_ON` line 25.
- `_Caustics: 0` line 104; `_CausticsStrength: 0` line 111.
- `_ClipSurface: 1` line 114; `_ClipUnderTerrain: 1` line 115.
- `_Foam: 1` line 130; `_FoamScale: 1.1` line 133.
- `_ShorelineFoamMinDepth: 1.15` line 158.
- `_Transparency: 1` line 179; `_Underwater: 1` line 180.
- `_WaveFoamBubblesCoverage: 1.68` line 183; `_WaveFoamStrength: 1.25` line 195.
- `_DepthFogDensity` line 197.
- `_FoamBubbleColor: {r: 0, g: 0, b: 0}` line 201. Black foam bubble color is incompatible with premium readable photic water if visible.

Risk: this is the strongest static explanation for no underwater caustics and hard clipped/slab-looking underwater output. Runtime owner may override these values, but no proof was produced in this audit.

### `Assets/Crest/Crest/Materials/Ocean_UnderwaterCurtain.mat`

Assigned to Crest vendor `UnderWaterCurtainGeom.prefab`; not assigned to scene `UnderwaterRenderer._volumeGeometry`.

- Valid keywords include `_CAUSTICS_ON` line 17, `_FOAM_ON` line 19, `_UNDERWATER_ON` line 25.
- No `_TRANSPARENCY_ON` or `_CLIPUNDERTERRAIN_ON` keyword found in YAML.
- `_CausticsStrength: 10` line 53.
- `_ClipSurface: 0` line 56.
- `_FoamScale: 15` line 60.
- `_ShorelineFoamMinDepth: 0.86` line 72.
- `_Underwater: 1` line 84.
- `_WaveFoamBubblesCoverage: 1.78` line 85.
- `_DepthFogDensity: {r: 0.2, g: 0.15, b: 0.15, a: 1}` line 92.
- `_DiffuseGrazing: {r: 0, g: 0, b: 0}` line 94.
- `_FoamBubbleColor: {r: 0.43537414, g: 1, b: 0}` line 96. Neon green bubble risk.

Risk: not current serialized visible route, but unsafe to raw-enable. Values are overdriven and can create fake green curtain/caustic noise instead of believable volume.

### `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`

First-party candidate material. GUID scan found no scene/prefab reference in current route scan.

- Valid keywords include `_CAUSTICS_ON` line 17, `_CLIPSURFACE_ON` line 18, `_CLIPUNDERTERRAIN_ON` line 19, `_FOAM_ON` line 20, `_TRANSPARENCY_ON` line 25, `_UNDERWATER_ON` line 26.
- `_Caustics: 1` line 105; `_CausticsStrength: 1.45` line 112.
- `_ClipSurface: 1` line 115; `_ClipUnderTerrain: 1` line 116.
- `_Foam: 1` line 131; `_FoamScale: 0.019` line 134.
- `_ShorelineFoamMinDepth: 3.75` line 159.
- `_Transparency: 1` line 180; `_Underwater: 1` line 181.
- `_WaveFoamBubblesCoverage: 1.95` line 184; `_WaveFoamStrength: 3.45` line 196.

Risk: do not treat this as active route proof. If assigned, it reintroduces surface/underterrain clip keywords and overdriven foam/caustic settings.

### `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_FloorCausticSoft_1443.mat`

Active scene material on `H8_FloorCausticSoft_1443`.

- Render queue 3018 line 19.
- `_ScaleA: 1.05` line 28; `_ScaleB: 1.72` line 29; `_Sharpness: 8.2` line 30.
- `_Tint: {r: 0.58, g: 0.92, b: 1, a: 0.24}` line 32.
- Shader is transparent additive: `Blend SrcAlpha One`, `ZWrite Off`, `Cull Off`.
- Shader generates sine-wave world-space XZ caustic patterns; it has no intrinsic route depth, sun, cloud, eclipse, or occlusion validation.

Risk: acceptable only as a premium fake under the correct light/depth owner. It must not appear in abyss, caves, storms, eclipse darkness, or behind blocking geometry without an explicit believable light reason.

### `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_UnderwaterHazeCurtain_1454.mat`

Serialized inactive/renderer-disabled scene object.

- `_CausticScale: 0.38` line 28; `_Softness: 1.42` line 29.
- `_BottomColor: {r: 0.18, g: 0.56, b: 0.48, a: 0.42}` line 31.
- `_TopColor: {r: 0.075, g: 0.34, b: 0.37, a: 0.72}` line 33.
- Shader is transparent alpha, `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, `Cull Off`, with vertical band and sinusoidal shimmer.

Risk: if enabled without owner gating and route proof, it becomes a false green underwater curtain, not premium underwater volume.

### Foam Input And Authored Foam Materials

- `MAT_H8_CrestFoamInput_1464.mat`: render queue 3000 line 19, `_Strength: 4.8` line 28. Used by the scene Crest foam input pass.
- `MAT_H8_ShorelineFoamFine_1469.mat`: render queue 3012 line 20, `_Alpha: 0.72` line 41, `_Softness: 0.42` line 53, `_Surface: 1` line 56, `_Threshold: 0.18` line 57, `_ZWrite: 0` line 59.
- `MAT_H8_PhoticShoreFoamOrganic_1428.mat`: render queue 3012 line 20, `_Alpha: 0.82` line 41, `_Softness: 0.31` line 53, `_ZWrite: 0` line 59.

Risk: authored foam exists, but transparent ZWrite-off foam layers can fail from camera composition, overdraw, sorting, or insufficient route placement. Static existence is not acceptance.

## Scene Owner And Binding Evidence

### Active Crest Ocean

`Assets/_Project/Scenes/02_HECTON_WORLD.unity`

- `Ocean_Crest.prefab` instance override renames route ocean to `H8_WORLD_CREST_OCEAN_RUNTIME_1428` around line 43216.
- Override `_material` points to `Ocean.mat` GUID `9def92ac79181fe41b238e91663f0fad` at lines 43187-43189.
- Override `_createFoamSim` is `1` at lines 43195-43196.
- Override `_globalWindSpeed` is `28` at lines 43199-43200.

`Assets/_Project/Prefabs/Ocean_Crest.prefab`

- Base `Crest.OceanRenderer` `_material` points to `Ocean.mat` line 463.
- Base `_createFoamSim: 0` line 482; scene override turns it on.
- `Crest.RegisterFoamInput` exists, with `_disableRenderer: 1` line 333. Hidden renderer is expected for input registration and does not by itself mean foam input is absent.

Risk: route binding is correct for active `Ocean.mat`, but acceptance still needs proof that prefab override applies at runtime and Crest foam textures receive the input.

### Underwater Owner

`Assets/_Project/Scenes/02_HECTON_WORLD.unity`

- `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` line 4608.
- `oceanUnderwaterMaterial` references `Ocean-Underwater.mat` GUID `ef94c26e44a36e24a9dcbc5995a2bed1` line 4651.
- `enableShallowCaustics: 1` line 4743.
- `adaptiveCausticsBudgetFloor: 0.72` line 4803.
- Serialized debug values are `_debugIsUnderwater: 0` line 4842, `_debugCausticsStrength: 0` line 4843, `_debugSunVisualActive: 0` line 4870. These are not runtime proof, but they show no captured successful state in YAML.

Risk: the owner exists and has shallow caustics enabled, but active material defaults still say caustics off. Runtime owner must prove it is writing correct continuous values before acceptance.

### Crest UnderwaterRenderer

`Assets/_Project/Scenes/02_HECTON_WORLD.unity`

- `Crest.UnderwaterRenderer` line 67216.
- `_mode: 0` line 67218.
- `_filterOceanData: 13` line 67219.
- `_meniscus: 0` line 67220.
- `_volumeGeometry: {fileID: 0}` line 67222.
- `_copyOceanMaterialParamsEachFrame: 1` line 67228.
- `_viewOceanMask: 0` line 67231; `_disableOceanMask: 0` line 67232.

Risk: no explicit underwater volume geometry is assigned in scene. Copying ocean material params each frame can also erase manual material-only fixes. The owner route must be audited before tuning only the material asset.

### Crest Foam Input Pass

`Assets/_Project/Scenes/02_HECTON_WORLD.unity`

- `H8_CREST_FOAM_INPUT_PASS_1464` line 38681.
- `Crest.RegisterFoamInput` line 38698.
- `_disableRenderer: 1` line 38701.
- GameObject is active, RegisterFoamInput is enabled, MeshRenderer is disabled, material is `MAT_H8_CrestFoamInput_1464`, mesh is `MESH_H8_CrestFoamInput_1464`.

Risk: static setup supports input registration, but route proof must show Crest foam texture contribution and visible shoreline response.

### Visible Authored Foam Objects

Scene names found in `02_HECTON_WORLD.unity` include:

- `H8_BrokenShoreFoam_Inner_1434` line 29072.
- `H8_BrokenShoreFoam_Outer_1434` line 47823.
- `H8_SHORELINE_FOAM_RING_ASSET_1428` line 60518.
- `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` line 73844.
- `H8_BrokenShoreFoam_1439` line 77384.

Risk: assets are present, but final camera sees what the route frames show. No screenshot proof exists from this audit.

### Caustic And Haze Owners

`H8_FloorCausticSoft_1443`

- Scene object around line 64133.
- GameObject active at line 64138.
- MeshRenderer enabled at line 64161.
- Material GUID `dfaebc7c2bdb3ec44b4523487f34ce44` line 64179.
- Mesh GUID `f715884a162ee6c4fbc2846cf6f8eac9` line 64210.

`H8_UnderwaterHazeCurtain_1454`

- Scene object around line 93776.
- `m_IsActive: 0` line 93781.
- MeshRenderer `m_Enabled: 0` line 93804.
- Material GUID `242d3b4049cce8a498e5ee62bfaa628f` line 93822.
- Mesh GUID `5760d7ec1738e474ca1d9319eadb0122` line 93853.

Risk: floor caustic is active; haze curtain is not. Acceptance must verify caustics are visible where physically believable and haze curtain remains off unless explicitly owned and proven.

### Occlusion Slabs And Lid Objects

Serialized risk objects in `02_HECTON_WORLD.unity`:

- `NOIR_UPPER_PRESSURE_LID`: active object, transform position `{x: 0, y: 8.6, z: 5}`, scale `{x: 38, y: 0.25, z: 30}`, MeshRenderer disabled.
- `H8_WORLD_LOW_WATER_OCCLUSION_00_1428`: active object, transform position `{x: -7, y: -0.07, z: 18.8}`, scale `{x: 3.2, y: 0.06, z: 0.4}`, MeshRenderer disabled.
- `H8_WORLD_LOW_WATER_OCCLUSION_01_1428`, `_02_1428`, `_03_1428`: active objects with MeshRenderer disabled.
- `H8_DEPTH_LOW_SHELF_1428`: active object, transform position `{x: 0, y: -0.9, z: 30}`, scale `{x: 58, y: 1.15, z: 8}`, MeshRenderer disabled.
- `H8_DEPTH_CEILING_OCCLUSION_1428`: active object, transform position `{x: -4, y: 7.8, z: 25}`, scale `{x: 70, y: 1, z: 8}`, MeshRenderer disabled.

Risk: not a current serialized visible blocker. They remain dangerous if another owner or runtime toggle enables renderers without low-oblique proof.

## Current Flag And Keyword Risks

1. `Ocean-Underwater.mat` is active in the underwater owner and serializes `_CLIPSURFACE_ON`, `_CLIPUNDERTERRAIN_ON`, `_TRANSPARENCY_ON`, `_Caustics: 0`, and `_CausticsStrength: 0`.
2. `Ocean.mat` active surface clip values are repaired, but `_Diffuse` is dark green/blue and `_FoamScale` is low. Final premium water is unproven.
3. `MAT_H8_SurfaceCrestOcean_1428` is not active, and if assigned it reintroduces clip/transparency/underwater keywords plus overdriven foam and caustics.
4. `Ocean_UnderwaterCurtain.mat` is disabled/unassigned but has `_CausticsStrength: 10`, `_FoamScale: 15`, black grazing, and neon green foam bubble color.
5. `UnderwaterRenderer._copyOceanMaterialParamsEachFrame: 1` means asset-only hand tuning can be overwritten or produce false confidence.
6. First-party foam and caustic fakes are transparent `ZWrite Off`. They need sorting, overdraw, and route-camera proof.
7. Serialized debug values on `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` do not show a successful underwater/caustic runtime state.

## Safe Owner-Correct Plan

1. Keep active surface route bound to the owner-verified Crest ocean material path. Do not swap to `MAT_H8_SurfaceCrestOcean_1428` unless its clip/transparency/underwater flags are corrected and route captures prove the result.
2. Fix underwater from the owner route, not by random material edits. `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` and `Crest.UnderwaterRenderer` must own underwater material state, caustic strength, depth/light gates, and keyword state.
3. Treat `Ocean-Underwater.mat` as the primary static blocker. Caustics must not remain at zero in lit photic shallows. Clip keywords must not create slab/curtain artifacts in route captures.
4. Preserve the scene `_createFoamSim: 1` override and verify it at runtime. Do not count hidden `RegisterFoamInput` renderer state as a failure by itself; proof is Crest foam texture contribution and shoreline visual response.
5. Keep `H8_FloorCausticSoft_1443` as fake-first caustic only if owner-gated by light/depth/route. It must disappear or degrade smoothly in abyss, caves, storms, and eclipse windows.
6. Do not raw-enable `H8_UnderwaterHazeCurtain_1454`, occlusion slabs, pressure lid, low shelf, or depth ceiling. Enable only with explicit owner route, material value reduction, and low-oblique regression screenshots.
7. All quality scaling must consume continuous `GlobalQualityWeight`. No binary low/high switch, no gameplay truth/layout/save identity changes from quality.
8. Any final fix must pass all three pillars: premium visual read, frame cost/GC proof, and gameplay route clarity. Beautiful but empty, fast but flat, and visually dense but slow are all rejected.

## GlobalQualityWeight Consequences

These are anchor consequences, not binary tiers. Values must interpolate continuously from `0.0` to `1.0`.

- Low / Minimum Survival: clip-free bright surface read, visible authored shoreline foam silhouettes, restrained Crest foam cost, shallow-only caustic hints on lit floor, no haze curtain, no visible slabs, no GC.
- Mid: Crest foam simulation visibly contributes around route shore and wakes, authored foam decals add breakup, underwater material receives nonzero light-gated caustics, floor caustic fake is present only in shallow lit route spaces.
- High: denser foam breakup, richer surface specular/normal response, stronger but bounded caustic pattern, controlled underwater haze from owner state, profiler-proven transparent overdraw.
- Ultra / Visual Overkill: layered shoreline foam, high-frequency premium caustic variation, richer photic volume and surface sparkle, optional volume geometry only after no-slab proof. Gameplay truth ownership, DTO layout, save identity, and route authority do not change.

## Required Proof Before Acceptance

No proof image or metadata was produced in this audit by constraint. Required proof packet before any acceptance claim:

1. Six route screenshots or frame captures: surface/coast/Aegir, shoreline close foam/wet contact, underwater 0-5m photic shallows, underwater medium depth route, Aegir/celestial long view, and low-oblique slab regression.
2. For every capture: scene name, camera position/rotation/FOV, route label, timestamp, resolution, render scale, hardware/tier, `GlobalQualityWeight`, weather/light state, sun/moon state, depth, and player underwater state.
3. Material metadata at capture time: active ocean material GUID/name, underwater material GUID/name, valid keywords, `_ClipSurface`, `_ClipUnderTerrain`, `_Caustics`, `_CausticsStrength`, `_Foam`, `_FoamScale`, `_ShorelineFoamMinDepth`, `_Transparency`, `_Diffuse`, `_FoamBubbleColor`, and `_FoamWhiteColor`.
4. Owner metadata: `OceanRenderer._createFoamSim`, active foam sim texture or debug evidence, `RegisterFoamInput` registration state, `UnderwaterRenderer._mode`, `_volumeGeometry`, `_copyOceanMaterialParamsEachFrame`, ocean mask state, and `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` debug caustics/light/depth values.
5. Object-state proof: `H8_FloorCausticSoft_1443`, `H8_UnderwaterHazeCurtain_1454`, low-water occlusion slabs, pressure lid, low shelf, and depth ceiling active/renderer states.
6. Frame Debugger or RenderGraph evidence that foam, caustic, and underwater passes render in the intended order and are not hidden by sorting, masks, occlusion, or keyword stripping.
7. Profiler proof at Low/Mid/High/Ultra anchor weights: frame cost, transparent overdraw, VRAM delta, CPU cost, GPU cost, managed allocations, and no same-frame schedule/readback loop.
8. Log/import proof newer than screenshots: no compile errors, no material import churn after capture, no stale WeatherEvents leak, no dirty log masquerading as acceptance.

## Non-Acceptance Notes

- Static YAML evidence can identify blockers and risk. It cannot certify Subnautica-level water.
- Current route should not be accepted until live images prove bright readable surface water, visible shoreline foam, visible believable caustics, no slab/curtain artifact, and no false underwater view.
- No files were edited except this report.
