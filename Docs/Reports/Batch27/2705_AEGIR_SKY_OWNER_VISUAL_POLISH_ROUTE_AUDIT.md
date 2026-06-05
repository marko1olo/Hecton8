# Batch27 Worker 2705 - Aegir/Sky Owner Visual Polish Route Audit

## Scope

Report-only static/source/material audit. No Unity launch, Play Mode, dotnet build, process kill, asset import, or project edit outside this report.

Primary sources inspected:
- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `celestial.md`
- `atmosphere.md`
- `rendering.md`
- `shaders.md`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_GPU_Sovereignty.txt`
- `Docs/Reports/Batch26/2604_AEGIR_CELESTIAL_OWNER_AUDIT.md`
- `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md`
- `Docs/Reports/Batch26/BATCH26_SYNTHESIS_FOR_UNITY_OWNER.md`
- `Docs/Orchestration/UNITY_OWNER_STEER_20260604_1474_FULL_PACKET_REJECT_FALSE_VIEWS.md`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- sky/Aegir/sun material and shader YAML/source files named below

## Verdict

Recommended primary route: **sky-material sun disc**, owned by `Mat_HectonSky.mat` / `Hecton_AlienSky_Master.shader`, driven by `HectonCelestialEngine` and published through `AtmosphereDirector`.

Rejected primary route: **scene-mesh sun disc** using `SURFACE_LOW_SUN_DISC_1428`.

Reason:
- `HectonCelestialEngine.ApplySunOcclusion()` already treats `_atmosphereManager != null` as `skyOwnsPrimarySunDisc`.
- `HectonCelestialEngine.RestoreSunDefaults()` hides `sunVisualTransform` when atmosphere ownership exists.
- `HectonUnderwaterVisuals.ApplySunVisualState()` hides the assigned sun visual whenever `_cachedAtmoManager != null`.
- `RenderSettings.m_Skybox`, `HectonCelestialEngine._skyMaterial`, and `HectonUnderwaterVisuals.skyMaterial` all route to `Mat_HectonSky.mat` GUID `c94a1beef2372b8458941c2ed9d05d5e`.
- `SURFACE_LOW_SUN_DISC_1428` is inactive, its `MeshRenderer` is disabled, and its material `MAT_SurfaceSunDisc_1428.mat` is flat/untextured. Activating it would add a second sun truth owner and would not meet the visual floor.

`HectonCelestialEngine.sunVisualTransform == null` is acceptable only if the route is explicitly recorded as `PrimarySunDiscOwner=SkyMaterial` and proof no longer expects the disabled mesh sun.

## Static Evidence

Scene state in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`:
- `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` is active/enabled and has `sunVisualTransform: {fileID: 1985271341}`, `atmosphereManager: {fileID: 1893406171}`, and `skyMaterial` GUID `c94a1beef2372b8458941c2ed9d05d5e`.
- `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428` is active/enabled and has `sunLight`, Aegir transform/renderer/body refs, `_atmosphereManager`, and `_skyMaterial` assigned.
- `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428.sunVisualTransform` is `{fileID: 0}`.
- `RenderSettings.m_Skybox` points at `Mat_HectonSky.mat` GUID `c94a1beef2372b8458941c2ed9d05d5e`.
- `SURFACE_LOW_SUN_DISC_1428` is inactive (`m_IsActive: 0`), its `MeshRenderer` is disabled, and it uses material GUID `c19903d8225ff1e41943f466caf4ad5d`.
- Aegir primary renderer uses `MAT_AegirGasGiant_Impostor_1428.mat` GUID `ab7b03af667690149bdc7be9a1ae023c`, scale `17293.156`, fixed direction `{0.033154998, 0, -0.9994502}`, fixed vertical offset `0.135`, anchor distance `50000`, angular diameter `38`.
- `H8_AEGIR_SKY_BACKDROP_1428` and other alternate gas giant/backdrop renderers are disabled and must not be treated as primary.

