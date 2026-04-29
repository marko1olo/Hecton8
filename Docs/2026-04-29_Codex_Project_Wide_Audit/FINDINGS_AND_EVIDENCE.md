# HECTON-8 Project-Wide Findings And Evidence

Date: 2026-04-29  
Status: PENDING VERIFICATION  
Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## 1. Physical Inventory

- `948` first-party scripts under `Assets/_Project/Scripts`
- `822` runtime-like scripts after excluding `Editor`, `Dev`, `SmokeTester`, `Verifier`
- `406783` script lines total
- `429.1` average lines per script
- `31` scripts over `2000` lines
- `6` scripts over `4000` lines
- `4` first-party test scripts

Largest files observed:

1. `World/HectonMapMagicVegetationBridge.cs` - `13205`
2. `WorldProceduralScatterDirector.cs` - `10316`
3. `HectonPlayerMovement.cs` - `7847`
4. `HectonUnderwaterVisuals.cs` - `4826`
5. `UI/SuitHUDV4CanvasOverlay.cs` - `4524`
6. `HectonVoxelEngine.cs` - `4130`

Interpretation:

- Large file count is no longer incidental.
- World, player, visuals, and UI owners are overloaded enough to distort maintainability and verification cost.

## 2. Runtime-Like Pattern Counts

Counts below exclude `Editor`, `Dev`, `SmokeTester`, and `Verifier` paths unless stated otherwise.

- `StartCoroutine`: `0` executable hits confirmed in runtime-like code
- `GameObject.Find`: `0`
- `FindAnyObjectByType`: `11`
- `DontDestroyOnLoad`: `61`
- `SetActive(...)`: `159`
- direct `.text =`: `61`
- `TryFormat(...)`: `32`
- `SetCharArray(...)`: `56`
- `.Complete()`: `128`
- `Addressables`: `33`
- `LoadAssetAsync<T>`: `3`
- native Unity loop declarations (`Update/LateUpdate/FixedUpdate`): `12`

Native loop owners found in runtime-like code:

- `Core/SystemDispatcher.cs`
- `Core/ConnectionSplineBatchRenderer.cs`
- `GlobalPhysicsStateManager.cs`
- `Interaction/EquipmentInteractionHandler.cs`
- `ObserverRelativeCelestialBody.cs`
- `SkySystemFollowCamera.cs`
- `TetherManager.cs`
- `UI/SuitHUDV4CanvasOverlay.cs`
- `UI/LocalizedTMPAutoSizer.cs`
- `UI/LocalizedLayoutMirror.cs`

Interpretation:

- Coroutine discipline is materially better than expected.
- Native-loop discipline is mostly concentrated, but not sealed.
- DDOL and UI activation churn remain major problem surfaces.

## 3. Top Offenders By Pattern

### Direct `.text =`

Top files:

- `PDAInventoryTab.cs` - `29`
- `UI/PDAAtlasSignalTab.cs` - `6`
- `UI/AcousticEcholocationTranslator.cs` - `5`
- `UI/PDASpectrumTab.cs` - `4`
- `UI/PDAControlsRebindUI.cs` - `4`

Meaning:

- The main suit HUD is not the worst offender anymore.
- The PDA and secondary interfaces are.

### `SetActive(...)`

Top files:

- `ModdingAPI/ModMenuUIController.cs` - `10`
- `HectonVoxelVolume.cs` - `7`
- `PDAInventoryTab.cs` - `6`
- `HectonUnderwaterVisuals.cs` - `6`
- `WorldCaveDirector.cs` - `5`
- `FaunaDirector.cs` - `5`

Meaning:

- UI and world presentation both still rely heavily on activation toggles.
- Some hits may be cold-path, but the volume is high enough that this needs targeted review.

### `.Complete()`

Top files:

- `World/HectonMapMagicVegetationBridge.cs` - `11`
- `HectonWorldGenerator.cs` - `9`
- `Core/ConnectionSplineBatchRenderer.cs` - `6`
- `World/AcousticOcclusionUtility.cs` - `6`
- `World/WorldSpatialHashGrid.cs` - `6`
- `SubmarineFluidDynamics.cs` - `5`
- `SaveBinaryStorage.cs` - `3`
- `Optimization/AssetLifecycleGovernor.cs` - `3`

Meaning:

- The barrier problem is concentrated in world/render/save layers.
- Not every `.Complete()` is wrong, but this many barrier sites demand profiler proof.

### `FindAnyObjectByType`

Top files:

- `Bootstrap/GameBootstrapper.cs` - `5`
- `Bootstrap/BootstrapController.cs` - `3`
- `World/HectonCaveVoxelAmbientOcclusionController.cs` - `1`
- `Input/InputManager.cs` - `1`
- `Core/HectonUrpTextureRequirementsGuard.cs` - `1`

Meaning:

- Search-driven recovery is still concentrated in bootstrap and guard code.
- Better than having it scattered through gameplay, but still not clean.

### `DontDestroyOnLoad`

Top files:

- `Bootstrap/BootstrapController.cs` - `12`
- `Bootstrap/GameBootstrapper.cs` - `7`
- `Input/InputManager.cs` - `2`
- `RuntimePerformanceProfiler.cs` - `2`
- `Input/RebindingManager.cs` - `2`
- `Input/UserOptionsPersistence.cs` - `2`
- `UI/SettingsManager.cs` - `2`

Meaning:

- Bootstrap and settings persistence still dominate lifetime ownership.
- This reinforces the split-authority problem.

