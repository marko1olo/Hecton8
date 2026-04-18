# HECTON-8 Underwater / LOD / Perf Recovery Master Plan

Status: `PENDING VERIFICATION`
Date: `2026-04-15`
Scope: underwater visuals, visor runoff texture integration, underwater art prompts, LOD/culling reality check, GC/perf debt.

## Hard Verdict

### Underwater

- `HectonUnderwaterVisuals` is the correct runtime owner and should remain the single authority.
- The underwater stack is materially stronger than the LOD/culling stack.
- The project now has authored underwater detail anchors in first-party prefabs:
  - player-local shallow beam / motes / exhale bubbles
  - hazard pocket bubble columns
  - module leak sheen / leak VFX
  - ruin seep sheen
- Visual direction is correct: NASA-punk visor medium response + suspended underwater particulate + shallow light breakup + leak/seep wetness.
- Runtime truth is still unproven. No live Unity session, no GCMonitor, no profiler capture.

### LOD / Culling

- `LODSystemManager`, `CullingManager`, `DynamicResolutionScaler`, `ImpostorSystem` currently read as isolated code, not production-integrated systems.
- No trustworthy scene/prefab integration proof was found for the LOD stack.
- `ImpostorSystem` is openly stubbed.
- `CullingManager` uses whole-object `SetActive` toggling. This is the wrong ownership model for world visibility.

### Biolum

- `HectonBiolumManager` / `HectonBiolumZone` currently violate project rules more aggressively than the underwater stack.
- There is double-registration / possible double-tick risk.
- Runtime light creation is not pooled.
- Shared dirty-cache fields are used across multiple lights, which is architecturally wrong.

## Confirmed Problems

### Underwater stack

- Runtime verification blocked.
- Production-scene wiring for underwater owners is suspicious and must be re-verified.
- Underwater audio contract is not closed by first-party code.
- Texture import path exists, but the current visor shader still relies on procedural runoff only.

### LOD / culling stack

- `LODSystemManager` only changes LOD fade mode and global LOD bias.
- `CullingManager` caches bounds once, then toggles object active state later.
- `DynamicResolutionScaler` only pushes `renderScale`; it is not a full degradation controller.
- `ImpostorSystem` does not stream real impostor textures at runtime.

### Project-level perf governance

- Live benchmark / GC proof is absent.
- Runtime perf control is weaker than the contracts describe.
- There is still a gap between docs/README claims and real, wired runtime truth.

## New Texture Assessment

Folder:
- `Assets/_Project/Art/TEXTURES/Detali`

### Approved now

- `visor runoff normal.png`
  - Good surface logic.
  - Good streak directionality.
  - Reads like believable visor runoff normal data.
  - Best use: runoff refraction / distortion modulation in `SuitVisor.shader`.

- `visor droplet mask.png`
  - Strong breakup.
  - Good density hierarchy from micro-droplets to larger beads.
  - Good fit for masked runoff accumulation.
  - Best use: droplet mask / accumulation bias / highlight shaping.

### Usable with caveats

- `soft plume noise - какой то серый ну норм.png`
  - Usable as a first-pass grayscale plume breakup/noise field.
  - Needs controlled import and likely channel packing later.
  - Good enough for leak/silt/plume modulation if kept subtle.

- `mineral seep mask - looks seamless.png`
  - Good mood and shape language.
  - Needs separation from baked panel detail if used as a reusable mask.
  - Best use: large-scale wet/mineral breakup, not direct albedo.

### Rework required

- `bubble vent atlas - bad - redo.png`
  - Silhouette direction is usable.
  - Atlas framing and transparency readability are not safe enough yet.
  - Needs cleaner sprite isolation, more spacing, more size variance, less rectangular contamination.

## Texture Pipeline Constraint

- User-generated textures may arrive without alpha.
- Therefore every planned texture must be safe in one of these modes:
  - grayscale mask in RGB
  - normal map in RGB
  - atlas with luminance key extraction
  - repack in editor/import step later
- Do not make the shader depend on alpha-only authoring.

## Correct Execution Order

1. Wire the visor shader and controller to accept authored runoff textures without breaking the existing procedural fallback.
2. Keep the old procedural runoff path as fallback when textures are absent.
3. Reuse grayscale data from RGB if alpha is absent.
4. Only after texture-driven visor pass is stable, move to leak/plume/mineral texture integration.
5. Do not touch LOD/culling visuals until the underwater owner path is verified.
6. After underwater texture integration, return to LOD/culling and biolum debt.

## Work Items

### Phase 1 — Visor runoff texture integration

- Add runoff normal texture input to `SuitVisor.shader`.
- Add droplet mask texture input to `SuitVisor.shader`.
- Blend authored runoff normal with existing scratch normal/procedural runoff.
- Use droplet mask from RGB luminance, not alpha-only.
- Preserve current procedural runoff if textures are missing.
- Add controller-side MPB texture/property support if required.

### Phase 2 — Prompt packet refinement

- Write explicit redo prompts for:
  - bubble vent atlas
  - cleaner mineral seep mask variant
  - optional packed soft plume mask variant
- Bias prompts toward RGB-safe outputs because alpha is unreliable.

### Phase 3 — Underwater validation pass

- Re-verify player prefab wiring.
- Re-verify visor material/shader path.
- Re-verify underwater owner scene wiring.
- Re-verify whether runtime still blocks on compile/session issues.

### Phase 4 — Post-underwater debt

- Rebuild or remove fake LOD/culling integration.
- Fix biolum rule violations.
- Close underwater audio contract.
- Restore benchmark/profiler truth.

## Prompt Packet

### Bubble vent atlas redo

`RGBA-style bubble vent sprite sheet for underwater hydrothermal vent columns, 4x4 atlas layout, each cell isolated with generous padding, varied plume density from thin to heavy, realistic bubble size distribution, clean transparent-style separation, no background contamination, no rectangular fog blocks, cold deep-sea lighting, high readability for particle use`

### Mineral seep mask cleaner variant

`Tileable grayscale mineral seep mask for submerged sci-fi ruins, wet calcified streaks, vertical drip hierarchy, porous corrosion islands, residue breakup, no baked perspective, no object lighting, no panel story baked into the texture, pure reusable material mask, high contrast, production-ready`

### Soft plume noise packed-safe variant

`Tileable grayscale underwater particulate plume breakup texture, soft suspended sediment wisps, broad cloudy structures plus fine grain, no hard edges, no directional composition bias, reusable VFX modulation map, high dynamic range between soft fog and dense pockets, production-safe monochrome texture`

### Optional wet streak breakup mask

`Grayscale wet streak breakup mask for hard-surface sci-fi modules, thin rivulets, medium streaks, drip branching, clean black background, reusable material mask, no perspective, no lighting, no frame narrative`

## Immediate Next Action

- Implement Phase 1 now.
- Keep all changes reversible and fallback-safe.
- Do not claim visual success without runtime proof.

## Execution Log

### 2026-04-15 - Started

- `SuitVisor.shader` patched to accept `_WaterRunoffNormalTex` and `_WaterDropletMaskTex`.
- Shader keeps the procedural runoff path as fallback and layers authored data on top.
- `Mat_Visor_Glass.mat` now points to:
  - `Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png`
  - `Assets/_Project/Art/TEXTURES/Detali/visor droplet mask.png`
- `visor runoff normal.png.meta` corrected away from default sRGB import toward normal-map usage.
- `visor droplet mask.png.meta` corrected to linear sampling.
- Batch compile could not run because another Unity instance already has the project open. This is not proof. Status remains `PENDING VERIFICATION`.

### 2026-04-15 - Runtime follow-up

- Unity compile/readback was restored through live MCP session.
- `HectonBiolumManager` was corrected to stop violating core lifecycle rules:
  - removed `DontDestroyOnLoad`
  - removed manager-driven double-tick of zones
  - removed `OnGUI`
- `HectonBiolumZone` was corrected to stop runtime light creation in the hot path:
  - removed duplicate registration in `Start`
  - prewarmed light pool in cold path
  - removed broken shared dirty-cache logic across multiple lights
- Runtime inspection of `02_HECTON_WORLD` shows `HectonBiolumManager` exists on `--- SYSTEMS ---/[MANAGERS]`, but no active biolum zone instances were found in the checked runtime state.
- That means biolum code is cleaner now, but scene/content integration is still unproven and may be incomplete.

### 2026-04-15 - Underwater startup cadence fix

- Live runtime console initially showed false startup failures in `HectonUnderwaterVisuals` and `ProximityColliderSystem` caused by early bootstrap timing, not by missing scene wiring.
- `HectonUnderwaterVisuals` now:
  - late-resolves `playerCamera`
  - late-resolves `mainCamera`
  - late-resolves `sunVisualTransform` from `RenderSettings.sun` -> `Sun_Body`
  - defers runtime missing-reference logging into throttled retry path instead of throwing immediate false errors on `OnEnable`
- `ProximityColliderSystem` now:
  - no longer disables itself immediately when `playerTransform` is late
  - retries runtime player resolution instead of hard-failing on `OnEnable`
  - logs only throttled late warnings if the player still does not resolve
- After recompile, Console no longer reported the earlier `playerCamera not found`, `sunVisualTransform not assigned`, or `playerTransform is not assigned` startup failures.
- Runtime truth still remains `PENDING VERIFICATION` because this pass did not include deep gameplay traversal or profiler capture.

### 2026-04-15 - LOD runtime reality check

