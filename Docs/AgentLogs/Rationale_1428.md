# Rationale 1428

Problem: `00_BOOTSTRAP` GameView showed cyan/amber rectangles where bootstrap text should be readable.
Solution: Scene YAML inspection found three legacy `TextMesh` objects with `m_Font: {fileID: 0}` and ordinary URP geometry materials. Assigned existing project font `Share Tech Mono` (`tekst.ttf`) plus its font material to the three text renderers. This avoids C# recompilation and keeps the fallback cheap.
Rejected Alternatives: Converting the scene to TMP through a new editor script would add another compile pass while Unity was already busy. Leaving runtime TMP fallback alone would not fix the open scene/PlayMode-off visual failure the user was seeing.
Scalability potential: On weak devices this remains three static text meshes and existing cube geometry; on high-tier devices the runtime TMP fallback can still provide richer boot presentation during Play Mode.
Hardware Impact: No runtime polling, no hot allocations, no extra render targets. Expected CPU impact is effectively zero; visual correctness improves immediately after asset refresh.

Problem: Verified GameView screenshot showed `H8_Boot_Status` readable but intersecting the animated/symbolic signal bars.
Solution: Moved the status text upward inside the core panel, reduced character size from `0.032` to `0.03`, and moved it closer to the bootstrap camera plane. This preserves the cheap static fallback and fixes the immediate readability defect without adding scripts or UI canvases.
Rejected Alternatives: Rebuilding the fallback as TMP/UI Toolkit during a compile stabilization pass would add unnecessary import and script churn. Moving all signal bars would risk breaking the authored composition more than the single offending label.
Scalability potential: Low and handheld lanes keep a three-TextMesh static fallback; higher lanes can still use runtime TMP/presentation layers after PlayMode is stable.
Hardware Impact: Scene transform-only edit. No runtime CPU cost, no new materials, no new assets, no heap allocations.

Problem: `BeaconHUDElement.cs` was left in a non-compilable state by a partial object-pool rewrite: cold slot allocation was moved out of `Awake`, but pool fields/method bodies were inconsistent and compiler saw methods as top-level declarations.
Solution: Restored a single deterministic cold path: allocate 16 icon displays in `Awake`, instantiate the authored prefab under the icon container, add `CanvasGroup` only if missing, disable raycast targets, then only update transforms/alpha/text in `LateFrameTick`.
Rejected Alternatives: Keeping the half-integrated pool path would make HUD visibility depend on prewarmed pool authoring during bootstrap and menu bring-up. That is not acceptable while compile/editor stability is still recovering.
Scalability potential: Low devices pay one cold UI setup cost and get no per-frame prefab/pool recovery polling. Higher tiers can later replace the cold setup with an authored pool only after the prefab warmup contract is validated.
Hardware Impact: Hot path stays cached; no `GlobalRegistry.Get<T>()`, `GetComponent()`, prefab spawn, or pool recovery inside `LateFrameTick`.

Problem: Editor quit log reported persistent `GraphicsBuffer` leaks from visor/soot/retina URP renderer features allocated during `ScriptableRendererFeature.Create()` triggered by `OnValidate`.
Solution: Removed eager buffer prewarm from `Create`; buffers now allocate only when a real render pass needs them and existing `Dispose` paths remain the owner for release.
Rejected Alternatives: Disabling the features would hide the leak but remove important deep-sea visor presentation. Leaving eager prewarm would keep leaking on asset validation/import cycles and destabilize Editor restarts.
Scalability potential: Weak devices avoid GPU buffer allocation until the effect is active. High-end devices still receive the full effect once the pass executes.
Hardware Impact: Editor import/validation no longer allocates persistent GPU buffers just by touching renderer feature assets; runtime allocation is delayed to the actual active visual path.

Problem: `BeaconHUDElement` was overwritten back into a hybrid state: serialized `_uiIconPool`, cached `ObjectPoolService`, retry timers, despawn/recover methods, and display slots that only owned char buffers. That could leave the HUD with no visual icon instances while also adding a hot recovery branch.
Solution: Removed the unfinished pool integration from this HUD only. `Awake` now precreates each icon instance under the authored container, caches `Transform`, `CanvasGroup`, label/distance TMP references, disables graphic raycasts, and hides the slot. `LateFrameTick` now only reads cached services and applies presentation state.
Rejected Alternatives: Keeping a pool recovery path would require an authored warmup contract and runtime pool availability proof that the current scene does not provide. Retrying pool lookup every frame while HUD is visible is the wrong dependency direction for a tiny fixed 16-slot overlay.
Scalability potential: Weak and middle devices pay one cold setup cost for 16 static UI objects. High and ultra lanes can still replace the prefab visuals with richer materials/text effects without changing the timing contract.
Hardware Impact: Removes `ObjectPoolService` dependency, retry timer, and despawn/recover branch from HUD runtime flow. Hot path remains cached reference updates and no service lookups.

Problem: Static scan found more visor renderer features allocating `GraphicsBuffer` resources or DataVault handles from `ScriptableRendererFeature.Create()`. That matches the Editor quit leak class already seen in `Editor.log`: import/validation can run `Create()` without a stable render lifetime.
Solution: Removed `PrepareResources()`/`PrewarmBuffers()`/`TryEnsureVaultBuffers()` from the `Create()` path of stochastic SSR, scooter shafts, depth fog, half-res particles, VR brownout, and visor AR stencil. Resource creation now happens only after camera/type/gate checks inside `AddRenderPasses`.
Rejected Alternatives: Prewarming GPU buffers in `Create()` gives no proof of a real camera render and leaks under asset validation. Disabling the features would hide the symptom while stripping major visor/abyss presentation.
Scalability potential: Low devices avoid allocating optional visor buffers until the effect is active. Middle/high/ultra lanes still get the full presentation when their render path admits the pass.
Hardware Impact: Static proof: `NO_VISOR_RENDER_FEATURE_CREATE_ONVALIDATE_BUFFER_HITS`. Expected Editor stability gain is fewer persistent GraphicsBuffer allocations during import and fewer GPU resources surviving failed/aborted render feature lifetimes.

Problem: The project needed a no-Unity verification pass while the Editor is intentionally not launched.
Solution: Ran a method-body static scan over non-Editor `Tick`, `FixedTick`, `LateFrameTick`, and `Execute` declarations for target hot-path tokens: `GlobalRegistry`, `GetComponent`, `Camera.main`, scene search, text formatting, `.text =`, `SetText`, and managed container creation.
Rejected Alternatives: Broad `rg` output over the full repository was too noisy because `Awake`, `OnEnable`, Editor windows, and calls to `.Execute()` polluted the evidence. The method-body scan is narrower and defensible.
Scalability potential: Confirms current touched runtime paths are not adding hot lookup or GC hazards while Unity stays offline.
Hardware Impact: Result was `NO_RUNTIME_HOTPATH_TOKEN_HITS` for the target token set after excluding Editor code.

Problem: `00_BOOTSTRAP` proved that scene YAML can contain visible text with invalid rendering references. The same class had to be checked across production scenes without launching Unity.
Solution: Scanned production scene/prefab YAML for `m_Font: {fileID: 0}`, `m_fontAsset: {fileID: 0}`, and `m_Script: {fileID: 0}`. The focused scan returned no hits. Broader `m_Material: {fileID: 0}` was rejected as evidence because TMP/uGUI/default renderer entries legitimately serialize null material slots.
Rejected Alternatives: Bulk-assigning materials to every null `m_Material` would corrupt authored defaults and generated proxies. Visual screenshot review still remains required when Unity is allowed again.
Scalability potential: Static font/script integrity keeps menu/bootstrap/world readable across device tiers; no runtime fallback patching needed.
Hardware Impact: No runtime change. Prevents another rectangle-text failure class without adding code or assets.

