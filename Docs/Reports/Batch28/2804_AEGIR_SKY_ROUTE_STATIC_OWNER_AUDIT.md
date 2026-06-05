# Batch28 Worker 2804 - Aegir/Sky Route Static Owner Audit

Status: STATIC VERIFIED / RUNTIME PROOF NOT RUN
Scope: static Aegir, sky, and sun-owner route audit only.

## Task Boundary

Owned write path:
- `Docs/Reports/Batch28/2804_AEGIR_SKY_ROUTE_STATIC_OWNER_AUDIT.md`

No Unity, Play Mode, build, scene activation, source edit, asset edit, or material edit was run.

Authority read:
- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_GPU_Sovereignty.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `rendering.md`
- `shaders.md`
- `celestial.md`
- `atmosphere.md`
- `water.md`
- `quality.md`
- `Docs/Reports/Batch27/2705_AEGIR_SKY_OWNER_VISUAL_POLISH_ROUTE_AUDIT.md`
- `Docs/Reports/Batch27/BATCH27_SYNTHESIS_FOR_UNITY_OWNER.md`

## Verdict

Primary sun route remains:
- `PrimarySunDiscOwner=SkyMaterial`
- owner driver: `Assets/_Project/Scripts/HectonCelestialEngine.cs`
- skybox assignment bridge: `HectonAtmosphereManager.AtmosphereDirector`
- primary material: `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- primary shader: `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`

Rejected route:
- activating `SURFACE_LOW_SUN_DISC_1428`
- wiring it into `HectonCelestialEngine.sunVisualTransform`
- treating its inactive mesh as proof debt to fix by activation

Reason:
- the active scene already routes RenderSettings, CelestialEngine, and UnderwaterVisuals to `Mat_HectonSky.mat`;
- `HectonCelestialEngine` already suppresses mesh sun-disc ownership when `_atmosphereManager != null`;
- `HectonUnderwaterVisuals` already hides the mesh sun visual when an atmosphere manager is cached;
- the mesh candidate is inactive, renderer-disabled, and uses a flat untextured material;
- activating it would create a second sun-disc truth route while the shader sun disc remains active.

## Static Scene Evidence

Scene:
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

RenderSettings:
- line 29: `m_SkyboxMaterial` points to GUID `c94a1beef2372b8458941c2ed9d05d5e` (`Mat_HectonSky.mat`).

Underwater owner:
- lines 4608-4613: `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` is active.
- line 4632: `sunVisualTransform: {fileID: 1985271341}` points at `SURFACE_LOW_SUN_DISC_1428`.
- line 4650: `atmosphereManager: {fileID: 1893406171}` is assigned.
- line 4652: `skyMaterial` points to GUID `c94a1beef2372b8458941c2ed9d05d5e`.

Celestial owner:
- lines 90871-90876: `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428` is active.
- lines 90888-90895: `HectonCelestialEngine` has `sunLight`, Aegir transform/renderer, player, `_atmosphereManager`, and `_skyMaterial` assigned.
- line 90895: `_skyMaterial` points to GUID `c94a1beef2372b8458941c2ed9d05d5e`.
- line 91163: `HectonCelestialEngine.sunVisualTransform` is `{fileID: 0}`.
- line 91167: `blendedSkyboxMaterial` also points to GUID `c94a1beef2372b8458941c2ed9d05d5e`.

Aegir primary renderer:
- lines 89860-89868: prefab instance name is `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` and active.
- lines 89870-89893: renderer is enabled, shadows/reflection probes disabled, and material slot points to GUID `ab7b03af667690149bdc7be9a1ae023c`.
- lines 90890-90892: CelestialEngine references the same Aegir transform and renderer.

Disabled alternate Aegir backdrop:
- lines 94850-94855: `H8_AEGIR_SKY_BACKDROP_1428` GameObject is active.
- lines 94856-94864: its MeshRenderer is disabled.
- It is not the accepted primary Aegir path.

Rejected sun mesh:
- lines 95890-95895: `SURFACE_LOW_SUN_DISC_1428` exists but GameObject is inactive.
- lines 95911-95918: MeshRenderer exists but `m_Enabled: 0`.
- line 95936: material slot points to GUID `c19903d8225ff1e41943f466caf4ad5d` (`MAT_SurfaceSunDisc_1428.mat`).

## Static Material And Shader Evidence

Primary sky material:
- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- GUID `c94a1beef2372b8458941c2ed9d05d5e`
- shader GUID `6302a783d2378694c9db8d0036358965`
- shader path `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`
- has shader sun-disc fields: `_SunSize`, `_SunEdgeSoftness`, `_SunDiscColor`, `_SunScatterColor`, `_SunScatterIntensity`
- has sky/Aegir fields: `_SunDirection`, `_AegirDirection`, `_AegirHaloIntensity`, `_AegirGlowIntensity`, `_AtmosphereTransmittanceWeight`, `_AtmosphereInscatterWeight`
- has cloud/star texture refs for `_HighCloudTex`, `_MainCloudAtlas`, `_StarTex`
- static `_BakedStarCubemap` and `_StarTwinkleLUT` are null; runtime/bake state must be disclosed in proof, not hidden.

Primary sky shader:
- `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`
- GUID `6302a783d2378694c9db8d0036358965`
- properties include `_SunSize`, `_SunDiscColor`, `_SunScatterIntensity`, `_SunScatterColor`, `_AegirHaloIntensity`, `_AegirGlowIntensity`, `_SunDirection`, `_AegirDirection`, `_BakedStarCubemap`, `_StarTwinkleLUT`.
- lines 1102-1135 implement shader sun disc, corona, scatter, cloud/eclipse visibility, and horizon gating.
- lines 1142-1151 implement Aegir halo/lensing contribution.

Primary Aegir material:
- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
- GUID `ab7b03af667690149bdc7be9a1ae023c`
- shader GUID `0661c64fe7dfd77469f3bd686cbc254e`
- texture refs:
  - `_MainTex` GUID `6c173d4e1a858b34ca1b7e5610aae988`
  - `_DetailTex` GUID `e1aefa60ab4517644bb884257440872b`
  - `_StormTex` GUID `d9d11072e85a2b54cacd11eaad6614a8`
- static risk values:
  - `_HorizonVeilStrength: 0.76`
  - `_HorizonVeilStart: -0.025`
  - `_HorizonVeilEnd: 0.32`
  - `_RimStrength: 0.58`
  - `_RimPower: 2.8`
  - `_RimTint.b: 1.3`
  - `_DetailStrength: 1.08`
  - `_StormStrength: 0.62`
  - `_DetailTiling.z: 0.08`
  - `_StormTiling.z: 0.18`

Primary Aegir shader:
- `Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader`
- GUID `0661c64fe7dfd77469f3bd686cbc254e`
- lines 144-153 animate base/detail/storm UVs using `_GameTime`, `_GlobalRotation`, and `_AutoRotationSpeed`.
- lines 174-190 apply rim and horizon veil.
- Artifact risk is static-only until fresh crop proof exists.

Rejected sun-disc material:
- `Assets/_Project/Art/Materials/World/MAT_SurfaceSunDisc_1428.mat`
- GUID `c19903d8225ff1e41943f466caf4ad5d`
- `_MainTex` and `_BaseMap` are null.
- base color is flat brown/orange `{r: 0.65, g: 0.39, b: 0.18, a: 1}`.
- It is not a premium sky/sun material and cannot satisfy the surface visual floor.

## Source Hooks Requiring Route-Aware Metadata

`Assets/_Project/Scripts/HectonCelestialEngine.cs`
- lines 2273-2281: visual sync writes globals, sky material, Aegir material, moon overrides, texture residency, then applies sun occlusion.
- lines 5959-5974: `UpdateSkyMaterial()` writes `_SunDirection` and `_AegirDirection` into `_skyMaterial`.
- lines 5980-6013: `ApplySkyboxMaterialOwnership()` / `ForceMandatedSkyMaterialReference()` make the mandated sky material the skybox route.
- lines 6040-6067: `ApplySkyMaterialProperties()` writes time, night blend, star intensity/seed, sun elevation, eclipse, penumbra, atmosphere weights, sun direction, Aegir direction, sky colors, Aegir glow, haze, and surface weather.
- lines 6246-6307: `ApplySunOcclusion()` declares `skyOwnsPrimarySunDisc = _atmosphereManager != null` and only toggles `sunVisualTransform` when sky does not own the primary sun disc.
- lines 6328-6345: `RestoreSunDefaults()` hides the mesh sun visual when `_atmosphereManager != null`.
- lines 6750-6797: `UpdateAegirMaterial()` writes Aegir MPB values including `_FresnelSunDir`, `_GlobalRotation`, `_GameTime`, `_NightBlend`, atmosphere weights, sky colors, and wind direction.

Required source metadata plan:
- Add a single route predicate, not scattered local guesses:
  - `PrimarySunDiscOwner=SkyMaterial`
  - `SkyMaterialOwnsPrimarySunDisc == _atmosphereManager != null && _skyMaterial != null && IsMandatedSkyMaterial(_skyMaterial)`
- Use that predicate in `ApplySunOcclusion()` and `RestoreSunDefaults()` instead of the loose `_atmosphereManager != null` local variable.
- Expose a read-only proof snapshot for the capture harness. It must not publish signals, search scene hierarchy, allocate, or mutate state.
- Snapshot fields: owner route, sky material path/GUID if available, sky shader path/GUID if available, `sunVisualTransform` assigned state, mesh-sun active/renderer state if assigned, Aegir renderer/material/shader identity, texture residency flags, current sky/Aegir runtime values, and route validity flags.
- Validate route health without making the mesh sun mandatory when sky material ownership is valid.

`Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- lines 2533-2541: `ApplySunVisualState()` hides mesh sun visual when `_cachedAtmoManager != null`.
- lines 2568-2574: `RestoreSunVisual()` also hides mesh sun visual when `_cachedAtmoManager != null`.
- lines 2602-2635: `ApplyRuntimeSkyboxOwnership()` and `ForceMandatedSkyboxOwnership()` push `skyMaterial` through `AtmosphereDirector`.
- lines 4977-4980: runtime owner resolution still tries to resolve `sunVisualTransform` unconditionally.
- lines 5020-5032: `RequestRuntimeVisualOwnerResolveIfMissing()` still treats `sunVisualTransform == null` as missing.
- lines 5570-5594: `ResolveSunVisualTransform()` searches `sunLight.transform.Find("Sun_Body")`.
- lines 7100-7106: `ValidateReferences()` resolves sun visual in play mode.
- lines 7130-7143: `WarnIfRuntimeReferencesStillMissing()` logs `[HectonUnderwaterVisuals] sunVisualTransform still unresolved after runtime retry.` whenever `sunVisualTransform == null`.

