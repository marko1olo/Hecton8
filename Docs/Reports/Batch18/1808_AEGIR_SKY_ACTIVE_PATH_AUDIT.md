# 1808 AEGIR SKY ACTIVE PATH AUDIT

Agent: 1808 / AEGIR_SKY_ACTIVE_PATH_AUDITOR
Date: 2026-06-04
Proof ceiling: STATIC_SOURCE and STATIC_DOC only.

No Unity launch, PlayMode, profiler, Frame Debugger, or player capture was performed. This report identifies the likely active scene path and the missing proof required before acceptance. It does not certify visuals, frame cost, runtime material state, or player-facing brightness.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `celestial.md`
- `lighting.md`
- `presentation.md`
- `rendering.md`
- `shaders.md`
- `performance.md`
- `Docs/Reports/Batch18/1801_WORLD_SURFACE_ROUTE_EVIDENCE.md`
- `Docs/Reports/Batch18/1802_SURFACE_SHALLOW_ASSET_INVENTORY.md`

Selected mandates:

- `QA_Evidence_Text_Filter_Audit.txt`
- `REND_DescriptorBinding_Reality_Check.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `STRM_Async_Asset_Upload_Texture_Settings.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Finding

Likely active Aegir route in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`:

1. `RenderSettings.m_SkyboxMaterial` binds `Assets/_Project/Art/Materials/Mat_HectonSky.mat`.
2. `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428` is active and has enabled `HectonCelestialEngine`.
3. `HectonCelestialEngine._skyMaterial` also binds `Mat_HectonSky.mat`.
4. `HectonCelestialEngine.aegirTransform`, `aegirRenderer`, and `aegirObserverRelativeBody` point at `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428`.
5. `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` is active, renderer-enabled, and material-bound to `MAT_AegirGasGiant_Impostor_1428`.
6. Active supporting surface sky cards include cloud decks, two moon meshes, the sun key light, and a low sun disc.

Static conclusion: the active route is not the disabled `H8_AEGIR_SKY_BACKDROP_1428` renderer and not the inactive noir/card routes. The active route appears to be `Mat_HectonSky` plus the observer-relative `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` impostor body.

Runtime acceptance: not met. A player capture, Frame Debugger pass, and material/texture binding confirmation are still required.

## Active Route Evidence

### RenderSettings and Atmosphere Owner

- Scene skybox material GUID `c94a1beef2372b8458941c2ed9d05d5e` resolves to `Mat_HectonSky.mat`.
- RenderSettings fog is enabled with color `{0.6, 0.72, 0.8, 1}`, density `0.00135`, ambient sky `{0.56, 0.68, 0.76}`, ambient intensity `0.42`.
- RenderSettings sun field is null, but `HectonCelestialEngine.sunLight` points to `H8_SURFACE_SUN_KEY_1428`.
- `HectonCelestialEngine` has `driveObserverBodiesFromAnalyticalOrbits: 1`, `enableAdaptiveDeepTextureResidency: 1`, and active Aegir renderer/body references.
- `nightAtmosphereExposure: 0.001` and very dark night profile values are surface-brightness risks if night/eclipsed states enter the surface path without capture proof.
- `_surfaceCloudShadowCookie` and `aegirRingShadowCookie` are null. `HectonAtmosphereManager._useAegirRingShadowCookie` is `0`.

### Aegir Body

- `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` is active.
- Its renderer is enabled, casts no shadows, receives no shadows, uses no reflection probe, and disables motion vectors.
- Material: `MAT_AegirGasGiant_Impostor_1428`.
- Shader: `H8_AegirGasGiantImpostor_1428.shader`.
- Key textures:
  - `clouds0_diff.png` on `_MainTex`, source 4096x2048, import max 2048.
  - `Aegir_storms.png` on `_StormTex`, source 4096x2048, import max 2048.
  - `Sky/oblakajip.png` on an additional texture slot, source 2048x2048.
- Observer-relative component binds the player observer, body renderer, body mesh filter, `anchorDistance: 50000`, and `angularDiameterDegrees: 38`.

### Active Surface Sky Cards

- `H8_SURFACE_CLOUD_DECK_LOW_1428`: active, renderer enabled, `MAT_H8SurfaceCloudDeck_1428`.
- `H8_SURFACE_CLOUD_DECK_HIGH_1428`: active, renderer enabled, `MAT_H8SurfaceCloudDeck_1428`.
- `CloudDeck_Left_Horizon_1428`: active, renderer enabled, `MAT_AtmosphericCloudSheet_1428`.
- `CloudDeck_Mid_Shear_1428`: active, renderer enabled, `MAT_AtmosphericCloudSheet_1428`.
- `H8_SURFACE_MOON_KHEPRI_REAL_1428`: active, renderer enabled, `MAT_CelestialMoon_Khepri`.
- `H8_SURFACE_MOON_THALOS_REAL_1428`: active, renderer enabled, `MAT_CelestialMoon_Thalos`.
- `SURFACE_MOON_A_1428`: active, renderer enabled, `MAT_SurfaceMoonCold_1428`, no base/main texture.
- `SURFACE_MOON_B_1428`: active, renderer enabled, `MAT_SurfaceMoonCold_1428`, no base/main texture.
- `H8_SURFACE_SUN_KEY_1428`: active directional light, enabled, intensity `0.94`, shadow type `2`.
- `SURFACE_LOW_SUN_DISC_1428`: active, renderer enabled, `MAT_SurfaceSunDisc_1428`.

## Candidate, Disabled, Stale, and Uncertain Routes

- `H8_AEGIR_SKY_BACKDROP_1428` is GameObject-active but its MeshRenderer is disabled. `MAT_AegirSky_Master` is therefore candidate-only by static evidence.
- `MAT_AegirSky_Master` and `Hecton_AegirSky.shader` contain explicit continuous quality weight support, but the renderer path is disabled in the inspected scene.
- `SURFACE_GAS_GIANT_1428` is inactive and renderer-disabled. It is stale/candidate only.
- `H8_SURFACE_GAS_GIANT_DISC_1428` is inactive while renderer-enabled. It uses `MAT_H8SurfaceGasGiantDisc_1428` and `TX_H8AegirGasGiantBakedDisc_1428.png`; it is candidate-only, not active proof.
- `H8_SURFACE_SKY_CARD_1428` is inactive and renderer-disabled. It uses noir gradient material/texture and must not be used to hide weak surface sky.
- `SURFACE_SKY_NOIR_BACKDROP_1428` and `SURFACE_SKY_DOME_NOIR_1428` are inactive/stale by scene evidence.
- `H8_SURFACE_CLOUD_PANORAMA_1428`, `H8_SURFACE_CLOUD_DECK_HORIZON_1428`, and `SURFACE_HORIZON_SALT_HAZE_1428` are inactive even where renderers are enabled. They are candidate-only.
- Multiple active noir/depth curtain objects exist in the scene search surface. They are not sky/Aegir active-path proof and must not be used as surface acceptance or permanent surface-darkness cover.

## Material and Shader Risks

1. `Mat_HectonSky.mat` is the active skybox material, but two referenced cloud texture GUIDs were not resolved to `.meta` owners under `Assets`: `97dacc0c8637b304f9451ecd290acffb` and `161f2ad7f77e8bf408b29aa7e3d29966`. This is a static binding risk until Inspector/Frame Debugger proves the runtime texture state.
2. Active `Hecton_AlienSky_Master.shader` was not statically proven to consume a continuous `GlobalQualityWeight` token. It uses shader LOD and many material controls, but explicit continuous quality scaling was not found in the source scan.
3. Active `H8_AegirGasGiantImpostor_1428.shader` also was not statically proven to consume a continuous `GlobalQualityWeight` token. It is opaque geometry, uses material CBUFFERs, instancing variants, and samples Aegir/storm textures.
4. `Aegir_storms.png` and `clouds0_diff.png` are 4096x2048 source images imported with max texture size 2048. This can be acceptable for compact devices but needs high/ultra proof because Aegir is large on the horizon.
5. Active cloud deck shaders are transparent, `ZWrite Off`, and not proven to have quality-scaled cadence/capacity. Overdraw needs Frame Debugger and capture proof.
6. `MAT_CelestialMoon_Khepri` and `MAT_CelestialMoon_Thalos` use a basalt terrain color JPG as moon albedo. Normal/emission/detail slots are null. They are acceptable only if player capture proves they do not read as placeholder spheres.
7. `SURFACE_MOON_A_1428` and `SURFACE_MOON_B_1428` are active flat-color moon cards using `MAT_SurfaceMoonCold_1428` with `_BaseMap` and `_MainTex` null. They require capture proof or removal from the active surface composition.
8. `MAT_SurfaceSunDisc_1428` has no texture and uses a shared unresolved shader GUID. It is flat-color by material metadata; visual acceptability depends on scale, bloom, and composition proof.
9. The common shader GUID `650dd9526735d5b46b79224bc6e94025` appears across many simple world materials but has no `.meta` owner found under the project scan. Treat as external/built-in/unresolved until Unity proves the concrete shader name.

## Surface Lighting Risks

- The project vision locks require bright, premium 0-100 m surface and photic shallows. Permanent noir/darkness is not a valid surface cover.
- Fog and ambient values in RenderSettings are bright enough on paper, but `nightAtmosphereExposure: 0.001` and dark night profile colors can crush surface readability if they leak into daytime/eclipsed surface states.
- Ring shadow and cloud shadow cookies are null in the inspected owner path. If Aegir shadowing is expected, it is not statically proven through cookie bindings.
- Surface sky acceptance cannot be inferred from active flags. It needs player captures showing Aegir, sky, clouds, water, coastline, and moons under the actual camera.

## Texture Metadata Notes

- `TX_H8AegirGasGiantBakedDisc_1428.png`: 2048x2048, sRGB, alpha transparency on, aniso 4. Strong candidate texture on inactive `H8_SURFACE_GAS_GIANT_DISC_1428`, but not the active Aegir material path found in scene.
- `Aegir_storms.png`: 4096x2048 source, max import 2048, sRGB, aniso 1. Active through `MAT_AegirGasGiant_Impostor_1428` and candidate through `MAT_AegirSky_Master`.
- `clouds0_diff.png`: 4096x2048 source, max import 2048, sRGB, aniso 1. Active Aegir main texture.
- `Sky/oblakajip.png`, `Sky/oblaka!.png`, `Sky/clod1.png`, `Sky/clod2.png`: 2048x2048, sRGB. Used by active/candidate cloud materials.
- `Rock031_1K-JPG_Color.jpg`: 1024x1024, used as active Khepri/Thalos moon albedo. Static risk: terrain rock texture masquerading as moon surface.
- `TX_H8SurfaceSkyNoirGradient_1428.asset`: 512x256 generated Texture2D, inactive/noir candidate. Do not activate for surface coverage.

## Binding Matrix

CSV artifact: `Docs/Reports/Batch18/1808_AEGIR_SKY_BINDING_MATRIX.csv`.

The matrix labels every inspected route with:

- active-state: static GameObject/renderer/material state where available.
- proof-state: `STATIC_SOURCE`, `STATIC_DOC`, or `UNRESOLVED_STATIC`.
- risk: why the route cannot be accepted without runtime/player proof.

## Compact, Middle, High, Ultra Consequences

Compact:

- Keep Aegir visible, readable, and bright enough from the surface. Use lower texture residency/cadence only if it does not flatten the gas giant, clouds, or sky.
- Transparent card count and cloud overdraw must be capped by proof, not guesswork.
- No permanent noir cover. Surface water/sky must remain legible.

Middle:

- Preserve active Aegir plus cloud decks and moons with stable horizon composition.
- Use continuous quality weight to scale optional stars, cloud layers, haze, and update cadence.
- Do not change gameplay truth, DTO layout, save identity, or authority route based on visual quality.

High:

- Raise cloud richness, Aegir rim/storm readability, and atmospheric layering.
- Use saved budget for stronger sky composition, not expensive physical simulation.
- Require Frame Debugger proof that transparent cards and sky passes remain within budget.

Ultra:

- Visual overkill should add premium storm band detail, horizon mist, moon atmosphere response, and sky depth.
- Still use continuous scalar behavior. Do not introduce binary quality switches.
- The active route must remain deterministic and inspectable through material/property proof.

## Required Player Screenshots

1. Day surface horizon: skybox, Aegir, active cloud decks, coastline, water surface, and terrain in one frame.
2. Waterline shot: Aegir and sky reflected/readable over ocean, foam, wet rock, and shallow water.
3. Cloud edge shot: low/high cloud decks against the horizon, enough angle to prove card quality is not flat.
4. Moon shot: Khepri and Thalos visible enough to prove the basalt albedo does not read as placeholder.
5. Compact-quality comparison from the same camera: readable surface retained, no muddy/noir cover.
6. Eclipse/storm/night transition shot if applicable: darkness allowed only as temporary event state, not surface default.
7. Frame Debugger capture frame: active skybox, Aegir renderer, cloud cards, moons, sun disc, pass order, and texture bindings.

## Required Frame Debugger and Material Proof

- Active skybox material name and shader name.
- Runtime texture bindings for `Mat_HectonSky.mat`, especially the two unresolved cloud GUID slots.
- Active Aegir renderer material, mesh, shader pass, and texture bindings.
- Whether Aegir uses `MAT_AegirGasGiant_Impostor_1428` at runtime or gets swapped by script/material property block.
- Cloud card draw count, pass order, overdraw hot spots, and alpha blend cost.
- Moon material pass names and final bound albedo textures.
- Sun light shadow settings and whether sun disc material resolves to an internal/simple shader.
- Quality scalar values active during capture and which material/shader properties they drive.

## Do Not Do

- Do not claim disabled `H8_AEGIR_SKY_BACKDROP_1428` or inactive noir/card routes as the active surface sky.
- Do not activate noir sky cards, noir dome, or dark backdrops to hide weak surface art.
- Do not report visual acceptance without player-facing captures.
- Do not report profiler cost, overdraw, or Frame Debugger order without Unity proof.
- Do not change materials or shaders in this audit pass.
- Do not use binary quality switches. Use continuous `GlobalQualityWeight` behavior for any later implementation.
- Do not replace sky/Aegir with flat, muddy, primitive, or placeholder-looking fakes. Fake-first still has to look premium.

## Unity-Slot Implementer Prompt

```xml
<SUB_AGENT_PROMPT role="Unity Slot: Aegir Sky Runtime Proof">
  <scope>
    Use Unity only after an available slot is confirmed. Do not edit materials, shaders, or scene objects unless the owner explicitly authorizes a scoped fix.
  </scope>
  <inputs>
    Read Docs/Reports/Batch18/1808_AEGIR_SKY_ACTIVE_PATH_AUDIT.md and Docs/Reports/Batch18/1808_AEGIR_SKY_BINDING_MATRIX.csv.
  </inputs>
  <mission>
    Prove or falsify the static active route in 02_HECTON_WORLD.unity. Capture player-facing screenshots for day horizon, waterline, clouds, moons, compact-quality comparison, and any eclipse/storm/night state available. Capture Frame Debugger/material proof for skybox, Aegir, cloud cards, moons, and sun disc.
  </mission>
  <acceptance>
    Report only observed runtime facts. Include screenshot paths, active material/shader names, texture bindings, pass order, draw/overdraw evidence, and quality scalar values. Do not call darkness an acceptable surface solution.
  </acceptance>