Source state:
- `HectonCelestialEngine.UpdateSkyMaterial()` writes sun/Aegir direction and sky state into `_skyMaterial`.
- `HectonCelestialEngine.ApplySkyMaterialProperties()` writes `_SunDirection`, `_AegirDirection`, sky colors, sunset blend, Aegir glow/haze, atmosphere weights, eclipse, and game time.
- `HectonCelestialEngine.ApplySunOcclusion()` only toggles `sunVisualTransform` when atmosphere does not own the primary sun disc.
- `HectonCelestialEngine.UpdateAegirMaterial()` uses `MaterialPropertyBlock` for Aegir runtime properties, not per-object material clones.
- `HectonCelestialEngine.DetachDeepCelestialTextures()` can detach sky/Aegir texture refs under deep/perf residency reduction. Surface proof must record that defaults are restored and resident.
- `HectonUnderwaterVisuals.ApplyRuntimeSkyboxOwnership()` and `ForceMandatedSkyboxOwnership()` route the skybox through `AtmosphereDirector.SetSkybox(skyMaterial)`.
- `HectonUnderwaterVisuals` still has validation that can warn when `sunVisualTransform` cannot resolve. That warning must become route-aware if the sky route removes or ignores mesh sun refs.
- `HectonAtmosphereManager.AtmosphereDirector` is a static facade for skybox assignment. It should not become a second celestial truth owner.

Material/shader state:
- `Mat_HectonSky.mat` uses `Hecton_AlienSky_Master.shader`, has cloud and star textures, has shader sun disc parameters, and is the scene/default/runtime sky route.
- `Hecton_AlienSky_Master.shader` implements the shader sun disc, sun glow/scatter, Aegir halo/lensing, clouds, stars, and sky color response.
- `MAT_AegirGasGiant_Impostor_1428.mat` uses `H8_AegirGasGiantImpostor_1428.shader` with `_MainTex`, `_DetailTex`, and `_StormTex` assigned.
- `MAT_SurfaceSunDisc_1428.mat` is not an acceptable primary sun route without a separate scoped owner conversion. It has no authored texture and is currently disabled in scene.
- `MAT_SurfaceNoirProceduralSkybox_1428.mat` is a dark procedural/event-style sky material and must not be promoted to normal surface default.

## Required Owner Route Changes

Source targets for the Unity owner after runtime health clears:
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
  - Record the route explicitly as sky-material-owned primary sun, not implicit side effect.
  - Keep `sunVisualTransform == null` valid when `_atmosphereManager` and `_skyMaterial` are present.
  - Validate that `_skyMaterial` uses the intended sky shader and has sun disc parameters.
  - Continue writing sun/Aegir directions and sky state through `_skyMaterial` and global shader properties.
  - Do not add a second runtime sun disc owner.
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
  - Treat `sunVisualTransform` as auxiliary/legacy when atmosphere sky ownership is active.
  - Make unresolved-sun warnings route-aware so sky ownership does not produce false blockers.
  - Continue routing skybox assignment through `AtmosphereDirector`; do not make UnderwaterVisuals the sun truth owner.
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
  - Keep `AtmosphereDirector` as skybox material assignment bridge only.
  - Do not move celestial phase, sun visibility, Aegir direction, or proof truth into AtmosphereManager.

Scene/material targets:
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
  - Keep one active UnderwaterVisuals owner and one active Atmosphere/Celestial owner.
  - Keep `RenderSettings.m_Skybox`, `HectonCelestialEngine._skyMaterial`, and `HectonUnderwaterVisuals.skyMaterial` on `Mat_HectonSky.mat`.
  - Do not activate `SURFACE_LOW_SUN_DISC_1428`.
  - Do not wire `SURFACE_LOW_SUN_DISC_1428` into `HectonCelestialEngine.sunVisualTransform` as primary sun.
  - Mark the disabled mesh sun as auxiliary/dev-only or remove it later under scoped deletion proof after route acceptance.
- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - Remains the primary surface sky/sun material.
  - Runtime proof must capture post-write values, not static YAML alone.
- `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`
  - Remains the primary shader sun/sky implementation.
  - Polish should improve sun disc, scatter, haze, and Aegir sky integration without adding mesh duplicate truth.
- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
  - Remains the primary Aegir material route.
  - Requires visual proof and tuning against sticker/rim/stripe artifacts.
- `Assets/_Project/Art/Shaders/Celestial/H8_AegirGasGiantImpostor_1428.shader`
  - Remains the primary Aegir impostor shader route unless proof shows it cannot meet the floor.

## Changes That Must Not Be Done

