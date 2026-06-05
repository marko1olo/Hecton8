# Batch25 Agent 2503 - Underwater Visuals Owner Phase Audit

Status: STATIC VERIFIED / PENDING UNITY OWNER FIX  
Date: 2026-06-04 19:20 +04  
Scope: `HectonUnderwaterVisuals` ownership, serialized scene references, and `GlobalRegistry` registration phase.  
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_YAML`. No Unity, no build, no runtime proof.

## Authority Read

- `AGENTS.md`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `water.md`
- `rendering.md`
- `systems.md`
- `quality.md`
- `Docs/Orchestration/UNITY_OWNER_STEER_20260604_1905_UNDERWATER_VISUALS_UNASSIGNED.md`

## Static Findings

### 1. `HectonUnderwaterVisuals` scene ownership

Claim: `02_HECTON_WORLD.unity` contains exactly one serialized `HectonUnderwaterVisuals` instance.  
Evidence class: `STATIC_YAML`  
Artifact: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`  
Command: `Select-String` for script guid `7b8d6f3311640f64ba03f2b62d8a00cd`  
Date: 2026-06-04  
Residual risk: Unity import/Prefab override state not executed.

Exact instance:

- MonoBehaviour fileID: `101536743`
- GameObject fileID: `101536742`
- GameObject name: `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`
- Script guid: `7b8d6f3311640f64ba03f2b62d8a00cd`
- Scene block: `Assets/_Project/Scenes/02_HECTON_WORLD.unity:4614`

Serialized required references in that block:

- `playerCamera: {fileID: 1505808849}` -> `Main Camera` transform.
- `sunLight: {fileID: 532047523}` -> `H8_WORLD_BLUE_SHAFT_KEY_1428` light.
- `sunVisualTransform: {fileID: 532047522}` -> `H8_WORLD_BLUE_SHAFT_KEY_1428` transform.
- `mainCamera: {fileID: 1505808848}` -> `Main Camera`.
- `atmosphereManager: {fileID: 1893406171}` -> `HectonAtmosphereManager` on `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428`.
- `oceanUnderwaterMaterial: {fileID: 2100000, guid: ef94c26e44a36e24a9dcbc5995a2bed1, type: 2}` -> `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`.
- `skyMaterial: {fileID: 2100000, guid: c94a1beef2372b8458941c2ed9d05d5e, type: 2}` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat`.
- `biomePalette: {fileID: 11400000, guid: 1ed7cad7d9660ec4898f244d19b99da4, type: 2}` -> `Assets/_Project/Data/biom/Main_Ocean_Palette.asset`.
- `biomeMatrixDirector: {fileID: 1075616976}` -> `BiomeMatrixDirector` on `[MANAGERS]`.

Result: the static YAML no longer matches the 19:05 unassigned `HectonUnderwaterVisuals` warnings for `biomePalette`, `oceanUnderwaterMaterial`, or `skyMaterial`. Runtime cleanliness is still `PENDING UNITY VERIFICATION`.

### 2. Related celestial unresolved `sunVisualTransform`

Claim: `02_HECTON_WORLD.unity` contains one serialized `HectonCelestialEngine` instance, and its `sunVisualTransform` is unresolved.  
Evidence class: `STATIC_YAML`  
Artifact: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`  
Command: `Select-String` for script guid `86667f9831733ab48aaa2bb3a38047ee` and `sunVisualTransform:`  
Date: 2026-06-04  
Residual risk: Unity Inspector may show prefab override context not proven by static text.

Exact instance:

- MonoBehaviour fileID: `1893406170`
- GameObject fileID: `1893406169`
- GameObject name: `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428`
- Script guid: `86667f9831733ab48aaa2bb3a38047ee`
- Scene block: `Assets/_Project/Scenes/02_HECTON_WORLD.unity:90877`

Relevant fields:

- `sunLight: {fileID: 1772751213}` -> `H8_SURFACE_SUN_KEY_1428`.
- `aegirTransform: {fileID: 1873583736}` -> stripped prefab transform.
- `_atmosphereManager: {fileID: 1893406171}`.
- `_skyMaterial: {fileID: 2100000, guid: c94a1beef2372b8458941c2ed9d05d5e, type: 2}`.
- `sunVisualTransform: {fileID: 0}` at `Assets/_Project/Scenes/02_HECTON_WORLD.unity:91163`.

Candidate visible sun-disc object found in the same scene:

- GameObject fileID: `1985271340`
- GameObject name: `SURFACE_LOW_SUN_DISC_1428`
- Transform fileID: `1985271341`
- Scene lines: `95878-95910`
- Current static state: `m_IsActive: 0`.
- MeshRenderer fileID: `1985271342`
- Current static state: `m_Enabled: 0`.

Risk: `HectonCelestialEngine.ApplySunOcclusion()` only toggles `sunVisualTransform.gameObject` when `_atmosphereManager == null`. In the current scene `_atmosphereManager` is assigned, so assigning the inactive `SURFACE_LOW_SUN_DISC_1428` transform without also fixing active/renderer policy may still produce no visible sun-disc behavior.

### 3. Registration timing

Claim: `HectonUnderwaterVisuals` registers itself during `OnEnable`, before reference validation and before tick/render dispatcher registration.  
Evidence class: `STATIC_SOURCE`  
Artifact: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:1025`  
Command: targeted line extraction around `OnEnable`, `Start`, `OnDisable`, and tick registration methods.  
Date: 2026-06-04  
Residual risk: runtime execution order not observed.

Relevant source route:

- `OnEnable()` sets `_runtimeVisualCallbacksActive = Application.isPlaying`.
- If playing, it sets `ActiveRuntimeInstance = this`.
- It calls `GlobalRegistry.RegisterUnderwaterVisualsRuntime(this)` at `HectonUnderwaterVisuals.cs:1041`.
- Reference validation happens later in the same method at `HectonUnderwaterVisuals.cs:1058`.
- Tick registration happens later through `TryRegisterTickManagers()` at `HectonUnderwaterVisuals.cs:1084`, with implementation at `HectonUnderwaterVisuals.cs:7776`.
- `Start()` retries dependency caching, render registration, owner setup, and tick registration, but it does not retry `RegisterUnderwaterVisualsRuntime`.
- `OnDisable()` and `OnDestroy()` unregister when `GlobalRegistry.UnderwaterVisuals == this`.

### 4. Ready-lock risk

Claim: registration can occur after `GlobalRegistry.LockReady()` and be rejected unless a scene runtime publication gate is open.  
Evidence class: `STATIC_SOURCE`  
Artifacts:

- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:2497`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:2507`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:3519`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7042`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:7099`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:2180`

Command: targeted line extraction around `LockReady`, scene publication gate, and `RegisterServiceAllowSameInstance`.  
Date: 2026-06-04  
Residual risk: actual scene activation ordering not observed.

Route:

- `GameBootstrapper` calls `GlobalRegistry.LockReady()` after `InitializeSceneActivatePhaseAsync` succeeds.
- `RegisterUnderwaterVisualsRuntime()` calls `RegisterServiceAllowSameInstance(ref _underwaterVisualsRuntime, instance)`.
- That reaches `RegisterService`, resolves slot `UnderwaterVisualsRuntime`, and calls `GuardServicePublication`.
- `GuardServicePublication` throws `CriticalBootException` and logs `[GlobalRegistry] Ready-locked registry rejected registration: HectonUnderwaterVisuals` when `Phase == Ready` and no valid override token exists.
- A valid override token can be issued only while `BeginSceneRuntimePublicationGate()` depth is open and the service slot is allowed by `IsSceneRuntimeHotSwapSlot`.
- `UnderwaterVisualsRuntime` is not in the hard forbidden list inside `IsSceneRuntimeHotSwapSlot`, so the slot is eligible for gated scene runtime publication.

Conclusion: the clean route is not runtime scene search or hot registration polling. The owner must ensure the scene-owned `HectonUnderwaterVisuals` instance is enabled/published before ready-lock or inside the explicit scene runtime publication gate.

## Unity Owner Checklist

1. In `02_HECTON_WORLD`, keep exactly one `HectonUnderwaterVisuals` component: `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` / MonoBehaviour `101536743`.
2. Confirm the component is active during the scene activation window that precedes `GlobalRegistry.LockReady()` or is enabled only inside `GlobalRegistry.BeginSceneRuntimePublicationGate()` / `EndSceneRuntimePublicationGate()`.
3. Do not add runtime scene search, `FindObjectOfType`, or hot fallback registration to repair this. Fix scene ownership and bootstrap phase.
4. Keep these serialized `HectonUnderwaterVisuals` assignments:
   - `biomePalette` -> `Assets/_Project/Data/biom/Main_Ocean_Palette.asset` / guid `1ed7cad7d9660ec4898f244d19b99da4`.
   - `oceanUnderwaterMaterial` -> `Assets/Crest/Crest/Materials/Ocean-Underwater.mat` / guid `ef94c26e44a36e24a9dcbc5995a2bed1`.
   - `skyMaterial` -> `Assets/_Project/Art/Materials/Mat_HectonSky.mat` / guid `c94a1beef2372b8458941c2ed9d05d5e`.
5. Fix `HectonCelestialEngine.sunVisualTransform` on `H8_ATMOSPHERE_CELESTIAL_OWNERS_1428` / MonoBehaviour `1893406170`.
6. If `SURFACE_LOW_SUN_DISC_1428` is the intended sun visual, assign transform fileID `1985271341`, set its GameObject active, and ensure its MeshRenderer is enabled or otherwise intentionally controlled. Current static YAML has both inactive/disabled.
7. If the sky material fully owns the primary sun disc, document that ownership and remove the stale expectation from the proof route. Do not leave a silent `{fileID: 0}` while the owner prompt expects a transform route.
8. Enter Play Mode and capture a clean Unity Console/log tail newer than the fix.
9. Required clean log proof:
   - No `[HectonUnderwaterVisuals] biomePalette not assigned.`
   - No `[HectonUnderwaterVisuals] oceanUnderwaterMaterial not assigned.`
   - No `[HectonUnderwaterVisuals] skyMaterial not assigned.`
   - No `[HectonUnderwaterVisuals] sunVisualTransform still unresolved after runtime retry.`
   - No `[GlobalRegistry] Ready-locked registry rejected registration: HectonUnderwaterVisuals`.
   - No `HectonCelestialEngine` missing-sun-disc warning if the owner adds one for proof.
10. Only after clean log proof should the Batch24 slab/caustic isolation route continue.

## Static YAML Validation Checklist

Run static checks against `Assets/_Project/Scenes/02_HECTON_WORLD.unity` after the Unity owner fix.

Required checks:

- `guid: 7b8d6f3311640f64ba03f2b62d8a00cd` appears exactly once in the scene.
- `guid: 86667f9831733ab48aaa2bb3a38047ee` appears exactly once in the scene.
- `--- !u!114 &101536743` still has `m_GameObject: {fileID: 101536742}`.
- GameObject `&101536742` remains named `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474`.
- In block `&101536743`, `oceanUnderwaterMaterial` equals guid `ef94c26e44a36e24a9dcbc5995a2bed1`.
- In block `&101536743`, `skyMaterial` equals guid `c94a1beef2372b8458941c2ed9d05d5e`.
- In block `&101536743`, `biomePalette` equals guid `1ed7cad7d9660ec4898f244d19b99da4`.
- In block `&101536743`, `sunVisualTransform` is not `{fileID: 0}`.
- In block `&1893406170`, `sunVisualTransform` is not `{fileID: 0}`.
- If using `SURFACE_LOW_SUN_DISC_1428`, block `&1893406170` should contain `sunVisualTransform: {fileID: 1985271341}`.
- If using `SURFACE_LOW_SUN_DISC_1428`, GameObject `&1985271340` should have `m_IsActive: 1` unless a documented sky-disc owner route intentionally controls it elsewhere.
- If using `SURFACE_LOW_SUN_DISC_1428`, MeshRenderer `&1985271342` should have `m_Enabled: 1` unless a documented material/renderer activation route enables it before first celestial visual sync.
- No `HectonUnderwaterVisuals` required material/palette field may be `{fileID: 0}`.

Suggested static commands:

```powershell
Select-String -LiteralPath 'Assets/_Project/Scenes/02_HECTON_WORLD.unity' -Pattern '7b8d6f3311640f64ba03f2b62d8a00cd','86667f9831733ab48aaa2bb3a38047ee','oceanUnderwaterMaterial:','skyMaterial:','biomePalette:','sunVisualTransform:'
```

```powershell
Select-String -LiteralPath 'Assets/_Project/Scenes/02_HECTON_WORLD.unity' -Pattern 'H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474','H8_ATMOSPHERE_CELESTIAL_OWNERS_1428','SURFACE_LOW_SUN_DISC_1428'
```

## Proof Boundary

This audit proves only static source/YAML state. It does not prove Unity import health, Play Mode behavior, console cleanliness, render quality, profiler cost, GC behavior, or screenshot acceptance.

First-20-minutes route relevance: removes a runtime visual proof blocker for surface/photic-shallow readability and celestial-water presentation. Acceptance remains blocked until the Unity owner produces current clean log proof and capture proof.