Required warning/proof plan:
- Add `RequiresMeshSunVisual()` or equivalent:
  - false when `_cachedAtmoManager != null`, `skyMaterial != null`, and `AtmosphereDirector.IsSkybox(skyMaterial)` or the mandated sky material reference is active;
  - true only for standalone/non-atmosphere fallback routes where the mesh sun is the declared owner.
- Gate these paths with that predicate:
  - `ResolveSunVisualTransform()`
  - `RequestRuntimeVisualOwnerResolveIfMissing()`
  - `WarnIfRuntimeReferencesStillMissing()`
  - `ValidateReferences()`
- When sky ownership is active, runtime warning should become route proof, not a blocker:
  - `PrimarySunDiscOwner=SkyMaterial`
  - `meshSunVisualRequired=false`
  - `meshSunVisualAssigned=<bool>`
  - `meshSunVisualActive=<bool/unknown>`
- Do not reassign `SURFACE_LOW_SUN_DISC_1428` as a quick fix.
- Do not make `HectonUnderwaterVisuals` the celestial truth owner. It is underwater presentation plus skybox assignment bridge consumer.

`Assets/_Project/Scripts/HectonAtmosphereManager.cs`
- lines 69-82: `AtmosphereDirector` is a static facade around `RenderSettings.skybox`.
- lines 571-575: `IAtmosphereRenderSettingsBridge` forwards to `AtmosphereDirector.SetSkybox()`.
- lines 1841-1860: Atmosphere computes sun intensity/horizon fade only.
- lines 1968-2027: Atmosphere publishes Aegir abyss light and `_AegirDirection` from the cached celestial engine.

