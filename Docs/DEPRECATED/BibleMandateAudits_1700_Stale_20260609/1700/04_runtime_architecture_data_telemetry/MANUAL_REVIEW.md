# Runtime Architecture / Data / Telemetry Manual Review

Status: STATIC REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Core/H8Debug.cs`
- `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`
- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` from pass 1
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`
- `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs`
- `Assets/_Project/Scripts/Core/PlayerSensoryManager.cs`
- `Assets/_Project/Scripts/Core/Bridge/H8PrefabRegistry.cs`
- `Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs`
- `Assets/_Project/Scripts/Core/Contracts/CoreLowLevelUtilities.cs`
- `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`
- `Assets/_Project/Scripts/Core/UIStateStore.cs`
- `Assets/_Project/Scripts/Core/FrameTimeWatchdog.cs`
- `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs`
- `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`
- `Assets/_Project/Scripts/Core/BurstCallback.cs`
- `Assets/_Project/Scripts/World/BiomeBoundarySdfRuntime.cs`
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs`
- `Assets/_Project/Scripts/Construction/ConstructionRuntimeProxyFactory.cs`

## What Exists

- `H8Debug` is compile-stripped for non-development players. Static log scans now treat `H8Debug.*` as legal diagnostic code.
- `ContentAuthorityRuntime` routes through dispatcher interfaces and avoids raw per-frame `Update`.
- Content hologram proxy pool and fixed arrays are created in `Awake`.
- VFX prewarm uses Addressables async handles and fixed ledgers.
- `GlobalDataVault` owns many native buffers and has generation handles/disposal patterns.
- `BiomeBoundarySdfRuntime` shows legal owner/cold native storage shape in the reviewed route.
- `ConstructionRuntimeProxyFactory` is compile-guarded by `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; runtime proxy creation is not legal for release-player visuals unless excluded by build symbols.

## What Is Missing / Not Proven

- Addressables residency and release ledgers have not been runtime-proven.
- `GlobalDataVault.EnsureGenerationHandle` growth windows still need proof that gameplay hot paths do not trigger allocation/relocation.
- Content black-box dump route exists statically, but no fault-dump test was run.
- Hologram proxy pool exhaustion behavior has not been stress-tested.
- `TryGetLatestCreated()` callsites found in static search are editor/tuner/scanner routes, but `EnsureGenerationHandle` growth still needs runtime counters.
- `H8Memory` and `GlobalDataVault` are legal as owner systems only if consumers do not allocate/grow from hot read accessors.
- `GPUScatterDirector` has method names and paths that perform resource upload/readback from gameplay-facing systems. These need counters proving allocation/upload/readback cadence is bounded and owner-controlled.
- `BiomeBoundarySdfRuntime` fault-dump temporary allocation is acceptable only as a fault path; fault injection should prove it does not happen during healthy gameplay.

## Current Classification

- `H8Debug.cs`: `GREEN_STATIC_DIAGNOSTIC_ROUTE`.
- `ContentRuntimeServices.cs`: `YELLOW_STREAMING_LEDGER_PROOF_REQUIRED`.
- `GlobalDataVault.cs`: `YELLOW_NATIVE_OWNER_LIFETIME_PROOF_REQUIRED`.
- `H8Memory.cs`: `GREENISH_CORE_OWNER_WITH_CALLSITE_RISK`.
- `GlobalRegistry.cs`: `YELLOW_LOG_GUARD_AND_HOT_POLL_SCAN_REQUIRED`.
- `BiomeBoundarySdfRuntime.cs`: `YELLOW_OWNER_COLD_STORAGE_WITH_FAULT_DUMP_PROOF_REQUIRED`.
- `GPUScatterDirector.cs`: `YELLOW_GPU_UPLOAD_READBACK_PROOF_REQUIRED`.
- `ConstructionRuntimeProxyFactory.cs`: `LEGAL_EDITOR_OR_DEVELOPMENT_BUILD_PROXY`.

## Required Next Proof

- Unity/player capture showing no sync load or `WaitForCompletion`.
- Memory profiler snapshot around VFX prewarm/residency.
- DataVault allocation/generation-handle counter during 300-frame gameplay stress.
- Fault injection for content black-box dump path.
- Hot accessor scan proving `Get*`, `TryGet*`, `Resolve*`, and `Read*` methods do not allocate, publish, complete jobs, or search the scene.
- Build-symbol proof that editor/development proxy factories are stripped from non-development release player builds.
- GPU scatter upload/readback counters proving no ownership violation through hot read accessors or unbounded resource growth.

## Pass 6 Addendum - Runtime Boundary Sweep

- `GameBootstrapper.Update()` appears bounded to lifecycle recovery before bootstrap completion, but it still requires boot proof because raw Unity phases are forbidden as private gameplay schedulers.
- Many `GameBootstrapper` debug calls inspected in context are editor/development guarded. Remaining direct runtime `Debug.Log*` callsites in bootstrap/global systems require method-level classification before release claims.
- `SaveManager.cs:542` uses `FindObjectsByType<SaveManager>` for manager duplication/lifecycle validation. This is probably a cold guard, but it needs bootstrap-only proof.
- Temp/TempJob payload routes in `FabricationAssemblerRuntime` and `FoveatedRenderCommander` look fault-dump-only after method reading, but closure still requires fault-only callsite proof and black-box dump tests.
- Non-editor `OnGUI` diagnostic/tuner files outside `/Editor/` folders require assembly/define proof that release players do not include them.

## Pass 7 Addendum - H8Memory Growth Detail

- `H8Memory.CompleteAllOwnerJobs()` contains an explicit shutdown-only blocking sync comment; it is not a defect if no gameplay tick calls it.
- `H8Memory.EnsureTrackingCapacity()` reallocates persistent tracking arrays when allocation record capacity is exhausted. That is legal only if capacity is prewarmed or runtime counters prove no gameplay growth.
- Static memory-owner code remains yellow, not green, until DataVault/H8Memory growth counters are captured during real gameplay stress.

## Pass 8 Addendum - Owner Lifecycle Still Needs Runtime Counters

- Pass 8 found more owner-shaped cold routes: vegetation chunk build arrays, MapMagic chunk payload arrays, HUD material/hierarchy bootstrap, voxel obstacle snapshot pools, and VFX mesh/material/RT fallbacks. These are not automatically hot-path defects, but none are closed by comments like `COLD ALLOC`.
- Runtime architecture acceptance now needs a single 300-frame owner-lifecycle proof packet that records post-bootstrap object creation count, material/mesh/RT creation count, native allocation/growth counters, and forced job completion counters under normal gameplay stress.

## Pass 11 Addendum - Scatter, Biome SDF, Resource Storage, Ocean, And Gas

- `GPUScatterDirector` owns compute, indirect, visibility, and cache buffers, but capacity helpers can release/recreate buffers when required capacity grows. Release proof must show capacities are prewarmed or growth is excluded from healthy gameplay.
- `GPUScatterDirector.TryRefreshBiomeHeatmapTextureHot()` writes heatmap data through `SetPixelData()` and `Apply(false, false)` when the Data Monolith heatmap payload length or checksum changes. This is acceptable only as a dirty-data route, not recurring per-frame upload.
- `GPUScatterDirector` uses `AsyncGPUReadback.RequestIntoNativeArray()` for visible counts, but `CompletePendingVisibleCountReadbackForRelease()` calls `WaitForCompletion()`. The method name suggests teardown/release; callsite proof must show no healthy gameplay blocking wait.
- `BiomeBoundarySdfRuntime` uses persistent owner scratch for sample results and temporary byte payloads only for black-box snapshot export. This is greenish only with fault-injection proof that healthy gameplay does not enter the dump path.
- `ResourceDistributionDirector` has a no-growth pool intent for metamorphism workspaces: hot capacity checks refuse growth while a job is active or a workspace is leased, while cold capacity routes handle sizing. This does not close runtime resource proxy art fallback blockers.
- `ShinobuOceanSurfaceAtmosphereRuntime` uses triple-buffered async wave-height readback and defers disposal while pending. GPU proof is still required for compact/high waterline readability and bounded readback latency.
- `GasDynamicsSolver` schedules in fixed cadence and completes in owner phases with persistent telemetry scratch. Stress proof must show room/base pressure cases do not create job completion spikes.

## Pass 12 Addendum - TBDR Mock Storage And Diagnostic Runtime Assets

- `TBDRPipelineSurgeonRuntime` uses DataVault handles for mock visible instances, sort scratch, quality signal, camera, and budget buffers when possible, but persistent fallback native arrays are created if that route fails. This cannot be accepted as production rendering readiness without DataVault boot proof.
- `TBDRPipelineSurgeonTypes` also has fallback `NativeArray` storage for budget counters, warnings, overdraw counters, telemetry, and texture slice tracking when DataVault is unavailable. The fallback language is explicit, but release proof still needs build/boot policy.
- `ArchitectEyeVisualizer` registers into slow/render lanes and creates diagnostic quad mesh/material/GPU buffers at runtime. This can be a valid diagnostic feature only if release inclusion, debug flags, and resource counts are documented and measured.

## Pass 13 Addendum - DataVault, H8Memory, And Content Authority Growth

- `GlobalDataVault.Initialize()` owns central UnsafeHashMaps, NativeLists, H8Memory arrays, macro payload cache maps, and a raw arena. This is the correct owner route for cross-domain native state, but it is not green until runtime growth counters are flat.
- `GlobalDataVault.EnsureGenerationHandle()` can route into arena growth when required bytes exceed current arena size. `TryGrowArenaForBytes()` / `TryGrowArena()` can relocate the arena after lock/pinned-view checks, and deferred growth can be processed later. This must never be triggered by hot `Get*`, `TryGet*`, `Resolve*`, or `Read*` accessors.
- `GlobalDataVault.TryReserveMacroDatabaseCache()` can increase macro payload cache/list capacities; `TryStoreMacroDatabasePayload()` allocates raw persistent payload memory. Macro payload growth needs boot/import/streaming-window proof, not general gameplay proof by comment.
- `H8Memory.EnsureTrackingCapacity()` doubles tracking records and hash maps when allocation record capacity is exhausted. H8Memory remains `YELLOW_CORE_NATIVE_GROWTH_COUNTER_PROOF_REQUIRED` until scene capacity is prewarmed or soak captures prove no tracking resize.
- `ContentAuthorityRuntime` fixed ledgers and DataVault buffers are good structure; release proof still needs Addressables ledger counts, pending release drains, resident VFX handle counts, and black-box/fault path proof.

## Pass 15 Addendum - Core Boundary And Lazy First-Use Init

- `H8Debug` is a release-stripped diagnostic facade. Calls through `H8Debug.*` should not be counted as release-player log cost after build-symbol proof. Direct `Debug.Log*` callsites are separate and remain covered by RB-113.
- `GameBootstrapper.Update()` only advances bootstrap recovery while `_isBootstrapComplete` is false. This is not a normal gameplay scheduler, but boot trace proof must show it becomes inert after completion.
- `SceneRuntimeService` uses async scene activation gates and cold transition overlay construction, but it creates transition UI roots/materials and scans scene cameras during activation. This remains `YELLOW_SCENE_TRANSITION_COLD_UI_MATERIAL_LIFECYCLE_PROOF_REQUIRED`.
- `PlayerRuntimeContextService` and `PlayerSensoryManager` keep hierarchy scans in cold/rebind routes. The steady-state paths read cached runtime context, but rebind counts and `GetComponentsInChildren` counts must be captured so hot gameplay does not hide repeated hierarchy scans.
- `GlobalTelemetryBus`, `UIStateStore`, `ThreadSafeCommandQueue`, `SignalBusRuntime`, `FrameTimeWatchdog`, and `BurstCallbackQueue` use fixed persistent native storage after initialization. The unresolved risk is lazy first-use allocation: if boot does not prewarm them, the first telemetry/UI/signal/command event can allocate during gameplay. Current classification: `YELLOW_CORE_LAZY_FIRST_USE_NATIVE_INIT_PROOF_REQUIRED`.
- `CoreLowLevelUtilities.TryComplete(...)` and `TryFinalizeCompleted(...)` are greenish helpers: they avoid blocking unless the handle is complete or the caller explicitly forces completion. Closure is callsite-based, not helper-based.

## Pass 16 Addendum - Runtime Auto-Bootstrap And Thermal Owner Boundaries

- `ShinobuStormPropagationRuntime` uses `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` to create `H8_ShinobuStormPropagationRuntime` if no claimed instance exists. This is recovery/boot safety, not authored release scene proof. Runtime architecture acceptance needs a boot manifest or scene prefab proof that this fallback does not execute in normal release scenes.
- `AsyncBuoyancyReadbackRuntime`, `ShinobuStormPropagationRuntime`, `AbyssalThermodynamicsSolver`, and `ThermodynamicsHazardGridRuntime` all allocate/create owner resources during enable/first readiness windows. These are legal only if boot prewarm counters show they occur before player interaction and never as first-use gameplay spikes.
- Storm telemetry dump currently stages into managed scratch and `TryWriteTelemetryDumpSnapshotCold(...)` only validates the scratch payload shape. Crash-reporting acceptance needs a real artifact or an explicitly documented bridge to the project black-box dump writer.
- Thermodynamics hazard/reactor routes use DataVault/H8Memory owners and fault dumps. They are not hot accessor violations by shape, but they remain tied to RB-101/RB-120/RB-130 until arena/tracking growth and dump artifacts are proven.

## Line-Level Classification Addendum

- All 275 runtime architecture/data/bootstrap/telemetry static suspect lines are now classified in `LINE_LEVEL_CLASSIFICATION.md`: 124 editor/dev guarded, 151 cold/setup/fault/owner-lifetime, 0 false positives, and 0 new runtime violations.
- `H8Debug.*` and the `H8Debug.cs` direct Unity log calls are classified as release-stripped diagnostic code. Direct `Debug.Log*` calls in `GlobalRegistry`, `BootstrapStatus`, and `GameBootstrapper` remain covered by `RB-113` because they are fatal/bootstrap/fault shaped, not automatically release-proven.
- `GameBootstrapper.Update()` is classified as cold bootstrap recovery. It still requires a boot trace proving it becomes inert after `_isBootstrapComplete`.
- `SceneRuntimeService` transition overlay material assignment, player context/sensory hierarchy scans, prefab renderer scans, lore root `TryGetComponent`, and URP camera guard scans are cold/setup/rebind shaped. They remain proof-gated by activation and rebind counters.
- `CoreLowLevelUtilities` job completion helpers are classified as helper/callsite-bound, not standalone violations. Forced completion must still be proven teardown/fault/owner-window only.
- `GlobalTelemetryBus`, `UIStateStore`, `ThreadSafeCommandQueue`, `SignalBusRuntime`, `FrameTimeWatchdog`, and `BurstCallbackQueue` remain under `RB-129`: fixed storage shape after init is good, but first-use initialization must be prewarmed before gameplay.
- `GlobalDataVault` and `H8Memory` remain under `RB-101` and `RB-120`: central ownership is the intended architecture, but arena/macro/tracking growth must be flat in gameplay stress.