- Live scene readback on loaded `02_HECTON_WORLD` did not find active runtime instances of:
  - `LODSystemManager`
  - `CullingManager`
  - `DynamicResolutionScaler`
  - `ImpostorSystem`
- This confirms the earlier suspicion: the current LOD/culling stack is still not proven as wired runtime infrastructure in the checked world state.
- The code may exist in the repository, but runtime ownership/integration remains `PENDING VERIFICATION` and likely incomplete.

### 2026-04-15 - Leak / seep texture integration pass

- Two new user-generated textures were explicitly imported through Unity MCP:
  - `Assets/_Project/Art/TEXTURES/Detali/Soft Plume Noise - second try.png`
  - `Assets/_Project/Art/TEXTURES/Detali/Mineral Seep Mask - second try.png`
- Both new textures were corrected to linear sampling because they are modulation masks/noise, not albedo.
- A first-party seep material variant was created:
  - `Assets/_Project/Art/Materials/Construction/Mat_LeakWetSheen.mat`
  - owner shader remains `Triplebrick/Glass`
  - `_RoughnessDirt` now points at `Mineral Seep Mask - second try.png`
- A first-party plume material variant was created:
  - `Assets/_Project/Art/Materials/VFX/Mat_LeakPlume.mat`
  - based on `DustParticles.mat`
  - `_MainTex` now points at `Soft Plume Noise - second try.png`
- Construction/support owner prefabs were rewired to first-party leak/seep materials:
  - `PFB_Module_Corridor.prefab`
  - `PFB_Module_Foundation.prefab`
  - `PFB_Ruin_ClusterMedium.prefab`
  - `PFB_Ruin_Megastructure.prefab`
  - `PFB_Support_Pocket_Hazard.prefab`
- Seep quads now point to `Mat_LeakWetSheen`.
- Leak plume particle instances now carry material override to `Mat_LeakPlume`.
- Bubble columns and player-local dust/exhale source prefab were intentionally left on the third-party dust material. This avoids collateral regression in unrelated underwater owners.
- Unity accepted reimport of all five touched prefabs as assets.
- Console did not surface new prefab/material parse errors from this pass.
- Current compile state is still blocked by unrelated errors in `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`.
- Therefore visual/runtime outcome of the leak/seep pass remains `PENDING VERIFICATION`.

### 2026-04-15 - CameraJuiceSystem compile recovery

- `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs` was blocking compilation for the wrong reason:
  - it dereferenced `transform` through `IInteractable`
  - it still used obsolete `FindObjectOfType`
  - it overwrote camera local position directly with shake offset instead of preserving the authored rest pose
- The system was patched to:
  - cache `Transform` only when the hovered `IInteractable` is also a `Component`
  - restore and preserve the camera local rest position when applying shake
  - late-resolve cold-path references for camera / volume / survival / movement without leaving obsolete calls in place
  - restore camera local position and base FOV on disable
- After recompilation, the earlier `CameraJuiceSystem` compile errors disappeared from the live Unity Console.
- `validate_script` still produced false-positive duplicate-signature diagnostics, but live Console stopped reporting real compile failures from this file.
- Status remains `PENDING VERIFICATION` because no gameplay traversal or GC/profiler capture was performed for the repaired camera-juice path.

### 2026-04-15 - LOD world-scene integration pass

- `02_HECTON_WORLD` now has live scene integration on `--- SYSTEMS ---/[MANAGERS]` for:
  - `LODSystemManager`
  - `CullingManager`
  - `DynamicResolutionScaler`
  - `WorldLODSceneBootstrap`
- `ImpostorSystem` was intentionally not added to the live scene because the current runtime path is still openly stubbed and not safe to present as production integration.
- `CullingManager` had a real scene-add blocker:
  - it called `LayerMask.NameToLayer()` in static field initialization
  - this threw a `UnityException` when the component type was instantiated
- `CullingManager` was patched to:
  - move layer lookup into a lazy `EnsureLayerCache()` path
  - retry `ApplyLayerCullDistances()` during `SlowTick` until a camera exists
- A new scene bootstrap was added:
  - `Assets/_Project/Scripts/World/WorldLODSceneBootstrap.cs`
  - responsibility: one-shot registration of authored `LODGroup` components from the active world scene into `LODSystemManager`
- Live play-mode Console proof confirmed:
  - `LODSystemManager` initializes
  - `CullingManager` initializes
  - `DynamicResolutionScaler` initializes
  - `CullingManager` applies layer cull distances in runtime
- The first live bootstrap pass exposed a real integration bug:
  - `WorldLODSceneBootstrap` initially filtered by `gameObject.scene`
  - at runtime `[MANAGERS]` had already moved into `DontDestroyOnLoad`
  - result: bootstrap logged `Registered 0 LODGroup components for scene 'DontDestroyOnLoad'`
- That bug was patched:
  - `WorldLODSceneBootstrap` now registers against `SceneManager.GetActiveScene()`
- Post-fix positive registration count is still `PENDING VERIFICATION`:
  - Unity MCP play-mode transport became unstable during the second smoke pass
  - Unity MCP `execute_code` is currently unusable in this environment (`mono.exe` / filename-too-long failure)
- Scene asset was saved after manager integration. The world scene now contains the new LOD managers and bootstrap component as serialized scene state.

### 2026-04-15 - LOD manager hardening pass

- `WorldLODSceneBootstrap` was hardened beyond the initial scene-wiring patch:
  - it now caches its authoring scene path/name in `Awake`
  - it subscribes to `SceneManager.sceneLoaded`
  - it resolves the registration target from the original authoring scene instead of trusting the manager object's current scene
  - it only tracks `LODGroup` entries that actually increased `LODSystemManager.RegisteredLODGroupCount`
- This is specifically to survive the `DontDestroyOnLoad` handoff path without silently scanning the wrong scene.
- `CullingManager` was improved:
  - bounds are now refreshed during `SlowTick` instead of using one stale registration-time snapshot
  - renderer-backed objects now use `Renderer.forceRenderingOff` for distance culling instead of whole-object `SetActive`
  - manager shutdown/unregister now restores visual state instead of leaving renderers or fallback objects culled
  - renderer resolution is now explicit and cold-path only
- `CullingManager` still contains fallback `SetActive` behavior when no renderer exists. This is a compatibility fallback, not the preferred path.
- `DynamicResolutionScaler` had inverted quality logic:
  - before the fix, `Medium`/`High` could degrade below `Low`
  - that was architecturally wrong
- `DynamicResolutionScaler` now:
  - stores the default URP render scale from the active pipeline asset
  - restores that default scale on destroy
  - uses minimum scale floors that respect quality direction (`Low=0.7`, `Medium=0.8`, `High=0.9`)
  - resets to the captured default scale when dynamic resolution is disabled
- Current compile state after these hardening changes is clean in live Unity Console.
- Runtime registration count and visual culling behavior remain `PENDING VERIFICATION` because Unity MCP play-mode readback is still unstable in this environment.

## Manual Verification After Unity Reimport

1. Let Unity reimport the two visor textures and the visor shader/material.
2. Inspect `Mat_Visor_Glass`:
   - `Water Runoff Normal` should show the authored normal map.
   - `Water Droplet Mask` should show the authored droplet mask.
3. In play mode, trigger water entry/exit and confirm:
   - runoff refraction uses the authored streak normal instead of only scratch noise
   - droplet breakup persists even without alpha in the mask texture
   - no hard rectangular contamination appears on the visor
4. Check Console for shader/property errors.
5. If the normal looks inverted, flip green on the runoff normal import and recheck.
6. Inspect leak/seep owner prefabs in Scene or Prefab Mode:
   - `LeakWetSheen`, `RuinSeepSheen_*`, and `VentSheen_*` should now use `Mat_LeakWetSheen`
   - `LeakVfx` / `RuinLeakPlume_*` should now use `Mat_LeakPlume`
7. In play mode, verify:
   - seep quads read as mineral runoff instead of generic glass smear
   - leak plumes read softer and more particulate, without obvious card-shaped texture read
   - player-local motes/exhale bubbles remain visually unchanged
8. Treat `CameraJuiceSystem.cs` compile errors as unrelated blocker noise unless leak/seep assets show additional errors on top.
### 2026-04-15 - Impostor system hardening pass

- `Assets/_Project/Scripts/World/ImpostorSystem.cs` had a real correctness bug, not just a style problem:
  - active impostor unregister/disable/destroy paths could leave the original renderers hidden forever
  - runtime visibility still relied on whole-object `SetActive(false/true)` for the source object
  - billboard cleanup and original-visibility restoration were not symmetric
- The system was hardened without pretending that runtime streaming is finished:
  - original-object visibility now uses cached renderer `forceRenderingOff` state instead of whole-object `SetActive`
  - unregister, disable, destroy, and missing-billboard recovery now restore original renderer visibility before removing the impostor record
  - active billboard position is kept in sync with the source transform while the impostor is active
  - camera resolve now supports an explicit serialized reference and only falls back to cold-path camera lookup
  - active instance data now caches source transform, managed renderers, and original renderer visibility state
- Important limitation remains unchanged:
  - runtime impostor texture streaming is still stubbed
  - `ImpostorSystem` is still not safe to call production-ready or scene-integrated impostor coverage
- Live Unity Console did not surface new compile errors after this pass.
- Status remains `PENDING VERIFICATION` because no runtime impostor activation path was exercised and no GC/perf capture exists.

