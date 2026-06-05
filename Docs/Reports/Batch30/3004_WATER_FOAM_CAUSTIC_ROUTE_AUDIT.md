# 3004 Water / Foam / Caustic Route Audit

ID: `3004_WATER_FOAM_CAUSTIC_ROUTE_AUDIT`  
Date: 2026-06-04  
Status: `STATIC VERIFIED` for source/metadata presence only. `PENDING VERIFICATION` for visual quality, runtime route, Frame Debugger, profiler, GC, and six-view acceptance.

## Scope

Static audit only. No Unity launch, no build, no Play Mode, no material edits, no shader edits, no scene edits, no Assets writes.

Task output file only:
- `Docs/Reports/Batch30/3004_WATER_FOAM_CAUSTIC_ROUTE_AUDIT.md`

## Mandates Followed

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_DescriptorBinding_Reality_Check.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Authority And Evidence Read

Authority:
- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `water.md`
- `rendering.md`
- `shaders.md`
- `quality.md`

Evidence:
- `Docs/Reports/Batch25/2504_CREST_OCEAN_CLIP_FOAM_CAUSTIC_RISK_AUDIT.md`
- `Docs/Reports/Batch26/BATCH26_SYNTHESIS_FOR_UNITY_OWNER.md`
- `Docs/Reports/Batch26/2602_FOAM_CAUSTIC_CREST_MATERIAL_AUDIT.md`
- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.txt`
- `Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt`
- `Docs/Screenshots/MCP/h8_1912_surface_after_quarantine_b.txt`
- Material YAML and relevant shader/source files listed below.

`Docs/Actual Domains of Project.txt` is absent.

## Current Material Paths Verified By Search

Generic Crest materials:
- `Assets/Crest/Crest/Materials/Ocean.mat`
- `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
- `Assets/Crest/Crest/Materials/Ocean_UnderwaterCurtain.mat`

