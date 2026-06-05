# Batch20 Worker 2006 - Sky Aegir Source Validator Packet

Status: STATIC SOURCE PACKET ONLY. No Unity, no GameView, no SceneView capture, no profiler, no runtime proof. All runtime and visual claims below remain PENDING VERIFICATION until the Unity capture checklist is executed.

## Authority Loaded

- Root authority: `AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `PROJECT_BIBLES.md`.
- Domain authority: `celestial.md`, `atmosphere.md`, `lighting.md`, `rendering.md`, `shaders.md`, `presentation.md`, `performance.md`.
- Mandates: `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`.

Authority result: bright surface sky is not optional. Aegir, moons, coastline, ocean surface, horizon, photic shallows, clouds, and sun/exposure must read as premium source-backed route content. Darkness/noir is valid for depth, caves, interiors, storms, and temporary eclipse windows only. Dark surface coverups are rejected.

## Static Source Route

### Aegir Prefab And Material Route

- `Assets/_Project/_PROLOGUE_CONTENT/Prefabs/GasGiant_Aegir.prefab`
  - Active `GasGiant_Aegir`, layer 15.
  - Uses Unity built-in sphere mesh GUID `0000000000000000e000000000000000`, fileID `10207`.
  - Scale is `40000,40000,40000`; position is `60000,15000,25000`.
  - Material GUID `ab7b03af667690149bdc7be9a1ae023c` resolves to `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`.
  - Static risk: this is an active primitive celestial route. It must not pass a source-aware validator as a final surface sky route without explicit exemption and visual proof.

- `Assets/_Project/Prefabs/GasGiant_Aegir.prefab`
  - Active `GasGiant_Aegir`, layer 15.
  - Uses project mesh GUID `fc0e817ab0eb67648b9a823825236a85`, path `Assets/_Project/Art/Models/gasgiant.asset`.
  - Scale is `100,100,100`; position is `61711,15000,24136.002`.
  - Uses the same Aegir impostor material.
  - Static result: better mesh route than the prologue prefab, but runtime route and visual quality are PENDING VERIFICATION.

- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
  - Shader: `Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader`.
  - `_MainTex`: `Assets/_Project/Art/TEXTURES/clouds0_diff.png`, 4096x2048.
  - `_DetailTex`: `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`, 2048x2048.
  - `_StormTex`: `Assets/_Project/Art/TEXTURES/Aegir_storms.png`, 4096x2048.
  - Notable scalars: `_Exposure 1.16`, `_DetailStrength 1.08`, `_StormStrength 0.62`, `_RimStrength 0.58`, `_HorizonVeilStrength 0.76`, `_HorizonVeilStart -0.025`, `_HorizonVeilEnd 0.32`.

### Aegir_storms Role

`Aegir_storms.png` is used in two static material routes:

- Impostor material `_StormTex`; shader samples RGB and converts to luma for storm mask/glow contribution.
- Sky master material `_AegirBandTex`; shader samples the texture for Aegir band contribution.

No channel-specific role is proven. Do not guess R/G/B/A meaning. Current `.meta` for `Assets/_Project/Art/TEXTURES/Aegir_storms.png` has `sRGBTexture: 1`, mipmaps enabled, max texture size 2048, and is not readable. That is acceptable for color art, suspicious for a numeric mask unless a shader/import contract explicitly defines it as color-luma input.

The duplicate `_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png` has separate GUID `24b48f...` and the same dimensions/size by static inspection. It is not the material-bound `_StormTex` in the inspected Aegir material.

### Sky Atlas Route

Two editor routes write `Assets/_Project/Art/Textures/Sky/HectonSkyAtlas_RGBA.png`:

- `Assets/_Project/Editor/HectonSkyTools.cs`
  - R = cloud density from density red.
  - G = detail noise from detail red.
  - B = flow X from curl of density gradient, remapped to `[0,1]`.
  - A = flow Y from curl of density gradient, remapped to `[0,1]`.
  - Imports Linear, Repeat, mipmapped, BC7 where supported.

- `Assets/_Project/Editor/HectonSkyAtlasGenerator.cs`
  - R = cloud density from processed source.
  - G = detail erosion.
  - B = deterministic triangle-wave flow X.
  - A = deterministic triangle-wave flow Y.
  - Imports Linear, Repeat, mipmapped, BC7 where supported.

Static risk: both routes target the same atlas filename but document different flow semantics. Source-aware validation must fail if shader consumers cannot identify one authoritative channel contract for the exact asset GUID currently bound in the sky material.

### Sky System And Horizon Route

- `Assets/_Project/Prefabs/Sky_System.prefab`
  - Child `Sphere` uses Unity built-in sphere mesh GUID `0000000000000000e000000000000000`, fileID `10207`.
  - Material resolves to `Assets/_Project/Art/Materials/Mat_HectonSky.mat`.
  - Root `SkySystemFollowCamera` serializes `followVerticalPosition: 0`, `lockToSeaLevel: 1`.

- `Assets/_Project/Scripts/SkySystemFollowCamera.cs`
  - Current script default has `followVerticalPosition = true` and `lockToSeaLevel = false`, but prefab serialization overrides this.
  - Static risk: sea-level lock can hide or expose horizon/parallax problems depending scene overrides. Visual relation to ocean/horizon is PENDING VERIFICATION.

- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - Shader: `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`.
  - `_MainCloudTex`: `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`.
  - `_StarTex`: `Assets/_Project/Art/TEXTURES/Sky/bo2.png`.
  - `_HighCloudTex` GUID and `_MainCloudAtlas` GUID did not resolve by static GUID search. This is a validator failure candidate until proven generated/imported and bound.
  - Notable risk scalars: `_SkyLuminanceMultiplier 1.34`, `_NightBlend 0`, `_SunElevation 0.62`, `_HazeIntensity 0.46`, `_HorizonMistShelfIntensity 0.58`, HDR `_SunDiscColor` values above 9.0.

### Scene/Game Consistency Route

- `Assets/_Project/Scripts/Editor/SceneViewSkyboxEnforcer.cs` forces SceneView skybox, clouds, image effects, fog, and skybox camera clear flags every 0.5 seconds.
- Static risk: it improves SceneView behavior but cannot prove GameView parity. It can also hide mismatch if Game camera, RenderSettings, scene overrides, or runtime material properties differ.
- Requirement: paired SceneView and GameView captures from identical headings are mandatory. Any Scene/Game match claim before capture is invalid.

### ProductFace Validator Coverage

`Assets/_Project/Scripts/Editor/ProductFaceSkyOceanSourceValidator.cs` currently covers:

- `Assets/_Project/Prefabs/Sky_System.prefab`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- `SargassumMicroFaunaBoids.boidMesh`
- Scene override warnings
- Surface visual floor rejection string against dark/fog/storm/noir concealment
- Built-in primitive mesh GUID detection, with narrow Crest input exceptions only

Static gaps:

- Does not inspect `_PROLOGUE_CONTENT/Prefabs/GasGiant_Aegir.prefab`.
- Does not inspect `Assets/_Project/Prefabs/GasGiant_Aegir.prefab`.
- Does not resolve Aegir material texture GUIDs and import contracts.
- Does not validate `Aegir_storms` role or duplicate source conflict.
- Does not validate sky atlas channel contract conflict.
- Does not validate moon texture sources, missing normal/height route, or shader slot availability.
- Does not validate sun/exposure/horizon readability.
- Does not enforce SceneView/GameView paired capture evidence.

## Repair Packet

### Validator Rules To Add In A Future Assets Pass

No Assets edits were made by worker 2006.

1. Add Aegir prefab validation for both Aegir prefab paths.
2. Fail active visible built-in primitive mesh use for Aegir and Sky_System unless an explicit source route card says it is a hidden input, not a visible final object.
3. Resolve every texture GUID on `MAT_AegirGasGiant_Impostor_1428.mat`, `MAT_AegirSky_Master.mat`, and `Mat_HectonSky.mat`; fail missing GUIDs.
4. Validate `Aegir_storms.png` by binding role, not guessed channels: color-luma storm/band texture unless a later material contract changes it.
5. Fail duplicate same-name Aegir storm textures unless the active binding, archive role, and intended owner are documented.
6. Validate exactly one sky atlas channel contract for `HectonSkyAtlas_RGBA.png`; fail if curl-flow and triangle-wave generator outputs are both plausible active owners.
7. Validate moon materials against shader slots. Current `Hecton_CelestialMoon.shader` is base-map-only; normal/height prompt output cannot be considered active until a shader/material route exists.
8. Require a capture proof artifact reference before any `Scene/Game consistent`, `bright surface passed`, `Subnautica-level`, or `Aegir integrated` claim.
9. Fail any surface route whose fix is lower exposure, fog wall, storm-only lighting, nighttime-only review, black sky, hidden horizon, or crushed ocean.

### Texture Channel Contracts

- Aegir `_MainTex`: color albedo/cloud-band source. RGB color. Alpha unclaimed.
- Aegir `_DetailTex`: detail/storm texture. Shader currently consumes RGB luma/detail. Channels unclaimed.
- Aegir `_StormTex` / `Aegir_storms.png`: shader consumes RGB luma as storm mask/glow input. Channels unclaimed. sRGB import is consistent with color-luma role, not with raw numeric mask unless explicitly contracted.
- Sky atlas RGBA: must be one contract only before binding:
  - contract A: R density, G detail, B curl-flow X, A curl-flow Y.
  - contract B: R density, G erosion, B triangle-flow X, A triangle-flow Y.
- Moon `_BaseMap`: active shader consumes base color only. Normal, height, phase mask, and terminator texture channels are future-route assets until shader/material support exists.

### Bright Surface Exposure Risks

PENDING VERIFICATION:

- HDR sun disc values in `Mat_HectonSky.mat` may wash out clouds, Aegir, moon silhouettes, or horizon if tone mapping/exposure are not bounded.
- Horizon mist and veil values may become a fog wall. Fog wall as surface coverup is rejected.
- `SkySystemFollowCamera` sea-level lock may stabilize ocean relation or hide altitude mismatch. Must be tested at sea level, 5 m above water, 30 m above water, and shallow underwater.
- Aegir horizon veil in the impostor shader must integrate with atmosphere and ocean haze without becoming a sticker edge or grey blanket.

### Cloud And Moon Source Gaps

- Cloud source has unresolved sky material GUIDs for `_HighCloudTex` and `_MainCloudAtlas` in static search.
- Sky atlas has two generators with conflicting flow contracts.
- Current moon materials reuse terrain rock/base color textures:
  - `MAT_CelestialMoon_Ione`, `MAT_CelestialMoon_Pelagia`, `MAT_CelestialMoon_Varda` bind `Rocks019_1K-JPG_Color.jpg`.
  - `MAT_CelestialMoon_Khepri`, `MAT_CelestialMoon_Nammu`, `MAT_CelestialMoon_Thalos` bind `Rock031_1K-JPG_Color.jpg`.
- Current moon shader has no normal/height texture slot. This is not a complete moon source route.

## Low / Middle / High / Ultra Consequences

- Low: one authoritative sky atlas, compressed mipmapped textures, cheap luma storm evaluation, no extra runtime texture generation, no darkening surface to save cost.
- Middle: higher cloud detail cadence, stable Aegir impostor material, paired moon base maps with readable silhouettes, bounded sun exposure.
- High: improved cloud flow and horizon integration, stronger Aegir limb/veil detail, moon source maps with shader route if implemented, ocean reflection/readability checks.
- Ultra: visual overkill through better authored textures, layered cloud motion, high-res Aegir storm/band assets, and richer horizon relation. Gameplay truth, DTO layout, save identity, and authority route must not change with `GlobalQualityWeight`.

## Gameplay And Optimization Requirements

- Gameplay readability: surface player must read horizon, coastline direction, waterline, Aegir bearing, moon phase/silhouette, sun glare danger, and ocean depth transition. Beauty without navigational clarity fails.
- Optimization: all visual scale choices must consume continuous `GlobalQualityWeight`; no binary quality switches.
- Shader hot path: prefer authored textures, atlas packing, material property blocks, and cheap deterministic fakes over simulation. No per-frame allocations. No tiny jobs or same-frame schedule/readback loops without profiler proof.
- Ocean relation: sky brightness, haze, Aegir tint, sun exposure, and water surface must be judged together. Sky-only screenshots do not prove the route.

## Proof Boundary

This packet proves static source risks and repair requirements only. It does not prove:

- Aegir is visible or integrated in runtime.
- Moons read correctly in SceneView or GameView.
- Cloud atlas is bound and visually correct.
- SceneView and GameView match.
- Exposure is acceptable.
- Frame cost is within budget.

All of those remain PENDING VERIFICATION.