### 2026-04-15 - Acoustic ownership recovery pass

- Earlier audit conclusion about underwater audio was incomplete:
  - first-party underwater acoustic logic does exist in `Assets/_Project/Scripts/AcousticZoneController.cs`
  - the real problem was ownership and integration quality, not total absence of code
- `AcousticZoneController` had explicit architecture violations:
  - `DontDestroyOnLoad(gameObject)`
  - runtime auto-spawn via `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` into `AcousticZoneController_Root`
  - scene had no authored owner, so the system could come up as an unconfigured transient root
- The system was corrected to scene ownership:
  - removed `DontDestroyOnLoad`
  - removed runtime auto-spawn root creation
  - added `AcousticZoneController` to `02_HECTON_WORLD` on `--- SYSTEMS ---/[MANAGERS]`
  - saved the world scene with the authored component present
- Authoring defaults were added for the transition clips:
  - editor path now assigns `swimming -onwater.wav` as `waterDrainSound`
  - editor path now assigns `swimming - underwater.ogg` as `waterFillSound`
  - because MCP add-component did not trigger `Reset`, the two clip refs were also written explicitly onto the scene component and saved
- Live scene readback confirmed:
  - `[MANAGERS]` now contains `AcousticZoneController`
  - serialized `waterDrainSound` and `waterFillSound` paths are present on the scene component
- Remaining gap:
  - mixer snapshot authoring is still incomplete; `underwaterSnapshot`, `baseInteriorSnapshot`, and surface snapshot refs remain null on the scene component
  - this means the system now has correct ownership and transition clips, but not a finished mixer-state authoring pass
- Status remains `PENDING VERIFICATION` because gameplay traversal and mixer-behavior proof are still absent.

### 2026-04-15 - Surface weather ownership recovery pass

- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs` had the same ownership smell as the acoustic system:
  - scene-load hooks created a runtime `[SURFACE_WEATHER]` root when no authored component existed
  - the world scene itself had no explicit owner for the surface weather director
- That bootstrap layer was removed:
  - deleted scene-load auto-bootstrap hooks and runtime root creation
  - kept only static-state reset
  - added `HectonSurfaceWeatherDirector` directly to `02_HECTON_WORLD` on `--- SYSTEMS ---/[MANAGERS]`
  - saved the scene after integration
- Live scene readback confirmed:
  - `[MANAGERS]` now contains `HectonSurfaceWeatherDirector`
  - the scene component already carries authored weather profile refs and fallback thunder clips
- This is materially better than the old transient-root path because surface weather is now an authored world subsystem instead of hidden runtime bootstrap behavior.
- Status remains `PENDING VERIFICATION` until play-mode traversal confirms weather transitions, local shelter sampling, and no new console noise after full domain reload.

### 2026-04-15 - Save contract compile blocker recovery

- Full script recompilation surfaced a real compile blocker outside the immediate underwater/LOD work:
  - `Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs` reads and writes `SaveData.playerExpressionProfileId`
  - `Assets/_Project/Scripts/SaveData.cs` did not have a clean single canonical field definition for that contract
- `SaveData` was normalized:
  - kept one canonical `playerExpressionProfileId` field on `SaveData`
  - removed duplicate declaration/initializer drift
  - preserved default initialization for new saves
- This was the correct fix location:
  - the profile id belongs to save-format ownership, not to `PlayerExpressionManager` fallback hacks
  - leaving the contract split would guarantee the next regression
- After the fix, Unity Console was rechecked:
  - stale MCP `ExecuteCode` noise was cleared
  - final Console readback returned `0` warnings/errors
- Compile state for the touched scripts is currently clean in live Unity Console.
- Status still remains `PENDING VERIFICATION` because compile-clean is not gameplay-proof and no runtime traversal/GC capture was performed.

### 2026-04-15 - Acoustic snapshot fallback hardening pass

- `Assets/_Project/Scripts/AcousticZoneController.cs` still had a brittle authoring path even after scene ownership recovery:
  - snapshot transitions were split across multiple methods with duplicated null handling
  - missing surface snapshots silently degraded to whatever happened to be serialized last
  - there was no cold-path authoring recovery if snapshots later appeared in `MasterMixer.mixer` but scene refs were left empty
- The controller was hardened without faking mixer authoring that still does not exist:
  - added serialized `masterMixer` ownership on the controller
  - editor defaults now auto-assign `Assets/_Project/MasterMixer.mixer`
  - cold-path binding now attempts to resolve snapshot refs by expected names:
    - `Underwater` / `UnderwaterSnapshot`
    - `BaseInterior` / `BaseInteriorSnapshot`
    - `Surface` / `SurfaceSnapshot`
    - `SurfaceRain` / `SurfaceRainSnapshot`
    - `SurfaceStorm` / `SurfaceStormSnapshot`
  - transition flow now goes through one centralized resolver instead of three partially duplicated null paths
  - one-time guarded warnings now report missing snapshot coverage and explicit fallback ladders instead of silent wrong-state behavior
  - surface weather re-tiering (`SetSurfaceWeatherMix` / `ClearSurfaceWeatherMix`) now reuses the same centralized snapshot transition path
- This does **not** mean the acoustic mixer authoring is done:
  - current `Assets/_Project/MasterMixer.mixer` still exposes only a single `Snapshot`
  - until authored underwater/interior/surface/rain/storm snapshots exist, the controller will still run through documented fallback paths
- Unity MCP was not available for a fresh live compile/state readback during this pass (`editor/state` ping not answered), so status remains `PENDING VERIFICATION`.

### 2026-04-15 - Seep/plume material pipeline hardening pass

- Two real art/runtime pipeline gaps were still present:
  - `CullingManager` still had a `GameObject.SetActive()` fallback for non-renderer registrations, which is wrong for world visibility management and risks lifecycle churn
  - ruin seep sheen still depended on third-party `Assets/ScifiFacility/Materials/GlassWet.mat`, which is the wrong owner and does not respect the new alpha-less AI mask pipeline
- `Assets/_Project/Scripts/World/CullingManager.cs` was hardened:
  - registration now refuses non-renderer owners instead of toggling whole objects
  - renderer resolve now falls back to `GetComponentInChildren<Renderer>(true)` in cold path for authored prefab roots that hold renderers on child nodes
  - runtime cull/restore path is now renderer-only via `Renderer.forceRenderingOff`
- First-party seep sheen owner was introduced:
  - added `Assets/_Project/Art/Shaders/Hecton_RuinSeepSheen.shader`
  - added `Assets/_Project/Art/Materials/Construction/Mat_RuinSeepSheen.mat`
  - shader derives opacity from RGB luminance, not texture alpha
  - shader adds a subtle flowing mask sample and fresnel highlight so the seep cards read like wet mineral runoff instead of generic transparent glass
- World authoring was moved to the new first-party owner:
  - `ConstructionBootstrapAuthoring.cs` now points ruin seep creation to `Mat_RuinSeepSheen`
  - existing `PFB_Ruin_ClusterMedium.prefab` and `PFB_Ruin_Megastructure.prefab` seep sheen quads were repointed from `GlassWet.mat` to `Mat_RuinSeepSheen`
  - existing `PFB_Module_Foundation.prefab` and `PFB_Module_Corridor.prefab` `LeakWetSheen` quads were also repointed so saved prefabs match the new authoring path
  - existing `PFB_Support_Pocket_Hazard.prefab` vent sheen quads (`VentSheen_Main`, `VentSheen_LOD1`, `VentSheen_Secondary`) were also repointed to keep the visual language on one first-party material owner
  - `WorldProceduralSupportFinalAuthoring.cs` now also points hazard vent sheen authoring to the same first-party material, so the next support-final rebuild does not silently revert the prefab back to `GlassWet.mat`
- Texture import budget was tightened for the two new generated textures:
  - `Soft Plume Noise - second try.png` capped to `1024` and clamped
  - `Mineral Seep Mask - second try.png` capped to `1024` and clamped
  - both remain linear-sampled and alpha-independent
- This is still `PENDING VERIFICATION`:
  - Unity MCP was not available for shader compile readback
  - no live visual check was possible for seep sheen brightness, quad sorting, or plume softness
  - `Hecton_RuinSeepSheen.shader` is code-review-only until Unity Console confirms it compiles cleanly

### 2026-04-16 - Scene serialization recovery for LOD/culling managers

- Live Unity scene readback exposed a real integration defect on `--- SYSTEMS ---/[MANAGERS]`:
  - `LODSystemManager` had `_cameraReference = null`
  - `CullingManager` had `_cameraReference = null`
  - `CullingManager` also had all critical serialized distances at `0`
    - `_smallObjectCullDistance = 0`
    - `_mediumObjectCullDistance = 0`
    - `_largeObjectCullDistance = 0`
    - `_hysteresisPercent = 0`
    - `_debrisLayerCullDistance = 0`
    - `_particlesLayerCullDistance = 0`
    - `_propsLayerCullDistance = 0`
    - `_floraLayerCullDistance = 0`
- That means the scene-authored state was objectively wrong:
  - culling logic was effectively neutered
  - manager behavior depended on cold fallback instead of explicit authored references
- The scene was corrected directly through Unity MCP and saved:
  - `LODSystemManager._cameraReference` now points to `--- GAMEPLAY ---/Player/Main Camera`
  - `CullingManager._cameraReference` now points to `--- GAMEPLAY ---/Player/Main Camera`
  - `CullingManager` distances now match the intended design contract:
    - small `30`
    - medium `80`
    - large `200`
    - hysteresis `10`
    - debris `40`
    - particles `40`
    - props `100`
    - flora `100`
  - `DynamicResolutionScaler._minRenderScale` was normalized to `0.8` to match the corrected medium-tier runtime floor
- Live scene readback confirmed after save:
  - the camera refs are now present on `LODSystemManager` and `CullingManager`
  - the culling distances persist with the corrected values
- This is still `PENDING VERIFICATION`:
  - scene serialization is confirmed
  - play-mode behavior, actual registration counts, and frame-time impact are not confirmed
  - Unity refresh/read-console handshake remained unstable during this pass, so there is still no final live Console proof after the latest script changes

### 2026-04-16 - LOD bootstrap and camera resolve hardening pass

- `Assets/_Project/Scripts/World/WorldLODSceneBootstrap.cs` still used a global `FindObjectsByType<LODGroup>` scan during rebuild:
  - wrong ownership scope
  - unnecessary whole-world scan when the bootstrap already knows its target scene
  - array allocation path for startup scans
- `Assets/_Project/Scripts/World/LODSystemManager.cs` still retried `Camera.main` directly when the camera was missing:
  - not as bad as per-frame `Update` spam, but still a weak hot/cold boundary
- Both systems were tightened:
  - `WorldLODSceneBootstrap` now traverses only the resolved target scene roots via `GetRootGameObjects(List<GameObject>)`
  - it then gathers `LODGroup` components with `GetComponentsInChildren(bool, List<LODGroup>)` into reusable buffers
  - this removes the global scene scan and keeps the work scoped to the authored scene owner
  - `LODSystemManager` now uses a `CameraResolveRetryInterval` timer so failed camera resolution does not keep hammering `Camera.main`
- This pass is code-review-only for now:
  - Unity Console could not be read after refresh because the MCP bridge stopped answering `read_console`
  - scene serialization is good, but compile/runtime proof for these two script edits is still missing

### 2026-04-16 - Culling integration recovery pass

- Additional live/code audit exposed a deeper problem than camera refs:
  - there are still **zero** first-party callers for `CullingManager.RegisterCullableObject(...)`
  - there are still **zero** first-party callers for `ImpostorSystem.RegisterImpostorCandidate(...)`
  - the only `LODGroup` objects found in the current world scene are seven staging/temp objects, mostly under disabled `Tool_Staging` and one disabled `__TEMP_DENSE_KELP_PREVIEW`
- That means the old “LOD stack is integrated” claim was materially false:
  - `LODSystemManager` had scene wiring
  - `CullingManager` and `ImpostorSystem` still had almost no real content feed
- `Assets/_Project/Scripts/World/CullingManager.cs` was upgraded to handle real authored roots:
  - cull ownership is no longer limited to a single renderer
  - cold-path registration now caches all child renderers under a root owner
  - distance cull/restore now toggles all tracked renderers, not one arbitrary renderer
  - combined bounds are now computed across the renderer set, which is mandatory for `LODGroup` roots with multi-renderer children
- `Assets/_Project/Scripts/World/WorldLODSceneBootstrap.cs` was extended:
  - when a scene `LODGroup` is successfully registered into `LODSystemManager`, its root now also co-registers into `CullingManager` when the manager exists
  - unregister path now removes the same roots from `CullingManager`
  - this does not solve the total lack of active production `LODGroup` content, but it finally makes the bootstrap path coherent when real authored scene content exists
- Important remaining truth:
  - in the **current** live world scene, active production `LODGroup` content still appears absent
  - so this pass restores architecture correctness, not proven runtime effect
- Status remains `PENDING VERIFICATION`:
  - Unity refresh/read-console handshake broke again right after script changes
  - compile proof is absent
  - play-mode registration counts and perf impact are still not measured

### 2026-04-16 - Runtime scatter ownership recovery pass

- Further code audit exposed the next real integration gap:
  - `WorldProceduralScatterDirector` is the actual runtime spawn owner for scatter instances
  - `WorldGenerativeGeologyService` can add a generated child `LODGroup` later under those instances
  - the old recovery work still depended too much on scene bootstrap and scene-authored content, while runtime scatter instances had no self-owned registration lifecycle
- `Assets/_Project/Scripts/WorldProceduralProxyInstance.cs` was promoted into the runtime optimization owner for scatter instances:
  - it now implements `IPoolable`
  - it refreshes `LODSystemManager` and `CullingManager` registration on `OnEnable`, `OnSpawn`, and `MarkScatterSync(...)`
  - it unregisters cleanly on `OnDisable`, `OnDestroy`, and `OnDespawn`
  - it scans child `LODGroup` components and keeps registration in sync, which is necessary because generated geology can create `LODGroup` after the base instance already exists
  - it registers the root object with `CullingManager` only when the manager actually accepts the owner, using `RegisteredObjectCount` as the contract check
- `Assets/_Project/Scripts/WorldGenerativeGeologyService.cs` was corrected to respect the project LOD contract:
  - generated `LODGroup` now explicitly uses `LODFadeMode.CrossFade`
  - `animateCrossFading = true` is now forced on generated geology roots
- This is the first pass that gives the runtime scatter path a plausible real feed into the LOD/culling layer without inventing another global bootstrap owner.
- Status remains `PENDING VERIFICATION`:
  - Unity MCP ping was not healthy after the edits
  - no compile proof, Console proof, or play-mode registration count was captured
  - impostor runtime is still intentionally not wired here because its texture streaming path remains stubbed

### 2026-04-16 - Compile recovery chain and UI cleanup pass

- Once the editor was forced back into stable edit mode, the first honest compile pass exposed a chain of real blockers:
  - `AcousticZoneController` referenced `BiomeMatrixDirector.Instance`, but `BiomeMatrixDirector` is not a singleton in this project
  - `HectonPlayerMovement` still contained an inline `PlayerLocomotionMode` enum while the project also expected a standalone `PlayerLocomotionMode.cs`
  - the standalone `PlayerLocomotionMode.cs` source file was actually missing on disk, so Unity later failed with `CS2001`
- Fixes applied:
  - `Assets/_Project/Scripts/AcousticZoneController.cs`
    - replaced the invalid singleton access with `WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector)`
    - added `using Hecton8.World;` to use the existing runtime helper instead of inventing a new owner pattern
  - `Assets/_Project/Scripts/HectonPlayerMovement.cs`
    - removed the duplicate inline `PlayerLocomotionMode` enum definition
  - `Assets/_Project/Scripts/PlayerLocomotionMode.cs`
    - restored the standalone enum file as the canonical source of truth
  - `Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs`
    - improved camera handling so the tab no longer depends on a single fragile `Camera.main` lookup only in `Awake`
    - added cold retry resolve state for the main camera path
- What is actually confirmed:
  - Unity Console explicitly reported the `AcousticZoneController` singleton error and the duplicate/missing `PlayerLocomotionMode` failure chain before the fixes
  - the relevant source files on disk now reflect the corrective changes
- What is not confirmed:
  - final post-fix Console state after recreating `PlayerLocomotionMode.cs`
  - `PDAAtlasSignalTab` still needs one more cleanup pass to remove the lingering `Resources.Load` fallback cleanly; the current file encoding made direct patching noisy in this pass
- Status remains `PENDING VERIFICATION`:
  - Unity MCP bridge dropped out again during the last import/recompile cycle
  - no final green compile proof was captured after the file recreation step

### 2026-04-16 - Compile owner normalization and UI camera fallback cleanup pass

- The next live pass exposed an uglier truth:
  - `PlayerLocomotionMode.cs` was physically missing on disk again
  - `HectonPlayerMovement.cs` had drifted back to an inline enum owner
  - Unity Console was still failing on missing source / duplicate-owner confusion instead of a single canonical locomotion contract
- Corrective ownership cleanup applied:
  - `Assets/_Project/Scripts/PlayerLocomotionMode.cs`
    - restored again as the standalone canonical enum owner
  - `Assets/_Project/Scripts/HectonPlayerMovement.cs`
    - removed the inline enum owner again
    - replaced editor-only `ToString()` diagnostics with a cached string table so the new locomotion diagnostics do not allocate through enum conversion in the editor tick path
- UI rule-cleanup applied in the same pass:
  - `Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs`
    - fully removed the lingering `Resources.Load(...)` fallback
    - font now resolves only through serialized input or `TMP_Settings.defaultFontAsset`
  - `Assets/_Project/Scripts/UI/SettingsLivePreview.cs`
    - removed the final `Camera.main` fallback
    - camera resolve now retries on a cold cadence and only walks explicit owner paths (`SceneBootstrap` player, self, children, parent)
  - `Assets/_Project/Scripts/UI/SettingsManager.cs`
    - removed `Camera.main` plus global `FindObjectsByType<Camera>` fallback
    - camera resolve now uses the same explicit ownership chain instead of scene-wide fishing
  - `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs`
    - removed `Camera.main` fallback from save thumbnail capture
    - thumbnail capture now succeeds only from explicit owner cameras (serialized, player, self/child/parent), otherwise it fails honestly
- What is actually verified:
  - live Console truth before this pass still showed the missing `PlayerLocomotionMode.cs` compile blocker
  - the file exists again on disk after the normalization pass
  - the bad `Resources.Load` fallback is gone from `PDAAtlasSignalTab`
- What is not verified:
  - final clean compile after the restored enum owner and UI cleanup
  - runtime behavior of settings preview / thumbnail capture after removing fallback magic
- Status remains `PENDING VERIFICATION`:
  - `refresh_unity(... wait_for_ready=true)` kept timing out while the editor was reloading scripts
  - Unity MCP ping dropped during the compile window again

### 2026-04-16 - Camera fallback normalization and UI rule cleanup verification pass

- After the missing-file chain was stabilized, the next cleanup target was hidden fallback magic:
  - multiple first-party runtime/UI systems still depended on `Camera.main` in cold resolve paths
  - multiple PDA tabs still contained forbidden `Resources.Load(...)` font fallback logic
  - one diagnostic string path in `HectonPlayerMovement` still allocated through enum-to-string conversion in editor diagnostics
- Additional code cleanup applied:
  - `Assets/_Project/Scripts/HectonPlayerMovement.cs`
    - removed dead `surfaceBreachImpulseMultiplier` / `surfaceBreachSurfaceLockoutTime` fields after the surface-breach branch was deleted
  - `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`
    - camera resolve now prefers explicit reference, `SceneBootstrap` player camera, then local hierarchy; no `Camera.main` fallback remains
  - `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
    - removed `Camera.main` fallback and replaced it with explicit local/child/parent hierarchy resolve after the player-camera path
  - `Assets/_Project/Scripts/World/LODSystemManager.cs`
    - cold camera resolve now uses explicit reference or `SceneBootstrap` player camera
  - `Assets/_Project/Scripts/World/CullingManager.cs`
    - cold camera resolve now uses explicit reference or `SceneBootstrap` player camera
    - stale tooltip/log text mentioning `Camera.main` was corrected
  - `Assets/_Project/Scripts/World/ImpostorSystem.cs`
    - cold camera resolve now uses explicit reference or `SceneBootstrap` player camera
  - `Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs`
    - removed the remaining `Camera.main` fallback; it now resolves from player/local/child/parent camera ownership only
  - `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
    - removed forbidden `Resources.Load(...)` font fallback
  - `Assets/_Project/Scripts/UI/PDASpectrumTab.cs`
    - removed forbidden `Resources.Load(...)` font fallback
  - `Assets/_Project/Scripts/UI/SettingsManager.cs`
    - removed `Camera.main` plus global camera scan fallback
  - `Assets/_Project/Scripts/UI/SettingsLivePreview.cs`
    - already cleaned in the previous pass; kept as explicit owner-chain resolve only
  - `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs`
    - already cleaned in the previous pass; no hidden main-camera magic remains
- What is actually verified:
  - Unity Console readback after the final compile pass returned `0` warnings and `0` errors
  - first-party grep for `Resources.Load(...)` under `Assets/_Project/Scripts` is now empty
  - remaining `Camera.main` grep hits are limited to:
    - editor-only scanner comments
    - deprecated non-production file `PlayerController - Old - deprecated - do not use or open.cs`
- What is not verified:
  - runtime visual correctness after removing cold fallback magic in every touched system
  - gameplay traversal / perf / GC effect
- Status remains `PENDING VERIFICATION`:
  - compile-only proof exists
  - runtime proof does not

### 2026-04-16 - Underwater audio authoring verification and lifecycle guard pass

- The next truthful pass focused on `AcousticZoneController` because the underwater audio contract was still weak and previously described too abstractly.
- Live scene/component readback confirmed the authoring gap on `[MANAGERS]` in `02_HECTON_WORLD`:
  - `masterMixer` is assigned to `Assets/_Project/MasterMixer.mixer`
  - `underwaterSnapshot`, `baseInteriorSnapshot`, `surfaceSnapshot`, `surfaceRainSnapshot`, and `surfaceStormSnapshot` are all still `null`
  - `playerUnderwaterAmbientSource` is still `null`
  - `biomeMatrixDirector` is still `null`
- Direct asset inspection of `Assets/_Project/MasterMixer.mixer` confirmed the mixer itself is not authored for the intended contract:
  - only one snapshot exists (`Snapshot`)
  - no named coverage exists for `Underwater`, `BaseInterior`, `Surface`, `SurfaceRain`, or `SurfaceStorm`
  - the current effect graph exposes only `Attenuation`; there is no authored LPF/reverb-style contrast layer for real underwater/interior acoustics
- Code changes applied in `Assets/_Project/Scripts/AcousticZoneController.cs`:
  - added editor/runtime-facing diagnostics for snapshot/mixer coverage so the deficit is visible directly on the component instead of only through Console warnings
  - fixed a real lifecycle bug in `OnDisable` / `OnDestroy`: `_registeredToTickManager` is now cleared even if `GameTickManager` has already been torn down, avoiding a false "already registered" state on later re-enable
  - normalized the cold-allocation comment on the reused `List<AudioSource>` buffer to match AGENTS.md canonical format
- Additional regression risk observed during this pass:
  - `Assets/_Project/Scripts/PlayerLocomotionMode.cs` disappeared from disk again after it had already been restored earlier
  - the file was recreated again, but this now has to be treated as an external/shared-workspace corruption risk, not a closed local fix
- What is actually verified:
  - scene readback proved the `AcousticZoneController` snapshot refs are unassigned
  - mixer asset inspection proved the current `MasterMixer.mixer` authoring is incomplete for underwater/interior transitions
  - the lifecycle unregister defect in `AcousticZoneController` was corrected on disk
- What is not verified:
  - final clean compile after the latest `PlayerLocomotionMode.cs` recreation
  - runtime acoustic behavior after the new diagnostics/lifecycle guard change
- Status remains `PENDING VERIFICATION`:
  - Unity MCP repeatedly timed out during script reload
  - batch compile could not run because another Unity instance already had the project open

### 2026-04-16 - Tick registration teardown hardening pass

- A repeated lifecycle defect was found across the systems already touched in this recovery:
  - multiple managers only cleared their `_registered` flag if `GameTickManager.Instance` was still alive during `OnDisable` / `OnDestroy`
  - if `GameTickManager` tears down first, the component keeps a false "already registered" state and can fail to re-register on the next enable/reload
- Corrective cleanup applied:
  - `Assets/_Project/Scripts/AcousticZoneController.cs`
    - centralized unregister logic via `TryUnregister()`
    - always clears `_registeredToTickManager` even when `GameTickManager` is already gone
  - `Assets/_Project/Scripts/World/CullingManager.cs`
    - added `TryRegister()` / `TryUnregister()`
    - `OnDisable` and `OnDestroy` now clear `_registered` deterministically
  - `Assets/_Project/Scripts/World/LODSystemManager.cs`
    - added `TryRegister()` / `TryUnregister()`
    - unregister now happens safely during both disable and destroy before singleton cleanup
  - `Assets/_Project/Scripts/World/ImpostorSystem.cs`
    - added `TryRegister()` / `TryUnregister()`
    - impostor manager no longer depends on `GameTickManager` surviving teardown just to reset its registration state
  - `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`
    - added centralized `TryUnregister()`
    - `OnDisable` and `OnDestroy` now clear tick subscriptions and local `_registered` state deterministically
- Why this matters:
  - this is not a cosmetic refactor
  - the previous pattern creates latent reload/respawn bugs where a system looks enabled but never ticks again after one bad teardown order
- What is actually verified:
  - code review on disk confirms the bad pattern existed in all listed files
  - the corrective unregister helpers are now present on disk
- What is not verified:
  - live play-mode reload / scene transition traversal proving the bug is gone
- Status remains `PENDING VERIFICATION`:
  - editor session was still unstable during this pass
  - no live post-fix registration counts or Console proof were captured

### 2026-04-16 - Residual fallback audit correction pass

- A follow-up grep proved the previous cleanup report was still too optimistic:
  - `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs` still contained a real `Camera.main` fallback
  - `Assets/_Project/Scripts/UI/SettingsLivePreview.cs` still contained a real `Camera.main` fallback
- Corrective changes applied:
  - `Assets/_Project/Scripts/UI/SaveSlotThumbnail.cs`
    - removed the final `Camera.main` fallback entirely
    - capture camera resolution now fails honestly with `null` if no explicit/player/local hierarchy camera exists
  - `Assets/_Project/Scripts/UI/SettingsLivePreview.cs`
    - removed the final `Camera.main` fallback entirely
    - preview camera resolution now returns `false` instead of silently grabbing a global camera
  - `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs`
    - received the same `TryRegister()` / `TryUnregister()` teardown fix as the other LOD-runtime managers
- What is actually verified:
  - fresh grep over `Assets/_Project/Scripts` now returns no active `Camera.main` or `Resources.Load(...)` usage in first-party runtime scripts (editor code and the deprecated non-production controller excluded intentionally)
- What is not verified:
  - final compile and play-mode behavior after removing these last hidden fallbacks
- Status remains `PENDING VERIFICATION`:
  - filesystem proof exists
  - live editor proof for this exact pass does not

### 2026-04-16 - Compile recovery confirmation and acoustic scene wiring pass

- Unity editor recovered after the earlier disconnect cycle and a fresh script refresh/compile was run.
- What is actually verified now:
  - Unity Console readback returned `0` log entries after the latest script refresh
  - this is the first clean compile proof after:
    - re-restoring `Assets/_Project/Scripts/PlayerLocomotionMode.cs`
    - adding `AcousticZoneController` diagnostics/lifecycle hardening
    - removing the last hidden `Camera.main` fallbacks
    - hardening teardown registration in the LOD/runtime stack
- Additional safe scene wiring applied and verified:
  - `AcousticZoneController.biomeMatrixDirector` was explicitly assigned on `[MANAGERS]` to the colocated `BiomeMatrixDirector` component in `02_HECTON_WORLD`
  - the scene was saved after the assignment
  - readback confirms the field is no longer `null`
- What remains intentionally unresolved:
  - `underwaterSnapshot`, `baseInteriorSnapshot`, `surfaceSnapshot`, `surfaceRainSnapshot`, and `surfaceStormSnapshot` are still unassigned because the current `MasterMixer.mixer` authoring does not actually provide the required snapshot set
  - `playerUnderwaterAmbientSource` is still left dynamic; no safe authored scene reference was forced here
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - scene readback proof exists
  - runtime acoustic behavior, transition quality, and perf/GC impact still do not

### 2026-04-16 - Adjacent world/audio/readability stabilization pass

- After the acoustic/LOD recovery pass, the next audit targeted the adjacent world-context owners that feed biome, atmosphere, readability, and first-hour guidance.
- Confirmed repeated lifecycle defect:
  - `BiomeMatrixDirector`, `HectonAtmosphereManager`, `WorldReadabilityDirector`, `FirstHourDirector`, and `SoundscapeSystem` all used the same fragile unregister pattern that only cleared their tick-registration flag if `GameTickManager.Instance` was still alive
  - this is the same teardown-order bug already removed from `AcousticZoneController` and the LOD/runtime managers
- Corrective changes applied:
  - `Assets/_Project/Scripts/BiomeMatrixDirector.cs`
    - added deterministic `TryRegister()` / `TryUnregister()`
    - `OnEnable`, `Start`, `OnDisable`, and `OnDestroy` now leave registration state consistent
  - `Assets/_Project/Scripts/HectonAtmosphereManager.cs`
    - added deterministic `TryRegister()` / `TryUnregister()`
    - start-up error path remains intact if `GameTickManager` is still absent at `Start()`
  - `Assets/_Project/Scripts/World/WorldReadabilityDirector.cs`
    - added deterministic `TryRegister()` / `TryUnregister()`
  - `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
    - added deterministic `TryRegister()` / `TryUnregister()`
  - `Assets/_Project/Scripts/World/SoundscapeSystem.cs`
    - added deterministic `TryRegister()` / `TryUnregister()`
