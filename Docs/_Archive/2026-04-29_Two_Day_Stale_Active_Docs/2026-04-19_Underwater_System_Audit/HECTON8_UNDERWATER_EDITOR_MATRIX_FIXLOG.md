# HECTON-8 Underwater Editor + Matrix Fixlog
Date: 2026-04-29

Status: `PENDING VERIFICATION`

## Scope

This pass targeted three concrete failures in the underwater stack:

1. `BiomeMatrixDirector` was resolving depth from `surfaceOffsetMeters = 0` instead of the real water surface.
2. `HectonUnderwaterVisuals` was not applying matrix-biome visual overrides in editor preview.
3. All `108` matrix biome profiles had `runtimeVisualProfile = None`, so the visual matrix layer did not exist as authored data.

## Code Changes

### `Assets/_Project/Scripts/BiomeMatrixDirector.cs`

- Added `ExecuteAlways` so matrix evaluation exists in editor preview.
- Added depth-source resolution chain:
  - `HectonPlayerMovement.CurrentWaterSurfaceY`
  - `HectonFluidEngine.WaterLevel`
  - `MapMagicBridge.WaterSurfaceLevel`
  - `HectonAtmosphereManager.SeaLevelY`
  - fallback: `surfaceOffsetMeters`
- Depth is now clamped from real water surface instead of raw world origin subtraction.
- Added diagnostics:
  - `_debugSurfaceLevelY`
  - `_debugDepthSource`

### `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`

- `RefreshTargetsFromCurrentProfile()` now prefers the active matrix runtime visual profile and no longer stomps matrix-driven targets back to the legacy palette every slow tick.
- `ResolveBiomeMatrixDirector()` now resolves in editor preview as well.
- `ApplyCurrentMatrixVisualOverride()` now works outside play mode.
- Added `ResolveActiveMatrixRuntimeVisualProfile()` to centralize matrix override lookup.

### `Assets/_Project/Scripts/Editor/BiomeMatrixRuntimeVisualProfileAuthoring.cs`

- Added editor-side authoring utility for rebuilding matrix runtime visual profiles.
- Current Unity session did not expose the menu item, so batch authoring was executed via workspace automation instead of the menu path.

## Data Changes

### Runtime visual profiles

- Created `108` assets under:
  - `Assets/_Project/Data/Biomes/RuntimeVisualProfiles`
- Assigned all `108` matrix profiles to non-null `runtimeVisualProfile`.
- After authoring:
  - `runtimeVisualProfile: {fileID: 0}` count = `0`

### Scene ownership

- Removed duplicate `BiomeMatrixDirector` from `--- SYSTEMS ---/EventSystem`
- Kept the authoritative director on:
  - `--- SYSTEMS ---/[MANAGERS]`

## Unity Readback After Changes

### `BiomeMatrixDirector` on `[MANAGERS]`

- `CurrentDepthTier = 12`
- `CurrentDepthMeters = 4850`
- `_debugSurfaceLevelY = 4900`
- `_debugDepthSource = PlayerMovement`
- `CurrentProfile = Biome_046_South_12`

This confirms the director is no longer living on fake depth `0`.

### `HectonUnderwaterVisuals` in editor preview

After framing `Scene View` on the player underwater:

- `mainCamera = SceneCamera`
- `playerCamera = SceneCamera`
- `CurrentDepth = 933.2126`
- `IsUnderwater = true`
- `_debugEditorDriven = true`
- `CurrentTurbidity = 1.294693`
- `_debugFogDensity = 0.107589`

This confirms the editor path is entering underwater state and reading matrix-authored visual data.

### `HectonUnderwaterVisuals` runtime ownership readback

After the runtime ownership pass:

- `CurrentDepth = 4848.4575`
- `_debugDepth = 4848.4575`
- `_debugPlayerMovementFound = true`
- `_debugPlayerMovementSource = PlayerCameraHierarchy`

This confirms the runtime component is no longer stuck on depth `0` because of a missing `HectonPlayerMovement` reference.

### Runtime blocker found during verification

Runtime still exposed a second bug after the ownership fix:

- `CurrentDepth = 4848.4575`
- `IsUnderwater = false`
- `_debugIsUnderwater = false`
- `HectonPlayerMovement.CurrentLocomotionMode = UnderwaterSwim`
- `HectonPlayerMovement._debugIsSubmerged = true`

Root cause:

- `ResolveUnderwaterVisualState()` was still gating the entire underwater state from raw `cameraDepth`.
- In this runtime rig, `Main Camera` world `Y` can remain above the water plane while the player body and locomotion state are already deep underwater.
- Result: underwater visuals stayed logically "dry" even though depth and biome data were valid.

Fix applied:

- `ResolveUnderwaterVisualState()` now resolves from effective visual depth:
  - `visualDepth = max(depth, cameraDepth)`
- The underwater enter/exit and forced-underwater logic now uses `visualDepth`, not raw `cameraDepth`.

Status after this code change: `PENDING VERIFICATION`

Reason:

- Unity MCP lost the active session after the final compile / play transition.
- The final post-fix play-mode readback could not be captured inside the same pass.

## Evidence Files

- Scene view capture:
  - `Assets/Screenshots/underwater-sceneview-after-frame.png`

## Known Risks

- `beerLambertSurfaceClarityDepth = 5` is still aggressive. This can still crush readability too early.
- `maxFogDensity = 0.08` is still high for a premium underwater readability target.
- `MapMagicBridge.CurrentBiomeID` was still `-1` during editor inspection; matrix depth/region now works, but terrain-biome coupling still needs separate runtime verification.
- `HectonUnderwaterVisuals._debugPhysicsEngineFound = false` in editor. This needs play-mode confirmation.
- Unity MCP session stability is currently a real verification blocker during play-mode recompilation / transition. Do not treat missing runtime proof as a solved state.

## Next Verification Pass

1. Reconnect Unity MCP to the active editor instance and re-enter play mode cleanly.
2. Capture:
   - `CurrentDepth`
   - `CurrentLightFactor`
   - `IsUnderwater`
   - `CurrentTurbidity`
   - `_debugFogDensity`
   - `_debugFinalSunIntensity`
   - `_debugPlayerMovementFound`
   - `_debugPlayerMovementSource`
3. Compare Scene View vs Game View at the same depth band.
4. Tune:
   - `beerLambertSurfaceClarityDepth`
   - `maxFogDensity`
   - matrix profile families that still read too placeholder or too uniform

Without runtime logs and visual comparison captures, status remains `PENDING VERIFICATION`.

## 2026-04-19 — Editor SceneView Ownership Pass

### Root cause confirmed

`BiomeMatrixDirector` was not truly live in edit mode.

- It is an `ISlowTickable`.
- `GameTickManager` was not driving it for Scene View authoring.
- Result: `HectonUnderwaterVisuals` could use `SceneCamera`, while `BiomeMatrixDirector` still held the last player-driven matrix biome.
- This produced false editor states:
  - milky surface haze from the wrong biome family
  - instant abyss-dark underwater when framing the Scene View below water
  - editor preview not matching the actual camera context

### Code change applied

`Assets/_Project/Scripts/BiomeMatrixDirector.cs`

- Kept the earlier `ExecuteAlways` / surface-level depth resolution work.
- Added editor-only live evaluation via `EditorApplication.update`.
- Kept runtime path on `playerTransform`.
- In edit mode, `ResolveEvaluationTransform()` now uses `SceneView.lastActiveSceneView.camera.transform`.

This is not a workaround. This is the missing owner-path for editor preview.

### Unity readback after compile

`BiomeMatrixDirector` on `[MANAGERS]`:

- `_debugEvaluationSource = SceneView`
- `CurrentDepthTier = 1`
- `CurrentDepthMeters = 0`
- `CurrentProfile = Biome_003_East_01`

This confirms Scene View is no longer stuck on the player's deep placeholder biome while authoring.

### Shallow underwater Scene View probe

After framing Scene View on a temporary probe slightly below `SeaLevel = 4900`:

`BiomeMatrixDirector`:

- `_debugEvaluationSource = SceneView`
- `CurrentDepthTier = 2`
- `CurrentDepthMeters = 1.7412`
- `CurrentProfile = Biome_008_West_02`

`HectonUnderwaterVisuals`:

- `CurrentDepth = 1.7412`
- `IsUnderwater = true`
- `_debugFogDensity = 0.0039838`
- `_debugFinalSunIntensity = 1.55`
- `CurrentTurbidity = 1.2612869`

This is the first hard Unity proof in this audit that Scene View can now enter a shallow underwater state without collapsing straight into abyss-black.

### Surface haze tuning applied on `[MANAGERS]/HectonUnderwaterVisuals`

Serialized values changed:

- `beerLambertSurfaceClarityDepth = 28`
- `beerLambertExtinctionScale = 0.42`
- `maxFogDensity = 0.018`
- `submergeFogBoost = 0.12`
- `surfaceFogBlendDepth = 9`
- `surfaceOceanHorizonFogBlend = 0.22`
- `surfaceOceanBaseFogBlend = 0.05`
- `surfaceOceanHorizonLuminanceLift = 0.10`
- `crestSkyBaseFogLink = 0.34`

Unity readback after haze tune:

- `HectonUnderwaterVisuals._debugFogDensity = 0.0047205`
- `HectonUnderwaterVisuals.CurrentTurbidity = 0.80000025`
- `HectonUnderwaterVisuals.IsUnderwater = false`

This reduced the editor surface veil compared to the earlier fully washed-out Scene View capture.

### Evidence files

- `Assets/Screenshots/sceneview_after_matrix_fix.png`
- `Assets/Screenshots/gameview_underwater_probe_a.png`
- `Assets/Screenshots/screenshot-20260419-163636.png`
- `Assets/Screenshots/screenshot-20260419-163701.png`
- `Assets/Screenshots/screenshot-20260419-163743.png`

### Remaining blockers

- Scene View framing on `Ocean_Crest` is still unstable for clean human-readable before/after comparison. Some captures land in low-signal angles or empty water.
- `execute_code` is broken on this Unity instance with `The filename or extension is too long.` That blocks richer live introspection of Scene View transform data.
- `HectonUnderwaterVisuals._debugPlayerMovementFound = false` in editor is expected for Scene View authoring, but play-mode revalidation is still required.
- The generated runtime visual profiles still contain biome families that are too desaturated or too placeholder-like for premium final art. The owner-path is now correct; data polish is still incomplete.

Status remains `PENDING VERIFICATION`.

## 2026-04-19 — Shallow Profile Data Pass

### What was wrong

After the Scene View ownership fix, the next blocker was no longer logic but visual data:

- shallow runtime visual profiles were still too desaturated
- sediment / volcanic families were carrying too much turbidity too early
- editor surface water still read as white-grey milk in some camera bands even after the owner-path was corrected

### Generator status

`BiomeMatrixRuntimeVisualProfileAuthoring` was updated with cleaner shallow seeds and lower early-tier turbidity.

Unity confirmed the menu item executed:

- `[BiomeMatrixRuntimeVisualProfileAuthoring] Updated 108 runtime visual profiles. Created: 0. Assigned: 0.`

But the disk YAML for several runtime visual profile assets did **not** reflect the new generator outputs.

That means there is still an editor-side rebuild inconsistency:

- either the generated asset serialization is not being rewritten reliably
- or Unity is applying stale object state before save

This part is still `PENDING VERIFICATION`.

### Direct asset corrections applied

Because the generator path is currently unreliable, the live shallow assets that actually surfaced in Scene View were corrected directly on disk:

`Assets/_Project/Data/Biomes/RuntimeVisualProfiles/BiomeVisual_003_01_The_Granite_Spine.asset`

- `scatterColorBase = {0.024, 0.118, 0.184}`
- `scatterColorShallow = {0.084, 0.338, 0.386}`
- `depthFogDensity = {0.27, 0.17, 0.112}`
- `fogColor = {0.028, 0.168, 0.214}`
- `turbidityMultiplier = 0.68`

`Assets/_Project/Data/Biomes/RuntimeVisualProfiles/BiomeVisual_008_02_Sand_Fan_Deltas.asset`

- `scatterColorBase = {0.038, 0.108, 0.138}`
- `scatterColorShallow = {0.102, 0.236, 0.242}`
- `depthFogDensity = {0.34, 0.2, 0.118}`
- `fogColor = {0.044, 0.136, 0.154}`
- `turbidityMultiplier = 0.92`

### Unity readback after direct data patch

Scene View readback on `HectonUnderwaterVisuals`:

- `CurrentTurbidity = 0.83096814`
- `IsUnderwater = true`
- `_debugFogDensity = 0.0175496`

This confirms the direct asset patch is reaching the live underwater system.

### Remaining risks

- Scene View framing remains unstable for clean visual A/B capture. Some frames still land at misleading angles or empty water planes.
- The 108-profile generator still needs a real end-to-end serialization proof before it can be trusted for mass iteration.
- Only the shallow profiles that were actively surfacing in current editor verification were corrected directly. The full 108-biome art pass is still incomplete.