Problem: Editor smoke tests had a stale route assertion requiring `newGameTargetSceneName` to be `01_ORBIT`, while current root authority states `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD` and that `01_ORBIT` is not the main handoff.
Solution: Updated only the Editor smoke assertions and method name to validate `02_HECTON_WORLD`, matching `MainMenuController` defaults and `01_MAIN_MENU.unity` serialization.
Rejected Alternatives: Changing runtime/scene routing to orbit would violate the current root route contract and make a stale test define product flow.
Scalability potential: Keeps orbit functional as a standalone/prologue target while preventing false negative test failures in the main menu/world route.
Hardware Impact: Editor-only assertion change. Runtime cost is zero.

Problem: `BeaconHUDElement` was previously overwritten by a parallel writer back into an unfinished ObjectPool contract after the first repair.
Solution: Re-applied the deterministic HUD path and verified it survived a timed re-scan. The file now has no `_uiIconPool`, `ObjectPoolService`, pool owner, retry timer, recover, despawn, or poolOwner tokens. Icon instances are created once from the authored prefab under the icon container and hidden through cached `CanvasGroup` references.
Rejected Alternatives: Fighting this by adding a second pool fallback would preserve the broken dependency direction. A fixed 16-slot HUD overlay does not justify runtime pool lookup or retry logic.
Scalability potential: Weak, middle, high, and ultra lanes keep the same truth route and can scale only the authored icon visuals/materials. The runtime path stays cached reference presentation.
Hardware Impact: Removes one hot branch and all pool recovery state from `LateFrameTick`; no service lookup, prefab spawn, or pool mutation remains in the HUD update loop.

Problem: `HectonVisorUberPostFeature.Create()` still performed Play Mode noir/reconstruction GPU buffer allocation and DataVault handle ensure before any real camera render admission.
Solution: Removed `EnsureNoirConstantsBuffersCold`, `EnsureReconstructionConstantsBufferCold`, `EnsureNoirVaultHandles`, and `EnsureReconstructionVaultHandles` from `Create()`. `AddRenderPasses()` already gates those calls by play mode, camera type, base camera, material readiness, and pass mode.
Rejected Alternatives: Keeping early allocation makes renderer feature lifecycle harder to reason about after the observed GraphicsBuffer leak class. Disabling noir/reconstruction would remove important visual identity instead of fixing ownership timing.
Scalability potential: Low devices avoid optional GPU storage until the effect is actually admitted by a camera pass. Higher devices still get full noir/reconstruction once the render path is live.
Hardware Impact: No GPU buffer or DataVault buffer ensure from `Create()` for this feature. First admitted render pass owns storage creation; `LateFrameTick` only uploads constants when buffers are already valid.

Problem: Existing `Editor.log` reported `UnityException: CreateImpl is not allowed to be called from a MonoBehaviour constructor` for `HectonScanMarkerSystem` while importing `Tool_Scanner_Held.prefab`.
Solution: Verified current `HectonScanMarkerSystem.cs` source no longer contains any `GraphicsBuffer` creation. A narrow field-initializer scan over first-party scripts found no direct `new GraphicsBuffer`, `new ComputeBuffer`, `new RenderTexture`, `new Material`, or `new Mesh` field initializer; only reference arrays and indirect-args value arrays remain.
Rejected Alternatives: Editing `HectonScanMarkerSystem` blindly would risk reverting a parallel agent's already-applied repair. The correct action in no-Unity mode is evidence capture and static proof, then runtime re-import verification when Unity is allowed.
Scalability potential: Prevents import-time GPU object construction across all device lanes; GPU resources must be owned by explicit lifecycle or render admission.
Hardware Impact: Static proof only; no new runtime cost. Runtime verification remains pending until Unity import can be run again.

Problem: User asked whether unused third-party assets should be archived outside the project, but direct movement would break Unity GUID/path assumptions unless the asset is proven unused.
Solution: Audited top-level vendor roots and first-party references. Crest, MapMagic, GPUInstancer, Shapes, and VolumetricLightBeam are referenced by first-party scenes, prefabs, data, or scripts. `Assets/_ThirdParty` is empty, while live vendor roots remain directly under `Assets`. No archive move was performed.
Rejected Alternatives: Moving vendor roots out of the project during no-Unity mode would leave missing scripts/materials/prefabs and force a Unity repair pass that the user explicitly paused. Deleting Feel/MapMagic/Crest based on folder presence alone would be destructive.
Scalability potential: Keep used vendor systems until a GUID-preserving migration route exists. Later cleanup should target samples/docs/demo folders and unused direct roots only after reference counting and Unity import proof.
Hardware Impact: No runtime change. Avoids creating missing-script faults and unnecessary import churn.

Problem: Forbidden legacy dependencies had to be checked without assuming old documentation was still true.
Solution: Verified `Assets/AstarPathfindingProject`, `Assets/Easy Save 3`, `Assets/DOTween`, and `Assets/MasterAudio` are absent. First-party Astar-like archetype flags are serialized as `useAstarPathing: 0`; pathfinding code is first-party `Hecton8.AI.Pathfinding`, not Aron Granberg A* runtime. The only MasterAudio hit is Feel's own `MasterAudioMixerGroup` field name.
Rejected Alternatives: Archiving or editing Feel because it has an internal field named `MasterAudioMixerGroup` would be a false positive. The proper action is to keep the build guard and only remove physical roots when present.
Scalability potential: Keeps audio/save/tween/pathfinding authority first-party while allowing selected Feel feedback assets if they are explicitly authored.
Hardware Impact: Static proof only; no runtime cost.

Problem: PCVR and standalone XR readiness needed static verification while Unity is offline.
Solution: Checked package manifest, XR loader assets, graphics API matrix, and OpenXR settings. `com.unity.xr.management`, `com.unity.xr.openxr`, and `com.unity.xr.meta-openxr` are installed; Windows graphics APIs are explicit DX12+DX11 and Android is explicit Vulkan. Case-sensitive OpenXR feature parsing shows enabled features are controller profiles, foveated rendering, Meta lifecycle/display utilities, and performance settings. AR mesh/anchor/camera/session, runtime debugger, mock runtime, conformance automation, and debug utils are disabled.
Rejected Alternatives: Enabling or disabling XR features blindly from YAML would be high risk without Unity validation and device target proof.
Scalability potential: The enabled feature set is narrow enough for PCVR and Quest-style lanes while avoiding AR subsystem load for a non-AR game path.
Hardware Impact: No runtime change. Confirms settings are not pulling obvious OpenXR debug/AR bloat into the active feature set.

Problem: `MainMenuAtmosphereController` had been moved away from generated primitives/materials, but the new authored-reference contract was brittle. If the scene did not serialize every reference onto the dynamically-added camera component, menu atmosphere could silently collapse or spam fatal asserts.
Solution: Added cold fixed-name discovery for the existing `H8_MENU_VISUAL_STAGE_1428` layer and selected `Stage_*` authored quads. The controller now binds backdrop, haze, optional silt strips, and edge masks from scene geometry, drives colors through `MaterialPropertyBlock`, and skips missing optional visuals without runtime generation.
Rejected Alternatives: Reintroducing `GameObject.CreatePrimitive`, `new Material`, or `Shader.Find` would bring back import/runtime ownership debt and likely leak/leak-like renderer lifecycle noise. Editing scene YAML with serialized component references while Unity is paused is less robust than name-stable cold discovery.
Scalability potential: Weak lanes keep a small authored fake with no material clones. Middle/high/ultra lanes can raise style/concept intensity through existing continuous `GlobalQualityWeight` without changing gameplay truth.
Hardware Impact: Cold scene-root lookup and MPB allocation only during menu configuration. `LateFrameTick` stays cached transform/renderer/property-block writes and no scene search.