- Additional compile recovery in the same pass:
  - `Assets/_Project/Scripts/StunPistolTool.cs`
    - exposed `ResolveLocalized(...)` and `StunCategory` to the companion runtime class
    - aligned the helper with the real `LocalizationManager.GetOrFallback(...)` API
  - `Assets/_Project/Scripts/World/WorldReadabilityRuntimeBootstrap.cs`
    - replaced obsolete `FindFirstObjectByType` with `FindAnyObjectByType`
- What is actually verified:
  - the first compile after this batch surfaced the `StunPistolTool` localization/protection errors and one obsolete warning in `WorldReadabilityRuntimeBootstrap`
  - after the fixes, Unity Console readback returned `0` log entries on a fresh compile
- What is not verified:
  - play-mode scene reload proving these adjacent managers survive bad teardown order at runtime
  - readability/atmosphere/soundscape behavior under long traversal
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - runtime proof still does not

### 2026-04-16 - Atmosphere / music / atlas / readability lifecycle hardening pass

- The next code-first pass targeted adjacent ambience and discovery owners that still carried the same teardown-order defect:
  - `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`
  - `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`
  - `Assets/_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs`
  - `Assets/_Project/Scripts/AtlasSignal/AtlasSignalDecoder.cs`
  - `Assets/_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs`
  - `Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs`