## 4. Current Unity Compile Facts

A full Unity compile request was triggered during this audit.

Current console-backed errors after that compile:

### `Visor/VolumetricLightFeature.cs`

Errors:

- line `241`: `CS1503`
- line `280`: `CS1503`

Observed source shape:

- compute passes call `CommandBufferHelpers.GetNativeCommandBuffer(context.cmd)`
- the returned type no longer matches the API being expected by the code

Likely runtime consequence:

- visor volumetric-light path is broken at compile level
- the feature cannot be trusted as a production rendering lane right now

### `FluidCompartmentTemplate.cs`

Errors:

- line `57`: `CS0122`
- line `58`: `CS0122`

Observed source shape:

- `CompartmentRecord` fields are serialized private fields
- public properties exist
- some code path is still trying to touch the private fields directly

Likely runtime consequence:

- submarine/internal flooding authoring is currently blocked at compile level
- this is not just a style issue; it stops the build lane

### `Construction/BaseDegradationSystem.cs`

Errors:

- line `69`: `CS0103`
- line `75`: `CS0103`
- line `86`: `CS0103`
- line `106`: `CS0103`

Observed source shape:

- file references `ConnectionSplineBatchRenderer`
- file references `HectonFloatingOrigin`
- current source header does not import the namespaces those owners now live under

Likely runtime consequence:

- rupture/degradation visuals and fluid breach fanout for habitat systems are blocked

## 5. Validation Mismatch

Per-file `validate_script` results:

- `Visor/VolumetricLightFeature.cs` - `0` errors
- `SeamRegistry.cs` - `0` errors
- `UI/PDALoadoutTab.cs` - `0` errors

Full Unity compile result:

- still failing

Meaning:

- file-local validation is not enough for this workspace
- assembly/context-sensitive breakage is live
- audit reports must not treat a clean single-file validator result as proof that the project compiles

## 6. Source-Level Owner Reads

### Bootstrap split is proven

`Bootstrap/BootstrapController.cs`:

- explicit singleton controller
- heavy `DontDestroyOnLoad(...)`
- self-describes as the bootstrap entry owner

`Bootstrap/GameBootstrapper.cs`:

- second bootstrap authority
- explicitly registers dispatcher, tick manager, save, object pool, physics, audio, player services
- still states `InitializeUILayer()` has no final `GlobalRegistry` adapter

Conclusion:

- bootstrap authority is split in source, not inferred

### Event migration is partial, not absent

`SaveEvents.cs`:

- `NativeQueue<SaveEventPayload>`
- `RegistryBucket<ISaveEventListener>`
- flush on dispatcher late frame

`Quest/QuestEvents.cs`:

- `NativeQueue<QuestEventPayload>`
- hashed quest ids

`ModdingAPI/HectonEventBus.cs`:

- managed `List<>`
- abstract class events
- synchronous publish

Conclusion:

- the project has at least two event paradigms alive at once

### Asset streaming backend is more real than older docs implied

`Optimization/AssetLifecycleGovernor.cs`:

- real registry, release queue, eviction candidates, tracked resident bytes

`Optimization/AssetLoadDispatcher.cs`:

- request queue, ready tickets, inflight tracking, tier gating

`ItemCatalog.cs`:

- consumes ready tickets
- triggers `LoadAssetAsync<GameObject>()`
- releases `Addressables` handles

Conclusion:

- backend closure exists at least for the world-prefab catalog lane
- project-wide heavy-asset closure is still not runtime-proven

## 7. Current Render-Target Pressure Snapshot

Unity rendering stats snapshot in current editor state reported:

- `render_textures=1118`
- `render_textures_bytes=1511534709`
- `draw_calls=0`
- `batches=0`

Meaning:

- render-target residency is extremely high for an idle editor snapshot
- against the project VRAM target, this is dangerous enough to document immediately
- this is not yet proof of an in-game leak
- it is proof that render-target ownership needs urgent runtime profiling

## 8. What Got Better Relative To Older Audit Text

These older assumptions are no longer safe:

- "runtime `Resources.UnloadUnusedAssets()` is present"
  - current first-party script scan found `0`
- "runtime `GameObject.Find` is active in gameplay code"
  - current runtime-like scan found `0`
- "direct gameplay `AddForce` bypass is still live in player motor"
  - current global scan found only `PhysicsApplySystem.cs` as the active `AddForce/AddTorque` owner
- "all event buses are still pure delegate buses"
  - false; `SaveEvents` and `QuestEvents` now have queued payload lanes

This does not mean the codebase is healthy.
It means stale audit text has to be corrected when source evidence changes.

## 9. Regression Model

CPU:

- barrier density in world/save/render code
- oversized owner files
- UI activation churn

GC:

- long-tail `.text =` usage in PDA and secondary UI
- mixed event paradigms
- potential cold/hot boundary violations hidden in oversized owners

Memory:

- suspicious render-target residency
- heavy DDOL ownership
- large world/visual systems with many retained buffers

Cadence:

- split bootstrap authority
- native-loop exceptions outside the dispatcher core

Correctness:

- current compile blockers
- assembly-wide validation mismatch

## 10. Bottom Finding

The strongest truthful summary is:

- core engine direction exists
- migration toward the declared architecture is real in several places
- the project is still not in a clean trustworthy build state
- secondary UI, bootstrap lifetime ownership, render-target pressure, and oversized world owners are the main reality risks right now

Anything more flattering would be false.
