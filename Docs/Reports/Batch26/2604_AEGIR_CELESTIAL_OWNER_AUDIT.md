# Batch26 2604 - Aegir / Celestial Owner Audit

## Verdict

BLOCKED. Owner decision required before any clean Aegir/celestial acceptance packet.

Static scene state is better than the rejected 1474 packet in one narrow way: `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` now has a concrete `sunVisualTransform` reference. That does not close the route. `HectonCelestialEngine.sunVisualTransform` is still null, `SURFACE_LOW_SUN_DISC_1428` is inactive with its renderer disabled, and both UnderwaterVisuals and CelestialEngine source paths treat an assigned `HectonAtmosphereManager` as the sky-material-owned primary sun route.

No runtime acceptance exists in this audit. No Unity Editor, Play Mode, dotnet build, profiler, or screenshot proof was run by this worker.

First-20-minutes route relevance: this removes no visual blocker by itself. It identifies the owner ambiguity blocking surface/coast/photic-shallow Aegir, sun, and celestial proof.

## Authority And Mandates Read

- `AGENTS.md`
- `VISION_LOCKS.md`
- `celestial.md`
- `rendering.md`
- `shaders.md`
- `TASTE.md`
- `atmosphere.md`, `water.md`, `world.md`
- `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md`
- `Docs/Orchestration/UNITY_OWNER_STEER_20260604_2503_UNDERWATER_CELESTIAL_OWNER.md`
- `Docs/Reports/Batch25/2503_UNDERWATER_VISUALS_OWNER_PHASE_AUDIT.md`
- `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_DescriptorBinding_Reality_Check.txt`
- `.agents-skills/REND_GPU_Sovereignty.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Scene Owner Evidence

Scene: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

Static counts:

- `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`: 1
- `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428`: 1
- `SURFACE_LOW_SUN_DISC_1428`: 1
- `HectonUnderwaterVisuals` script GUID `7b8d6f3311640f64ba03f2b62d8a00cd`: 1
- `HectonCelestialEngine` script GUID `86667f9831733ab48aaa2bb3a38047ee`: 1

UnderwaterVisuals scene state:

- Lines 4597-4614: `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` is active and enabled.
- Lines 4626-4654:
  - `playerCamera: {fileID: 1505808849}`
  - `sunLight: {fileID: 1772751213}`
  - `sunVisualTransform: {fileID: 1985271341}`
  - `mainCamera: {fileID: 1505808848}`
  - `atmosphereManager: {fileID: 1893406171}`
  - `oceanUnderwaterMaterial`: GUID `ef94c26e44a36e24a9dcbc5995a2bed1` -> `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
  - `skyMaterial`: GUID `c94a1beef2372b8458941c2ed9d05d5e` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - `biomePalette`: GUID `1ed7cad7d9660ec4898f244d19b99da4` -> `Assets/_Project/Data/biom/Main_Ocean_Palette.asset`
  - `biomeMatrixDirector: {fileID: 1075616976}`

Celestial scene state:

- Lines 90859-90895: `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428` is active and enabled.
- Lines 90889-90895:
  - `sunLight: {fileID: 1772751213}`
  - `aegirTransform: {fileID: 1873583736}`
  - `aegirObserverRelativeBody: {fileID: 1873583740}`
  - `aegirRenderer: {fileID: 1873583737}`
  - `playerTransform: {fileID: 1505808849}`
  - `_atmosphereManager: {fileID: 1893406171}`
  - `_skyMaterial`: GUID `c94a1beef2372b8458941c2ed9d05d5e` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Lines 91151-91167:
  - `dayAtmosphereExposure: 1.02`
  - `sunsetAtmosphereExposure: 0.58`
  - `nightAtmosphereExposure: 0.001`
  - `sunVisualTransform: {fileID: 0}`
  - `daySkybox`: GUID `adb9fe9be55e5c240a9028e61ecea987` -> `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
  - `nightSkybox`: GUID `e6841cd12af6d9b42a1016d5492aa4b1` -> `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
  - `blendedSkyboxMaterial`: GUID `c94a1beef2372b8458941c2ed9d05d5e` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Lines 91243-91256:
  - `firmamentBakeCompute: {fileID: 0}`
  - `enableGpuFirmamentBake: 0`
  - `firmamentCubemapResolution: 2048`
  - `starTwinkleNoiseLut: {fileID: 0}`
  - `atmosphereScatteringLutWidth: 128`
  - `atmosphereScatteringLutHeight: 32`
  - `atmosphereScatteringDensity: 1`
  - `atmosphereScatteringExposure: 4.4`

Sun-disc candidate:

- Lines 95878-95895: `SURFACE_LOW_SUN_DISC_1428` exists but `m_IsActive: 0`.
- Lines 95896-95910: transform fileID `1985271341`; position `{x:72.363235,y:35.901917,z:46.861877}`; scale `2.2`.
- Lines 95911-95918: MeshRenderer fileID `1985271342`, `m_Enabled: 0`.
- Line 95936: material GUID `c19903d8225ff1e41943f466caf4ad5d` -> `Assets/_Project/Art/Materials/World/MAT_SurfaceSunDisc_1428.mat`.
- Line 95967: MeshFilter uses built-in mesh fileID `10207`.

Aegir route:

- Lines 89794-89901: Aegir is a prefab instance under Sky_System with source GUID `63b512813e5eb97489d4e2dd88d716d8`.
- Lines 89802-89824: Aegir scale is `17293.156`; local position is `{x:1642.847,y:6690.8687,z:-49523.266}`.
- Lines 89890-89893: Aegir renderer material override GUID `ab7b03af667690149bdc7be9a1ae023c`.
- GUID `ab7b03af667690149bdc7be9a1ae023c` resolves to `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`.
- Lines 89922-89957: `ObserverRelativeCelestialBody` uses fixed direction `{x:0.033154998,y:0,z:-0.9994502}`, vertical offset `0.135`, anchor distance `50000`, angular diameter `38`, renderer `1873583737`, mesh filter `1873583739`.

## Material Evidence

Live sky material:

- `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Shader GUID `6302a783d2378694c9db8d0036358965` -> `Assets/_Project/Art/Shaders/Hecton_AlienSky_Master.shader`
- Referenced by scene skybox, UnderwaterVisuals, and CelestialEngine.
- Has `_HighCloudTex`, `_MainCloudAtlas`, and `_StarTex`.
- No `_BakedStarCubemap` assignment.
- No `_StarTwinkleLUT` assignment.
- Static material values include `_AegirGlowIntensity: 0`, `_AegirHaloIntensity: 0.74`, `_SkyColorZenith {0.16,0.27,0.32}`, `_SkyColorHorizon {0.36,0.46,0.5}`, `_SkyColorNadir {0.11,0.165,0.19}`, and `_SunDiscColor {16.306889,14.676201,9.784134}`.
- Static conclusion: the sky material is the apparent primary visual route, but the material file alone does not prove bright premium surface/Aegir readability. Runtime globals and screenshots are required.

Live Aegir material:

- `Assets/_Project/Art/Materials/Celestial/MAT_AegirGasGiant_Impostor_1428.mat`
- Shader `H8_AegirGasGiantImpostor_1428`, GUID `0661c64f3ed21d64282395fac9453f10`.
- Uses detail, main, and storm textures:
  - Detail: GUID `e1aefa6e458c8ea46b58d9f00b326e3c`
  - Main: GUID `6c173d11916f2174d947e27dde687db4`
  - Storm: GUID `d9d110267d842b04398dfd5f03608991`
- Static values include `_Exposure: 1.16`, `_DetailStrength: 1.08`, `_StormStrength: 0.62`, `_HorizonVeilStrength: 0.76`, `_DeepTint {0.09,0.13,0.3}`, `_HighTint {0.42,0.58,1.08}`.
- Static conclusion: there is a real textured Aegir material in the live scene path. That is not visual acceptance. The 1474 packet was rejected for weak/dirty Aegir/celestial artifacts, so the material must be proven in 1475-class captures.

Inactive sun-disc material:

- `Assets/_Project/Art/Materials/World/MAT_SurfaceSunDisc_1428.mat`
- Flat/untextured material. `_BaseMap` and `_MainTex` are null.
- `_BaseColor` and `_Color` are `{r:0.65,g:0.39,b:0.18}`.
- Static conclusion: this is not adequate as sole premium sun proof. If used, it needs owner activation plus visual polish or shader-backed presentation.

Event/stale risk materials:

- `MAT_SurfaceNoirProceduralSkybox_1428.mat` is not referenced by `02_HECTON_WORLD` and is marked event-only by validation code, but it has `_Exposure: 0.42`, `_SunSize: 0.0001`, and very dark ground/sky tint. It must not become default surface sky.
- `MAT_SurfaceSkyNoirGradient_1428.mat`, `MAT_SurfaceGasGiant_1428.mat`, `MAT_H8AegirGasGiantReal_1428.mat`, and `MAT_AegirSky_Master.mat` appear as candidate or alternate assets, not the current scene-owned skybox route found in this audit.

## Source Ownership Evidence

UnderwaterVisuals:

- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- Line 75: `[ExecuteAlways]`
- Line 77: `[DefaultExecutionOrder(-4000)]`
- Lines 1025-1085: `OnEnable` registers `ActiveRuntimeInstance`, calls `GlobalRegistry.RegisterUnderwaterVisualsRuntime(this)`, validates references, registers render dispatcher, then tick managers.
- Lines 1594-1625: `Start` retries dependencies, dispatcher, owner setup, and tick registration. It does not retry the runtime service publication.
- Lines 2534-2592: `ApplySunVisualState` hides the assigned sun visual when `_cachedAtmoManager != null`.
- Lines 2603-2612: runtime skybox ownership sets `skyMaterial` through `AtmosphereDirector.SetSkybox`.
- Lines 5571-5595: fallback sun visual resolution only looks for `Sun_Body` under `sunLight.transform`.
- Lines 7136-7144: unresolved sun visual warning still exists.
- Lines 7807-7823 and 7845-7850: tick and renderable registration depend on registry/dispatcher availability.

CelestialEngine:

- `Assets/_Project/Scripts/HectonCelestialEngine.cs`
- Line 486: `[DefaultExecutionOrder(-3000)]`, later than UnderwaterVisuals.
- Line 823: serialized `sunVisualTransform`.
- Lines 1407-1529: `OnEnable` registers `GlobalRegistry.RegisterCelestialEngineRuntime(this)` before `ValidateReferences()`, then registers listeners and tick callbacks.
- Lines 1531-1535: `Start` retries tick and late-frame registration only, not the runtime service publication.
- Lines 4954-4972: continuous `HomeostasisBrain.GlobalQualityWeight` affects snapshot cadence. This is correct shape; no binary quality switch found in this path.
- Lines 6221-6305: `ApplySunOcclusion` sets `skyOwnsPrimarySunDisc = _atmosphereManager != null`. When sky owns the primary route, it does not toggle `sunVisualTransform.gameObject`; MaterialPropertyBlock updates only occur if the transform exists and is active.
- Lines 6321-6344: `RestoreSunDefaults` hides the sun visual when `_atmosphereManager != null`.
- Lines 6532-6610: publishes sky rotation, occluders, and ocean celestial projection globals.
- Lines 7089-7180: writes a 300-frame celestial black-box buffer and dumps on non-finite state. Static source satisfies the doctrine shape; runtime dump path was not exercised in this audit.

GlobalRegistry phase risk:

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- Lines 3519-3522: `RegisterUnderwaterVisualsRuntime`.
- Lines 3607-3610: `RegisterCelestialEngineRuntime`.
- Lines 3931-3935: `RegisterAtmosphereRuntime`.
- Lines 7099-7112: ready-locked registry rejects late service publication without a valid force override token and throws `CriticalBootException` in development/editor.

AtmosphereManager:

- `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
- Line 563: `[DefaultExecutionOrder(-6000)]`
- Line 564: `[ExecuteAlways]`
- Lines 954-966: play-mode `OnEnable` registers service, hot-swap listener, tick managers, and event cache.
- Lines 1149-1162: `TryRegisterService` registers `GlobalRegistry.RegisterAtmosphereRuntime(this)` or destroys duplicate service instances.