- Confirmed defect pattern:
  - these owners still left `_registered` / `_registeredTick` / `_registeredSlowTick` set when `GameTickManager` died first
  - several singleton-like owners also did not clear their `Instance` reference on destroy
- Corrective changes applied:
  - `HectonSurfaceWeatherDirector`
    - added deterministic `TryUnregisterTickManagers()`
    - `OnDisable` and `OnDestroy` now clear tick registration state even if `GameTickManager` is already gone
  - `HectonMusicDirector`
    - added deterministic `TryUnregisterTickHandlers()`
    - `TryRegisterTickHandlers()` now resolves `GameTickManager` once per call instead of repeatedly through the singleton accessor
    - `OnDisable` and `OnDestroy` now clear tick registration state deterministically
  - `AtlasSignalSystem`
    - replaced inline tick registration with `TryRegister()` / `TryUnregister()`
    - added `OnDestroy` cleanup for both registration state and `Instance`
  - `AtlasSignalDecoder`
    - replaced inline tick registration with `TryRegister()` / `TryUnregister()`
    - added `OnDestroy` cleanup for both registration state and `Instance`
  - `Atlas6DirectiveSystem`
    - replaced inline tick registration with `TryRegister()` / `TryUnregister()`
    - added `OnDestroy` cleanup for both registration state and `Instance`
  - `AudioLogSystem`
    - replaced inline tick registration with `TryRegister()` / `TryUnregister()`
    - added `OnDestroy` cleanup for both registration state and `Instance`
- Readability fail-safe was also hardened:
  - `Assets/_Project/Scripts/World/WorldReadabilityDirector.cs`
    - gained internal `ApplyRuntimeDependencies(...)`
  - `Assets/_Project/Scripts/World/WorldReadabilityRuntimeBootstrap.cs`
    - now configures existing or newly spawned `WorldReadabilityDirector` instances with explicit runtime `WorldZoneDirector` / `BiomeMatrixDirector` dependencies
    - this avoids spawning or leaving a readability owner in a half-wired state during runtime fail-safe recovery
- Important branch-truth note:
  - some atlas/audio-log files already contained unrelated in-flight edits before this pass
  - this pass only hardened lifecycle and owner cleanup; it did not author new atlas gameplay behavior or claim those unrelated diffs as verified work
- What is actually verified:
  - filesystem diff and code review confirm the lifecycle hardening is present in the targeted owners
- What is not verified:
  - Unity compile/read_console for this exact pass
  - runtime reload/traversal behavior after these changes
  - whether `WorldReadabilityDirector` is now authored correctly in `02_HECTON_WORLD`; scene authoring still needs live MCP recovery
- Status remains `PENDING VERIFICATION`:
  - editor reconnect timed out repeatedly during this pass
  - no fresh Console proof was captured after these edits

### 2026-04-16 - Narrative director lifecycle hardening pass

- Another runtime audit found the same teardown-order defect in narrative/gameplay owners:
  - `Assets/_Project/Scripts/HectonNarrativeDirector.cs`
  - `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs`
  - `Assets/_Project/Scripts/Gameplay/EndingSystem.cs`
  - `Assets/_Project/Scripts/Narrative/CorporateOrderSystem.cs`
- Corrective changes applied:
  - all four owners now use deterministic `TryRegister()` / `TryUnregister()` instead of clearing registration state only when `GameTickManager.Instance` still exists
  - all four owners now clear their singleton/static `Instance` reference on destroy
- Important branch-truth note:
  - `HectonNarrativeDirector.cs` already had unrelated in-flight gameplay changes in the dirty branch before this pass
  - this pass only hardened lifecycle/instance cleanup; it did not claim or verify those unrelated narrative behavior edits
- What is actually verified:
  - filesystem diff confirms the lifecycle hardening is present in the four targeted owners
- What is not verified:
  - Unity compile/read_console for this exact pass
  - long-session narrative/event/endgame traversal after bad teardown order
- Status remains `PENDING VERIFICATION`:
  - Editor reconnect was still unstable
  - no fresh Console proof was captured after these edits

### 2026-04-16 - Player / quest / PDA lifecycle hardening pass

- The same teardown-order defect was still present in a smaller player-facing cluster:
  - `Assets/_Project/Scripts/PlayerThrusterAudio.cs`
  - `Assets/_Project/Scripts/Quest/QuestManager.cs`
  - `Assets/_Project/Scripts/UI/PDAAtlasSignalTab.cs`
- Corrective changes applied:
  - all three owners now use deterministic `TryRegister()` / `TryUnregister()` instead of only clearing registration state when `GameTickManager.Instance` survives teardown
  - `PlayerThrusterAudio` and `PDAAtlasSignalTab` now also call `TryUnregister()` from `OnDestroy()`
  - `QuestManager` now clears `Instance` on destroy as well as its tick registration state
- Important branch-truth note:
  - these files already contained unrelated in-flight changes in the dirty branch before this pass
  - this pass only hardened lifecycle/cleanup and did not claim unrelated audio/HUD/quest behavior changes as new verified work
- What is actually verified:
  - filesystem diff confirms the lifecycle hardening is present in the three targeted owners
- What is not verified:
  - Unity compile/read_console for this exact pass
  - gameplay traversal for thruster audio, quest progression, or PDA atlas tab behavior after reload
- Status remains `PENDING VERIFICATION`:
  - no live Console proof was captured after these edits

### 2026-04-17 - Player / UI / depth / eclipse lifecycle hardening pass

- Continued the same teardown-order recovery pattern across another runtime-facing cluster:
  - `Assets/_Project/Scripts/UI/LoadingTipsDisplay.cs`
  - `Assets/_Project/Scripts/UI/PDADataLogTab.cs`
  - `Assets/_Project/Scripts/UI/PauseMenuController.cs`
  - `Assets/_Project/Scripts/UI/SettingsLivePreview.cs`
  - `Assets/_Project/Scripts/World/DepthZoneDirector.cs`
  - `Assets/_Project/Scripts/World/HectonBiolumController.cs`
  - `Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs`