Problem: `MainMenuValidator` still required `slotPrefab`, but `MainMenuController` now resolves three scene-owned `SaveSlotUI` entries from `slotsContainer`.
Solution: Removed `slotPrefab` from the required serialized field list.
Rejected Alternatives: Adding a dummy serialized `slotPrefab` field would restore legacy prefab-spawn semantics and conflict with the scene-owned save shell.
Scalability potential: Scene-owned save slots avoid menu-time prefab churn across all device classes.
Hardware Impact: Editor-only validator fix. Runtime cost is zero; false validation failures are reduced.

Problem: Quality settings risked conceptual drift: Unity has multiple platform/static profiles while runtime systems consume continuous `GlobalQualityWeight`.
Solution: Verified `SettingsManager.QualityLevel` maps only to `HomeostasisBrain.SetUserGlobalQualityWeightPreference`, and static Unity quality profiles remain separate from the four user graphics presets. No `QualitySettings.SetQualityLevel` path was introduced.
Rejected Alternatives: Collapsing platform profiles and user graphics presets into one enum would require UI, localization, persistence migration, and runtime proof. Doing it statically now would be a control bug, not polish.
Scalability potential: Static profiles cover platform envelopes; continuous weight still scales simulation/presentation density inside a profile.
Hardware Impact: No code change. Confirms no new binary quality switch was added.

Problem: Scene cameras serialize `m_AllowDynamicResolution: 0`, which could look like adaptive rendering is disabled.
Solution: Verified `ThermalDynamicResolutionAdapter` self-creates after scene load, installs Unity's system dynamic-resolution scaler, and toggles `camera.allowDynamicResolution` in `RenderPipelineManager.beginCameraRendering` using a cached camera shield. Also verified old `DynamicResolutionScaler` is not serialized in production scenes or `Player.prefab`.
Rejected Alternatives: Blindly flipping camera YAML would duplicate runtime ownership and would not prove STP policy, world-camera filtering, or service registration.
Scalability potential: Weak devices can lower scale and shed visual budget from a single runtime owner; high/ultra lanes preserve render scale and spend budget on visual overkill.
Hardware Impact: No runtime edit in this pass. Static proof supports keeping dynamic-resolution authority in the STP adapter rather than scattered scene flags.

Problem: `MainMenuController` used `_panelTransitionDeltaTime` as the shared LateFrame presentation delta and still described the atmosphere binder as generated layers. That naming/comment drift is exactly how earlier primitive/material generation debt can return.
Solution: Renamed the field/local to `_menuPresentationDeltaTime`/`menuPresentationDeltaTime` and corrected the atmosphere component allocation comment to "authored menu atmosphere binder".
Rejected Alternatives: Leaving misleading names in a hot UI owner invites future agents to gate atmosphere updates only during transitions or reintroduce generated primitives. Renaming public API was avoided; this is private implementation only.
Scalability potential: Same runtime behavior across weak, middle, high, and ultra lanes; clearer VISUAL_SYNC ownership keeps future visual overkill in the presentation phase.
Hardware Impact: Zero runtime cost. Static clarity only; no extra allocations.

Problem: `MainMenuController.AutoWireSceneReferences()` could restore most scene links but not `btnBackFromSettings`. If the serialized reference is lost, `DetermineSettingsAvailability()` disables the Settings button even when `Panel_Settings` and `SettingsPanel` exist.
Solution: Added a cold fallback resolver for `Btn_BackFromSettings`. Current `01_MAIN_MENU` already has the serialized reference, but the fallback prevents a false-negative Settings gate after scene authoring churn.
Rejected Alternatives: Removing the Settings availability gate would hide a real broken panel. Hard-authoring YAML now would be redundant because the scene already contains the fileID.
Scalability potential: Settings remains reachable on all device lanes, including quality/style controls that drive continuous `GlobalQualityWeight` presentation choices.
Hardware Impact: One cold recursive name lookup only if the serialized reference is missing. Hot path cost is zero.

Problem: Runtime-created Settings menu visual rows could have repeated the bootstrap "rectangle text" failure if TMP default font was unset.
Solution: Verified `Assets/TextMesh Pro/Resources/TMP Settings.asset` has default font `tsifry_SDF`, `01_MAIN_MENU` uses the same TMP font asset in existing text, and focused scene scan found no null TMP font, legacy font, or missing-script entries.
Rejected Alternatives: Adding another serialized font field to `SettingsPanel` would create new authoring burden without evidence of a null default. Blind scene edits are unnecessary while Unity is offline.
Scalability potential: Menu style/concept/accessibility rows remain cheap cold UI and readable across device classes.
Hardware Impact: Static proof only. No runtime change.

Problem: `HectonDrsRenderFeatureGate.ResolveSurvivalPressure01()` cached the DRS scaler but still fell back to `GlobalRegistry.ResolutionScaler` when the cache was empty. That path is reached from render features and violates the cold-only registry doctrine even if the static property read is cheap.
Solution: Added cold priming through `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`, renderer-feature `Create()`, and a small hot-swap listener keyed to `ResolutionScalerService`. `ResolveSurvivalPressure01()` now reads only `s_cachedScaler` and returns zero pressure if the scaler is unavailable.
Rejected Alternatives: Disabling Mobile/Low renderer features would remove adaptive visual fakes instead of fixing dependency ownership. Polling the registry once per render feature was rejected because render-path code should not decide service identity.
Scalability potential: Weak devices still shed SSDO, half-res particles, shafts, and related post effects from the cached STP scale state. Middle, high, and ultra lanes keep the same route and can spend recovered budget on visual overkill.
Hardware Impact: Removes hidden per-frame registry fallback from DRS-dependent render features. Runtime behavior remains fail-open visually when the scaler is absent and rebinds through hot-swap when the service appears.

Problem: The bootstrap text rectangle fault needed project-wide confirmation, not only a fix in `00_BOOTSTRAP`.
Solution: Scanned `Assets/**/*.unity` and `Assets/**/*.prefab` for null legacy `m_Font`, null TMP `m_fontAsset`, and missing script fileIDs. No hits were found.
Rejected Alternatives: Editing arbitrary prefab typography without a null reference would be churn and could break authored UI.
Scalability potential: Readability problems now move to runtime visual capture, localization overflow, or material/shader contrast checks rather than broken serialized font links.
Hardware Impact: Static proof only. No runtime change.

Problem: Heavy-asset streaming readiness cannot be claimed from `com.unity.addressables` package presence. Current `Assets/AddressableAssetsData` contains zero non-meta files, so there is no serialized settings/group/catalog proof.
Solution: Ran the static platform audit in JSON-to-stdout mode with report/json paths mapped to `NUL`, then confirmed the folder count directly. Runtime Addressables code paths and validators exist, but content readiness remains blocked until Unity Editor API creates/verifies real `Core`, `High_Res`, and `Overkill` groups.
Rejected Alternatives: Manually fabricating Addressables YAML while Unity is offline was rejected because Addressables settings/groups are GUID-heavy editor-owned assets. Disabling Addressables code would break the intended async heavy-content route and hide the real payload gap.
Scalability potential: Weak lanes need `Core` payloads only; middle/high/ultra lanes need `High_Res` and `Overkill` labels. The current code scaffolding supports that shape, but the serialized content database is absent.
Hardware Impact: No runtime change. Current player remains dependent on direct scene/prefab/DataMonolith content until Addressables data is populated.

