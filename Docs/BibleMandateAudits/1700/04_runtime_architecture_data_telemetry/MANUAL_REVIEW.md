# Runtime Architecture / Data / Telemetry Manual Review

Status: STATIC REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Core/H8Debug.cs`
- `Assets/_Project/Scripts/Core/Content/ContentRuntimeServices.cs`
- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` from pass 1
- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
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