</SUB_AGENT_PROMPT>
```

## Texture/Material Polish Prompt

```xml
<SUB_AGENT_PROMPT role="Texture Material Polish: Aegir Sky Surface">
  <scope>
    Work only after Unity-slot proof identifies a concrete visual defect or missing binding. No broad shader rewrite. No noir cover-up.
  </scope>
  <inputs>
    Use the runtime screenshots and Frame Debugger proof produced from the 1808 Unity-slot prompt.
  </inputs>
  <mission>
    If the active route is visually weak, propose the smallest premium fix: resolve missing cloud bindings, adjust existing material parameters, raise texture import caps where justified, or swap to the already-authored baked Aegir disc only if the active runtime route is proven wrong.
  </mission>
  <quality>
    Preserve Compact, Middle, High, and Ultra scaling through a continuous GlobalQualityWeight. Compact must still read as premium surface sky. Ultra should spend saved budget on Aegir storm detail, atmospheric layering, and moon/sky readability.
  </quality>
</SUB_AGENT_PROMPT>
```

## Missing Evidence

- Unity Inspector proof of current runtime material instances.
- Player-facing screenshots.
- Frame Debugger pass and texture binding proof.
- Profiler/overdraw proof.
- Confirmation whether unresolved shader GUID `650dd9526735d5b46b79224bc6e94025` resolves to a built-in, package, or missing shader in the live project.
- Confirmation whether the unresolved cloud texture GUIDs in `Mat_HectonSky.mat` are intentionally null, stale, or resolved outside the scanned asset set.

## Final State

STATIC SKY PATH AUDIT COMPLETE.

Three-pillar acceptance is not complete. Graphics are unverified, optimization is unverified, and gameplay/readability impact is unverified. The static owner route is mapped and ready for Unity-slot proof.