Required boundary:
- keep `AtmosphereDirector` as skybox assignment bridge only;
- do not move sun-disc ownership, Aegir direction truth, celestial phase, or proof ownership into `HectonAtmosphereManager`.

## Why Activating `SURFACE_LOW_SUN_DISC_1428` Is Rejected

Activating it violates owner and taste rules:
- It creates a second primary sun-disc owner while `Hecton_AlienSky_Master.shader` already renders the shader sun disc.
- It is inactive in scene and MeshRenderer disabled in scene YAML.
- `HectonCelestialEngine.sunVisualTransform` is null, and current code hides mesh sun visuals when atmosphere ownership exists.
- `HectonUnderwaterVisuals.sunVisualTransform` points at the candidate, but its own code hides it when `_cachedAtmoManager != null`.
- Its material has no texture input and a flat color; it is below the surface/sky visual floor.
- It does not solve 1475 proof requirements because the proof route needs sky-material runtime values, Aegir crop proof, clean log, texture residency, and manifest binding.

Only acceptable future use:
- auxiliary/dev-only reference;
- deleted/quarantined later under scoped cleanup with `.meta` handling;
- or converted under a separate owner route card that disables shader sun ownership first and proves premium visual quality. That is outside this task and not recommended.

## Required `1475` Manifest Metadata

