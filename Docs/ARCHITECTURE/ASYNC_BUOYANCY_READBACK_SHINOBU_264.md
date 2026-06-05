# Async Buoyancy Readback Route - SHINOBU_264

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING
Evidence class: STATIC_DOC / STATIC_SOURCE
Owner domain: Physics buoyancy async readback
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

Owner: `AsyncBuoyancyReadbackRuntime`

Runtime assembly: `Hecton8.Physics.Buoyancy.Runtime` under `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback`.

## Route

- Vehicles submit sample AUPs through `TryQueueSample(double3 sampleAup, double3 cameraAup, uint entityHash)`.
- Owner-published camera AUP sources: `TryPublishCameraAupSnapshot` or `TryQueueSample(..., cameraShiftSequence, ...)`.
- Runtime `Transform.position` is not used in player builds.
- Serialized transform anchor is `UNITY_EDITOR` fallback only.
- The owner subtracts `cameraAup` in double precision and stores only `float2 LocalXZ` in `ReadbackRequestDTO`.
- The runtime listens to `IOriginShiftListener` and caches the latest origin snapshot. Hot `PreSimulation` uses the cached origin, not `GlobalRegistry` or `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
- `PreSimulation` issues one compute dispatch and one `AsyncGPUReadback.Request` for the whole batch.
- Dispatch status: no work, dispatched, unavailable, or ring backlog.
- Mock readback covers unavailable GPU/compute only; backlog keeps cached/dead-reckoned real rows.
- `Simulation` consumes only completed old requests. No `WaitForCompletion` route exists.
- A ready `AsyncGPUReadbackRequest` slot is retained if the results Vault write lock is unavailable; GPU error, zero payload, or successful copy clears it.
- Timing uses cached `DispatcherTimingDTO.FixedDelta`; readback/mock accumulation advances by fixed delta. Owned async files do not read Unity `Time`.
- `PostSimulation` reconstructs absolute water height with `cameraAup.y + ResultHeight` through sequential single-buffer Vault write passes after dispatcher simulation fences are closed.
- Empty frames return the inbound dispatcher handle. When samples exist, apply work is capped to the actual `max(dispatchCount, completedCount)` and the fixed request capacity.
- `PostSimulation` records the 300-frame black-box telemetry ring and raises a dump request if latency exceeds four frames.
- `VisualSync` performs the diagnostic file write to `Docs/AgentLogs/Dump_SHINOBU_264.bin` as a 16-byte header plus raw `ReadOnlySpan<byte>` telemetry rows, keeping physics phases free of file I/O.
- GPU wave inputs use `AsyncBuoyancyWaveParametersDTO` in the Physics-owned Vault lane. Runtime no longer borrows `Hecton8.Atmosphere` concrete DTOs or constants.
- Request buffers and wave-parameter buffers are three-slot GPU rings.
- Each readback slot binds its own request buffer and wave buffer.
- Wave upload is dirty-hashed per slot, so older GPU dispatches are not overwritten.
- `ReleaseGpuBuffers` resets readback request metadata, active flags, counts, frames, slots, and mock state so re-enable cannot inherit stale pending requests.
- The compute kernel also samples the render-published `_H8OceanWakeDisplacement` texture through `_H8OceanShorelineDepthParams`; if the render path has not published a wake target, runtime binds `Texture2D.blackTexture`.
- Wake UV is AUP-stable: runtime writes camera AUP modulo the wake texture world size into `_H8OceanCameraAupLocalProjection.xy`, and the shader samples wake at `request.LocalXZ + cameraProjection`.

## Layout
`ReadbackRequestDTO` is the GPU/CPU bridge:

- offset 0: `float2 LocalXZ`
- offset 8: `float ResultHeight`
- offset 12: `uint EntityHash`
- size: 16 bytes

Editor validation: `HECTON-8/Physics/Validate Async Buoyancy Readback Layout`. The validator checks size, offsets, 16-byte stride, and a temp NativeArray base pointer aligned to 16 bytes.

`AsyncBuoyancyWaveParametersDTO` is a Physics-owned 64-byte shader bridge:

- offset 0: `float4 Wave1`
- offset 16: `float4 Wave2`
- offset 32: `float4 Wave3`
- offset 48: `float4 GlobalWindAndStorm`
- size: 64 bytes

## Determinism Fence
The readback buffers are latency-dependent presentation/force assists, not deterministic truth. Exclude these buffer IDs from lockstep/Merkle state rings:

- `71820` Requests
- `71821` CompletedRequests
- `71822` ResolvedHeights
- `71823` ResultStates
- `71824` Tuning
- `71825` TelemetryRing
- `71826` TelemetryCursor
- `71827` MockRing
- `71828` FallbackWaves
- `71829` VehicleSamplingProfiles
- `71830` CsvScratch
- `71831` Counter

Rollback must reuse cached resolved heights. It must not block on a new GPU readback.

## Compile Wall Boundary
- Runtime code is isolated in `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback` under `Hecton8.Physics.Buoyancy.Runtime.asmdef`.
- The runtime assembly depends on `Hecton8.Core`/`Hecton8.Core.Contracts`/`Hecton8.Core.Memory`, Unity Collections/Jobs/Mathematics, and local Physics DTOs only.
- No root `Buoyancy` asmdef was added because that would capture neighboring agents' files already present in the folder.
- No sibling-domain `Hecton8.Atmosphere` runtime type is referenced by the buoyancy readback engine.
- GPU uploads use private local `GraphicsBuffer.LockBufferForWrite` helpers; the route no longer depends on internal `GraphicsBufferUploadUtility`.
- Cross-domain ocean presentation remains shader ABI only.
- Shader routes: `_H8OceanWaveParameters`, `_H8OceanWakeDisplacement`, `_H8OceanShorelineDepthParams`.
- These are not C# assembly dependencies.
- Wave ownership stays in Physics fallback until a contracts-only provider is approved.

## Vault Access
- Pure public/editor read routes use `IDataVault.TryReadHandle`.
- Direct owner writes use `TryAcquireWriteLock` / `ReleaseWriteLock`.
- Mock, apply, telemetry, tuning, profile, and counter writes hold at most one Vault write lock at a time and release each lock in the same method `finally` block.

## Tooling
- `AsyncGpuReadbackXRayWindow` is UI Toolkit editor tooling. It reads Vault telemetry/counters and writes tuning through `ApplyEditorTuning`; refresh is throttled to 10Hz and unchanged label writes are skipped.
- `SynchronousGpuReadbackScanner` is a Roslyn AST scanner.
- It flags `ReadPixels`, `GetPixel*`, `WaitForCompletion`, unsafe `GetData`, `SetData`, texture allocation, hot arrays, `Pack=1`, and DTO properties.
- Async readback `GetData<T>()` is allowed only after `SystemDispatcher.IsAsyncReadbackReadyNoWait` in the same method.
- `ApplyMicros` telemetry measures the immediate single-buffer apply passes in `PostSimulation`; no retained apply job lock window remains.
- No separate telemetry Burst job exists. One 64-byte telemetry row is written directly during `PostSimulation`; dumping stays in `VisualSync`.

## Scalability
Sample count follows `GlobalQualityWeight` through a smoothstep curve:

- weak device: 4 sample corners
- middle tier: moderate hull grid
- high tier: dense hull profile
- ultra tier: maximum configured sample grid plus wake/shoreline response from the final displaced water texture

Apply workload scales with active sample count.

A no-sample frame returns the inbound dispatcher handle and schedules no tiny job.

Shader direction precompute is deferred because it requires a derived wave-lane payload or ABI repack. Current quality scaling still reduces active wave lanes and sample count continuously.

No binary hardware quality switch owns gameplay truth.
