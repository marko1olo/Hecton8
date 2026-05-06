# 2026-04-29 - CODEX Mandate Compliance Audit Phase 2
Date: 2026-04-29

Status: PENDING VERIFICATION
Author: Codex
Scope: static audit only

## Mandates Followed

- `AGENTS.md`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_World_Streaming_Residency_Chunk_Management.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

## Method

- Additional targeted audit over bootstrap, scene loading, save pipeline, runtime thumbnail capture, Crest/MapMagic integration, and graphics ownership patterns.
- All findings below are source-backed.
- No Unity runtime session or profiler capture was run.

## What Is Actually Aligned

These items are not complete proof of correctness, but they are materially aligned with the mandates:

- Build settings scene order is correct in `ProjectSettings/EditorBuildSettings.asset`:
  - `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
  - `Assets/_Project/Scenes/01_MAIN_MENU.unity`
  - `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Main menu load flow uses activation gating:
  - `Assets/_Project/Scripts/MainMenuController.cs`
  - `SceneManager.LoadSceneAsync(...)`
  - `allowSceneActivation = false`
  - later promoted to `allowSceneActivation = true`
- Pause-to-menu flow also uses activation gating:
  - `Assets/_Project/Scripts/UI/PauseMenuController.cs`
  - `SceneManager.LoadSceneAsync(...)`
  - `allowSceneActivation = false`
  - later promoted to `allowSceneActivation = true`
- Save binary path appears materially mature:
  - `SaveManager.cs` writes `.sav.tmp`, rotates `.sav.bak`, then promotes temp to primary
  - `SaveBinaryStorage.cs` exposes binary write path
  - `SaveDataMigration.cs` exists and is used during load
  - checksum/integrity handling is present

This means the project is not uniformly broken. Some contracts were implemented. The problem is inconsistency and boundary leakage.

## Confirmed Findings

### 1. Bootstrap route is partially correct, but ownership is still singleton-heavy and persistent-object heavy

Mandate conflict:

- Bootstrap should be one explicit sequence.
- Architecture should not rely on classic singleton persistence patterns.

Evidence:

- Combined `RuntimeInitializeOnLoadMethod` / `DontDestroyOnLoad` scan returned `168` shipping-script files with at least one match.
- Confirmed bootstrap and manager files:
  - `Assets/_Project/Scripts/Bootstrap/BootstrapController.cs`
  - `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
  - `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
  - `Assets/_Project/Scripts/SaveManager.cs`
  - `Assets/_Project/Scripts/ObjectPoolManager.cs`
  - `Assets/_Project/Scripts/SpatialAudioManager.cs`

Direct source evidence:

- `BootstrapController.cs` uses multiple `RuntimeInitializeOnLoadMethod(...)` hooks and repeatedly forces `DontDestroyOnLoad(...)`.
- `GameBootstrapper.cs` uses multiple runtime-init hooks and `DontDestroyOnLoad(...)`.
- `SaveManager.cs`, `ObjectPoolManager.cs`, and `SpatialAudioManager.cs` all retain persistent singleton ownership.

What is objectively missing:

- A fully consolidated bootstrap ladder with one authoritative owner model.
- Removal of broad persistent-manager self-ownership spread.

Impact:

- Boot and lifetime behavior remain distributed.
- Architecture still depends on many persistent roots instead of one clearly sealed bootstrap chain.

### 2. Scene route policy is split between correct async loading and fallback synchronous hard jumps

Mandate conflict:

- Heavy scene flow should be guarded and loading-screen mediated.

Evidence:

- Shipping scripts contain both gated async transitions and direct synchronous `LoadScene(...)` paths.

Direct source evidence:

- Good:
  - `MainMenuController.cs` uses `LoadSceneAsync(...)` and activation gating.
  - `PauseMenuController.cs` uses `LoadSceneAsync(...)` and activation gating.
- High-risk sync fallback paths:
  - `BootstrapController.cs:210` -> `SceneManager.LoadScene(MainMenuSceneName);`
  - `BootstrapRouteEnforcer.cs:44` -> `SceneManager.LoadScene(BootstrapSceneName);`
  - `SceneGuard.cs:64` -> `SceneManager.LoadScene("00_BOOTSTRAP");`
  - `GameBootstrapper.cs:327` -> `SceneManager.LoadScene(BootstrapSceneName);`
  - `Core/SceneRuntimeService.cs:83` -> `SceneManager.LoadScene(sceneName);`

Assessment:

- Some sync paths may be intentional recovery routes.
- The omission is the lack of one sealed policy saying which scene loads are recovery-only and which are normal runtime paths.

Impact:

- Scene route discipline is not fully unified.
- The project can still bypass the intended async handoff path.

### 3. Save subsystem core is stronger than average, but surrounding save UX/runtime helpers violate project contracts

What appears aligned:

- `SaveManager.cs` uses `.sav.tmp` and `.sav.bak`.
- Binary storage and migration path exist.
- Integrity drift handling and backup restoration hooks exist.

What is not aligned:

- `SaveManager.cs` still lives as a classic persistent singleton.
- `SaveEvents.cs` still uses direct static delegate events instead of the mandated queue-backed transport.
- Save-adjacent UI/runtime helpers violate async and render-path contracts.

Direct source evidence:

- `Assets/_Project/Scripts/SaveThumbnailSystem.cs`
  - `captureCamera.Render();`
  - `File.WriteAllBytesAsync(tempPath, bytes).ContinueWith(...)`
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
  - `private async System.Threading.Tasks.Task SaveSlotAsync(string slotName)`
- `Assets/_Project/Scripts/SceneBootstrap.cs`
  - `private async void Start()`
  - file comments explicitly justify `async void Start()`

Mandate conflicts:

- `AGENTS.md`: use `Awaitable`, not `Task`
- `AGENTS.md`: `async void` forbidden
- URP mandate: built-in `Camera.Render()` use is forbidden in runtime gameplay pathing

Assessment:

- Save core container logic is not the main problem.
- The save orchestration perimeter is where contract drift re-enters.

### 4. Save thumbnail capture violates URP runtime render-path discipline

Mandate conflict:

- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`: built-in `Camera.Render()` calls are forbidden inside gameplay loop policy.

Evidence:

- `1` shipping `Camera.Render()` call found.

Direct source evidence:

- `Assets/_Project/Scripts/SaveThumbnailSystem.cs:78`
  - `captureCamera.Render();`

Additional concerns in the same file:

- `ReadPixels(...)`
- `EncodeToJPG(...)`
- `File.WriteAllBytesAsync(...).ContinueWith(...)`

Assessment:

- This is not a per-frame hot path.
- It is still a runtime rendering path that bypasses the declared URP contract.

What is objectively missing:

- One sanctioned thumbnail capture path consistent with the project's render architecture.

### 5. Third-party boundary discipline for Crest is not sealed

Mandate conflict:

- `AGENTS.md`: do not build custom runtime wrappers or override patterns around complex third-party assets like Crest.

Evidence:

- `8` runtime first-party Crest integration files were found.

Confirmed examples:

- `Assets/_Project/Scripts/Crest4KinematicsAdapter.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs`
- `Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheBootstrap.cs`
- `Assets/_Project/Scripts/World/HectonCrestOceanDepthCacheRuntimeBridge.cs`
- `Assets/_Project/Scripts/World/CrestDepthCacheDebugger.cs`
- `Assets/_Project/Scripts/World/CrestFoamDebugger.cs`
- `Assets/_Project/Scripts/World/SargassumCrestDampingController.cs`

Direct source evidence:

- `Crest4KinematicsAdapter.cs`
  - explicitly resolves `Crest.OceanRenderer.Instance`
  - supports forced scene search fallback
  - acts as a first-party runtime adapter around Crest ownership
- `HectonCrestOceanDepthCacheRuntimeBridge.cs`
  - `using System.Reflection;`
  - `MethodInfo InitObjectsMethod = typeof(OceanDepthCache).GetMethod(...)`
  - `InitObjectsMethod.Invoke(...)`
- `HectonCrestOceanDepthCacheBootstrap.cs`
  - dynamically creates missing `OceanDepthCache`
  - disables legacy depth cache components
  - falls back to `Resources.FindObjectsOfTypeAll<Terrain>()`

Assessment:

- This is not a thin passive asset assignment pattern.
- It is active runtime adaptation, runtime repair, reflection-based bridging, and search fallback around Crest.

What is objectively missing:

- A stricter "assign the asset, do not wrap the asset" compliance boundary.

### 6. Third-party boundary discipline for MapMagic is also not sealed

Mandate conflict:

- `AGENTS.md`: MapMagic access should route through `MapMagicBridge.Instance`
- world streaming mandate forbids scene-search style fallback on hot/runtime paths

Evidence:

- `7` runtime first-party MapMagic integration files were found.

Confirmed examples:

- `Assets/_Project/Scripts/MapMagicBridge.cs`
- `Assets/_Project/Scripts/HectonScatterOutput.cs`
- `Assets/_Project/Scripts/HectonRockOutput.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
- `Assets/_Project/Scripts/WorldStreamingDirector.cs`
- `Assets/_Project/Scripts/SceneBootstrap.cs`
- `Assets/_Project/Scripts/ScavengePopulator.cs`

Direct source evidence:

- `MapMagicBridge.cs`
  - `Resources.FindObjectsOfTypeAll<MapMagicObject>()`
  - `GetComponentsInChildren<TerrainTile>(true)`
  - fallback rebinding when the scene object is missing
- `HectonMapMagicVegetationBridge.cs`
  - `RuntimeMapMagicObject.GetComponentsInChildren<TerrainTile>(true)`
