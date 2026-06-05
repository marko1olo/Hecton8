# 2201 Foam/Caustics/Underwater Activation Matrix

## Scope And Boundary
Static audit only. Evidence came from files, YAML, materials, renderer assets, Batch21/Batch22 reports, screenshots listed by the task, and existing Unity editor logs. Unity, Play Mode, imports, builds, and new captures were not run. This report cannot claim visual acceptance.

## Static Route Matrix
| Route name | File path | Owner type | Scene/prefab/material reference | Active/inactive evidence | Proof boundary |
|---|---|---|---|---|---|
| Crest ocean foam/caustic material | `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat` | Material/shader route | Ocean material reference in Crest prefab override; material has `_Foam: 1`, `_FoamTexture`, `_Caustics: 1`, `_CausticsTexture` | Material parameters are populated; visibility still depends on active ocean renderer, Crest data, camera path, depth, lighting | STATIC VERIFIED material setup; PENDING FRAME DEBUGGER/runtime capture |
| Crest foam simulation | `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | Crest runtime/system route | `H8_CREST_FOAM_INPUT_PASS_1464`; Crest ocean override `_createFoamSim: 1` | `Crest.RegisterFoamInput` enabled, `_disableRenderer: 1`, MeshRenderer disabled; disabled renderer is expected for sim input, not visible mesh output | STATIC VERIFIED serialized input; PENDING Crest debug/runtime foam output proof |
| Authored shoreline/surface foam ribbons | `Assets/_Project/Art/Materials/MAT_H8_SurfaceFoamRibbons_1428.mat`, scene meshes | Authored mesh/decal fake | `SURFACE_FOAM_RIBBON_1428_2`, material `MAT_H8_SurfaceFoamRibbons_1428` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Offshore foam break | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_PhoticShoreFoamOrganic_1428.mat` | Authored mesh/decal fake | `H8_OFFSHORE_FOAM_BREAK_1428_0` | GameObject active, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled renderer |
| Visible wave foam | `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | Authored mesh/decal fake | `H8_VisibleWaveFoam_1438` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Surface foam lace/blob | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_SurfaceFoamBlob_1447.mat` | Authored mesh/decal fake | `H8_SurfaceFoamLace_1453` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Visible unlit foam | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_VisibleFoamUnlit_1436.mat` | Authored mesh/decal fake | `H8_VisibleFoamUnlit_1436` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Broken/readable foam legacy | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_SurfaceFoamRing_1432.mat` | Authored mesh/decal fake | `H8_VisibleBrokenFoam_1435` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Deferred caustics pass | `Assets/_Project/Scripts/Rendering/AbyssalCaustics/HectonDeferredCausticsFeature.cs` | URP renderer feature | `PC_Renderer.asset`, `PC_High_Renderer.asset`, `Mobile_Renderer.asset`, `Quest_VR_Renderer.asset` | Feature `m_Active: 1` in all four renderer assets | STATIC VERIFIED feature active; PENDING runtime owner/buffer proof |
| Deferred caustics runtime publisher | `Assets/_Project/Scripts/Rendering/AbyssalCaustics/AbyssalDeferredCausticsRuntime.cs` | Runtime owner/publisher | Script GUID `4cbb8fb5b0c14e57aa7d232232ca0004` | No GUID hits in searched scene/prefab/data assets | STATIC VERIFIED not serialized in searched assets; hidden bootstrap not proven |
| Legacy caustics projector | `Assets/_Project/Scripts/Visor/CausticsProjectorManager.cs` | Legacy shim | Code route only | `Awake`/`OnEnable` disable component | STATIC VERIFIED disabled by code |
| Analytical caustics service | `Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs` | Legacy shim | Code route only | `EnsureRuntimeInstance()` returns null; `Awake` disables component | STATIC VERIFIED disabled by code |
| Floor caustic soft mesh | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_FloorCausticSoft_1443.mat` | Authored floor mesh fake | `H8_FloorCausticSoft_1443` | `m_IsActive: 1`, MeshRenderer enabled, material assigned | STATIC VERIFIED active mesh; PENDING camera visibility and strength proof |
| Floor caustic patches | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_FloorCausticVertex_1438.mat` | Authored floor mesh fake | `H8_FloorCausticPatches_1438` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Photic terrain caustics | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_FloorCausticSoft_1443.mat`, terrain/flora shaders | Material/procedural fake | `H8_PhoticTerrainCaustics_1453` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0`; shader caustics also depend on material masks/vertex alpha | STATIC VERIFIED mesh disabled; PENDING material receiver proof |
| Water caustic ribs | `Assets/_Project/Art/Materials/World/MAT_WorldThinServiceSignal_1428.mat` | Authored mesh/service marker | `WATER_CAUSTIC_RIB_3`, `WATER_CAUSTIC_RIB_1428_10` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Crest underwater renderer | `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | Crest camera underwater pass | `Crest.UnderwaterRenderer` on camera object | `m_Enabled: 1`, `_depthFogDensityFactor: 0.92`, `_copyOceanMaterialParamsEachFrame: 1` | STATIC VERIFIED serialized/enabled; PENDING capture/post-stack proof |
| Atmosphere underwater profile | `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | Atmosphere/profile route | `HectonAtmosphereManager` profile refs | `_profileUnderwater` assigned, `_waterSurfaceY: 14.02`, `_useAutoUnderwaterDetection: 0`, abyss absorption params assigned | STATIC VERIFIED profile data present; PENDING runtime state/camera depth proof |
| WaterOptics runtime | `Assets/_Project/Scripts/Rendering/WaterOptics/WaterOpticsRuntime.cs` | Runtime owner/constant buffer | Script GUID `26500000000000000000000000000004` | No GUID hits in searched scene/prefab/data assets | STATIC VERIFIED not serialized in searched assets; hidden bootstrap not proven |
| WaterOptics telemetry feature | `Assets/_Project/Scripts/Rendering/WaterOptics/HectonWaterOpticsTelemetryFeature.cs` | Renderer/telemetry feature | Script GUID `26500000000000000000000000000005` | No GUID hits in searched renderer scene/prefab/data assets | STATIC VERIFIED not serialized in searched assets |
| Underwater horizon haze | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_UnderwaterHorizonHaze_1437.mat` | Authored mesh fake | `H8_UnderwaterHorizonHaze_1437` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Underwater haze curtain | `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_UnderwaterHazeCurtain_1454.mat` | Authored mesh fake | `H8_UnderwaterHazeCurtain_1454` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Underwater surface sheet | `Assets/_Project/Art/Materials/World/Photic1453/MAT_H8_UnderwaterSurfaceSheet_1455.mat` | Authored mesh fake | `H8_UnderwaterSurfaceSheet_1455` | `m_IsActive: 0`, MeshRenderer `m_Enabled: 0` | STATIC VERIFIED disabled |
| Underwater suspended specks | `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_UnderwaterSpecks_1446.mat` | Authored particulate mesh fake | `H8_UnderwaterSuspendedSpecks_1446` | GameObject `m_IsActive: 0`, MeshRenderer enabled but suppressed by inactive GameObject | STATIC VERIFIED disabled |
| Suspended particulate field | `Assets/_Project/Scenes/02_HECTON_WORLD.unity` | Authored particulate mesh fakes | `SUSPENDED_PARTICULATE_1428_39` and related objects | Example object active but MeshRenderer `m_Enabled: 0` | STATIC VERIFIED renderer disabled in sampled object |
| Marine snow renderer | `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs` | GPU runtime VFX owner | Script GUID `117377a1299b43878297cc0f26000481` | No GUID hits in searched scene/prefab/data assets | STATIC VERIFIED not serialized in searched assets |
| Jacobian foam runtime | `Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs` | GPU runtime foam owner | Script GUID `2e0bd2c7a9684b68b5bea776ea7775a0` | No GUID hits in searched scene/prefab/data assets | STATIC VERIFIED not serialized in searched assets |
| Jacobian foam render feature | `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs` | Renderer feature | Script GUID `fc79c581ebb44ecfa8fc6f97b39e8666` | No GUID hits in renderer scene/prefab/data assets | STATIC VERIFIED not serialized in searched renderer assets |

## Material Property Findings
- `MAT_H8_SurfaceCrestOcean_1428.mat` uses `_Foam`, `_FoamTexture`, `_FoamScale`, `_WaveFoamCoverage`, `_WaveFoamStrength`, `_Caustics`, `_CausticsTexture`, `_CausticsStrength`, `_CausticsTextureScale`, and `_Underwater`.
- Scene-authored foam materials include several routes whose current YAML owners are disabled; material correctness cannot overcome `m_IsActive: 0` or `m_Enabled: 0`.
- Batch21 static validation flagged empty or unresolved default texture slots on several related materials (`_MainTex`, `_BaseMap`). Treat this as risk only; current runtime import status was not revalidated.
- Caustic shader/material routes include procedural properties such as `_CausticScale`, `_CausticSpeed`, `_CausticStrength`, `_CausticColor`; several require receiver masks or vertex alpha, so active shader code does not prove visible caustics.

## Likely Root Causes For 1472 Rejection
- No visible foam: most authored foam meshes are disabled by GameObject or MeshRenderer. Crest foam simulation is partially serialized, but visible Crest foam output is not proven by static files. The active ocean material has foam fields populated, so the first suspect is scene/camera/Crest sim output or inactive authored visible foam, not absence of all foam assets.
- No visible caustics: renderer features are active, but the required `AbyssalDeferredCausticsRuntime` publisher is not serialized in searched assets, and legacy projector/analytical routes are disabled by code. Only one floor caustic mesh is active; it may be outside capture geometry, too weak, or occluded/masked.
- Underwater too transparent/empty: Crest underwater is enabled, but custom WaterOptics runtime, marine snow renderer, haze curtain, horizon haze, underwater sheet, and speck mesh routes are absent from searched serialization or disabled. Crest fog alone is not enough evidence for dense premium underwater particulate/haze.
- Photic-shallow believability failure: the route stack is fragmented. The scene has many handcrafted water/foam/caustic/haze assets, but most visible helpers are disabled while runtime publishers for the stronger routes are not statically present.

## Crest Vs Custom Underwater Route
- Crest underwater rendering is present and enabled in the proof scene.
- Custom atmosphere underwater profile data is present through `HectonAtmosphereManager`.
- `WaterOpticsRuntime` and `HectonUnderwaterVisuals` were not found as serialized script GUIDs in searched scene/prefab/data assets.
- Result: static proof supports Crest underwater plus atmosphere profile; it does not support an active custom WaterOptics/HectonUnderwaterVisuals proof route.

## Safe Activation Sequence For Unity Owner
1. Capture exact 1472 baseline from the rejected camera paths before changing anything. Store proof under `Docs/Screenshots/MCP` or `Docs/Reports/Batch22`, not `Assets/Screenshots`.
2. Surface foam: enable one nearest visible authored foam route only (`H8_VisibleWaveFoam_1438`, `H8_SurfaceFoamLace_1453`, or the shoreline/offshore mesh relevant to the camera). Capture shoreline. Rollback: restore original `m_IsActive`/`m_Enabled` values.
3. Crest foam: inspect Crest foam debug/Frame Debugger for foam sim output and material foam sampling on the ocean. Rollback: restore Crest debug/settings only; do not rewrite ocean route.
4. Caustics deferred route: add or enable the existing `AbyssalDeferredCausticsRuntime` owner only if the renderer feature material and runtime registry ownership are valid. Capture floor and underwater routes. Rollback: remove the added owner or restore its disabled state.
5. Caustics authored mesh route: test `H8_FloorCausticSoft_1443` visibility from the exact underwater camera, then enable only `H8_FloorCausticPatches_1438` if the active mesh is outside view. Rollback: restore object state/material alpha.
6. Underwater haze: verify Crest underwater pass executes; then enable one authored haze surface/curtain route if camera geometry requires local volume cues. Rollback: restore object state and material opacity.
7. Marine snow/particulate: prefer the GPU `HectonMarineSnowRenderer` route with bound camera/compute/material if available; otherwise test one authored speck field near the camera. Rollback: remove owner or restore renderer/object states.
8. After every step: one screenshot, Frame Debugger where relevant, profiler sample where runtime work is introduced. Stop on the first route that breaks readability or hides terrain with darkness.

## Tier Consequences
| Tier | Foam | Caustics | Haze/water optics | Marine snow/particulate |
|---|---|---|---|---|
| Minimum | Visible shoreline/ocean foam identity through cheapest authored masks/decals; no flat blank water | Sparse procedural/floor caustic cues on visible receivers | Lightweight fog/LUT, bright readable photic water | Sparse bounded specks, no CPU readback |
| Low | More shoreline coverage and wake/lace variation, low texture/cadence | Low-frequency projected or mesh caustics, subtle but visible | Crest underwater plus minimal local haze | Low-count GPU or static field near camera |
| Middle | Crest foam plus authored highlights, stable material response | Deferred or projected caustics with receiver proof | WaterOptics constant buffer route if owner/profiler passes | Bounded GPU marine snow with camera bindings |
| High | Richer breakup, normals, and foam layering | Stronger screen/floor caustic composition | Denser haze, shafts, color extinction, still bright in shallows | Denser marine snow and local depth response |
| Ultra | Visual-overkill foam layering and responsive wake detail | Multi-layer caustics/receiver/material response | Premium volumetric/presentation stack without gameplay truth change | High-density GPU particles, still bounded and profiled |

## Profiler And Proof Requirements
- Crest foam/underwater: Frame Debugger proof of Crest passes, material sampling, camera underwater route, and capture before/after.
- Deferred caustics: proof `HectonDeferredCausticsFeature` enqueues and `AbyssalDeferredCausticsRuntime.TryGetActiveConstantBuffer` succeeds; GPU timing required. Current status: PENDING PROFILER.
- WaterOptics: proof `VisualSyncTick` publishes `_GlobalWaterOptics`, 0 B GC, no hot scene search, and no hidden same-frame complete. Current status: PENDING PROFILER.
- Marine snow/Jacobian foam: proof dispatch/draw cost, buffer memory, no CPU particle readback, no fallback black texture. Current status: PENDING PROFILER.
- Authored meshes/materials: proof object is visible in exact camera path and does not rely on darkness/fog to hide weak terrain.

## Rejection Gates For Proposed Fixes
- Do not write screenshots under `Assets/Screenshots`.
- Do not darken surface/photic water to hide missing foam, terrain, or caustics.
- Do not treat active renderer feature as runtime acceptance.
- Do not add hot allocations, hot `GlobalRegistry` polling, scene searches, or tiny jobs.
- Do not collapse tiering into binary low/high; all routes must scale Minimum/Low/Middle/High/Ultra.

## Files/Areas Inspected
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Assets/_Project/Data/*Renderer.asset`
- `Assets/_Project/Art/Materials/**`
- `Assets/_Project/Art/Shaders/**`
- `Assets/_Project/Scripts/**Foam**`, `**Caustic**`, `**Underwater**`, `**MarineSnow**`, `**WaterOptics**`, `**Crest**`, `Scripts/VFX/**`
- `Docs/Reports/Batch21/**`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart*.log`