The `1475` proof packet must include:
- `PrimarySunDiscOwner=SkyMaterial`
- `MeshSunVisualRequired=false`
- `SURFACE_LOW_SUN_DISC_1428.active=false`
- `SURFACE_LOW_SUN_DISC_1428.rendererEnabled=false`
- scene path and scene file hash/revision
- loaded scenes
- proof session id and UTC/local timestamps
- screenshot path, byte size, dimensions, SHA256
- camera world position, rotation, FOV, capture source, render target source, UI state
- route/view id
- player/depth band and surface/underwater state
- continuous `GlobalQualityWeight`
- render scale, resolution, quality label if present, post stack, exposure, fog/haze, weather/storm, cloud, eclipse, time-of-day
- sky material path/GUID: `Assets/_Project/Art/Materials/Mat_HectonSky.mat` / `c94a1beef2372b8458941c2ed9d05d5e`
- sky shader path/GUID: `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader` / `6302a783d2378694c9db8d0036358965`
- runtime sky values after CelestialEngine writes:
  - `_SunDirection`
  - `_AegirDirection`
  - `_SunSize`
  - `_SunEdgeSoftness`
  - `_SunDiscColor`
  - `_SunScatterColor`
  - `_SunScatterIntensity`
  - `_AegirHaloIntensity`
  - `_AegirGlowIntensity`
  - `_AtmosphereTransmittanceWeight`
  - `_AtmosphereInscatterWeight`
  - `_NightBlend`
  - `_StarIntensity`
  - `_EclipseOcclusion`
  - `_PenumbraFactor`
- Aegir material path/GUID: `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat` / `ab7b03af667690149bdc7be9a1ae023c`
- Aegir shader path/GUID: `Assets/_Project/Art/Shaders/H8_AegirGasGiantImpostor_1428.shader` / `0661c64fe7dfd77469f3bd686cbc254e`
- Aegir texture GUIDs for `_MainTex`, `_DetailTex`, `_StormTex`
- Aegir transform position, scale, fixed direction lock, fixed vertical offset, anchor distance, angular diameter
- Aegir MPB/runtime values:
  - `_FresnelSunDir`
  - `_GlobalRotation`
  - `_GameTime`
  - `_NightBlend`
  - `_PlanetPhase`
  - `_SunBacklitFactor`
  - `_AtmosphereTransmittanceWeight`
  - `_AtmosphereInscatterWeight`
  - sky colors
  - wind direction