- Confirmed defect pattern:
  - these owners still relied on `GameTickManager.Instance` surviving teardown before clearing `_registered`
  - the singleton-style world owners in this cluster also did not deterministically clear `Instance` on destroy
- Corrective changes applied:
  - all seven files now use deterministic `TryRegister()` / `TryUnregister()` helpers
  - `OnDisable()` paths now clear registration state even when `GameTickManager` is already gone
  - `LoadingTipsDisplay`, `PDADataLogTab`, `PauseMenuController`, and `SettingsLivePreview` now call `TryUnregister()` from `OnDestroy()`
  - `DepthZoneDirector`, `HectonBiolumController`, and `EclipseGameplaySystem` now call `TryUnregister()` from `OnDestroy()` and also clear their singleton/static `Instance` reference when they own it
- Important branch-truth note:
  - several UI files in this pass already had unrelated in-flight changes in the dirty branch before lifecycle cleanup continued
  - this pass only hardened registration / teardown ownership and does not claim those unrelated UI behavior diffs as newly verified work
- What is actually verified:
  - filesystem diff confirms the lifecycle hardening exists in the seven targeted owners
  - a fresh Unity script compile after this pass completed with `0` Console entries
  - after clearing Console and refreshing assets, generic `The referenced script (Unknown) on this Behaviour is missing!` spam no longer reproduced
- What is not verified:
  - play-mode reload/traversal proving these owners survive bad teardown order in runtime
  - gameplay correctness for pause menu, PDA data log, loading tips, depth transitions, eclipse, or global biolum after these edits
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - gameplay/runtime traversal proof still does not

### 2026-04-17 - Missing-script asset cleanup / console recovery pass

- Live Console recovery exposed two separate truths:
  - the current script pass was not the source of compile failure
  - the editor still surfaced generic missing-script spam and the already-known `AcousticZoneController` snapshot authoring debt
- What was executed through first-party editor tooling:
  - `Tools/Hecton/Dev/Scene/Remove Missing Scripts In Loaded Scenes`
    - result: `No missing scripts found in loaded scenes.`
  - `Tools/Hecton/Dev/Scene/Remove Missing Scripts In _Project Prefabs`
    - during prefab probing Unity surfaced a broken text PPtr while loading `Assets/_Project/Prefabs/Suit_HUD_Canvas.prefab`
    - Console message: `Component at index 4 could not be loaded when loading game object 'Suit_HUD_Canvas'. Removing it.`
- Important branch-truth note:
  - no on-disk diff was captured for `Suit_HUD_Canvas.prefab` from this pass
  - this means the broken reference was observed live in the editor load path, but persistence of that exact cleanup on disk is not proven by file diff
- What is actually verified:
  - after clearing Console and refreshing assets, Unity Console returned `0` entries
  - this removed the previously reproduced generic missing-script spam from the current editor state
- What is not verified:
  - whether `Suit_HUD_Canvas.prefab` still contains latent asset corruption that only repros on a different import/reload path
  - whether more prefab-level missing-script debt exists outside the currently loaded/editor-touched path
- Status remains `PENDING VERIFICATION`:
  - editor state is currently clean
  - asset-level permanence still needs future reimport/open-cycle confirmation

### 2026-04-17 - Core owner lifecycle hardening pass

- Continued the deterministic teardown-order recovery into core runtime owners:
  - `Assets/_Project/Scripts/AmbientWaterMotionManager.cs`
  - `Assets/_Project/Scripts/BaseModule.cs`
  - `Assets/_Project/Scripts/Fabricator.cs`
  - `Assets/_Project/Scripts/HectonFloatingOrigin.cs`
  - `Assets/_Project/Scripts/PowerGridManager.cs`
  - `Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs`
- Confirmed defect pattern:
  - these owners still used direct `GameTickManager.Instance` registration/unregistration or null-conditional register/unregister calls
  - in bad teardown order that leaves registration state ambiguous and can strand systems in an enabled-but-not-ticking state after reload
- Corrective changes applied:
  - all six files now use deterministic `TryRegister()` / `TryUnregister()` helpers
  - `AmbientWaterMotionManager`, `BaseModule`, and `Fabricator` now carry explicit tick-registration state instead of relying on bare null-conditional calls
  - `OnDestroy()` paths in this cluster now also force unregister, not only `OnDisable()`
  - `HectonFloatingOrigin`, `PowerGridManager`, and `HectonBiolumManager` now clear tick-registration state deterministically even if `GameTickManager` is already gone
- Important branch-truth note:
  - this pass intentionally did not rewrite larger architecture debt that is still visible in the same files, for example existing `DontDestroyOnLoad` usage in `PowerGridManager`
  - the work here is lifecycle hardening only, not a claim that those owners are otherwise fully compliant with every repository rule
- What is actually verified:
  - filesystem diff confirms the lifecycle hardening exists in the six targeted owners
  - Unity performed a fresh script compile after this pass without surfacing project script errors
  - after clearing MCP transport noise, Unity Console returned `0` entries
- What is not verified:
  - play-mode traversal proving these core owners survive scene reload/domain teardown in runtime
  - gameplay correctness for ambient motion, base interiors, fabricator crafting cadence, floating-origin shifts, power-grid balancing, or biolum manager behavior under long sessions
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - runtime traversal proof still does not

### 2026-04-17 - Gameplay power / interactable compile-blocker recovery

- Fresh compile truth exposed three real API/contract drifts in the gameplay-power cluster:
  - `Assets/_Project/Scripts/Gameplay/BatteryCharger.cs` was still calling a non-existent `PlayerToolManager.Instance`
  - `Assets/_Project/Scripts/Gameplay/BioReactor.cs` still referenced invalid `ItemCategory.Organic`, which does not exist in the real `ItemData` contract
  - `Assets/_Project/Scripts/Gameplay/DeployableBeacon.cs` had drift between its class declaration and tick-registration path; the file was also still using a hardcoded shader property ID path instead of the serialized property name
- Corrective changes applied:
  - `BatteryCharger.cs`
    - interaction path now resolves `PlayerToolManager` and `PlayerInventory` through owner-safe lazy helpers instead of the invented singleton
    - fallback inventory lookup now uses `Object.FindAnyObjectByType<PlayerInventory>()` in the non-hot interaction path
    - cached runtime refs for tool manager / player inventory were added to avoid repeated global resolve after first hit
  - `BioReactor.cs`
    - default accepted category array now matches the real enum contract (`ItemCategory.Material`)
    - fuel acceptance now also honours `ResourceFamily.Organic`, which preserves the intended bio-fuel behavior without inventing a fake enum value
    - interaction fallback inventory lookup moved off obsolete `FindFirstObjectByType`
  - `DeployableBeacon.cs`
    - file is now self-consistent around `ITickable` + `IFixedTickable` registration
    - deterministic `TryRegisterTickSystems()` / `TryUnregisterTickSystems()` landed
    - `OnDestroy()` now forces unregister
    - serialized `emissionProperty` is now respected through a cached runtime property ID
- What is actually verified:
  - after this recovery pass, a fresh Unity compile completed with `0` Console entries
  - the intermediate `HectonCelestialEngine` symbol errors were confirmed to be stale editor-reload noise; the same file on disk already contained the referenced fields and a forced follow-up compile returned clean
- What is not verified:
  - runtime behavior for charger interaction, reactor fuel acceptance, or deployable-beacon blink / buoyancy / registry behavior
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - runtime traversal proof still does not

### 2026-04-17 - Warning-debt cleanup pass (storage / door / floater)

- After the gameplay-power compile recovery, the next bounded debt cluster was warning-only:
  - `Assets/_Project/Scripts/Gameplay/StorageCrate.cs`
  - `Assets/_Project/Scripts/Gameplay/SealedDoor.cs`
  - `Assets/_Project/Scripts/Gameplay/Floater.cs`
- Confirmed defect pattern:
  - `StorageCrate` and `SealedDoor` both exposed serialized trigger-name fields but still hardcoded animator hashes, making the serialized authoring misleading and causing warning debt
  - `Floater` exposed `applyInFixedUpdate` but did not use it in runtime
  - `SealedDoor` and `Floater` still had teardown-order risk because unregister only fully completed if `GameTickManager.Instance` survived
- Corrective changes applied on disk:
  - `StorageCrate.cs`
    - open/close animator hashes now resolve from serialized trigger-name fields in `Awake()`
  - `SealedDoor.cs`
    - open trigger hash now resolves from serialized authoring instead of a hardcoded static hash
    - `OnDestroy()` now forces tick unregister
    - unregister now clears `_isRegistered` even if `GameTickManager` is already gone
  - `Floater.cs`
    - `applyInFixedUpdate` now actually gates buoyancy force application
    - fixed-tick registration now uses direct `this` instead of unnecessary interface-cast syntax
    - `OnDestroy()` now forces unregister
    - unregister now clears `_isRegistered` even if `GameTickManager` is already gone
- What is actually verified:
  - code-side diff only
  - Unity verification is currently absent because the MCP bridge timed out during reload and then stopped answering `read_console`
- What is not verified:
  - fresh compile after this exact warning-debt pass
  - runtime behavior for crate animation authoring, sealed-door cutting lifecycle, or floater buoyancy semantics
