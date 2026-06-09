# Manual Review Pass 11 - Sonar, Scatter, Biome SDF, Resource Metamorphism, Ocean, And Gas

Status: STATIC METHOD REVIEW - NO GPU / PLAY MODE / PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs`
- `Assets/_Project/Scripts/World/GPUScatterDirector.cs`
- `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs`
- `Assets/_Project/Scripts/World/ResourceDistributionDirector.cs`
- `Assets/_Project/Scripts/Atmosphere/ShinobuOceanSurfaceAtmosphereRuntime.cs`
- `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs`

## Findings

### 1. TopographicalSonarSynthesizer Is Buffered, But Mock SDF Remains A Truth Gate

`TopographicalSonarSynthesizer` allocates persistent job buffers through `H8Memory` for points, hit mask, counters, mock SDF, mock material IDs, and material color LUT at `TopographicalSonarSynthesizer.cs:536-578`. `OnEnable()` allocates persistent state, creates graphics buffers, registers late/slow/render routes, and listens for sonar pings at `:707-733`. The scan path schedules jobs and uses `DispatcherJobFence.TryFinalizeCompleted` rather than a forced same-frame completion in the normal late-frame path.

The production risk is truth, not only allocation. `ScheduleSonarScan()` uses a published SDF snapshot when available, but falls back to `GenerateMockSdfJob` and sets `UsedMockSdfFlag` when no published SDF exists at `:1129-1204`. That is acceptable as diagnostic/degraded presentation only. Production sonar cannot present mock geometry as real world information.

Classification: `YELLOW_BUFFERED_SONAR_ROUTE_WITH_MOCK_SDF_AND_GPU_UPLOAD_PROOF_REQUIRED`.

### 2. GPUScatterDirector Uses Persistent GPU Resources, But Capacity And Readback Need Proof

`GPUScatterDirector` creates compute/indirect/visibility buffers in `EnsureResources()`, `EnsureInstanceBufferCapacity()`, `EnsureVisibleIndexBufferCapacity()`, and `EnsureVisibilityCacheBufferCapacity()` at `GPUScatterDirector.cs:769-923`. These are owner resources, but they can release/recreate when required capacity grows. `TryRefreshBiomeHeatmapTextureHot()` only updates when Data Monolith byte length/checksum changes, but it still writes a texture through `SetPixelData()`/`Apply(false, false)` at `:1118-1156`.

Visible-count readback uses `AsyncGPUReadback.RequestIntoNativeArray()` at `:2234-2242`, with persistent readback data allocated at `:2245-2261`. `CompletePendingVisibleCountReadbackForRelease()` calls `WaitForCompletion()` at `:2297-2306`; the method name suggests release/teardown, but release acceptance requires callsite proof that this never runs as healthy gameplay cadence.

Classification: `YELLOW_GPU_SCATTER_CAPACITY_HEATMAP_READBACK_PROOF_REQUIRED`.

### 3. BiomeBoundarySdfRuntime Looks Like Owner-Cold Storage Plus Fault Dump Payload

`BiomeBoundarySdfRuntime.EnsureSampleScratchCold()` creates one persistent `NativeArray<BiomeBoundarySdfResult>` at `BiomeBoundarySdfRuntime.cs:629-634`, then releases it through `DisposeSampleScratchCold()`. `TryWriteBlackBoxSnapshotCold()` creates a temporary byte payload at `:811-847` for a black-box dump worker. This reads as a fault/export route, not a normal gameplay path.

Classification: `GREENISH_OWNER_COLD_STORAGE_WITH_FAULT_DUMP_PROOF_REQUIRED`.

### 4. ResourceDistributionDirector Has No-Growth Pool Intent, But Proxy Art Remains A Separate P0

`ResourceDistributionDirector` owns `MetamorphismWorkspaceOwner`, which allocates persistent `PressureMetamorphismSample` storage only when capacity is insufficient. The normal runtime check `EnsureMetamorphismCapacity()` refuses to grow while a job is active or workspace is leased; cold capacity is handled by `EnsureMetamorphismCapacityCold()` at `ResourceDistributionDirector.cs:1979-2095`. Sector state pool logic uses no-growth lease/release helpers.

This is a decent owner/capacity shape for metamorphism sampling. It does not close the separate resource proxy asset blocker: runtime prefab/material fallback is still covered by RB-010.

Classification: `GREENISH_RESOURCE_METAMORPHISM_OWNER_STORAGE_WITH_POOL_PROOF_REQUIRED`.

### 5. ShinobuOceanSurfaceAtmosphereRuntime Uses Triple Async Readback, Still Needs GPU Proof

`ShinobuOceanSurfaceAtmosphereRuntime` consumes only completed readbacks, dispatches wave-height compute samples, uploads query buffers, then requests async readback into a persistent native array at `ShinobuOceanSurfaceAtmosphereRuntime.cs:1390-1501`. It creates three query/result graphics buffer slots at `:1642-1687`, and `TryDisposeWaveReadbackGraphicsBuffers()` defers disposal while requests are pending rather than forcing an immediate wait.

This is the right shape for an ocean presentation cheat: bounded sample capacity, triple buffering, async readback, and latency telemetry. It still needs compact/high GPU proof, readback-latency proof, and visual proof that low-tier waterline behavior remains readable.

Classification: `GREENISH_TRIPLE_ASYNC_WAVE_READBACK_WITH_GPU_PROOF_REQUIRED`.

### 6. GasDynamicsSolver Uses Dispatcher Phases And Telemetry Scratch, But Job Windows Remain Proof-Gated

`GasDynamicsSolver.FixedTick()` tries to complete any previous step, accumulates cadence, and schedules a new step only when the quality-scaled cadence elapses. `PostFixedTick()` completes scheduled work. Telemetry scratch is one persistent `NativeArray<GasDynamicsTelemetryEntry>` created by `TryEnsureTelemetryScratchCold()` at `GasDynamicsSolver.cs:1740-1760`. Consecutive state write lock failures route to black-box dump.

Static structure is not a release guarantee. Gas/flood/pressure gameplay needs profiler proof that fixed/post-fixed completion windows do not stall when many rooms, bases, and failure signals are active.

Classification: `GREENISH_GAS_OWNER_PHASE_WITH_COMPLETION_STRESS_PROOF_REQUIRED`.

## Blocker Changes From Pass 11

- Strengthen `RB-005`: sonar mock SDF is the production-truth risk; ping cadence and GPU upload proof are also required.
- Strengthen `RB-106`: GPU scatter capacity growth, heatmap texture apply, and visible-count readback lifecycle are now explicit proof points.
- Strengthen `RB-107`: ocean wave async readback and gas solver completion windows both need compact/high stress proof.

## Current Honest Verdict

The reviewed systems are not naive runtime allocation loops. Most have owner resources, persistent buffers, async readback, and black-box telemetry. They remain yellow because the project requires proof, not intent: no mock SDF as production truth, no post-bootstrap buffer growth, no blocking readback in healthy gameplay, no hidden GPU stalls, and no gas/ocean completion spikes on compact hardware.