- sky texture residency:
  - `_HighCloudTex`
  - `_MainCloudAtlas`
  - `_MainCloudTex`
  - `_StarTex`
  - `_BakedStarCubemap`
  - `_StarTwinkleLUT`
  - whether deep/perf detachment is active
- Aegir texture residency:
  - `_MainTex`
  - `_DetailTex`
  - `_StormTex`
  - whether deep/perf detachment is active
- clean log path, timestamp, and fault summary
- explicit absence of false warning:
  - no `sunVisualTransform still unresolved after runtime retry` when `PrimarySunDiscOwner=SkyMaterial`

## Visual Artifact Checks Required

Required views:
- surface coast/Aegir long view, UI off
- surface sky/sun sweep proving shader sun position and scatter
- Aegir long view
- Aegir crop view
- shoreline close view
- underwater 0-5 m view
- underwater 20-50 m route view
- low oblique view
- diagnostic route overlay view if harness exists

Required Aegir/sky checks:
- rim: no neon outline, no pasted-sphere edge
- veil: no grey wash, no disconnected horizon layer
- seam: no visible UV seam from `_GlobalRotation`, `_GameTime`, `_Rotation`, or texture offsets
- sticker: Aegir must integrate with sky/ocean lighting, not look like a flat overlay
- stripe: cloud bands must read as structured atmospheric bands, not sine/procedural barcodes
- dirty-noir hiding: no darkness, haze, bloom, crop, exposure crush, or storm state hiding weak surface art
- sun disc: shader sun must be visible/readable when route state says it should be visible; no duplicate mesh sun
- water/shore relation: surface and shallows remain bright, readable, and above the Subnautica-level floor

## GlobalQualityWeight Consequences

Low / compact:
- keep sky-material sun disc as the only primary sun route;
- keep textured Aegir visible and readable;
- reduce optional cloud/star richness, proof capture resolution, and diagnostics cadence before reducing sky/ocean readability;
- no noir fallback, muddy sky, flat sun mesh, or disabled Aegir textures.

Middle:
- baseline acceptance route is `Mat_HectonSky` plus `MAT_AegirGasGiant_Impostor_1428`;
- route-aware warnings are required so the clean-log gate is not blocked by missing mesh sun;
- full 1475 manifest is required.

High:
- spend extra budget on richer sky cloud depth, controlled Aegir halo/veil integration, shoreline reflection/contact cues, and crop stability;
- route truth and DTO ownership remain unchanged.

Ultra:
- visual overkill through stronger sky texture/LUT detail, richer Aegir atmospheric layering, higher-resolution proof captures, and controlled scatter;
- no second sun owner and no gameplay truth change.

## Strongest Blockers

1. `HectonUnderwaterVisuals` still has route-unaware missing-sun warning and owner-resolve logic. Lines 5020-5032 and 7130-7143 can still treat `sunVisualTransform == null` as a blocker even when `PrimarySunDiscOwner=SkyMaterial`.
2. `PrimarySunDiscOwner=SkyMaterial` is not yet a first-class source/proof field. The route exists as behavior, but proof metadata still has to infer it from scene/material state.
3. No `1475` proof packet or manifest exists. Static report cannot replace runtime screenshots, clean logs, texture residency, runtime material values, or checksums.
4. `SURFACE_LOW_SUN_DISC_1428` remains a stale scene reference in UnderwaterVisuals. It is inactive/renderer-disabled and visually inadequate; activating it is the wrong fix.
5. Aegir material values have static artifact risk: rim, horizon veil, detail/storm tiling, and rotation can produce sticker, seam, stripe, or dirty veil defects. Fresh crop proof is mandatory.
6. Static sky material has null baked star cubemap and twinkle LUT. Not a route blocker, but 1475 must disclose runtime/bake/fallback state.

## Required Controller Follow-Up

Source work after controller approval:
1. Add the sky-route predicate and proof snapshot in `HectonCelestialEngine`.
2. Make `HectonUnderwaterVisuals` mesh-sun validation and warnings route-aware.
3. Keep `AtmosphereDirector` as skybox bridge only.
4. Produce `1475` through owned capture harness with manifest, clean log, and artifact checks above.

No source or asset changes were made by this audit.
