# Manual Hotspot Classification Pass 14 - Vegetation/Radar Native Staging

Status: STATIC METHOD REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

This pass narrows the broad vegetation/radar native-allocation blockers into method-level evidence. It does not close runtime allocation risk. It separates strong dispatcher/pool structure from the exact allocation and upload routes that still require stress proof or preallocation.

## Files Reviewed

- `Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs`
- `Assets/_Project/Scripts/World/VegetationFlowFieldIntegrator.cs`
- `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs`
- `Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs` (`GraphicsBufferUploadUtility`)

## Method Evidence

### VegetationNavGridSynchronizer

- `TryScheduleAbyssalPath(...)` validates finite endpoints and avoids overlapping `_abyssalPathScheduled` work, which is correct owner-state gating.
- `PreallocateAbyssalNavigationBuffers()` preallocates fixed path/node handles and managed output arrays, so the system is not a naive per-frame generator.
- The same path scheduling route still allocates `NativeList<Vector3>` raw/result path buffers with `Allocator.Persistent` per request and stages predator/nav/scratch snapshots through `H8Memory.Allocate<T>`.
- `CompleteAbyssalPathJob(false)` uses a non-forced `DispatcherJobSwap.TryComplete` route, and `ReleaseAbyssalPathPendingJob(...)` releases the pending native lists and H8Memory handles.
- `DumpAbyssalPathTelemetry(...)` uses temporary payload buffers only for NaN/fault dump output. That is a fault-dump payload, not a normal gameplay proof issue.

Verdict: `YELLOW_ABYSSAL_PATH_PERSISTENT_SCRATCH_PER_REQUEST_PROOF_REQUIRED`.

### VegetationFlowFieldIntegrator

- Threat, flow, thermal, and navigation-support sampling are owner-phased and use dispatcher schedules rather than raw `Update`.
- `ScheduleThreatPropagationJob(...)` stages H8Memory persistent arrays for threat output, compressed output, echo output, previous echo flags, previous threat, voxel output, and counters.
- `ScheduleFlowFieldJob(...)` stages flow output and threat-grid snapshots through H8Memory persistent buffers.
- `ScheduleThermalGridJob(...)` stages thermal output, flow-volume output, and optional previous-flow snapshots through H8Memory persistent buffers.
- Completion routes use non-forced `DispatcherJobSwap.TryComplete` and publish snapshots through DataVault-style payload copies. That is the right shape, but not proof of zero post-bootstrap growth.
- Cadence is better than naive polling: threat/flow/thermal phases rotate and intervals scale continuously by `GlobalQualityWeight`. The remaining question is native allocation growth under stress, not whether the phase model exists.

Verdict: `YELLOW_FLOW_THREAT_THERMAL_OWNER_PHASE_SCRATCH_PROOF_REQUIRED`.

### GroundPenetratingRadarRuntime

- The radar runtime implements dispatcher interfaces (`ISlowTickable`, `ILateFrameTickable`, `IRenderable`) instead of a raw hot `Update` loop.
- `OnEnable()` registers owner routes and calls cold allocation/resource setup helpers.
- `LateFrameTick(...)` completes pending radar work through non-forced `CompleteRadarJob(false)` before advancing frame state.
- `TryCreateRadarPendingJob(...)` allocates H8Memory persistent pending arrays per scheduled job: hits, signal strength, age seconds, ore types, ping GPU payload, counters, and max signal.
- `TryStageSdfLeaseToPendingSnapshot(...)` can release/reallocate the pending SDF snapshot to match the leased SDF length. Because the pending payload is per job, radar scan cadence can become native allocation cadence unless maximum-capacity buffers are prewarmed.
- `EnsureRuntimeDrawResourcesCold(...)` creates a fallback material only when `radarPingMaterial` is missing. The fallback is guarded as a recovery route, but release still requires assigned production material proof through RB-005.

Verdict: `YELLOW_GPR_PER_SCAN_PENDING_AND_SDF_SNAPSHOT_PROOF_REQUIRED`.

### VegetationChunkResidencyDirector / HectonMapMagicVegetationBridge

- `HectonMapMagicVegetationBridge.OnEnable()` performs broad prewarm and registration work: chunk pools, abyssal navigation buffers, fixed hash buffers, renderer bindings, and event route setup.
- Fixed maps exist for tile state and chunk payloads. This is a better structure than dictionary churn.
- `VegetationChunkResidencyDirector.ProcessPendingChunkBuilds(...)` limits chunk build starts per slow tick.
- `ScheduleChunkBuild(...)` copies sand/rock/height tile cache data into H8Memory persistent job payloads, allocates job-record arrays for grass/floating/kelp instances, and can allocate threat echo copies.
- `FinalizeChunkBuildJob(...)` uses non-forced job completion and releases pending job storage after writing records into DataVault-backed pools.
- `BuildChunkPayloadFromJob(...)` and aggregate write buffer routes can ensure/grow backing storage if requested record counts exceed capacity. That is legal only with prewarm or measured no-growth proof.
- `UploadChannel(...)` ensures GraphicsBuffer capacity and can release/recreate a buffer if the needed count exceeds current capacity. It then uploads full matrix/data channels for the active aggregate.

Verdict: `YELLOW_CHUNK_BUILD_STAGING_AND_POOL_GROWTH_PROOF_REQUIRED`.

### GraphicsBufferUploadUtility

- `GraphicsBufferUploadUtility.UploadNativeArray(...)` uses `LockBufferForWrite` and unsafe copy, which is structurally better than managed array churn.
- The helper includes upload-budget frame controls and manual upload gates.
- The utility does not prove callsite safety by itself. Vegetation aggregate and flow-field callsites still need upload byte, recreate count, dirty-page, and compact/high GPU proof.

Verdict: `GREENISH_LOCKBUFFER_UPLOAD_BUDGET_HELPER_WITH_CALLSITE_PROOF_REQUIRED`.

## Release Blocker Updates

- RB-006 remains open and is now stronger: it is no longer a vague "native scratch churn" suspicion. The reviewed methods show per-path native lists, per-flow/threat/thermal H8Memory scratch, per-chunk tile/job record staging, per-radar pending arrays, and SDF snapshot growth.
- RB-116 remains open for vegetation path/flow. Closure requires preallocated scratch ownership or path/flow/threat/thermal stress proof with no post-bootstrap growth and bounded completion windows.
- RB-117 remains open for ground radar. Closure requires maximum-size pending buffers/SDF snapshot or ping-spam proof with no H8Memory growth, plus production material proof via RB-005.

## Required Proof Packet

- 300-frame and 10-minute vegetation path-spam test with H8Memory allocation counters, native handle counts, path request cadence, job completion delay, and frame-time spikes.
- 300-frame threat/flow/thermal stress with `GlobalQualityWeight` compact/middle/high/ultra lanes, proving phase cadence changes fidelity and cadence without changing ownership or DTO layout.
- Chunk streaming stress across tile boundaries with chunk build starts, H8Memory job payload bytes, DataVault pool growth count, finalized record count, GraphicsBuffer recreate count, and upload bytes per frame.
- Radar ping-spam stress with pending buffer allocation count, SDF snapshot capacity, scan cadence, GPU point upload count, material assignment proof, and no post-bootstrap H8Memory growth.
- GPU capture or upload telemetry proving vegetation aggregate uploads are bounded, dirty-page-based, or cheap enough on compact hardware.

## Non-Closure

This pass does not claim release readiness. No Unity import, Console, Play Mode, Profiler, GCMonitor, Frame Debugger, Memory Profiler, player build, or hardware device proof was run. The correct current result is stronger yellow classification with explicit proof gates.