- Status remains `PENDING VERIFICATION`:
  - measured / logged proof absent for this exact sub-pass

### 2026-04-17 - Optimization manager lifecycle hardening + localization registry recovery

- Continued deterministic teardown-order recovery into the optimisation / RT budget cluster:
  - `Assets/_Project/Scripts/Optimization/CameraRTManager.cs`
  - `Assets/_Project/Scripts/Optimization/PostFXRTManager.cs`
  - `Assets/_Project/Scripts/Optimization/UIRTManager.cs`
  - `Assets/_Project/Scripts/Optimization/VRAMMonitor.cs`
  - `Assets/_Project/Scripts/Optimization/VisorRTManager.cs`
  - `Assets/_Project/Scripts/Optimization/RenderTextureLifecycleTracker.cs`
- Confirmed defect pattern:
  - all six owners still relied on direct `GameTickManager.Instance` register/unregister in `OnEnable()` / `OnDisable()`
  - in bad teardown order that can leave `_registeredSlowTick` stuck true and strand the manager in an enabled-but-not-ticking state after reload
- Corrective changes applied:
  - all six files now use deterministic `TryRegister()` / `TryUnregister()` helpers
  - `OnDestroy()` in the cluster now also forces unregister instead of trusting `GameTickManager` lifetime
  - `VRAMMonitor` keeps its profiler-recorder disposal path but now clears slow-tick registration first
  - `RenderTextureLifecycleTracker` keeps existing leak-audit logic and only hardens lifecycle ownership
- Adjacent compile-blocker recovery was required:
  - Unity first surfaced stale `KnifeTool` / `LocalizationKeys` failures
  - root cause was not the optimisation edits themselves; the real blocker was a corrupted `LocalizationKeys.cs` registry containing repeated `KNIFE_*` constant blocks inserted multiple times by prior agent drift
  - `LocalizationKeys.cs` was structurally deduplicated so each `public const string` now exists once, keeping the first occurrence and removing later repeated knife-key copies
  - one canonical `KNIFE_*` block is now present on disk and `KnifeTool` compiles against it again
- Important branch-truth note:
  - the localisation repair was not cosmetic; without it Unity could not complete a clean compile
  - this pass does not claim the entire `LocalizationKeys.cs` registry is elegantly organised, only that duplicate constant drift was removed and the current compile is clean
- What is actually verified:
  - filesystem diff confirms lifecycle hardening exists in the six optimisation owners
  - filesystem diff confirms `LocalizationKeys.cs` now contains one deduplicated `KNIFE_*` key set instead of repeated copies
  - after Unity recovered from disconnect/retry, a fresh script refresh/compile returned `0` Console entries
- What is not verified:
  - play-mode traversal proving the optimisation managers survive scene reload/domain teardown in runtime
  - runtime behavior of VRAM / RT budget warnings after these lifecycle edits
  - semantic correctness of every localisation key consumer outside the now-clean compile path
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - runtime traversal proof still does not

### 2026-04-17 - UI utility lifecycle verification pass

- Closed the verification gap left by the previous code-only UI utility cleanup:
  - `Assets/_Project/Scripts/UI/LoadingScreenController.cs`
  - `Assets/_Project/Scripts/UI/SaveSlotHoverPreview.cs`
  - `Assets/_Project/Scripts/UI/SettingsPanelAnimator.cs`
  - `Assets/_Project/Scripts/UI/UIFadeTransition.cs`
  - `Assets/_Project/Scripts/UI/UIScreenShake.cs`
  - `Assets/_Project/Scripts/UI/SettingsComparisonView.cs`
- Confirmed defect pattern:
  - these UI runtime owners previously only cleared `_registered` when `GameTickManager.Instance` still survived teardown
  - that leaves the systems vulnerable to the already-confirmed enabled-but-not-ticking state after bad scene/domain teardown order
- Corrective changes already on disk and now compile-verified:
  - all six files have deterministic unregister paths
  - all six files now also force unregister from `OnDestroy()` instead of relying only on `OnDisable()`
- What is actually verified:
  - Unity completed a fresh script compile after these UI lifecycle edits with no project script errors
  - the later compile cycle also remained error-free after adjacent hazard recovery work landed
- What is not verified:
  - play-mode reload traversal for loading screen fades, hover preview timing, settings comparison view, panel animation, fade transition, or screen shake cadence
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - runtime traversal proof still does not

### 2026-04-17 - Hazard owner recovery + small runtime lifecycle cluster

- Adjacent compile-blocker recovery exposed architecture drift in the hazard layer:
  - `Assets/_Project/Scripts/Gameplay/HazardType.cs` already existed as the canonical enum owner
  - `Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs` had drifted into a second incompatible `HazardType` definition and was still using alloc-heavy `Physics.OverlapSphere`
  - legacy `RadiationHazard` / `ToxinHazard` scripts were remnants of an older inheritance design and no longer matched the real `EnvironmentalHazard` owner
- Corrective changes applied:
  - `EnvironmentalHazard.cs`
    - removed the duplicate local `HazardType` enum and now uses the canonical enum owner
    - integrated `HazardExposureNotifier` directly into the real owner on enter/exit/teardown transitions
    - switched radius detection from `Physics.OverlapSphere` to `Physics.OverlapSphereNonAlloc` with a preallocated collider buffer
    - added deterministic `TryRegister()` / `TryUnregister()` plus teardown-safe state clearing in `OnDisable()` / `OnDestroy()`
    - aligned inspector hazard-name handling to canonical enum values (`Toxicity`, `Biohazard`)
  - `RadiationHazard.cs` and `ToxinHazard.cs`
    - retained as compatibility wrapper components instead of the broken inheritance chain, because Unity's current assembly graph still expects those source paths
- Continued the same deterministic lifecycle hardening into the next small runtime cluster:
  - `Assets/_Project/Scripts/BeaconRuntime.cs`
  - `Assets/_Project/Scripts/GasGiantRotationDriver.cs`
  - `Assets/_Project/Scripts/Gameplay/HectonHazardSource.cs`
  - `Assets/_Project/Scripts/World/FloraInteractionManager.cs`
  - `Assets/_Project/Scripts/WorldProceduralFillDirector.cs`
  - `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`
  - `Assets/_Project/Scripts/WorldGenerativeGeologySeamExecutionDirector.cs`
- Corrective changes applied in that cluster:
  - all seven owners now clear registration state deterministically even when `GameTickManager` is already gone
  - `OnDestroy()` in this cluster now also forces unregister, not only `OnDisable()`
  - duplicated `OnEnable()` / `Start()` registration branches in the world-generation owners were collapsed into single `TryRegister()` helpers
- What is actually verified:
  - Unity compile after the hazard recovery no longer reported the previous `HazardType` / sealed inheritance blockers
  - Unity compile after the runtime-cluster hardening completed with no project script errors; only pre-existing warning noise remains
- What is not verified:
  - gameplay/runtime traversal for environmental hazards, beacon flicker, gas giant material animation, flora interaction, procedural fill, terrain seam application, or seam execution
  - the assembly-graph reason Unity still expects `RadiationHazard.cs` / `ToxinHazard.cs` source paths beyond current compatibility stubs
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - runtime traversal proof still does not

### 2026-04-17 - Player / buoyancy / entity-change teardown pass + local warning cleanup

- Continued the deterministic teardown-order recovery into another bounded runtime cluster:
  - `Assets/_Project/Scripts/BuoyancyObject.cs`
  - `Assets/_Project/Scripts/EntityChangeDetector.cs`
  - `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs`
  - `Assets/_Project/Scripts/LandingImpactVFX.cs`
- Confirmed defect pattern:
  - these owners still had direct unregister calls that only cleared registration state when `GameTickManager.Instance` survived teardown
  - `HectonPlayerHealth` still duplicated register logic across `OnEnable()` and `Start()` instead of using one guarded owner path
  - `EntityChangeManager` still contains separate design debt (`DontDestroyOnLoad`) that was intentionally not rewritten in this pass
- Corrective changes applied:
  - `BuoyancyObject` now routes fixed-tick teardown through `TryUnregisterFromFixedTick()` and also forces that cleanup from `OnDestroy()`
  - `EntityChangeManager` now uses deterministic `TryRegister()` / `TryUnregister()` and clears `_instance` in `OnDestroy()`
  - `HectonPlayerHealth` now uses one guarded tick-registration path plus deterministic unregister in both `OnDisable()` and `OnDestroy()`
  - `LandingImpactVFX` now clears registration state deterministically even if `GameTickManager` is already gone
- Local warning cleanup applied to the already-touched hazard files:
  - `EnvironmentalHazard.cs` now actually uses the serialized `emissionProperty` through a cached runtime property ID instead of ignoring it
  - `RadiationHazard.cs` and `ToxinHazard.cs` now explicitly suppress their retained placeholder serialized-field warnings, because those compatibility fields are intentionally parked for future subsystem wiring
- What is actually verified:
  - Unity completed a fresh compile after this pass with no project script errors
  - warning count dropped again after the local hazard cleanup, and the new/adjacent hazard warnings introduced during recovery are no longer present
- What is not verified:
  - play-mode traversal for buoyancy grounding, entity-change manager lifetime, player-health tick lifetime, or landing-impact post effects
  - runtime semantic correctness of the retained `EntityChangeManager` singleton design with its existing `DontDestroyOnLoad` behavior
- Status remains `PENDING VERIFICATION`:
  - compile proof exists
  - runtime traversal proof still does not
