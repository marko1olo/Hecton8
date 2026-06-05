# Batch29 Worker 2901 - Route-Aware Sun Warning Patch Plan

Status: STATIC PATCH PLAN / RUNTIME PROOF NOT RUN
Scope: `HectonUnderwaterVisuals` route-aware sun-visual validation plan only.

## Task Boundary

Write path:
- `Docs/Reports/Batch29/2901_ROUTE_AWARE_SUN_WARNING_PATCH_PLAN.md`

Forbidden work respected:
- No Assets/source edits.
- No Unity, Play Mode, build, process kill, or runtime capture.
- No activation of `SURFACE_LOW_SUN_DISC_1428`.
- No visual acceptance claim.

Evidence labels used:
- `STATIC_DOC`: authority docs and prior audit text.
- `STATIC_SOURCE`: source text and line regions only.
- `PENDING_RUNTIME`: required future Unity/runtime/profiler/console proof.

## Authority Inputs

- `STATIC_DOC`: `AGENTS.md`, `TASTE.md`, `celestial.md`, `atmosphere.md`, `water.md`, `rendering.md`, `shaders.md`.
- `STATIC_DOC`: `.agents-skills/ARCH_Execution_Phases.txt`, `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`, `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`.
- `STATIC_DOC`: `Docs/Reports/Batch28/2804_AEGIR_SKY_ROUTE_STATIC_OWNER_AUDIT.md`.
- `STATIC_SOURCE`: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`.
- `STATIC_SOURCE`: `Assets/_Project/Scripts/HectonCelestialEngine.cs`.
- `STATIC_SOURCE`: `Assets/_Project/Scripts/HectonAtmosphereManager.cs`.

## Static Verdict

Claim: The current primary sun route must remain `PrimarySunDiscOwner=SkyMaterial`.
Evidence Class: `STATIC_DOC`
Artifact: `Docs/Reports/Batch28/2804_AEGIR_SKY_ROUTE_STATIC_OWNER_AUDIT.md`
Command or Unity tool: PowerShell `Get-Content`, `rg`
Date: 2026-06-04
Residual risk: Runtime scene binding, material values, and clean console remain `PENDING_RUNTIME`.

Claim: The warning defect is in `HectonUnderwaterVisuals`, not in the sky shader route.
Evidence Class: `STATIC_SOURCE`
Artifact: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
Command or Unity tool: `rg` and line-scoped `Get-Content`
Date: 2026-06-04
Residual risk: Compile and runtime log behavior remain `PENDING_RUNTIME`.

Claim: `HectonAtmosphereManager.AtmosphereDirector` is a skybox assignment bridge only.
Evidence Class: `STATIC_SOURCE`
Artifact: `Assets/_Project/Scripts/HectonAtmosphereManager.cs:69-85`, `:571-575`
Command or Unity tool: line-scoped `Get-Content`
Date: 2026-06-04
Residual risk: No runtime bridge proof was produced in this task.

## Exact Patch Regions

### 1. `HectonUnderwaterVisuals.cs`

Modify region: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:2597-2641`

Add helper predicate near sky material ownership helpers:

```csharp
private bool RequiresMeshSunVisual()
```

Contract:
- Pure read-only predicate.
- No scene search.
- No mutation.
- No allocation.
- No signal publish.
- No `GlobalRegistry` lookup.
- Returns `false` when the sky material route owns the primary sun disc.
- Returns `true` only for fallback routes where the mesh sun visual is the declared sun-disc owner.

Proposed body contract:
- If `skyMaterial == null`, return `true` because no shader-sun route can be proven.
- If `_cachedAtmoManager != null` and `AtmosphereDirector.IsSkybox(skyMaterial)`, return `false`.
- If `_cachedAtmoManager != null` and `ReferenceEquals(RuntimeSkyMaterialReference, skyMaterial)`, return `false`.
- Optional narrow fallback: if `_cachedAtmoManager != null` and `AtmosphereDirector.Skybox != null && ReferenceEquals(AtmosphereDirector.Skybox, skyMaterial)`, return `false`.
- Otherwise return `true`.

Do not use asset path/GUID APIs inside the predicate. Editor-only asset database checks would create compile guards and unnecessary route fragility.

### 2. `ApplySunVisualState`

Modify region: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:2533-2566`

Current risk:
- `sunVisualTransform == null` exits before route context.
- `_cachedAtmoManager != null` hides a mesh if present, but does not prove that mesh absence is acceptable.

Patch plan:
- Leave mesh state mutation only for actual mesh routes.
- Replace `_cachedAtmoManager != null` route test with `!RequiresMeshSunVisual()`.
- If `!RequiresMeshSunVisual()`, call `HideSunVisualAboveWater()` only when `sunVisualTransform != null`, then return.
- Keep fallback mesh hysteresis thresholds unchanged for `RequiresMeshSunVisual() == true`.

### 3. `RestoreSunVisual`

Modify region: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:2568-2581`