- `HectonScatterOutput.cs`
  - direct `MapMagic.Core`, `MapMagic.Products`, `MapMagic.Nodes`
  - `OutputGenerator`, `IApplyData`
  - writes into `ScavengePopulator.Instance`
- `HectonRockOutput.cs`
  - direct `MapMagic` types
  - writes into `HectonRockManager.Instance`

Assessment:

- The project has not kept MapMagic behind one narrow adapter boundary.
- Integration code has spread into multiple runtime files and output-node implementations.

What is objectively missing:

- One clearly enforced boundary separating authoring/generation integration from gameplay/runtime ownership.

### 7. Runtime material cloning is widespread and exception boundaries are unclear

Mandate conflict:

- The project forbids material cloning/override patterns for complex third-party assets.
- Graphics mandates prefer stable material ownership and SRP-batcher-friendly discipline.

Evidence:

- `40` shipping `new Material(...)` matches were found.

Confirmed examples:

- `Assets/_Project/Scripts/World/GPUScatterDirector.cs`
- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs`
- `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/WreckMaterialRegistry.cs`
- `Assets/_Project/Scripts/UI/PDAMapTab.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`

Direct source evidence:

- `GPUScatterDirector.cs`
  - `_runtimeMaterial = new Material(scatterMaterial)`
  - draws via `Graphics.DrawMeshInstancedIndirect(...)`
- `HectonHLODRenderer.cs`
  - `_propertyBlock = new MaterialPropertyBlock()`
  - `_runtimeMaterial = new Material(_shader)`
  - `_brgMaterial = new Material(sourceMaterial)`
- `HectonDistantLandmarkRenderer.cs`
  - `_propertyBlock = new MaterialPropertyBlock()`
  - `_runtimeMaterial = new Material(_silhouetteShader)`
  - `_brgMaterial = new Material(sourceMaterial)`

Assessment:

- Some of these clones may be technically intentional.
- The omission is lack of a sealed policy that distinguishes legitimate renderer-local clones from prohibited runtime material ownership drift.
- This matters more because HLOD/BRG systems are performance-critical and were supposed to be tightly disciplined.

### 8. MaterialPropertyBlock usage exists in geometry/HLOD lanes that the project explicitly treats as sensitive

Mandate conflict:

- `AGENTS.md`: `MaterialPropertyBlock` is forbidden on standard geometry and should not be the default path for SRP-batcher-sensitive geometry.

Direct source evidence:

- `Assets/_Project/Scripts/World/HectonHLODRenderer.cs`
  - `_propertyBlock = new MaterialPropertyBlock()`
  - `_propertyBlock.SetBuffer(...)`
- `Assets/_Project/Scripts/World/HectonDistantLandmarkRenderer.cs`
  - `_propertyBlock = new MaterialPropertyBlock()`
  - `_propertyBlock.SetBuffer(...)`

Assessment:

- These are not UI/legacy particle exceptions.
- They are renderer systems in one of the most performance-sensitive lanes of the project.

What is objectively missing:

- A clarified and enforced buffer-binding strategy for BRG/HLOD renderers that does not drift against the declared SRP-batcher policy.

## System-Level Assessment

Bootstrap:

- Scene order and route intent are mostly correct.
- Ownership model is still too distributed.

Save:

- Binary persistence path is comparatively mature.
- Save-adjacent helpers violate async and render-path mandates.

Graphics:

- There is real render engineering work here.
- There is also widespread runtime material ownership complexity without one clean rule boundary.

Third-party integration:

- Crest and MapMagic are not sealed behind one thin integration layer.
- Runtime bridges, search fallbacks, reflection, and output-node coupling are all still present.

## What The Project Objectively Missed In This Phase

- A fully consolidated bootstrap owner model.
- One canonical scene-loading policy without sync runtime escape paths.
- Full `Awaitable` compliance in save/UI/bootstrap orchestration.
- A sanctioned thumbnail capture path that respects URP runtime rules.
- Tight third-party containment for Crest and MapMagic.
- A clear exception model for runtime material clones and HLOD/BRG property binding.

## Regression Model

CPU:

- Risk source: synchronous scene fallbacks, runtime rendering side paths, HLOD material/property complexity.

GC:

- Risk source: `Task` usage, `ContinueWith(...)`, `ReadPixels`/JPG conversion paths, runtime search fallbacks, and container churn around third-party bridges.

Memory:

- Risk source: many runtime material clones plus persistent manager lifetime spread.

Cadence:

- Risk source: split bootstrap ownership and mixed scene transition paths.

Correctness:

- Risk source: third-party wrappers and reflection bridges that can drift when vendor APIs change.

## Verification Status

Static verification only.

Not performed:

- Unity runtime execution
- bootstrap sequence timing capture
- save/load end-to-end slot test
- thumbnail capture runtime validation
- Frame Debugger / RenderDoc validation
- memory retention slope test

Final status: PENDING VERIFICATION