- Do not activate `SURFACE_LOW_SUN_DISC_1428` while the shader sun disc remains active.
- Do not assign `HectonCelestialEngine.sunVisualTransform` to fileID `1985271341` as a quick fix. Current source will hide it under atmosphere ownership, and the flat material is visually insufficient.
- Do not use `MAT_SurfaceNoirProceduralSkybox_1428.mat` or a dark procedural/noir sky as normal surface proof.
- Do not hide weak Aegir/sun/sky art with fog, darkness, haze, exposure crush, post bloom, or cropped screenshots.
- Do not accept Batch26/1474 screenshots or labels as proof. They were rejected for false labels, dirty runtime, missing manifest, and weak Aegir/celestial visuals.
- Do not add per-object material clones or hot `Renderer.material` mutations. Use shared materials plus `MaterialPropertyBlock` where runtime values are needed.
- Do not create binary low/high render switches. All fidelity/cadence/capacity decisions must consume continuous `GlobalQualityWeight`.
- Do not let deep/performance celestial texture detachment affect surface/Aegir proof.
- Do not make AtmosphereManager, UnderwaterVisuals, and CelestialEngine all claim primary sun truth.

## Aegir Artifact Risks

Current `MAT_AegirGasGiant_Impostor_1428.mat` values are not proof of quality. Risks from static values and shader route:
- `_HorizonVeilStrength: 0.76`, `_HorizonVeilStart: -0.025`, `_HorizonVeilEnd: 0.32`: high risk of disconnected atmosphere layer or washed sticker edge if crop view shows the planet/sky join.
- `_RimStrength: 0.58`, `_RimPower: 2.8`, `_RimTint` blue HDR component `1.3`: risk of neon rim or pasted-sphere outline.
- `_DetailStrength: 1.08`, `_StormStrength: 0.62`, `_DetailTiling` offset `0.08`, `_StormTiling` offset `0.18`: risk of crude stripes, seam exposure, or noisy vertical banding. Batch26/1474 already called out dirty green/black and crude vertical/seam/stripe artifacts.
- Opaque geometry route (`Queue=Geometry`, `ZWrite On`, `Blend One Zero`) can create hard horizon/occlusion interactions if the veil and sky integration are not tuned.
- `_AutoRotationSpeed: 0.00004` plus runtime `_GlobalRotation` and `_GameTime` MPB can expose seams over time. Proof must include crop inspection, not only long shot.
- `Mat_HectonSky._AegirGlowIntensity` is static `0`, while runtime may set glow/haze. Static material YAML cannot prove night/celestial quality.
- `Mat_HectonSky` has null `_BakedStarCubemap` and `_StarTwinkleLUT`. Not a blocker for primary sun/Aegir route, but manifest must disclose whether fallback procedural stars are active.
- `HectonCelestialEngine.DetachDeepCelestialTextures()` can null sky/Aegir textures under deep/perf paths. Surface/celestial proof must record texture residency state.

## Proof Requirements

Static proof requirements:
- `PrimarySunDiscOwner=SkyMaterial` recorded in source/scene/proof manifest.
- `Mat_HectonSky.mat` GUID `c94a1beef2372b8458941c2ed9d05d5e` is the RenderSettings skybox, CelestialEngine sky material, and UnderwaterVisuals sky material.
- `HectonCelestialEngine.sunVisualTransform == null` is route-valid and not a blocker in sky-material route.
- `SURFACE_LOW_SUN_DISC_1428` remains inactive/renderer-disabled and excluded from acceptance.
- Aegir primary renderer uses `MAT_AegirGasGiant_Impostor_1428.mat` GUID `ab7b03af667690149bdc7be9a1ae023c`.
- Alternate disabled Aegir/backdrop/noir sky materials are not active acceptance paths.

Runtime log proof requirements:
- No route false warning such as `sunVisualTransform still unresolved` when sky route is active.
- No ready-lock registry rejection involving UnderwaterVisuals, AtmosphereManager, CelestialEngine, skybox, or Aegir.
- No duplicate service publication for sky/celestial/atmosphere ownership.
- No WeatherEvents leak, shader/material/import/compile errors, asset refresh churn, or dirty recompile window during capture.
- Log timestamp must be newer than or bound to the final screenshot packet.

Visual proof requirements:
- Surface coast/Aegir long view, UI off.
- Surface sky/sun view or sky sweep proving shader sun position and scatter.
- Aegir/celestial long view.
- Aegir/celestial crop view showing cloud bands, rim, veil, and horizon integration.
- Shoreline close view.
- Underwater 0-5 m view.
- Underwater 20-50 m route view.
- Low oblique view.
- Views must be fresh proof, not Batch26/1474 reuse, and must not use darkness/noir/cropping to hide weak art.