Current risk:
- `_cachedAtmoManager != null` is a loose route test.

Patch plan:
- Replace `_cachedAtmoManager != null` with `!RequiresMeshSunVisual()`.
- If the sky route owns the sun, hide assigned mesh if any and return.
- Only reactivate mesh sun for fallback mesh-sun routes.

### 4. `EnsureRuntimeVisualOwners`

Modify region: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:4966-5004`

Current risk:
- Lines `4979-4980` call `ResolveSunVisualTransform()` whenever `sunVisualTransform == null`.
- This can search for a mesh sun even when `PrimarySunDiscOwner=SkyMaterial`.

Patch plan:
- Change:
  - `if (sunVisualTransform == null) ResolveSunVisualTransform();`
- To:
  - `if (RequiresMeshSunVisual() && sunVisualTransform == null) ResolveSunVisualTransform();`

### 5. `RequestRuntimeVisualOwnerResolveIfMissing`

Modify region: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:5015-5038`

Current warning path to gate:
- Line `5025`: `sunVisualTransform == null ||`

Patch plan:
- Replace the missing condition with:
  - `(RequiresMeshSunVisual() && sunVisualTransform == null) ||`

Required result:
- Missing mesh-sun no longer requests repeated owner resolution when sky material owns the sun.
- Other missing runtime owners still request resolution unchanged.

### 6. `ResolveSunVisualTransform`

Modify region: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:5570-5594`

Current risk:
- It searches `sunLight.transform.Find("Sun_Body")` unconditionally after sun-light resolution.

Patch plan:
- Insert at method start after `EnsurePrimarySunReference();`:
  - `if (!RequiresMeshSunVisual()) return;`
- Keep `Find("Sun_Body")` only for fallback mesh-sun route.

Reason:
- Scene hierarchy search is cold, but still wrong route behavior when the shader owns the sun disc.

### 7. `ValidateReferences`

Modify region: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:7098-7116`

Current risk:
- Line `7105` calls `ResolveSunVisualTransform()` in Play Mode.

Patch plan:
- Change line `7105` to:
  - `if (RequiresMeshSunVisual()) ResolveSunVisualTransform();`

No new warning should be added here. Validation should not convert sky-route mesh absence into an error.

### 8. `WarnIfRuntimeReferencesStillMissing`

Modify region: `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:7118-7144`

Exact warning path to gate:
- Lines `7140-7143`:
  - `WarnIfRuntimeReferenceMissing(sunVisualTransform == null, RuntimeReferenceWarningSunVisual, "[HectonUnderwaterVisuals] sunVisualTransform still unresolved after runtime retry.");`

Patch plan:
- Replace missing expression with:
  - `RequiresMeshSunVisual() && sunVisualTransform == null`

Optional diagnostic message when sky route owns the sun:
- Do not log a warning.
- If a proof harness exists later, emit route proof through that harness, not recurring console noise.

Required future clean-log condition:
- No `[HectonUnderwaterVisuals] sunVisualTransform still unresolved after runtime retry.` while `PrimarySunDiscOwner=SkyMaterial` and `MeshSunVisualRequired=false`.

## Celestial Engine Metadata Plan

### 9. `HectonCelestialEngine.cs`

Modify region: `Assets/_Project/Scripts/HectonCelestialEngine.cs:5980-6033`

Add helper predicate near `IsMandatedSkyMaterial`:

```csharp
private bool SkyMaterialOwnsPrimarySunDisc()
```

Contract:
- Pure read-only predicate.
- No scene search.
- No mutation.
- No allocation.
- No signal publish.
- Returns `true` only when `_atmosphereManager != null`, `_skyMaterial != null`, and `IsMandatedSkyMaterial(_skyMaterial)` is true.
- Optional stricter route proof: also require `AtmosphereDirector.IsSkybox(_skyMaterial)` after `ForceMandatedSkyMaterialReference()` has run.

Modify region: `Assets/_Project/Scripts/HectonCelestialEngine.cs:6246-6307`

Patch plan:
- Replace local `bool skyOwnsPrimarySunDisc = _atmosphereManager != null;` with `bool skyOwnsPrimarySunDisc = SkyMaterialOwnsPrimarySunDisc();`
- Keep sun-light intensity branch using `_atmosphereManager != null`; that branch concerns intensity chain, not sun-disc visual ownership.
- Gate only mesh-disc activation through `skyOwnsPrimarySunDisc`.

