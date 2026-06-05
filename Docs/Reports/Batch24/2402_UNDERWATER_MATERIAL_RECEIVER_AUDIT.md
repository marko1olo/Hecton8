# 2402 Underwater Material Receiver Audit

Status: STATIC AUDIT ONLY - PENDING UNITY/PROFILER VERIFICATION
Worker: 2402
Scope: underwater/caustic/haze/particle material receivers affecting the 1474 underwater diagnostic.

## Evidence Read

- `Docs/Screenshots/MCP/h8_1474_diag_underwater_route_from_mcp.png`
- `Docs/Reports/Batch23/BATCH23_SYNTHESIS_FOR_CONTROLLER.md`
- `Docs/Reports/Batch23/2303_FOAM_CAUSTIC_PATCH_PLAN.md`
- `Assets/Crest/Crest/Materials/Ocean.mat`
- `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
- `Assets/_Project/Data/Ocean/Sim_Settings_Foam.asset`
- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_FloorCausticSoft_1443.mat`
- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticSoftWaterHaze_1430.mat`
- `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_CrestFoamInput_1464.mat`
- `Assets/_Project/Art/Materials/World/Photic1469/MAT_H8_ShorelineFoamFine_1469.mat`
- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_UnderwaterSpecks_1446.mat`
- `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_UnderwaterHazeCurtain_1454.mat`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` static YAML only.

No Unity, builds, imports, scene edits, material edits, shader edits, or code edits were run.

## 1474 Screenshot Read

`h8_1474_diag_underwater_route_from_mcp.png` still fails visually as static image evidence:

- broad dark horizontal slab/curtain dominates the mid-background;
- underwater particles read absent or too sparse for route atmosphere;
- caustic read is a small bright streak, not subtle surface-driven lace across receivers;
- seabed/rocks look exposed and dry-flat rather than held inside photic water volume;
- visible water surface shimmer exists, but the underwater volume lacks depth layering.

This is not visual acceptance. It is a material/receiver rejection target for the next Unity owner.

## Current Material/Receiver State

### Active floor caustic receiver

`H8_FloorCausticSoft_1443` is active in `02_HECTON_WORLD.unity`.

- GameObject: `m_IsActive: 1`
- MeshRenderer: `m_Enabled: 1`
- Material: `MAT_H8_FloorCausticSoft_1443`
- Mesh: `MESH_H8_FloorCaustic...` GUID `f715884a162ee6c4fbc2846cf6f8eac9`
- Material values:
  - `_Tint: {r: 0.72, g: 0.96, b: 0.98, a: 0.42}`
  - `_ScaleA: 0.62`
  - `_ScaleB: 0.98`
  - `_Sharpness: 5.8`
- Shader route:
  - Transparent queue `Transparent+18`
  - `Blend SrcAlpha One`
  - `ZWrite Off`
  - procedural sine pattern in world XZ, no texture, no depth/light ownership input.

Risk:

- Additive alpha `0.42` plus broad receiver mesh can read as a glowing sheet if vertex alpha/mesh footprint is wide.
- Sine-only caustics can produce smooth bands or one bright streak instead of organic lace.
- There is no material-side depth gating, receiver mask texture, or light-source gating. It depends entirely on mesh placement/vertex alpha.

Rollback visible in YAML:

- Restore current material values: `_Tint.a 0.42`, `_ScaleA 0.62`, `_ScaleB 0.98`, `_Sharpness 5.8`; object active/enabled remains `1/1`.

### Photic soft water haze

`H8_PHOTIC_SOFT_WATER_HAZE_1430` is active and renderer-enabled.

- GameObject: `m_IsActive: 1`
- MeshRenderer: `m_Enabled: 1`
- Material: `MAT_H8_PhoticSoftWaterHaze_1430`
- Mesh: `MESH_H8_PhoticSoftWaterHaze_1430`
- Material values:
  - `_Alpha: 0.3`
  - `_NoiseScale: 8.2`
  - `_NoiseStrength: 0.34`
  - `_VerticalFade: 1.1`
  - `_NearColor: {r: 0.14, g: 0.64, b: 0.72, a: 0.11}`
  - `_FarColor: {r: 0.025, g: 0.22, b: 0.3, a: 0.24}`
  - `_Flow: {r: 0.018, g: 0.006, b: 0, a: 0}`

Risk:

- This is the only active named haze receiver among the checked haze routes, but 1474 still reads slabbed and under-layered.
- `_Alpha 0.3` with low color alpha values may be too weak to create particulate depth, while broad mesh geometry can still tint as a flat transparent plane.
- Haze is not paired with active suspended specks or horizon haze in the scene.

Rollback visible in YAML:

- Restore current values above; object active/enabled remains `1/1`.

### Disabled haze and particle routes

`H8_UnderwaterHorizonHaze_1437`:

- GameObject: `m_IsActive: 0`
- MeshRenderer: `m_Enabled: 0`
- Material: `MAT_H8_UnderwaterHorizonHaze_1437`
- Material values:
  - `_Tint: {r: 0.16, g: 0.5, b: 0.48, a: 0.1}`
  - `_FadePower: 2.25`

`H8_UnderwaterSuspendedSpecks_1446`:

- GameObject: `m_IsActive: 0`
- MeshRenderer: `m_Enabled: 1`
- Material: `MAT_H8_UnderwaterSpecks_1446`
- Material values:
  - `_Tint: {r: 0.7, g: 0.94, b: 0.92, a: 0.34}`
  - `_Softness: 1.35`
- Shader route is additive sprite/speck geometry, no pooled runtime particle proof.

`H8_UnderwaterHazeCurtain_1454`:

- GameObject: `m_IsActive: 0`
- MeshRenderer: `m_Enabled: 0`
- Material: `MAT_H8_UnderwaterHazeCurtain_1454`
- Material values:
  - `_TopColor: {r: 0.075, g: 0.34, b: 0.37, a: 0.72}`
  - `_BottomColor: {r: 0.18, g: 0.56, b: 0.48, a: 0.42}`
  - `_CausticColor: {r: 0.64, g: 1, b: 0.82, a: 0.32}`
  - `_CausticScale: 0.38`
  - `_Softness: 1.42`

Risk:

- The screenshot's missing particulate/haze read is consistent with the main speck route being inactive.
- Enabling `H8_UnderwaterHazeCurtain_1454` raw is dangerous: alpha top `0.72`, alpha bottom `0.42`, alpha-blend curtain, and broad mesh route can become the exact dark/green sheet problem.
- Horizon haze is safer than the curtain because current `_Tint.a` is only `0.1`, but it is currently fully disabled.

Rollback visible in YAML:

- Keep `H8_UnderwaterHorizonHaze_1437` inactive/renderer-disabled: `m_IsActive 0`, `m_Enabled 0`.
- Keep `H8_UnderwaterSuspendedSpecks_1446` inactive: `m_IsActive 0`; renderer currently `m_Enabled 1`.
- Keep `H8_UnderwaterHazeCurtain_1454` inactive/renderer-disabled: `m_IsActive 0`, `m_Enabled 0`.

### Crest foam input and ocean material edits

`H8_CREST_FOAM_INPUT_PASS_1464`:

- GameObject: `m_IsActive: 1`
- `Crest.RegisterFoamInput`: `m_Enabled: 1`
- `_disableRenderer: 1`
- `_radius: 20`
- `_featherWidth: 0`
- MeshRenderer: `m_Enabled: 0`
- Material: `MAT_H8_CrestFoamInput_1464`
- Material `_Strength: 4.8`

This is correctly configured as a Crest input lane, not a visible mesh. Do not enable its MeshRenderer for presentation.

Current modified `Ocean.mat` vs HEAD:

- `_CAUSTICS_ON` added.
- `_Caustics: 0 -> 1`
- `_CausticsStrength: 0 -> 0.56`
- `_CausticsTextureScale: 5 -> 9.5`
- `_CausticsDepthOfField: 0.33 -> 0.62`
- `_CausticsFocalDepth: 2 -> 3.4`
- `_CausticsDistortionStrength: 0.075 -> 0.12`
- `_ClipSurface: 1 -> 0`
- `_ClipUnderTerrain: 1 -> 0`
- `_FoamScale: 0.001528351 -> 0.044`
- `_ShorelineFoamMinDepth: 0.95 -> 0.82`
- `_WaveFoamFeather: 0.4 -> 0.19`
- `_WaveFoamLightScale: 0.55 -> 1.55`
- `_FoamWhiteColor` changed from muted alpha `0.72` to near-white alpha `1`.
- `_FoamBubbleColor` changed from dark teal to bright near-white.

Current modified `Ocean-Underwater.mat` vs HEAD:

- `_CAUSTICS_ON` added.
- `_Caustics: 0 -> 1`
- `_CausticsStrength: 0 -> 0.16`
- Other underwater values largely match HEAD, including `_FoamScale: 1.1`, `_Foam3DLighting: 1`, `_LightIntensityMultiplier: 4`, `_NormalsScale: 200`.

Current modified `MAT_H8_SurfaceCrestOcean_1428.mat` vs HEAD:

- `_CAUSTICS_ON` added.
- `_CausticsStrength: 0.22 -> 1.45`
- `_CausticsBase: 0.07 -> 0.11`
- `_CausticsTextureAverage: 0.07 -> 0.045`
- `_CausticsTextureScale: 6.2 -> 4.8`
- `_FoamScale: 0.0032 -> 0.019`
- `_FoamBubbleParallax: 0.1 -> 0.32`
- `_LightIntensityMultiplier: 0.52 -> 1.95`
- `_ShorelineFoamMinDepth: 1.28 -> 3.75`
- `_WaveFoamBubblesCoverage: 0.78 -> 1.95`
- `_WaveFoamBubblesStrength: 0.637 -> 1.18`
- `_WaveFoamCoverage: 0.52 -> 0.27`
- `_WaveFoamLightScale: 0.38 -> 2.15`
- `_WaveFoamStrength: 1.25 -> 3.45`
- Foam colors changed to near-white/green with alpha near `1`.

Current modified `Sim_Settings_Foam.asset` vs HEAD:

- `_foamFadeRate: 0.5 -> 0.42`
- `_waveFoamStrength: 1.4 -> 2.05`
- `_waveFoamCoverage: 0.75 -> 0.82`
- `_shorelineFoamMaxDepth: 1.2 -> 1.85`
- `_shorelineFoamStrength: 2.8 -> 4.25`

Risk:

- These are aggressive foam/caustic amplification changes without a same-session proof packet.
- `MAT_H8_SurfaceCrestOcean_1428` caustic strength `1.45` is especially high for a shallow/surface material and can make caustics read as neon or broad fake brightness if the receiver is not correct.
- `Ocean.mat` has `_ClipSurface` and `_ClipUnderTerrain` changed to `0`. If this affects the visible diagnostic route, it can expose water/ocean planes through terrain or produce slab reads. Roll back first before adding more haze if Unity owner sees clipping/plane artifacts.

### Photic1469 foam route

`H8_ORGANIC_SHORELINE_FOAM_FINE_1469` exists but is disabled.

- GameObject: `m_IsActive: 0`
- MeshRenderer: `m_Enabled: 0`
- Material: `MAT_H8_ShorelineFoamFine_1469`
- Mesh: `MESH_H8_ShorelineFoamFine_1469`
- Material values:
  - `_Alpha: 0.42`
  - `_Threshold: 0.26`
  - `_Softness: 0.38`
  - `_EdgeFade: 0.18`
  - `_FoamColor: {r: 0.9, g: 0.98, b: 0.93, a: 0.72}`
  - `_TilingA: {r: 1.35, g: 0.42, b: 0.006, a: 0.0025}`
  - `_TilingB: {r: 2.25, g: 0.75, b: -0.007, a: 0.003}`

Risk:

- It is safer than rejected 1473 sheet/blob routes because it uses a texture and threshold/edge fade, but it is not proven in scene and is currently inactive.
- It must be tested as one route only and rolled back immediately if it reads as a flat mesh strip.

Rollback visible in YAML:

- Restore disabled state: `m_IsActive 0`, `m_Enabled 0`.

## Top Material/Receiver Issues

1. Active caustic receiver can create sheet/streak artifacts.
   - `MAT_H8_FloorCausticSoft_1443` is active/enabled and additive.
   - Alpha is not tiny (`_Tint.a 0.42`), and the shader is sine-only with no depth/light gating.
   - This matches the small bright streak in 1474, not broad believable lace.

2. Underwater haze/particle presentation is structurally under-routed.
   - Active haze exists, but suspended specks and horizon haze are inactive.
   - The high-alpha curtain route is disabled and should stay disabled until isolated, because it can become a broad green sheet.
   - The screenshot reads almost particle-empty.

3. Crest/ocean foam and caustics were amplified beyond static proof.
   - `Ocean.mat`, `Ocean-Underwater.mat`, `MAT_H8_SurfaceCrestOcean_1428.mat`, and `Sim_Settings_Foam.asset` are modified.
   - Current values include high foam/caustic boosts and clip changes.
   - The safe Crest input object is active as an input with renderer disabled; visual failure should not be "fixed" by enabling its renderer.

## Safe Next Material Test Sequence For Unity Owner

Baseline rule: one route per capture. Record before/after screenshot, exact object state, and log tail. Roll back immediately on sheet/grid/noise.

1. Isolate the active caustic receiver first.
   - Capture current baseline from the exact 1474 underwater camera.
   - Temporarily disable only `H8_FloorCausticSoft_1443` renderer.
   - If the bright streak/sheet disappears, retune `MAT_H8_FloorCausticSoft_1443` rather than adding more effects.
   - First retune candidate:
     - `_Tint.a: 0.42 -> 0.14`
     - `_Sharpness: 5.8 -> 8.5`
     - `_ScaleA: 0.62 -> 1.25`
     - `_ScaleB: 0.98 -> 2.05`
   - Rollback:
     - renderer `m_Enabled: 1`
     - `_Tint.a 0.42`, `_Sharpness 5.8`, `_ScaleA 0.62`, `_ScaleB 0.98`
   - Acceptance target: subtle broken lace on floor/rocks, no visible transparent plane, no neon streak.

2. Restore/verify Crest clipping before amplifying foam.
   - Do not change `H8_CREST_FOAM_INPUT_PASS_1464` MeshRenderer; it should remain disabled because `RegisterFoamInput._disableRenderer: 1`.
   - If ocean plane/slab clipping is visible, test reverting `Ocean.mat`:
     - `_ClipSurface: 0 -> 1`
     - `_ClipUnderTerrain: 0 -> 1`
   - Keep `Ocean.mat` caustics modest during this check:
     - rollback values: `_Caustics 0`, `_CausticsStrength 0`, `_CausticsTextureScale 5`, `_CausticsDepthOfField 0.33`, `_CausticsFocalDepth 2`, `_CausticsDistortionStrength 0.075`
   - If Crest foam is the test route, use Frame Debugger/Crest debug; do not enable fallback mesh foam at the same time.

3. Add particulate depth with the least dangerous disabled route.
   - First test `H8_UnderwaterSuspendedSpecks_1446` active only, because it is particulate evidence and not a broad curtain.
   - Keep current material initially:
     - `_Tint.a 0.34`
     - `_Softness 1.35`
   - If too bright, reduce `_Tint.a` toward `0.16`; if invisible, raise object density/placement before exceeding `_Tint.a 0.34`.
   - Rollback: `m_IsActive 0`.
   - Acceptance target: sparse near-field motes, no full-screen snow, no additive wash.

4. Only after specks, test horizon haze, not the curtain.
   - Test `H8_UnderwaterHorizonHaze_1437` as a weak far-depth layer.
   - Keep `_Tint.a 0.1` and `_FadePower 2.25` initially.
   - Rollback: `m_IsActive 0`, `m_Enabled 0`.
   - Do not activate `H8_UnderwaterHazeCurtain_1454` in this pass. Its alpha values are too high for an unproven broad curtain.

5. Shoreline foam route stays separate from underwater receiver debugging.
   - `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` can be tested after Crest baseline, not during caustic/haze testing.
   - Enable one route only.
   - Rollback: `m_IsActive 0`, `m_Enabled 0`; material values unchanged.

## Low / Middle / High / Ultra Consequences

- Low/compact: keep Crest input and one subtle caustic receiver only if it does not sheet; use sparse specks and weak horizon haze. No curtain. No broad foam helpers.
- Middle: add active specks plus weak horizon haze after caustic isolation; Crest foam can run if Frame Debugger proves sampling and screenshot rejects no slab/grid.
- High: allow stronger receiver lace and richer foam breakup only after clip/receiver proof; caustic strength must scale continuously, not jump to neon.
- Ultra: layer surface Crest caustics, 1469 fine foam, specks, and local haze only after each route passes separately. No new gameplay truth.

## Final Static Verdict

The next safe test is not another broad visual overlay. First isolate `H8_FloorCausticSoft_1443`, restore/verify Crest clipping if slab geometry persists, then add `H8_UnderwaterSuspendedSpecks_1446` as the least dangerous missing particulate route. `H8_UnderwaterHazeCurtain_1454` is a sheet-risk receiver and must not be the next fix.

Acceptance remains `PENDING UNITY/PROFILER VERIFICATION`.