Manifest fields required for Aegir/surface sky proof:
- Screenshot path, byte size, SHA256, local timestamp, UTC timestamp.
- Scene path, scene hash or revision, loaded scenes.
- Camera world position, rotation, FOV, capture source, render target source, UI state.
- Player/depth band and route state.
- Continuous `GlobalQualityWeight`, render scale, resolution, quality tier label if any, post stack, exposure, fog/haze, cloud, weather, storm, eclipse, and time-of-day state.
- `PrimarySunDiscOwner=SkyMaterial`.
- Sky material path/GUID, sky shader path/GUID, and runtime values after CelestialEngine writes for `_SunSize`, `_SunDiscColor`, `_SunScatterIntensity`, `_SunDirection`, `_AegirDirection`, `_AegirHaloIntensity`, Aegir haze/glow fields, and atmosphere weights.
- Aegir material path/GUID, Aegir shader path/GUID, Aegir transform position/scale, fixed direction, fixed vertical offset, anchor distance, angular diameter.
- Aegir runtime MPB values if available: `_GlobalRotation`, `_GameTime`, `_NightBlend`, `_AtmosphereTransmittanceWeight`, `_AtmosphereInscatterWeight`, sky colors.
- `SURFACE_LOW_SUN_DISC_1428` active state and renderer enabled state.
- Texture residency state for sky cloud/star textures and Aegir main/detail/storm textures, including whether deep/perf detachment is active.
- Clean log path and fault summary.

## Quality Scaling Consequences

Continuous `GlobalQualityWeight` consequences:
- Low / compact survival:
  - Keep sky-material sun disc as the only primary sun route.
  - Keep Aegir textured impostor visible and readable.
  - Reduce optional cloud/reflection/star richness and capture diagnostics cadence before reducing surface readability.
  - No noir fallback, flat sky, muddy water, disabled textures, or primitive sun mesh.
- Middle:
  - Baseline acceptance path: `Mat_HectonSky` plus `MAT_AegirGasGiant_Impostor_1428`.
  - Tuned sun disc/scatter, Aegir bands, rim, haze, and horizon integration.
  - Full six-view proof packet plus manifest/log.
- High:
  - Spend budget on richer sky cloud depth, Aegir halo/veil integration, shoreline reflection/contact support, and crop stability.
  - Same truth route and DTO/owner layout as low/mid.
- Ultra:
  - Visual overkill through higher fidelity sky textures/LUTs, stronger but controlled scattering, denser Aegir atmospheric layering, higher resolution proof captures, and richer celestial detail.
  - No new gameplay truth, no second sun owner, no alternate authority route.

GlobalQualityWeight may scale visual fidelity, cadence, texture residency, optional diagnostics, cloud/detail density, Aegir halo strength, and proof capture resolution. It must not change sun ownership, Aegir identity, scene authority, celestial timing, or route truth.

## Owner-Correct Polish Plan

After runtime health clears:
1. Freeze `PrimarySunDiscOwner=SkyMaterial` in source/proof metadata.
2. Make sun-visual validation route-aware so a null CelestialEngine mesh sun is not a blocker under sky-material ownership.
3. Leave `SURFACE_LOW_SUN_DISC_1428` disabled and remove it from acceptance expectations. Deletion can wait for a scoped cleanup pass after proof.
4. Polish `Mat_HectonSky.mat` / `Hecton_AlienSky_Master.shader` for sun disc, scatter, cloud response, Aegir sky halo, and surface readability.
5. Polish `MAT_AegirGasGiant_Impostor_1428.mat` / `H8_AegirGasGiantImpostor_1428.shader` against rim, veil, seam, sticker, and stripe artifacts using long and crop views.
6. Verify surface/celestial captures occur with sky and Aegir texture defaults resident, not in deep/perf detached state.
7. Produce a fresh 1475-class proof packet with manifest/checksums/log binding.
8. Only after proof passes, consider removal or quarantine of stale alternate sky/Aegir/sun assets under separate scoped proof.

## Key Blockers

- Owner ambiguity remains until source/proof metadata explicitly declares sky-material sun ownership.
- `HectonCelestialEngine.sunVisualTransform` is null while `HectonUnderwaterVisuals` still has mesh sun references and warning paths. This must be made route-aware or it will keep producing false owner confusion.
- `SURFACE_LOW_SUN_DISC_1428` is inactive/disabled and visually inadequate; it cannot be the primary sun route without a separate owner conversion.
- Batch26/1474 Aegir/celestial proof is rejected and cannot be reused.
- Aegir material/shader needs fresh crop proof for rim, veil, seam, sticker, and stripe artifacts.
- Proof manifest does not yet exist for the required fresh packet with route, material GUIDs, runtime values, quality, camera, texture residency, log, and checksums.