Modify region: `Assets/_Project/Scripts/HectonCelestialEngine.cs:6328-6346`

Patch plan:
- Replace `_atmosphereManager != null` mesh hiding condition with `SkyMaterialOwnsPrimarySunDisc()`.
- Keep intensity restoration condition unchanged unless a separate route task proves it needs owner-specific change.

Modify region: `Assets/_Project/Scripts/HectonCelestialEngine.cs:2268-2281`, `:5959-5974`, `:6040-6067`, `:6750-6797`

No route behavior change required here for this patch. Future proof snapshot must read values written here after `VISUAL_SYNC`:
- `_SunDirection`
- `_AegirDirection`
- `_GameTime`
- `_NightBlend`
- `_StarIntensity`
- `_EclipseOcclusion`
- `_PenumbraFactor`
- `_AtmosphereTransmittanceWeight`
- `_AtmosphereInscatterWeight`
- sky colors
- Aegir MPB values including `_FresnelSunDir`, `_GlobalRotation`, `_PlanetPhase`, `_SunBacklitFactor`, and wind direction.

## Atmosphere Boundary

Do not modify `HectonAtmosphereManager` for sun-disc ownership.

Static source boundary:
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs:69-85`: `AtmosphereDirector` wraps `RenderSettings.skybox`.
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs:571-575`: render-settings bridge forwards skybox set.
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs:1841-1860`: atmosphere computes sun intensity/horizon fade.
- `Assets/_Project/Scripts/HectonAtmosphereManager.cs:1968-2027`: atmosphere publishes Aegir abyss light and `_AegirDirection` from cached celestial data.

Rule:
- Keep skybox assignment bridge here.
- Do not move primary sun-disc ownership, Aegir direction truth, celestial phase, or proof ownership into atmosphere.

## Future Proof Snapshot Fields

Required future snapshot fields:
- `PrimarySunDiscOwner=SkyMaterial`
- `SkyMaterialOwnsPrimarySunDisc=<bool>`
- `MeshSunVisualRequired=<bool>`
- `MeshSunVisualAssigned=<bool>`
- `MeshSunVisualActive=<bool/unknown>`
- `MeshSunRendererEnabled=<bool/unknown>`
- `SunVisualName=<string/hash or none>`
- `RenderSettingsSkyboxPath`
- `RenderSettingsSkyboxGuid`
- `SkyMaterialPath`
- `SkyMaterialGuid`
- `SkyShaderPath`
- `SkyShaderGuid`
- `AtmosphereDirectorSkyboxMatchesSkyMaterial=<bool>`
- `CachedAtmosphereManagerPresent=<bool>`
- `SunLightAssigned=<bool>`
- `SunLightIntensity`
- `SunDirection`
- `AegirDirection`
- Runtime sky material values: `_SunSize`, `_SunEdgeSoftness`, `_SunDiscColor`, `_SunScatterColor`, `_SunScatterIntensity`, `_AegirHaloIntensity`, `_AegirGlowIntensity`, `_AtmosphereTransmittanceWeight`, `_AtmosphereInscatterWeight`, `_NightBlend`, `_StarIntensity`, `_EclipseOcclusion`, `_PenumbraFactor`.
- Aegir renderer/material/shader path and GUID.
- Aegir texture residency for `_MainTex`, `_DetailTex`, `_StormTex`.
- Sky texture residency for `_HighCloudTex`, `_MainCloudAtlas`, `_MainCloudTex`, `_StarTex`, `_BakedStarCubemap`, `_StarTwinkleLUT`.
- `SURFACE_LOW_SUN_DISC_1428.active=false`
- `SURFACE_LOW_SUN_DISC_1428.rendererEnabled=false`
- `GlobalQualityWeight=<continuous float>`
- route/view id, scene path, loaded scenes, camera transform, FOV, capture source, screenshot checksum, clean console path.
- Explicit absence of false warning: `sunVisualTransform unresolved warning present=false`.

Snapshot contract:
- Read-only.
- No scene search in hot path.
- No managed allocations in runtime hot path.
- Capture harness can format strings outside hot path.

## Compile Risks

- `RequiresMeshSunVisual()` depends on `AtmosphereDirector`, already imported through `using Hecton8.Atmosphere;` in `HectonUnderwaterVisuals.cs`; compile risk is low but still `PENDING_RUNTIME/CLI_COMPILE`.
- Calling `RequiresMeshSunVisual()` inside `[Conditional]` warning method is legal, but any use of editor-only APIs inside that predicate would break player builds. Keep it runtime-safe.
- `HectonCelestialEngine.IsMandatedSkyMaterial` is currently private static; `SkyMaterialOwnsPrimarySunDisc()` must live inside the same class unless a public contract is deliberately added. Do not expand public API for this patch.
- Do not change `IAtmosphereRenderSettingsBridge` or other interfaces during this batch.
- Do not add route strings to unmanaged signal payloads.
- If a proof snapshot struct is added later, use explicit unmanaged field layout for native/binary/telemetry routes. Managed string paths belong in editor/capture formatting only.

## Runtime Proof Requirements

Required before acceptance:
- `CLI_COMPILE` or Unity compile artifact after source patch.
- `UNITY_CONSOLE`: clean log proving no `[HectonUnderwaterVisuals] sunVisualTransform still unresolved after runtime retry.` when `PrimarySunDiscOwner=SkyMaterial`.
- `PLAYMODE`: route snapshot showing `MeshSunVisualRequired=false` and sky material ownership active.
- `PLAYMODE`: `SURFACE_LOW_SUN_DISC_1428` remains inactive/renderer disabled.
- `PLAYMODE`: no duplicate mesh sun disc.
- `FRAME_DEBUGGER` or render proof where relevant: shader sun disc renders from `Mat_HectonSky.mat` / `Hecton_AlienSky_Master.shader`.
- Surface/sky/Aegir capture set from Batch28 remains mandatory; static source cannot prove visual quality.
- `PROFILER`/GC proof if snapshot or warning logic enters runtime cadence beyond existing development warning cadence.

## Reject List For Quick Mesh-Sun Fixes

Rejected:
- Activating `SURFACE_LOW_SUN_DISC_1428`.
- Assigning `SURFACE_LOW_SUN_DISC_1428` to `HectonCelestialEngine.sunVisualTransform`.
- Treating inactive mesh sun as primary route debt while sky shader owns the sun.
- Adding a second primary sun-disc owner.
- Replacing the shader sun route with the flat `MAT_SurfaceSunDisc_1428.mat`.
- Adding scene searches or `GlobalRegistry` polling to hot paths to silence the warning.
- Moving sun-disc truth into `HectonAtmosphereManager`.
- Claiming visual acceptance from static text.

## Continuous GlobalQualityWeight Consequences

Low / compact:
- `GlobalQualityWeight` keeps `PrimarySunDiscOwner=SkyMaterial`.
- Mesh sun remains not required when sky route is valid.
- Reduce optional sky/cloud/star proof capture resolution or diagnostics cadence before reducing surface readability.
- No flat mesh-sun fallback, no noir/darkness cover, no route warning spam.

Middle:
- Same route truth.
- Standard capture harness must record full sky-material and mesh-required fields.
- Warning gate must keep clean logs without hiding real missing references.

High:
- Same route truth.
- Extra budget buys richer sky texture response, Aegir integration checks, scatter/crop proof, and stronger material/celestial snapshot detail.
- No new gameplay truth or owner route.

Ultra:
- Same route truth.
- Visual overkill may increase capture resolution, texture residency detail, and sky/Aegir diagnostics.
- No second sun owner and no mesh activation shortcut.

## Strongest Blockers

1. `HectonUnderwaterVisuals.cs:5020-5038` still treats `sunVisualTransform == null` as a missing runtime owner without checking whether a mesh sun is required.
2. `HectonUnderwaterVisuals.cs:7130-7143` can still emit the false unresolved-sun warning while `PrimarySunDiscOwner=SkyMaterial`.
3. `HectonUnderwaterVisuals.cs:5570-5594` still searches for `Sun_Body` without a route-aware mesh requirement predicate.
4. `PrimarySunDiscOwner=SkyMaterial` is not yet a first-class runtime proof field.
5. Static evidence does not prove the clean console, shader sun visibility, Aegir crop quality, texture residency, or absence of duplicate sun disc.

## Final Implementation Order

1. Add `RequiresMeshSunVisual()` in `HectonUnderwaterVisuals`.
2. Gate `ApplySunVisualState`, `RestoreSunVisual`, `EnsureRuntimeVisualOwners`, `RequestRuntimeVisualOwnerResolveIfMissing`, `ResolveSunVisualTransform`, `ValidateReferences`, and `WarnIfRuntimeReferencesStillMissing`.
3. Add `SkyMaterialOwnsPrimarySunDisc()` inside `HectonCelestialEngine`.
4. Use it only for mesh-disc ownership/hiding in `ApplySunOcclusion()` and `RestoreSunDefaults()`.
5. Add or update proof snapshot fields in a separate approved source task.
6. Run compile/Unity/runtime proof in a separate task with Unity slot ownership.

No source or asset changes were made by this worker.