## Exact Owner Decision Required

The Unity owner must make one explicit product decision. Current scene/source state supports two competing interpretations and proves neither.

Required decision:

1. Primary sun disc is sky-material-owned.
   - `HectonCelestialEngine.sunVisualTransform` may remain null only if source/docs/proof explicitly state that `Hecton_AlienSky_Master` plus atmosphere/celestial globals own the visible sun disc.
   - `SURFACE_LOW_SUN_DISC_1428` must be treated as stale/dev/auxiliary and excluded from acceptance expectations, or removed later under scoped proof.
   - UnderwaterVisuals must not be a separate sun-disc truth owner. It may only feed or preview the chosen sky route.
   - Clean proof must show the shader route drawing a bright, readable, premium sun/Aegir surface without relying on inactive mesh objects.

2. Primary or auxiliary sun disc is scene-mesh-owned.
   - Assign `HectonCelestialEngine.sunVisualTransform` to scene fileID `1985271341`.
   - Activate `SURFACE_LOW_SUN_DISC_1428` and enable its MeshRenderer, or add an explicit first visual-sync activation policy.
   - Change the atmosphere-present path so CelestialEngine and UnderwaterVisuals do not immediately hide the visual that acceptance expects to see.
   - Replace or upgrade the flat/untextured `MAT_SurfaceSunDisc_1428.mat` if it is intended as visible premium presentation.

Rejected middle state:

- UnderwaterVisuals has `sunVisualTransform`.
- CelestialEngine has `sunVisualTransform: {fileID: 0}`.
- The scene sun disc is inactive/renderer disabled.
- Source hides scene sun visuals when atmosphere exists.
- Acceptance still expects a mesh sun visual.