Problem: DataMonolith readiness needed recheck after parallel agents changed data.
Solution: Verified `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists and is 8,212,352 bytes. The static audit reports command-line bake, prebuild gate, output validation, atomic temp-write/validate, little-endian guard, and production coverage gate tokens present.
Rejected Alternatives: Rebuilding or rebaking the blob while Unity is offline was rejected; the file exists and validation should be run through Unity/tooling only when needed.
Scalability potential: Static data can feed all device lanes without per-frame managed parsing. Optional high/ultra payloads still need Addressables content proof.
Hardware Impact: Static proof only. No runtime change.

Problem: Bootstrap recovery paths still used synchronous `SceneManager.LoadScene`, including menu handoff fallback, route enforcement, `SceneGuard`, and an entry-vector recovery branch. These paths execute exactly when the Editor/player is already in a bad route state and can convert recovery into a visible stall.
Solution: Replaced all first-party `SceneManager.LoadScene(` calls with async scene-load scheduling. The menu handoff now returns `false` if async bootstrap load cannot be scheduled, allowing the existing service/error path to handle failure. Bootstrap guards log a fail-closed error instead of forcing a blocking scene load.
Rejected Alternatives: Keeping sync fallback as "more reliable" was rejected because reliability here means preserving main-thread responsiveness and using existing watchdog/error routes. Rewriting all scene flow through a new abstraction was also rejected; `SceneRuntimeService` and `GameBootstrapper` already own the normal path.
Scalability potential: Weak devices avoid recovery-frame stalls; middle/high/ultra lanes keep identical scene truth and gain smoother recovery behavior during Editor and runtime route mistakes.
Hardware Impact: Removes every first-party synchronous scene-load call found by static scan. No hot-path allocation or new polling was introduced; only rare recovery branches changed.

Problem: `LutArrayResolver` was a `BeforeSceneLoad` cold bootstrap resolver but contained a `UnityWebRequest` staging branch that busy-waited with `Thread.Sleep(1)`. Even if the current Windows Editor path resolves a filesystem StreamingAssets file, that branch is unacceptable for portable targets and future route changes.
Solution: Removed the blocking URI cache staging path and its `System.Threading`/`UnityEngine.Networking` dependencies. When StreamingAssets resolves to a non-filesystem URI in the Editor, the resolver logs once and continues to persistent/project/fallback routes.
Rejected Alternatives: Converting this static initializer into an async boot dependency would require a larger bootstrap contract change and catalog readiness proof. Keeping the blocking web request was rejected because visual LUT loading must fail soft to the analytical fake, not stall startup.
Scalability potential: Weak and portable devices already use analytical fallback; middle/high/ultra filesystem routes still load the packed LUT from the existing binary payload.
Hardware Impact: Removes one potential boot-time main-thread sleep loop. No change to the normal Windows Editor binary file path; no new runtime allocation or polling.

Problem: `LoadAddressableDependencyChainAsync` and required UI prefab instantiation used frame-yield loops around Addressables handles without an operation watchdog. A broken label/catalog/provider could keep bootstrap alive forever while producing no fatal exit.
Solution: Added `BootstrapRequiredAddressableGateTimeoutSeconds = 15.0` and applied it to dependency-group downloads and required UI prefab handles. Timeout logs an error, releases the handle, clears the UI handle slot where applicable, and returns `false` into the existing bootstrap failure route.
Rejected Alternatives: Reusing the 2.5s tier-prewarm timeout was too aggressive for required local content. Treating required dependencies like optional prewarm and continuing was rejected because downstream services would consume missing assets.
Scalability potential: Weak devices get a bounded failure instead of endless boot; middle/high/ultra lanes still receive required content when local catalogs are valid.
Hardware Impact: Adds only cold bootstrap stopwatch checks inside existing async waits. No hot-path polling or gameplay allocations.

Problem: `01_MAIN_MENU` still contained 16 legacy `UnityEngine.UI.Text` labels bound to Unity's built-in Arial. This was not the null-font rectangle bug, but it creates a visibly weaker mixed-font shell next to TMP and the bootstrap techno fallback.
Solution: Repointed those legacy UI Text `m_Font` references to the existing project `tekst.ttf` GUID used by the corrected bootstrap text. TMP font assets and all layout values were left unchanged.
Rejected Alternatives: Converting the whole menu to TMP offline would be higher import risk and would require Unity visual proof. Leaving Arial in place was rejected because the menu is now the first product-facing screen and the change is a narrow serialized font reference swap.
Scalability potential: Same UI geometry and batching behavior across weak, middle, high, and ultra devices; only glyph style changes.
Hardware Impact: No new objects, scripts, materials, or runtime work. Static diff is 16 font reference replacements.

Problem: XR readiness claims needed a current static feature matrix, not package-name assumptions.
Solution: Parsed `Assets/XR/Settings/OpenXR Package Settings.asset` in memory. Standalone enables foveated rendering and controller profiles only. Android enables lifecycle, foveated rendering, Meta Quest support, display utilities, and controller profiles. AR, camera/passthrough, debug utils, mock runtime, runtime debugger, API layers, and conformance automation are serialized disabled.
Rejected Alternatives: Editing XR YAML without Unity validation was rejected because the current feature set is already narrow for PCVR/Quest and avoids the obvious bloat paths.
Scalability potential: Standalone PCVR keeps broad controller compatibility; Android/Quest keeps platform features needed for foveation and display handling without AR subsystem load.
Hardware Impact: Static proof only. No runtime change.

Problem: `AbyssalScatterBrgDataVaultBootstrap` loaded non-filesystem StreamingAssets URI payloads through `UnityWebRequest.Get` and then copied the full payload through `downloadHandler.data` before native ingest.
Solution: Replaced the managed-buffer route with `DownloadHandlerFile` into `Application.persistentDataPath/Hecton8/StreamingCache`, added a 30s timeout/abort path, deleted stale temp files, moved the complete temp file atomically, and reused the existing native file reader.
Rejected Alternatives: Keeping the byte-array bridge was rejected because scatter BRG payloads are data-heavy and should not create avoidable managed spikes during cold scene admission. Making the URI path blocking was also rejected.
Scalability potential: Weak and portable lanes stream to disk and fail closed on timeout; middle/high/ultra lanes keep the same native payload route and can spend memory on visual density instead of duplicate managed copies.
Hardware Impact: Removes one full managed payload copy on URI StreamingAssets routes. Hot path cost is zero; the change is cold bootstrap/slow-tick only.

Problem: `01_ORBIT` had camera MSAA enabled, dynamic resolution disabled, directional shadows active, and post intensity with a forced low-quality floor. That is the wrong baseline for a prologue/orbit scene that must scale across PC, VR, and weaker devices.
Solution: Disabled MSAA and directional shadows in scene/bootstrap, enabled camera dynamic resolution, and replaced forced bloom floor with a smooth continuous quality response.
Rejected Alternatives: Removing orbit visuals entirely would make the scene cheaper but not product-quality. Keeping shadows/MSAA as authored defaults would tax the weakest lanes before gameplay truth begins.
Scalability potential: Weak lanes keep silhouette, sky, gas giant, and overlay readability without shadow/MSAA cost; middle/high/ultra lanes regain bloom/post intensity through continuous quality.
Hardware Impact: Removes one directional shadow path and MSAA camera cost from orbit. Dynamic resolution can now be admitted by the runtime scaler when the scene is active.

Problem: User quality was described as continuous but exposed only four saved levels. Expanding `MaxContinuousQualityLevel` without separating graphic presets would corrupt settings UI and comparison math.
Solution: Added a 7-step user quality ladder (0..6) mapped to `GlobalQualityWeight` values 0.00/0.16/0.32/0.50/0.68/0.84/1.00, preserved four graphics presets through `MaxGraphicsPreset = 3`, migrated old saved 0..3 values to 1/3/4/6, and updated SettingsPanel labels plus comparison clamping.
Rejected Alternatives: Calling `QualitySettings.SetQualityLevel` from user options was rejected because Unity platform profiles are bootstrap-owned. A binary low/ultra split was rejected because HECTON-8 requires continuous quality weight.
Scalability potential: Weak lanes can drop to SURVIVAL/LOW/LEAN, middle lanes sit at MEDIUM/HIGH, and high/ultra lanes can use ULTRA/OVERKILL without changing gameplay authority or DTO layout.
Hardware Impact: Runtime application remains one scalar write to `HomeostasisBrain`; UI cost is unchanged. Avoids accidental FPS comparison underestimation after the expanded scale.

Problem: The menu-to-world cinematic transition could create a world-space blackout/terminal overlay with no camera when the serialized main-menu camera reference was missing or not configured after scene recovery.
Solution: `SceneRuntimeService` now resolves the transition camera once at cold transition start from the configured camera or the active scene's enabled camera list, then rebinds the overlay to the loaded scene camera before dissolve. `LateFrameTick` still only places cached overlay/camera references.
Rejected Alternatives: Switching the overlay to screen-space would lose the camera-relative handoff composition. Calling `Camera.main` or searching from `LateFrameTick` was rejected because scene search belongs to cold transition setup only.
Scalability potential: Weak lanes avoid black/no-camera transition windows; middle/high/ultra lanes keep the same dithered transition presentation without changing scene authority.
Hardware Impact: One cold scene-root camera scan on transition fallback only. No hot registry lookup, `Camera.main`, or scene search was added.

Problem: A parallel transition-material change made `SceneRuntimeService` require an authored `transitionDitherMaterial`, but the service is normally created at runtime by bootstrap and has no serialized asset reference.
Solution: Kept authored material support, added fallback creation from the already-registered `RuntimeShaderReferenceCatalog.sceneTransitionDitherShader`, and added explicit owner tracking so only fallback materials are destroyed at transition end.
Rejected Alternatives: Restoring `Shader.Find` was rejected because the shader catalog already exists and is registered by bootstrap. Always falling back to solid blackout was rejected because it degrades the visible transition for a recoverable missing reference.
Scalability potential: Weak lanes keep a cheap dither fake; high/ultra lanes preserve the richer transition without adding persistent material leaks.
Hardware Impact: At most one cold material allocation per transition when no authored asset is assigned, with deterministic `Destroy` on owned fallback. Hot path remains material float updates only.

Problem: `BeaconHUDElement` was again overwritten into an ObjectPool-dependent shape, with retry/recover logic in the HUD presentation route and stale compile errors in the last Editor log.
Solution: Removed the UI icon pool field, cached pool service, owner tracking, spawn/despawn/retry methods, and ObjectPool hot-swap branch. The HUD now allocates 16 fixed display slots and precreates icon prefab instances in cold lifecycle only.
Rejected Alternatives: Retrying a pool contract from `LateFrameTick` was rejected because a fixed 16-slot HUD overlay does not need runtime service negotiation. Destroying/recreating icons on disable was also rejected; visibility is enough.
Scalability potential: Weak devices get a stable fixed overlay with no pool recovery churn; high/ultra lanes can improve the prefab visuals without changing the update contract.
Hardware Impact: Removes ObjectPool service dependency and retry branch from beacon HUD runtime presentation. Static proof: no `ObjectPool`, `_iconPool`, `poolOwner`, `Spawn`, or `Despawn` tokens remain in `BeaconHUDElement.cs`.

Problem: Main menu diegetic UI depended on the serialized `mainMenuCamera` fileID. If scene recovery, merge churn, or prefab override loss clears that reference, the world-space canvas and menu atmosphere do not configure.
Solution: Added a cold active-scene camera resolver in `MainMenuController`. It first accepts the serialized camera if active, otherwise scans root objects once during setup and caches the first enabled camera into `mainMenuCamera`.
Rejected Alternatives: Using `Camera.main` was rejected because tag lookup is hidden scene search and can allocate/throw under bad tag state. Per-frame camera search was rejected; the menu already has cached LateFrame presentation state.
Scalability potential: Weak, middle, high, and ultra lanes keep the same diegetic canvas route and authored atmosphere layer even after reference loss.
Hardware Impact: One cold root-camera scan only when the serialized camera is missing. Hot path remains cached camera-relative pose sync.

Problem: `SuitHUDV4CanvasOverlay` created `MaterialPropertyBlock` in a field initializer. Unity-native object creation from MonoBehaviour construction can throw during import/domain reload before any scene logic is valid.
Solution: Moved threat-chevron MPB creation into play-mode cold resource setup and made the draw/material write path fail closed if setup did not run.
Rejected Alternatives: Keeping the initializer because it was labelled cold was rejected; constructor-time Unity-native allocation is not a safe cold phase. Creating the block from the render draw branch was rejected because that branch is presentation hot path.
Scalability potential: All device lanes keep the same instanced threat chevron visual when runtime setup succeeds; broken setup hides the optional chevrons instead of risking an Editor/runtime crash.
Hardware Impact: Removes one Unity-native constructor allocation from domain import. Hot path adds only a null guard and local cached MPB reference.

Problem: `HectonDeferredCausticsFeature.DeferredCausticsPass` created a `MaterialPropertyBlock` from its constructor. That pass is owned by a renderer feature and can be constructed during Unity renderer import/setup, where Unity-native allocation should stay out of constructors.
Solution: Moved MPB creation to pass setup, which only occurs when the feature has an authored material and is being admitted for rendering. The render func now guards a null property block before writing payload.
Rejected Alternatives: Recreating the property block per render graph execution was rejected because it would add avoidable presentation allocations. Leaving the constructor initializer was rejected for the same native construction risk that caused previous Editor crash signatures.
Scalability potential: Weak lanes can disable/degrade caustics via renderer settings without import risk; middle, high, and ultra lanes keep the baked atlas/waterline payload route when active.
Hardware Impact: Removes one Unity-native constructor allocation. First active caustics setup pays one MPB allocation; per-frame render work remains reuse-only.

Problem: The crash class needed project-wide closure, not isolated fixes.
Solution: Re-ran a first-party field-initializer scan for direct construction of `MaterialPropertyBlock`, `Material`, `Mesh`, `Texture2D`, `RenderTexture`, `GraphicsBuffer`, and `ComputeBuffer`. No direct native-object field initializer remains; remaining matches are managed arrays of Unity-object references.
Rejected Alternatives: Rewriting reference arrays was rejected because they do not create Unity-native payloads and would add churn.
Scalability potential: Import/domain reload stability improves across all target lanes without reducing visual feature availability.
Hardware Impact: Static proof only. No runtime cost.

Problem: Parallel work can reintroduce service lookup or allocation in hot loops while Unity is offline.
Solution: Ran a refined first-party non-Editor scan that enters only method signatures named `Tick`, `FixedUpdate`, `LateFrameTick`, or `Execute`, then checks for hot registry lookup, component lookup, global scene search, resource/addressable load, managed collection creation, `ToArray`, and `string.Format`. Hit count was zero.
Rejected Alternatives: A broad regex over all call sites produced false positives from cold lifecycle calls and fields. Full compiler-backed AST analysis was rejected for this pass because Unity is off and no new compiler process is needed for a pure text hot-path gate.
Scalability potential: Keeps weak lanes from hidden per-frame lookup/allocation stalls while preserving the same authority route for middle, high, and ultra lanes.
Hardware Impact: Static proof only. No runtime cost.

Problem: DataVault deadlock risk must be checked while Unity is offline, especially after parallel agents changed runtime systems.
Solution: Ran a read-only lexical source pass over non-Editor C# methods containing raw `.TryAcquireWriteLock` and `.ReleaseWriteLock` calls. The pass tracked per-method held-lock count by source order and reported zero methods where a second raw write lock is acquired before the previous raw write lock is released.
Rejected Alternatives: A global `DataVault` grep was rejected because it mixes DTO writers, docs, editor tools, and helper names. Launching a compiler-backed analyzer was rejected while a separate `dotnet build` was already running.
Scalability potential: Removes a class of hard stalls equally across all device lanes; visual quality scaling remains independent of vault lock ownership.
Hardware Impact: Static proof only. No runtime cost.

Problem: The current `Editor.log` contains old `BeaconHUDElement` compile errors and a `HectonScanMarkerSystem` native-constructor crash, but Unity is offline and the log timestamp predates current fixes.
Solution: Rechecked current sources directly. `BeaconHUDElement` has balanced braces and clean diff-check. `HectonScanMarkerSystem` fields are serialized references, managed scratch containers, and managed arrays; no direct `new MaterialPropertyBlock`, `new GraphicsBuffer`, `new ComputeBuffer`, `new RenderTexture`, `new Texture2D`, or `new Mesh` token remains.
Rejected Alternatives: Treating stale Editor.log as current truth was rejected. Launching Unity to refresh the log was rejected because the user explicitly deferred Unity work until they open it.
Scalability potential: Import stability improves for every device lane because the crash was editor/import phase, not fidelity dependent.
Hardware Impact: Static proof only. No runtime cost.

Problem: Project-level settings can undermine runtime scalability if they force one quality shape or old input/runtime assumptions.
Solution: Audited `ProjectSettings.asset`, `GraphicsSettings.asset`, and `QualitySettings.asset`. Current settings use URP global settings, incremental GC, deterministic compilation, unsafe code, new Input System, Android IL2CPP with high managed stripping, explicit Windows/Android graphics APIs, and multiple quality profiles with different URP assets, mip budgets, async upload buffers, and LOD bias values.
Rejected Alternatives: Offline YAML rewriting of platform quality defaults was rejected because Unity must validate profile ordering and renderer asset intent. The runtime continuous scalar already owns fine-grained adaptation.
Scalability potential: Static profiles provide platform buckets; `GlobalQualityWeight` and DRS remain the continuous runtime adaptation layer for weak, middle, high, and ultra machines.
Hardware Impact: Audit only. No runtime cost.

Problem: A foreign `dotnet build` left MSBuild nodeReuse `dotnet.exe` children and `VBCSCompiler.exe` after the parent build process disappeared.
Solution: Verified the parent PID was gone and no active `dotnet build`/`csc` was present, then stopped only the orphaned nodeReuse compiler tails. Follow-up process check showed no Unity, dotnet, csc, or VBCSCompiler processes.
Rejected Alternatives: Killing active compilers was rejected. Leaving orphaned compiler nodes was rejected because the user explicitly asked to avoid leftover processes.
Scalability potential: Frees CPU/RAM before the next Unity import session.
Hardware Impact: Removed seven idle MSBuild nodeReuse processes and one idle compiler server from the host process table.

Problem: Parallel scene/prefab edits can reintroduce missing scripts or null fonts, which produce invisible text/rectangles and import warnings before gameplay is testable.
Solution: Re-ran a serialized asset scan over all `.unity` and `.prefab` files for `m_Script: {fileID: 0}`, null legacy `m_Font`, and null TMP `m_fontAsset`. No hits were found.
Rejected Alternatives: Opening Unity only to discover missing references was rejected while Unity is off; the serialized YAML check is sufficient for this reference class.
Scalability potential: Keeps bootstrap/menu/HUD readability stable across all hardware lanes before runtime quality scaling begins.
Hardware Impact: Static proof only. No runtime cost.

Problem: `HectonVoxelVolume` awaited the published sonar SDF encode job until `IsCompleted` with frame-yielding but no watchdog. A broken job path would hold the DataVault payload write guard and stall descriptor publication indefinitely.
Solution: Added `PublishedSonarEncodeWaitWatchdogFrames = 240`. Cancellation/abort/watchdog now exits the await loop, force-completes the job before releasing the write guard, and returns false before descriptor publish.
Rejected Alternatives: Releasing the write guard without completing the job was rejected because the job writes into the guarded NativeArray. Blocking immediately on every publish was rejected because normal completion should stay frame-yielded.
Scalability potential: Weak machines get bounded failure instead of endless SDF publish wait; middle/high/ultra lanes still complete the encode normally and publish the same sonar payload.
Hardware Impact: Adds only integer checks in a cold async publish path. No per-frame gameplay hot path cost.

Problem: `SceneRuntimeService.CompleteMainMenuCinematicTransitionAsync` awaited previous-scene unload without a watchdog. If Unity failed to complete that unload, the transition could remain in a black/dissolve state forever after the target scene had already loaded.
Solution: Added a 20 second managed-unload watchdog. When elapsed, development builds log the scene name and frame count, then continue transition fail-open so input/camera/dissolve recovery can proceed.
Rejected Alternatives: Forcing a synchronous scene unload path was rejected because it would be a worse main-thread stall. Cancelling the full transition was rejected because gameplay scene activation already succeeded at this point.
Scalability potential: All device lanes recover from rare Unity unload stalls; weak devices are less likely to appear frozen during scene handoff.
Hardware Impact: Adds one stopwatch check per unload-wait frame only during menu-to-world transition.

Problem: Source still contained multiple syntactic blocking-wait tokens after earlier boot fixes.
Solution: Audited current matches. Bootstrap/addressables/scene activation loops already yield frames and have watchdogs. Terrain pager, marine snow profile reads, save storage pacing, macro database compaction, black-box heartbeat, and thermodynamics file waits are worker/background/shutdown paths. The runtime candidates without watchdogs were the published sonar encode wait and menu cinematic unload wait; both were bounded in this pass.
Rejected Alternatives: Removing all `Thread.Sleep` tokens was rejected because several are deliberate background worker pacing routes. Replacing every job wait with immediate `.Complete()` was rejected because it would create main-thread stalls.
Scalability potential: Weak lanes get bounded failure modes; higher lanes keep asynchronous visual/data work without extra blocking.
Hardware Impact: Audit plus two bounded waits. Normal-frame hot path cost remains zero.

Problem: Unity compile was blocked by a public editor baker method returning a less-accessible nested compute payload type.
Solution: Raised `GeologicalStrataBaker1724.GeologyBakeParams1724` to public to match `BakeSettings.ToParams()` and keep the GPU bake payload explicit.
Rejected Alternatives: Hiding `ToParams()` again was rejected because another pass had already exposed it for editor bake integration; changing call structure during an active Unity compile would add churn.
Scalability potential: Editor-only; no runtime device lane changes.
Hardware Impact: Compile unblock only. No frame cost.

Problem: First-party audio import had a self-induced drift loop. `AudioImportDictator` applied length-dependent settings in preprocess with unknown clip length, then postprocess changed them back after Unity decoded the clip. Legacy `HectonAudioPostprocessor` also owned overlapping `_Project/Audio` paths.
Solution: Made `AudioImportDictator` converge only in postprocess, deferred reimport outside the postprocessor callback, and made the legacy postprocessor return immediately for dictator-owned first-party audio assets.
Rejected Alternatives: Calling `SaveAndReimport()` inside postprocess was rejected because Unity throws on that route. Keeping both postprocessors active was rejected because it creates two owners for one importer policy.
Scalability potential: Weak devices avoid repeated editor reimports and retain lower sample-rate residency; high/ultra lanes keep Vorbis/music quality policy without importer churn.
Hardware Impact: Import-time stabilization. No runtime hot path cost.

Problem: Unity Console still had warnings after C# errors were cleared.
Solution: Kept `SaveBinaryStorage` layout validation as runtime locals to avoid constant-folded unreachable code, replaced `GetInstanceID()` with `GetEntityId()` folding in `TerminalOsRuntime`, and replaced obsolete Unity 6 `FindObjectsByType` overloads in `LightmapBakerEngine`.
Rejected Alternatives: Suppressing warnings was rejected because these were real API drift and compile-cleanliness issues. Removing the save-layout guard was rejected because it protects binary compatibility.
Scalability potential: Editor/build hygiene across all targets; no gameplay authority or quality-weight route changes.
Hardware Impact: No runtime allocation added; `TerminalOsRuntime` hash remains integer-only.

Problem: Play Mode exit was crashing in URP RenderGraph copy paths after first-party code called `Blitter.Cleanup()` during SRP/play lifecycle.
Solution: Removed first-party ownership of Unity package Blitter lifetime from `HectonRenderPipelineStaticResetGuard`; the guard now leaves Unity SRP package statics to Unity.
Rejected Alternatives: Reinitializing Blitter from first-party code was rejected because the package owns hidden static material state and RenderGraph timing. Local URP package patching was rejected because no package source bug was needed after removing the bad cleanup call.
Scalability potential: All hardware lanes get stable editor/play transitions; no visual feature is disabled.
Hardware Impact: Removes an editor/runtime crash vector. No frame cost.

Problem: `ThermalDynamicResolutionAdapter` missed `LateFrameTick`/`SlowTick` registration when it booted before `GlobalRegistry.Dispatcher` was published, leaving DRS commits and visual-budget globals phase-starved.
Solution: Added a bounded cold coroutine repair path that retries dispatcher lane registration for up to 180 frames and exits once Late/Slow are registered. The runtime `LateFrameTick` camera shield uses fixed cached camera arrays, not scene search or `GetComponent`.
Rejected Alternatives: Permanent `Update()` polling was rejected because it would create a continuous hot route. Direct per-frame `Camera.GetAllCameras()` was rejected because the existing cold cache is sufficient.
Scalability potential: Weak, middle, high, and ultra lanes all use the same continuous render-scale route; quality pressure changes now reach VISUAL_SYNC reliably.
Hardware Impact: One cold coroutine allocation per adapter startup race. Normal gameplay cost is a fixed cached-camera loop with capacity 32 and no allocations.

Problem: `Camera.allowDynamicResolution` stayed false in Unity 6 even after a direct setter on the live world `Main Camera`, while the adapter had STP active and a valid world-camera cache.
Solution: Treated the camera bool as a non-authoritative indicator in this project configuration and verified DRS through live adapter state and Unity dynamic-resolution handler route. Current world route reports `scale=0.794`, `late=True`, `slow=True`, `stp=True`.
Rejected Alternatives: Forcing the camera flag every frame was rejected because the setter is demonstrably ignored in this scene. Changing project-wide graphics YAML blindly was rejected without Unity validation.
Scalability potential: Continuous `GlobalQualityWeight`/stress-driven render-scale remains the adaptation owner across device classes.
Hardware Impact: No added frame cost beyond the phase repair described above.

Problem: `02_HECTON_WORLD` read as a gray test dock with full-bright cyan diagnostic slabs rather than a pressure-heavy deep-sea scene.
Solution: Created `MAT_WorldDepthHazeSignal_1428` and `MAT_WorldThinServiceSignal_1428`; reassigned large cyan depth lanes, drop shafts, foreground scan strips, surface ribs, and caustic ribs to transparent haze/thin-service materials. Scaled the biggest foreground strips down.
Rejected Alternatives: Deleting the signal geometry was rejected because it removes useful NASA-punk navigation language. Adding expensive volumetric simulation was rejected because a material/mesh composition pass buys the visual improvement with no runtime logic.
Scalability potential: Weak devices render the same cheap static meshes/materials; higher devices can later layer particles/post without changing gameplay truth.
Hardware Impact: Static material assignment only. No scripts, jobs, or allocations added.

Problem: `00_BOOTSTRAP` showed a debug text column over the player-facing boot screen after the text-font repair.
Solution: Disabled `HectonSystemsDebugUI_Root` in the scene by default and verified `1428_bootstrap_debug_hidden.png` has no left diagnostic column.
Rejected Alternatives: Shrinking the debug text was rejected because it still exposes diagnostic UI in the shipped boot frame.
Scalability potential: Clean boot presentation on all devices; diagnostic UI can still be re-enabled explicitly in editor if needed.
Hardware Impact: One inactive debug root. No runtime cost.

Problem: The `ORBIT DROP` route had to be proven independently from the new-game world route.
Solution: Invoked `MainMenuController.StartOrbitPrologue()` through MCP, captured early/mid/late `01_ORBIT` frames, measured live orbital distance/speed/heat, and verified automatic handoff to `02_HECTON_WORLD`.
Rejected Alternatives: Treating the previous world screenshot as orbit proof was rejected because the route can hand off quickly and scene identity must be measured.
Scalability potential: The orbit scene uses shader/mesh fakes and continuous quality-driven bloom, so weak lanes keep a cheap planet/ring composition while high/ultra lanes can spend more on post without changing sequence truth.
Hardware Impact: Verification only. No runtime cost.

Problem: Unity Leak Detection reported five Persistent allocations from `FutureCommandSandboxValidator.EnsureValidationScratchBuffersCold` during bootstrap.
Solution: Hardened `ReleaseValidationScratchBuffer` so a NativeArray that survives tracked `H8Memory.Release` is still disposed before the static reference is cleared. Unity/Bee compile completed and repeated Play start returned Console 0.
Rejected Alternatives: Disabling `ModLoader`/future command sandbox was rejected because it removes a project subsystem instead of fixing ownership. Ignoring the leak was rejected because persistent allocations across Play cycles corrupt long editor validation.
Scalability potential: Mod sandbox scratch remains cold and bounded; all device lanes avoid accumulating session memory across reload/play cycles.
Hardware Impact: Shutdown-only branch. Normal gameplay hot path cost remains zero.

Problem: `02_HECTON_WORLD` still read as a single flat gray deck after cyan signal attenuation.
Solution: Added `H8_WORLD_SURFACE_DETAIL_1428` with 31 static cube renderers for deck seams, service hatches, walkway rungs, pressure shadows, and low water occlusion; colliders, shadows, probes, and motion vectors are disabled.
Rejected Alternatives: Adding animated particles, volumetric simulation, or new runtime managers was rejected because static mesh/material composition fixes the readability problem without CPU or GC load.
Scalability potential: Weak lanes get a readable industrial deck from static geometry; middle/high/ultra lanes can later layer post/particles over the same authored structure.
Hardware Impact: 31 extra static renderers, no colliders, no scripts, no per-frame allocations. Expected CPU cost is scene culling/render submission only.

Problem: `01_MAIN_MENU` had two competing readable UI layers. The older serialized `H8_MENU_READABLE_OVERLAY_1428` and the current runtime `ReadableMainMenuOverlay1428` produced duplicated text, stale load-slot labels, and green source-panel ghosting after domain reload.
Solution: Quarantined the serialized legacy overlay by disabling its scene root, hardened `ReadableMainMenuOverlay1428` to rebind surviving runtime roots/slot labels/settings labels after reload, reapplied opaque panel/backdrop colors, and raised the runtime overlay sorting order to 32760. Verified Settings and Load screenshots plus MCP Console 0.
Rejected Alternatives: Deleting the old overlay data was rejected because its visual-variant stage may still be salvageable as art reference. Hiding source CanvasGroup alpha was rejected because those groups are still the route-state signal for the controller.
Scalability potential: Weak devices get one cheap UGUI overlay with no duplicate canvases; higher lanes can spend visual budget on authored menu atmosphere behind the opaque command surface without affecting navigation truth.
Hardware Impact: Cold rebind only after reload/lost caches. Normal frame cost is fixed null checks and no scene-wide search.

Problem: `MaterialDecayRuntime.OnDestroy` could run while Unity was in edit-mode teardown/domain reload and call DataVault buffer release, which previously surfaced as Unity `Destroy may not be called from edit mode` warnings.
Solution: Guarded `OnDisable`/`OnDestroy` so edit-mode teardown clears only the local black-box descriptor; actual DataVault buffer release remains Play Mode only.
Rejected Alternatives: Removing the material-decay black-box was rejected because crash/fault telemetry is required. Suppressing Unity warnings was rejected because the lifecycle route was objectively wrong.
Scalability potential: All device lanes keep the same material-decay shader state and telemetry contract; editor iteration no longer accumulates warning noise during reloads.
Hardware Impact: Teardown-only branch. No gameplay frame cost.

Problem: Unity Leak Detection reported four persistent allocations from `ModuleStatusEvents.EnsureInitialized()` after `PlayerStressMetricsRuntime` registered during play/bootstrap.
Solution: Added editor-only `EditorApplication.playModeStateChanged` teardown for `ModuleStatusEvents`, reusing the existing `ResetStaticState()` disposal path on `ExitingPlayMode` and `EnteredEditMode`.
Rejected Alternatives: Disabling `PlayerStressMetricsRuntime` was rejected because it hides a lifecycle leak. Moving the queues to managed collections was rejected because the module-status lane must remain native/deferred.
Scalability potential: Weak through ultra lanes keep the same native event route; editor validation can repeat Play cycles without accumulating persistent queue allocations.
Hardware Impact: Editor teardown only. Runtime hot enqueue/flush logic is unchanged.

Problem: `02_HECTON_WORLD` still read as a debug grid because screen-space `WaterColumnBand_*` and `SurfaceLeak_*` images, plus redundant far CAD bands/shafts, dominated the camera.
Solution: Disabled the screen overlay grid strips and redundant background CAD mesh strips while retaining dock geometry, haze veils, local amber/cyan gameplay markers, and HUD labels.
Rejected Alternatives: Lowering alpha repeatedly was rejected after screenshots showed the material/UI path still dominated the frame. Removing all HUD/signal styling was rejected because it would make the scene less NASA-punk and less readable.
Scalability potential: All lanes save overdraw and renderer work from disabled strips; higher lanes can later reintroduce authored particles/post effects through continuous quality, not static debug grids.
Hardware Impact: Fewer active UI images and mesh renderers. No scripts, no jobs, no per-frame allocations.

Problem: `02_HECTON_WORLD` still looked like a flat gray editor shell after the grid cleanup because the scene had no Volume grade and `H8_SHELL_MOON_SHAFT` was a `1.75` intensity directional light washing the huge shell planes.
Solution: Added static, no-collider `H8_WORLD_NOIR_STAGING_1428` depth curtains/pressure silhouettes/instrument ticks, added a global `H8_WORLD_POST_GRADE_1428` Volume with ColorAdjustments/Vignette/Bloom, reduced moon light to `0.52`, raised fog density to `0.038`, and darkened ambient/floor/water materials. Removed the first cable attempt because screenshot proof showed it read as debug lines.
Rejected Alternatives: Real volumetric water/fog simulation was rejected because the current route needs a cheap visual fake first. Keeping high directional light was rejected because it destroys deep-sea scale. Long LineRenderer cables were rejected after visual proof showed debug-line behavior.
Scalability potential: Weak devices get static mesh/material staging and one simple post grade; middle/high/ultra lanes can later spend continuous quality budget on particles, stronger bloom, and real water integration without changing gameplay truth.
Hardware Impact: Adds static renderers/materials and a lightweight post volume. No scripts, no jobs, no per-frame managed allocations. New-dive route screenshot `Assets/Screenshots/1428_new_dive_noir_world_runtime.png`; MCP Console returned 0 errors/warnings and Play exit returned 0 errors/warnings.

Problem: `H8_WORLD_TERRAIN_SHELL_1428` was a real Terrain/TerrainCollider but had no TerrainLayer/material, `drawInstanced=False`, and a nearly flat `0.015..0.037` normalized height field, so it functioned as a gray placeholder plane rather than terrain.
Solution: Authored `TX_H8TerrainBasaltSediment_1428.asset`, `H8_TerrainLayer_AbyssBasalt_1428.terrainlayer`, and a deterministic shelf/trench 33x33 height field in the existing persistent `H8_WorldShellTerrain_1428.asset`. Enabled terrain instancing and disabled grass/detail distances.
Rejected Alternatives: Leaving only the MapMagic-named marker was rejected because the active scene has no MapMagic component on that object. Runtime MapMagic generation was rejected for this pass because the first route needs stable authored shell proof before procedural integration.
Scalability potential: Weak lanes get a cheap dark terrain read with no grass/detail cost; middle/high/ultra can later replace this shell with MapMagic/voxel streaming while keeping the same terrain route silhouette.
Hardware Impact: Terrain stays 33x33 heightmap, instanced rendering enabled, no new scripts, no runtime generation, no managed allocations. New-dive route screenshot `Assets/Screenshots/1428_new_dive_terrain_world_runtime.png`; MCP Console returned 0 errors/warnings and Play exit returned 0 errors/warnings.

Problem: The first post-terrain biome readability attempt made the world worse: static kelp/coral/basalt proxy meshes produced a visible green pole and black triangle clutter in the runtime screenshot instead of readable ocean life.
Solution: Removed the bad `H8_WORLD_BIOME_READ_1428` layer and its unused temporary kelp/coral/basalt mesh/material assets. Kept only `H8_WORLD_BIOLUM_FIELD_1428`: 18 no-script, no-collider, no-shadow passive-life mesh silhouettes using one double-sided unlit material and one tiny mesh asset.
Rejected Alternatives: Saving the visible kelp/coral/basalt layer was rejected because screenshot proof showed garbage. Instantiating `Ocean_Crest.prefab` into the production scene was rejected for this pass because it contains 11 MonoBehaviours and has not yet been runtime-isolated from crash/import risk. Leaving all new biome assets despite rejection was rejected as asset clutter.
Scalability potential: Weak lanes pay only 18 static renderers and one instanced material; middle/high/ultra can later swap this fake with real Crest/MapMagic/fauna integration after component ownership is proven.
Hardware Impact: No runtime scripts, colliders, jobs, locks, or managed allocations. Runtime route `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD` held to 93s with MCP Console 0. Live audit showed `OceanKinematicsRuntimeService`, DRS, and `FaunaGeneticsManager` active, while Crest/MapMagic scene roots remain marker-only.

Problem: `Ocean_Crest.prefab` looked visually usable in the world shot but failed runtime contract proof: missing authored neutral abyssal-flow Texture3D, Crest shadow validation warnings, and `OceanRenderer.LateUpdateViewerHeight()` null-ref when sea-floor depth LOD data is disabled.
Solution: Authored `TX_H8NeutralAbyssalFlow_1x1x1_1428.asset`, assigned it to `SargassumMicroFaunaBoids`, disabled editor scene-camera follow and Crest shadow data on the prefab for the first stable world route, bound `Crest4KinematicsAdapter` to the colocated `OceanRenderer`, and patched Crest viewer-height smoothing to treat missing sea-floor depth data as immediate-height mode.
Rejected Alternatives: Runtime Texture3D synthesis was rejected because the component explicitly forbids it. Saving Crest with shadow data active was rejected after repeated validation warnings. Forcing real volumetric/wave visuals before the substrate was clean was rejected because it would hide integration faults behind more moving parts.
Scalability potential: Weak lanes get Crest/ocean kinematics and micro-fauna at a reduced stable authoring budget; middle/high/ultra lanes can later re-enable richer Crest shadow/foam/depth features behind continuous quality gates after measured proof.
Hardware Impact: First stable pass disables expensive Crest shadow data and uses one 1x1x1 authored texture. Production route reported `crestMono=87`, `adapterAvailable=True`, `fauna=True`, memory `1649.5/2245.0/240.5 MB`, and 0 Console errors/warnings through Play exit.