First-party/candidate route materials:
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
- `Assets/_Project/Art/Materials/World/Photic1469/MAT_H8_ShorelineFoamFine_1469.mat`
- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_FloorCausticSoft_1443.mat`

Working-tree state:
- `Ocean.mat`, `Ocean-Underwater.mat`, `Ocean_UnderwaterCurtain.mat`, and `MAT_H8_SurfaceCrestOcean_1428.mat` are modified.
- `MAT_H8_ShorelineFoamFine_1469.mat` and `MAT_H8_FloorCausticSoft_1443.mat` are untracked.
- The 1912 metadata files are untracked.

## Staleness Corrections Against Earlier Reports

Batch25 and Batch26/2602 are useful history, but current YAML differs:
- `Ocean.mat` current source has `_ClipSurface: 1`, `_ClipUnderTerrain: 1`, `_CLIPSURFACE_ON`, and `_CLIPUNDERTERRAIN_ON`. The older clip-off blocker is not current source truth.
- `Ocean.mat` current `_CausticsStrength` is `0.92`, not Batch25/2602's older `0.56`.
- `MAT_H8_SurfaceCrestOcean_1428.mat` current `_CausticsStrength` is `1.65`, not older `1.45`.
- `H8_FloorCausticSoft_1443` current scene YAML and 1912 metadata show `MeshRenderer.m_Enabled: 0`. Batch26/2602's "renderer-enabled" line is stale for current source.
- `MAT_H8_ShorelineFoamFine_1469.mat` current `_Alpha: 1`, `_Threshold: 0.07`, `_EdgeFade: 0.1`, `_FoamColor.a: 1`. Older lower-alpha values are stale.

## Current Static Route

Surface Crest owner:
- `Assets/_Project/Prefabs/Ocean_Crest.prefab` has `Crest.OceanRenderer._material` bound to `Ocean.mat` GUID `9def92ac79181fe41b238e91663f0fad`.
- `02_HECTON_WORLD.unity` overrides the same `_material` to `Ocean.mat` and sets `_createFoamSim: 1`.
- Therefore the current serialized surface route is `Ocean.mat`, not `MAT_H8_SurfaceCrestOcean_1428.mat`.

Underwater owner:
- `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` is active and enabled.
- It references `Ocean-Underwater.mat` GUID `ef94c26e44a36e24a9dcbc5995a2bed1`.
- It serializes `enableShallowCaustics: 1`, `causticsFadeOutDepth: 18`, `causticsMinLightFactor: 0.18`, and adaptive caustics scaling.
- Its serialized debug values still show `_debugIsUnderwater: 0` and `_debugCausticsStrength: 0`. Static debug fields are not runtime proof.

Crest underwater renderer:
- `Crest.UnderwaterRenderer` is enabled.
- `_volumeGeometry: {fileID: 0}`.
- `_copyOceanMaterialParamsEachFrame: 1`.
- Crest URP/HDRP source passes `_firstRender || _copyOceanMaterialParamsEachFrame` into `UpdatePostProcessMaterial`, so asset-only underwater material edits can be overwritten or become false confidence unless capture metadata proves final material state.

## Current Material Risk Findings

### `Ocean.mat` Surface Route

Current source:
- Valid keywords include `_CAUSTICS_ON`, `_CLIPSURFACE_ON`, `_CLIPUNDERTERRAIN_ON`, `_FOAM_ON`, `_UNDERWATER_ON`.
- `_ClipSurface: 1`, `_ClipUnderTerrain: 1`.
- `_Caustics: 1`, `_CausticsStrength: 0.92`, `_CausticsTextureScale: 9.5`.
- `_Foam: 1`, `_FoamScale: 0.032`, `_WaveFoamCoverage: 0.68`, `_WaveFoamStrength: 2.35`.
- `_FoamBubbleColor: {0.82, 0.95, 1, 1}`, `_FoamWhiteColor: {0.92, 1, 0.96, 1}`.
- `_Diffuse: {0.026, 0.12, 0.2}`, `_DiffuseGrazing: {0.29, 0.56, 0.72}`, `_SubSurfaceShallowCol: {0.34, 0.68, 0.82}`.

Risk:
- Clip is currently repaired in source.
- Foam and caustics are numerically enabled, but no evidence proves Crest foam texture output, material sampling, shader variant, pass order, camera-waterline composition, or visible shoreline result.
- Color is brightened versus older dark values, but final flat green/cyan water remains plausible if depth/particulate/normal/specular/route lighting do not create structure.

### `Ocean-Underwater.mat` Underwater Owner Material

Current source:
- Valid keywords include `_CLIPSURFACE_ON`, `_CLIPUNDERTERRAIN_ON`, `_FOAM_ON`, `_TRANSPARENCY_ON`, `_UNDERWATER_ON`.
- `_Caustics: 0`, `_CausticsStrength: 0`.
- `_ClipSurface: 1`, `_ClipUnderTerrain: 1`, `_Transparency: 1`.
- `_Foam: 1`, `_FoamScale: 1.1`.
- `_FoamBubbleColor: {0.72, 0.92, 1, 1}`, `_FoamWhiteColor.a: 0.86`.

Risk:
- Static material defaults explain no underwater caustics unless `HectonUnderwaterVisuals` successfully writes nonzero caustics at runtime.
- The owner code can compute light/depth-gated caustics and write `_Caustics`/`_CausticsStrength`, but current static evidence does not prove it happened in the 1912 capture.

### `Ocean_UnderwaterCurtain.mat`

Current source:
- Valid keywords include `_CAUSTICS_ON`, `_FOAM_ON`, `_UNDERWATER_ON`.
- `_CLIPUNDERTERRAIN_ON` and `_TRANSPARENCY_ON` are absent.
- `_CausticsStrength: 10`, `_FoamScale: 15`, `_LightIntensityMultiplier: 5.31`.
- `_DiffuseGrazing: {0,0,0,1}`, `_FoamBubbleColor: {0.435,1,0,1}`.

Risk:
- Not proven as current visible route because `UnderwaterRenderer._volumeGeometry` is null.
- If raw-enabled or routed through volume geometry, it is a blocker-grade green curtain/caustic sheet risk.

### `MAT_H8_SurfaceCrestOcean_1428.mat`

Current source:
- Same Crest shader GUID as `Ocean.mat`.
- Valid keywords include `_CAUSTICS_ON`, `_CLIPSURFACE_ON`, `_CLIPUNDERTERRAIN_ON`, `_FOAM_ON`, `_TRANSPARENCY_ON`, `_UNDERWATER_ON`.
- `_Caustics: 1`, `_CausticsStrength: 1.65`.
- `_FoamScale: 0.028`, `_WaveFoamStrength: 3.8`, `_WaveFoamLightScale: 2.15`, `_FoamWhiteColor.a: 1`.
- `_SubSurfaceBase: 0.42`, `_SubSurfaceSun: 0.95`, `_SubSurfaceShallowCol: {0.48,0.78,0.94}`.

Risk:
- This is not the current serialized surface route.
- If assigned without isolation proof, it is more overdriven than `Ocean.mat` for caustics and foam. It can explain sheet caustics, luminous cyan/green water, and pasted foam if used.

### `MAT_H8_ShorelineFoamFine_1469.mat`

Current source:
- Shader `H8_ShorelineFoamRibbon_1428`.
- Render queue `3012`.
- `_Alpha: 1`, `_Threshold: 0.07`, `_EdgeFade: 0.1`, `_Softness: 0.36`.
- `_FoamColor: {1,0.995,0.94,1}`.
- Shader is transparent, `ZWrite Off`, `Blend SrcAlpha OneMinusSrcAlpha`, `Cull Back`.

Risk:
- The object is visible in 1912 metadata, but it is a transparent ribbon overlay, not proof of Crest foam simulation or water-contact-caused foam.
- Full-alpha foam with low threshold can become a pasted strip if mesh placement/sorting is wrong.

### `MAT_H8_FloorCausticSoft_1443.mat`

Current source:
- Shader `Hecton8/World/FloorCausticSoft1443`.
- Render queue `3018`.
- `_Tint: {0.58,0.92,1,0.24}`, `_ScaleA: 1.05`, `_ScaleB: 1.72`, `_Sharpness: 8.2`.
- Shader uses sine-derived world XZ caustics, `Blend SrcAlpha One`, `ZWrite Off`, `Cull Off`.

Risk:
- Current scene/metadata has renderer disabled.
- If enabled, it is a visual fake that still needs owner gating by light/depth/route. It has no intrinsic sun, shadow, obstruction, storm, cave, eclipse, or depth ownership.

## 1912 Active / Disabled Object Evidence

`h8_1912_surface_edit_main.txt` and `h8_1912_surface_after_quarantine_b.txt` agree:
- `H8_ORGANIC_SHORELINE_FOAM_FINE_1469`: `activeSelf=True`, `activeHierarchy=True`, `rendererEnabled=True`, material `MAT_H8_ShorelineFoamFine_1469`.
- `H8_FloorCausticSoft_1443`: `activeSelf=True`, `activeHierarchy=True`, `rendererEnabled=False`, material `MAT_H8_FloorCausticSoft_1443`.
- `H8_UnderwaterSurfaceSheet_1455`: inactive, renderer disabled.
- `H8_UnderwaterHazeCurtain_1454`: inactive, renderer disabled.
- `H8_DEPTH_LOW_SHELF_1428`: activeSelf true but inactive hierarchy, renderer disabled.
- `H8_WORLD_LOW_WATER_OCCLUSION_00..03_1428`: activeSelf true but inactive hierarchy, renderers disabled.
- `H8_DEPTH_CEILING_OCCLUSION_1428`: activeSelf true but inactive hierarchy, renderer disabled.
- `NOIR_UPPER_PRESSURE_LID`: activeSelf true but inactive hierarchy, renderer disabled.

`h8_1912_surface_quarantine.txt` shows many bad debug/haze/slab/foam objects renderer-disabled after quarantine, including:
- broken foam sheets/rings,
- cyan depth lanes,
- far water curtains,
- noir pressure slabs,
- `H8_PHOTIC_SOFT_WATER_HAZE_1430`,
- `H8_FloorCausticSoft_1443`.

Current serialized visible summary therefore contains one visible authored foam ribbon and Aegir. It does not contain a visible caustic receiver.

## Why Visible Foam Is Still Not Proven

Static facts:
- Crest surface route has `_createFoamSim: 1`.
- `Crest.RegisterFoamInput` exists and `_disableRenderer: 1`; that is normal for an input lane.
- `Ocean.mat` has `_FOAM_ON` and nonzero foam values.
- `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` is visible.

Missing proof:
- No Frame Debugger/Crest debug evidence that foam input enters the foam simulation texture.
- No proof that the active visible ocean samples the foam texture in the 1912 capture.
- No waterline close capture proving foam follows contact/waves instead of a flat overlay.
- No overdraw/sorting proof for the transparent `ZWrite Off` shoreline ribbon.

Verdict:
- Foam route is wired enough to test, not accepted. Numeric foam strength and a visible ribbon do not prove believable foam.

## Why Valid Caustics Are Still Not Proven

Static facts:
- `Ocean.mat` and `MAT_H8_SurfaceCrestOcean_1428.mat` have nonzero caustics.
- `Ocean-Underwater.mat` serializes `_Caustics: 0` and `_CausticsStrength: 0`.
- `HectonUnderwaterVisuals` can compute `_cachedCausticsStrength` from underwater state, depth, light factor, adaptive budget, and soundscape tier, then write `_Caustics` and `_CausticsStrength`.
- Current serialized debug state still shows `_debugIsUnderwater: 0` and `_debugCausticsStrength: 0`.
- `H8_FloorCausticSoft_1443` is disabled in 1912 metadata.
- `Ocean_UnderwaterCurtain.mat` is dangerous if routed, but not proven active.

Missing proof:
- No underwater route capture with nonzero owner caustic debug values.
- No Frame Debugger/RenderGraph proof showing caustic pass/order.
- No proof of a believable light reason: shallow daylight, floodlight, glass, lamp, or local projector.
- No proof that caustics disappear/degrade in dark/deep/storm/cave routes.

Verdict:
- Current caustics are material numbers plus owner potential. They are not a valid visible route.

## Why Numeric Material Strength Is Not Visual Proof

Material values do not prove:
- the material is active in the rendered route;
- runtime owners did not overwrite the asset values;
- keywords compiled and bound the intended shader path;
- the object renderer is enabled and visible to the camera;
- transparent sorting and depth state are correct;
- Crest foam input was sampled into the ocean material;
- caustics are gated by actual light/depth/occlusion;
- the result passes surface/photic Subnautica-level readability;
- the cost passes Frame Debugger/profiler/GC gates.

Evidence class remains `STATIC_SOURCE`, not `PLAYER-CAPTURE`, `FRAME_DEBUGGER`, or `PROFILER`.

## Safe Owner-Correct Correction Plan For Unity Owner

1. Freeze current static values before testing. Do not bulk-revert or swap materials blindly.
2. Keep Crest package material usage owner-correct. Do not create Crest runtime material clones. If Crest needs an asset material, assign the asset.
3. Use the current active surface route first: `Ocean.mat` on `OceanRenderer._material`.
4. Capture a clean baseline with a same-session manifest before more tuning.
5. Prove Crest foam:
   - keep `RegisterFoamInput` renderer disabled;
   - prove registration and foam texture contribution through Crest debug/Frame Debugger;
   - only then tune foam values;
   - use `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` only as a narrow authored fallback, with sorting/overdraw/waterline proof.
6. Prove underwater caustics through `HectonUnderwaterVisuals`, not by asset-number hope:
   - show `_debugIsUnderwater=True`;
   - show nonzero `_debugCausticsStrength` only within light/depth gates;
   - log active `Ocean-Underwater.mat` runtime values at capture time;
   - prove caustics fade out by depth/weather/cave/darkness tier.
7. Keep `Ocean_UnderwaterCurtain.mat`, haze curtain, pressure lid, low shelf, depth ceiling, and water occlusion slabs renderer-disabled unless a named owner gate and low-oblique proof exist.
8. Use `MAT_H8_SurfaceCrestOcean_1428.mat` only as an isolated trial. Current values are overdriven; it is not active route proof.
9. If enabling `H8_FloorCausticSoft_1443`, gate it by shallow lit route ownership and validate that it does not show in abyss/caves/storm/eclipse or behind blocked geometry.
10. Produce mandatory six-view proof before any acceptance:
    - surface/coast/Aegir;
    - shoreline close 1 m foam/wet contact;
    - underwater 0-5 m photic shallows;
    - underwater 20-50 m route;
    - Aegir/celestial long/crop;
    - low-oblique slab/plane regression.
11. For each capture include scene, camera transform/FOV, depth, route label, active material GUIDs, keywords, key foam/caustic values, object active/renderer states, `GlobalQualityWeight`, render scale, log path, and clean-window summary.
12. Add Frame Debugger/RenderGraph and profiler proof for foam/caustic/transparent-overdraw cost before any runtime acceptance.

## Continuous `GlobalQualityWeight` Consequences

Use continuous interpolation. These are anchor consequences, not binary branches.

Low / Compact:
- Preserve bright readable ocean color, clip correctness, shoreline silhouette, and route cues.
- Use proven Crest foam only; no broad haze curtain, no pressure slabs, no global caustics.
- Caustics limited to justified shallow light/floor hints with low strength and no gameplay truth changes.

Middle:
- Add verified Crest shoreline foam contribution and one narrow authored foam layer if sorting is clean.
- Add light/depth-gated underwater caustics from owner state.
- Add sparse particulate/depth cues only if they do not flatten the water.

High:
- Buy richer foam breakup, better surface normals/specular, stronger but bounded caustic lace, and local underwater haze from owner snapshots.
- Keep gameplay truth, save identity, DTO layout, and route authority unchanged.

Ultra:
- Layer premium shoreline foam, higher-frequency caustic variation, richer photic volume, wet-rock response, and surface sparkle after the base route passes.
- Ultra buys sensory density, not new truth and not cover for broken water.

## Rejection Gates Checked

- No Crest runtime material clone recommendation.
- No global caustics without a believable light reason.
- No acceptance without six-view proof.
- No acceptance from material numbers, static YAML, screenshot filenames, or debug fields.
- No raw enable of curtain/slab/haze objects.

## Evidence-Class Summary

| Claim | Evidence Class | Artifact | Residual Risk |
|---|---|---|---|
| Surface route uses `Ocean.mat` | `STATIC_SOURCE` | `02_HECTON_WORLD.unity`, `Ocean_Crest.prefab`, `Ocean.mat.meta` | Runtime override/import state unproven. |
| Underwater owner references `Ocean-Underwater.mat` | `STATIC_SOURCE` | `02_HECTON_WORLD.unity`, `Ocean-Underwater.mat.meta` | Runtime writes/copy path unproven. |
| `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` is visible in 1912 metadata | `STATIC_DOC` / `STATIC_SOURCE` | `h8_1912_surface_edit_main.txt`, scene YAML | Visual quality and sorting unproven. |
| `H8_FloorCausticSoft_1443` renderer is disabled in current 1912 metadata and scene YAML | `STATIC_DOC` / `STATIC_SOURCE` | `h8_1912_surface_edit_main.txt`, scene YAML | Runtime toggle after metadata unproven. |
| Current caustic route is not accepted | `STATIC_SOURCE` | Material YAML, scene YAML, owner debug fields | Needs Unity capture, Frame Debugger, profiler. |
| Current foam route is not accepted | `STATIC_SOURCE` | Crest prefab/scene foam route, material YAML, metadata | Needs Crest debug/Frame Debugger and shoreline capture. |

## Static Verdict

Current source no longer supports the older "Ocean.mat clip-off" diagnosis as the primary active blocker. The current blockers are different:

1. `Ocean.mat` is active and numerically strong, but foam/caustics are still unproven because no Crest foam texture, pass order, waterline contact, or six-view proof exists.
2. `Ocean-Underwater.mat` defaults to caustics off, and the underwater owner's serialized debug fields show no captured nonzero caustic state.
3. The only visible 1912 foam object is a transparent authored ribbon. It is not Crest foam proof.
4. The floor caustic fake is disabled in current metadata, so it cannot be credited as a visible caustic route.
5. `Ocean_UnderwaterCurtain.mat` and `MAT_H8_SurfaceCrestOcean_1428.mat` are high-risk if used raw: overdriven caustics/foam and unsafe curtain/transparent behavior can recreate green sheets instead of premium water.

Acceptance remains `PENDING VERIFICATION`.