That state is owner-ambiguous and cannot produce clean proof.

## Clean Proof Requirements

Minimum acceptance packet after owner fix:

- Static YAML evidence:
  - Exactly one active UnderwaterVisuals runtime owner.
  - Exactly one active CelestialEngine/Atmosphere owner route.
  - Chosen primary sun owner recorded in scene or source.
  - If mesh route is chosen: `HectonCelestialEngine.sunVisualTransform` assigned to `1985271341`, GameObject active, MeshRenderer enabled, no atmosphere-path hide contradiction.
  - If shader route is chosen: `HectonCelestialEngine.sunVisualTransform` null state is documented and no proof checklist expects `SURFACE_LOW_SUN_DISC_1428`.
- Material evidence:
  - Live sky material GUID/path.
  - Live Aegir renderer material GUID/path.
  - Active sun-disc material only if mesh route is chosen.
  - No default surface route using event-only noir sky material.
- Clean log evidence from the same capture run:
  - No `sunVisualTransform still unresolved`.
  - No `Ready-locked registry rejected registration`.
  - No duplicate Atmosphere/Celestial/UnderwaterVisuals service publication.
  - No WeatherEvents leak reported by the Batch25 gate.
  - No shader/import/material errors.
- Visual packet:
  - Surface/coast/Aegir.
  - Shoreline close.
  - Underwater 0-5 m.
  - Underwater 20-50 m.
  - Aegir/celestial long.
  - Low-oblique regression.
  - Manifest tying screenshot timestamp, scene hash, material GUIDs, and log file.
- Runtime/profiler proof if render ownership changes:
  - Frame Debugger or equivalent pass evidence that the chosen sun/Aegir path is actually drawn.
  - No same-frame tiny job schedule/readback loop introduced.
  - No >0.1 ms suspicious hot path without profiler justification.

Visual pass criteria:

- Aegir must be large, textured, atmospheric, readable, and stable.
- Surface/coast sky must be bright/readable/premium outside intentional storm/eclipse windows.
- Darkness, fog, or noir cannot hide weak surface or water art.
- Primitive disc, muddy sine stripes, pasted-sphere look, dirty green/black wash, or invisible celestial proof remains REJECTED.

## Registration Phase Risk

Current source publishes core runtime services from `OnEnable`. `GlobalRegistry` can reject late publication after ready-lock. `HectonUnderwaterVisuals.Start` and `HectonCelestialEngine.Start` retry tick/dispatcher work but do not retry their runtime service publication.

Owner action required:

- Ensure these scene services publish before registry ready-lock, or route them through an explicit, documented scene-publication gate.
- Clean proof must include a log showing no ready-lock rejection for `HectonUnderwaterVisuals`, `HectonCelestialEngine`, or `HectonAtmosphereManager`.

## Low / Mid / High / Ultra Consequences

Low:

- Must still show readable bright surface sky, visible Aegir silhouette/detail, and non-placeholder sun route.
- Use cheapest accepted approximation: shader sun disc, coarse cloud layers, stable Aegir impostor, no expensive optional star twinkle/LUT path.
- No dark/noir default surface fallback.

Mid:

- Baseline product route.
- Live `Mat_HectonSky.mat` plus `MAT_AegirGasGiant_Impostor_1428.mat` must show convincing sky, clouds, Aegir texture, and sun readability in the six-view packet.
- Keep render ownership simple and deterministic.

High:

- Spend budget on richer Aegir halo, better cloud depth, reflections, ring-shadow/caustic projection, and cleaner horizon integration.
- Do not add another truth owner. High tier is fidelity, not a new route.

Ultra:

- Visual overkill only: deeper Aegir atmospheric layers, higher resolution firmament/scattering assets if enabled, denser celestial proof, and stronger surface/coast composition.
- GlobalQualityWeight may scale fidelity, cadence, and optional telemetry. It must not change gameplay truth ownership, DTO layout, save identity, or authority route.

## Final Blockers

1. `HectonCelestialEngine.sunVisualTransform` is null while the scene contains an inactive candidate sun disc.
2. Atmosphere-present source paths hide or ignore scene sun visuals, implying shader-owned sun, but that decision is not made explicit.
3. `SURFACE_LOW_SUN_DISC_1428` is inactive and renderer-disabled; its material is flat/untextured.
4. Live Aegir material is identifiable, but the last known packet, 1474, remains rejected for weak dirty Aegir/celestial artifacts.
5. Runtime service registration has ready-lock risk until clean log proof shows the owner publication route is valid.

Required next action: Unity owner chooses sky-material sun ownership or scene-mesh sun ownership, edits the scene/source consistently, then produces a clean 1475-class proof packet. Static YAML alone is not acceptance.
