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

- `970` first-party scripts under `Assets/_Project/Scripts`
- `1010` first-party `.cs` files under `Assets/_Project`
- `420468` script lines total under `Assets/_Project/Scripts`
- `433.47` average lines per script
- `32` scripts over `2000` lines
- `7` scripts over `4000` lines
- `4` first-party test scripts

Largest files observed:

1. `World/HectonMapMagicVegetationBridge.cs` - `13279`
2. `WorldProceduralScatterDirector.cs` - `10333`
3. `HectonPlayerMovement.cs` - `7851`
4. `HectonUnderwaterVisuals.cs` - `4826`
5. `UI/SuitHUDV4CanvasOverlay.cs` - `4608`
6. `Audio/PlayerCriticalProceduralAudioRenderer.cs` - `4200`
7. `HectonVoxelEngine.cs` - `4138`

Interpretation:

- large file count is no longer incidental
- world, player, visuals, audio, and UI owners are overloaded enough to distort maintainability and verification cost

## 2. Current Editor Facts

- active Unity scene: `02_HECTON_WORLD`
- loaded scenes: only `02_HECTON_WORLD`
- Build Settings scenes:
  - `00_BOOTSTRAP`
  - `01_MAIN_MENU`
  - `02_HECTON_WORLD`
- scene is dirty in editor
- latest console readback shows `15` errors, all visible as package-side MCP `ManageAsset` conversion failures rather than first-party compile errors

Observed console pattern:

- tool/package source: `./Library/PackageCache/com.coplaydev.unity-mcp.../Editor/Tools/ManageAsset.cs`
- repeated message: `Failed to convert -1 to a unsigned 32 bit int`
- affected assets: `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_*`

Meaning:

- earlier same-day first-party compile-break snapshots are no longer safe as current-state claims
- current first-party editor state is cleaner
- console still contains package-side MCP errors after asset inspection
- runtime safety is still unverified

## 3. Current Pattern Counts

- runtime `StartCoroutine(...)` hits across first-party scripts: `37`
- runtime `Update/LateUpdate/FixedUpdate` file owners after filtering interface noise: `28`
- runtime `GetComponent*` hits: `926`
- runtime `TryGetComponent(...)` hits: `647`
- current `SetActive(...)` hits in `UI` + `Interaction`: `2`
- runtime `Resources.UnloadUnusedAssets()` hits: `0`
- runtime `DG.Tweening` / `DOTween` hits under `Assets/_Project/Scripts`: `0`
- runtime `Find* / Resources.FindObjectsOfTypeAll / FindAnyObjectByType` hits: `136`
- runtime addressable release hits: `1`

Interpretation:

- coroutine migration is incomplete, but older "runtime coroutine swamp" narratives were too blunt
- UI activation churn is currently much lower than earlier same-day scans claimed
- broad search-driven ownership remains noisy
- older `UnloadUnusedAssets()` and DOTween violations are not current in the first-party script pass

## 4. Current Structural Truths

### Bootstrap split is proven

Source owners:

- `Bootstrap/BootstrapController.cs`
- `Bootstrap/GameBootstrapper.cs`
- `SceneBootstrap.cs`

Conclusion:

- startup ownership is split in source, not inferred

### Event migration is partial, not absent

Queue-backed:

- `SaveEvents`
- `QuestEvents`
- `ScanEvents`
- `NarrativeEvents`
- `AudioLogEvents`

Direct static buses still present:

- `InteractionEvents`
- `CraftingEvents`
- `PDAEvents`
- `FlashlightEvents`
- `RandomEventEvents`
- `HectonSubmarineOsEvents`

Conclusion:

- the project has at least two runtime event paradigms alive at once

### Service ownership is clearer than older audits claimed

Confirmed current direct owners:

- `SpatialAudioManager` -> `IAudioService`
- `SuitHUDV4CanvasOverlay` -> `IUIService`
- `HabitatIntegrityManager` -> `Hecton8.Core.IDamageReceiver`
- `HectonUnderwaterVisuals`, `HectonSubmarineOS`, `MissionMarkerSystem` -> `IRenderable`

Conclusion:

- older ghost/fragmentation claims were stale
- the remaining issue is not absent ownership but oversized ownership

### Physics force ownership is cleaner than older same-day reports claimed

Current global scan of `.AddForce(` / `.AddTorque(` in first-party scripts only found:

- `PhysicsApplySystem.cs`

Conclusion:

- older claims about active gameplay-side force bypass were stale in the current scan
- this specific mandate surface is cleaner than earlier notes stated

## 5. What Got Better Relative To Older Audit Text

These older assumptions are no longer safe:

- "the project currently has live compile blockers in first-party code"
- "runtime `Resources.UnloadUnusedAssets()` is present"
- "runtime `DOTween` usage is present in first-party scripts"
- "direct gameplay-side `AddForce` bypass is still live"
- "all major event buses are still pure delegate buses"

This does not mean the codebase is healthy.
It means stale audit text has to be corrected when source evidence changes.

## 6. Regression Model

CPU:

- barrier density and giant owners still require profiler proof

GC:

- long-tail UI and component-lookup pressure still need runtime validation

Memory:

- owner concentration and retained-world systems still need runtime profiling

Cadence:

- split bootstrap authority and explicit Unity loop owners remain structural concerns

Correctness:

- current editor state is cleaner than earlier same-day reports
- current runtime safety remains unverified

## 7. Bottom Finding

The project is in a better state than the earlier broken-compile snapshots suggested.
It is still not in a proven runtime-safe state.
The active problems now are ownership concentration, startup overlap, and missing runtime proof, not the stale compile-collapse story.
