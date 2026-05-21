# SHINOBU_264 Rationale

Status: PENDING VERIFICATION

## Initial Route
Problem: Large-vessel buoyancy needs shader-final water height without main-thread GPU stalls.
Solution: Central batched AsyncGPUReadback route with 2-3 frame latency, explicit 16-byte payload, dead-reckoned fallback, and mock path while ocean render dependencies are unstable.
Rejected Alternatives: `Texture2D.ReadPixels`, `Texture2D.GetPixel`, `ComputeBuffer.GetData`, and `AsyncGPUReadbackRequest.WaitForCompletion`; all create CPU/GPU synchronization or hot-path stalls.
Scalability potential: Low uses 4 hull samples and stale smoothing; Middle increases hull sample density; High adds denser wake/coast response; Ultra spends saved time on visual overkill via richer presentation, not deterministic truth bloat.
Hardware Impact: Expected gain on i3/MX350 is removal of sync stalls measured in whole milliseconds when legacy GPU readback blocks; exact microseconds remain PENDING VERIFICATION until profiler proof.

## Domain Boundary
Problem: Async GPU height readback touches physics, vehicles, ocean presentation, and tooling.
Solution: Keep runtime code under first-party Physics/Vehicles/Ocean boundary with DTO/tooling under owner-local namespaces; communicate through unmanaged sample packets and cached owner interfaces rather than new global registry slots.
Rejected Alternatives: New global service slot or direct concrete dependency on future ocean agents.
Scalability potential: Route can be consumed by small boats, submarines, and editor stress tools without multiplying readback requests.
Hardware Impact: Centralization prevents per-MonoBehaviour request overhead on low-end devices and keeps high-end devices free to increase sample density.

## Master Dispatcher Phase Split
Problem: Async readback work has four different phase needs and must not ride legacy `GameTickManager`.
Solution: `AsyncBuoyancyReadbackRuntime` registers four `IDispatcherSystem` bridge objects: `PreSimulation` dispatch, `Simulation` consume/apply, `PostSimulation` telemetry/dump, `VisualSync` editor-only visualization.
Rejected Alternatives: FixedTick-only owner because it does not expose the exact PRE/SIM/POST/VISUAL fences required by the batch prompt.
Scalability potential: Low tier keeps the same phase ownership with fewer samples; Middle/High/Ultra only increase sample density, not route complexity.
Hardware Impact: Removes accidental same-frame sync patterns; expected i3/MX350 gain is the avoided sync fence, commonly 1000+ us when `ReadPixels`/`GetData` blocks.

## 16-Byte Readback DTO
Problem: GPU readback payload must be stable on ARM64 and safe for Burst jobs.
Solution: `ReadbackRequestDTO` uses explicit 16-byte layout with offsets `LocalXZ=0`, `ResultHeight=8`, `EntityHash=12`; editor validator and XML self-audit record the proof.
Rejected Alternatives: `Vector3` payload, C# properties, `Pack=1`, and implicit struct layout; all hide alignment or create defensive-copy risk.
Scalability potential: Same payload supports 4-sample toaster mode and 128-sample dense hull mode without layout churn.
Hardware Impact: 16 bytes per sample means 64 bytes at 4 samples and 2048 bytes at 128 samples; MX350 path avoids inflated transfer rows.

## Dear Lie and Dead Reckoning
Problem: GPU data arrives two to three frames late; blocking for fresh data would stall physics.
Solution: `ApplyDelayedBuoyancyReadbackJob` applies completed old heights with smoothing and extrapolates stale rows from cached vertical wave velocity after five frames.
Rejected Alternatives: Freeze force output or synchronously wait for current-frame GPU data. Both expose latency instead of hiding it behind large-vessel inertia.
Scalability potential: Low uses stronger smoothing and fewer points; Middle uses moderate grid; High/Ultra use denser grid and saved stalls for richer visual water.
Hardware Impact: The fallback keeps vehicle motion continuous during GPU pressure; no measured microseconds until Unity profiler pass.

## AUP Localization
Problem: 100 km absolute coordinates lose precision if sent to GPU as floats.
Solution: Public queue path subtracts `cameraAup` from `sampleAup` in double precision, writes local `float2`, and reconstructs absolute water Y by adding `cameraAup.y`.
Rejected Alternatives: Sending absolute doubles/floats to the compute shader or pulling camera state from GlobalRegistry in hot loops.
Scalability potential: Precision is constant across weak/middle/high/ultra devices; only sample count changes.
Hardware Impact: Prevents edge-of-map jitter without extra runtime allocations.

## Tooling and Proof Artifacts
Problem: The architecture needs objective evidence, not chat claims.
Solution: Added scanner, route card, self-audit XML, XRay window, latency gizmo, reports, and status checklist.
Rejected Alternatives: Manual inspection only or runtime debug UI.
Scalability potential: Editor tools stress the same Vault buffers across all quality levels without gameplay code changes.
Hardware Impact: Tooling is editor/cold path; no runtime cost on low-end devices.

## Polish Pass: Sibling-Domain Compile Wall
Problem: The first runtime draft borrowed `Hecton8.Atmosphere` wave DTOs and helper math directly, creating a sibling-domain compile dependency.
Solution: Added Physics-owned `AsyncBuoyancyWaveParametersDTO` with the same 64-byte shader ABI and local phase/wavelength helpers inside `AsyncBuoyancyReadbackRuntime`.
Rejected Alternatives: Direct `WaveParametersDTO` lookup via `BufferID.ShinobuOceanWaveParameters`; it couples Physics to Atmosphere concrete runtime code and violates contracts-only boundaries.
Scalability potential: Low uses fewer wave lanes and samples; Middle/High/Ultra raise wave lane contribution and sample density without changing DTO layout or authority route.
Hardware Impact: Compile-wall/iteration protection, not a direct frame-time optimization. On i3/MX350 it avoids dependency churn and keeps runtime path bounded to local DTO upload.

## Polish Pass: Cold Allocation Fence
Problem: `EnsureRuntimeReady()` and `EnsureGpuBuffers()` were callable from public/hot and dispatcher phase methods, allowing Vault handle acquisition or GraphicsBuffer creation at the wrong time.
Solution: Hot paths now use pure `IsRuntimeReady()`. Vault descriptor acquisition happens during cold enable/hot-swap. GraphicsBuffers are prewarmed after cold readiness and hot dispatch only checks `HasGpuBuffers()`.
Rejected Alternatives: Lazy allocation in `PreSimulation`; it hides unpredictable render/driver allocation under the physics frame.
Scalability potential: Weak devices avoid allocation spikes; high/ultra devices spend frame budget on sample count instead of cold setup mistakes.
Hardware Impact: Removes potential multi-millisecond hitch on low-end drivers; exact microseconds require Unity profiler proof.

## Polish Pass: Editor Facades and Scanner
Problem: The XRay window used IMGUI, and the scanner used brittle line substring matching that missed generic `GetData<T>()`.
Solution: Replaced XRay with UI Toolkit controls and rewrote the scanner around Roslyn AST invocations, attributes, and property declarations. Added compatibility wrapper `Synchronous_GPU_Scanner`.
Rejected Alternatives: Keeping `OnGUI` or grep allow-lists; both fail the requested editor facade and AST proof requirements.
Scalability potential: Editor-only, but designers can continuously stress low/middle/high/ultra sample budgets without C# recompilation.
Hardware Impact: No player runtime cost. Editor proof quality improves; frame microseconds unchanged.

## Polish Pass: CSV Scratch and Fault I/O
Problem: Cold CSV ingest used a managed `byte[]`, and latency fault dumping could perform managed file I/O from `PostSimulation`.
Solution: CSV bytes are read into Vault `CsvScratch` and parsed via `ReadOnlySpan<byte>`. `PostSimulation` raises a dump flag; `VisualSync` writes a 16-byte header plus raw `ReadOnlySpan<byte>` telemetry rows.
Rejected Alternatives: `File.ReadAllBytes` and `string.Split`; they undermine the data-sovereign parser proof. BinaryWriter inside the physics phase was also rejected.
Scalability potential: Weak devices keep cold boot memory cleaner; high/ultra devices still use the same DTO/profile route with higher sample density.
Hardware Impact: Removes cold managed byte-array allocation and isolates fault I/O from the physics phase. Runtime frame gain is fault-path only pending profiler.

## Polish Pass: Wake Texture Shader Bridge
Problem: A Gerstner-only compute kernel does not satisfy final displaced water height for large vessels interacting with dynamic wakes and shoreline dampening.
Solution: `Hecton_WaveHeightSampler.compute` samples the render-published `_H8OceanWakeDisplacement` texture using `_H8OceanShorelineDepthParams` and folds wake height into `ResultHeight`. Runtime binds the global texture/vector directly to the compute kernel and falls back to `Texture2D.blackTexture` if the render path has not published a wake target.
Rejected Alternatives: CPU-side wake sampling, new cross-domain C# dependency on the ocean renderer, or claiming analytic waves represent shader-final water. CPU sampling would reintroduce the readback sin; direct renderer coupling would break the compile wall.
Scalability potential: Low keeps wake cost bounded by the reduced sample count; Middle/High/Ultra raise point density while preserving the same single texture tap per sample and spending fidelity on visible wake response.
Hardware Impact: On i3/MX350 this preserves the removed sync stall and adds only one GPU texture sample per requested point when wake strength is nonzero. At 4 low-tier samples that is four taps; at 128 ultra samples it is 128 taps on GPU with zero CPU fence.

## Subagent Audit Closure: Slot-Matched GPU Buffers
Problem: Request buffers were triple-ringed, but wave parameters initially used a single upload buffer that could be overwritten while an older GPU dispatch still read it.
Solution: Added `_waveParametersBuffer0/1/2` plus per-slot upload hashes/counts. The dispatch binds the wave buffer matching the request/readback slot and uploads wave rows only when that slot's content hash changes.
Rejected Alternatives: One shared wave buffer with every-frame upload; it risks stale/raced GPU input and wastes upload bandwidth.
Scalability potential: Low tier uploads the same compact two-row wave ABI only on changes; Middle/High/Ultra can raise sample density without multiplying wave upload churn.
Hardware Impact: Correctness and bandwidth discipline. On MX350-class hardware it avoids redundant `LockBufferForWrite` copies when waves are unchanged and prevents cross-frame GPU data hazards.

## Subagent Audit Closure: AUP-Stable Wake UV
Problem: Wake/shoreline sampling used only `request.LocalXZ`, so the wake texture coordinate changed with camera/origin shifts instead of sampling a stable projected AUP.
Solution: Runtime writes camera AUP modulo wake texture world size into `_H8OceanCameraAupLocalProjection.xy`; compute samples wake with `request.LocalXZ + cameraProjection`. Gerstner phase still uses double-precision CPU phase bases, and wake sampling remains one shader texture tap.
Rejected Alternatives: Passing absolute float world coordinates to the GPU or fetching wake data on the CPU. Absolute floats break at 100km; CPU texture reads recreate the stall.
Scalability potential: Low/Middle/High/Ultra use the same stable projection, while sample count and wake tap count still scale continuously through `GlobalQualityWeight`.
Hardware Impact: No additional CPU cost; one existing GPU texture tap now samples the correct spatial address.

## Subagent Audit Closure: Proof Tool Expansion
Problem: The static scanner was blind to `SetData`, hot managed array allocations, and runtime texture allocations; layout proof checked size/offsets but not pointer stride.
Solution: Extended Roslyn scanner rules for `SetData`, `new Texture2D`, `new RenderTexture`, hot `new[]`, and hot `new NativeArray`. Layout validator now checks 16B stride and temp NativeArray base pointer alignment; the domain editor asmdef permits unsafe code only for that validator.
Rejected Alternatives: Grep-only scanner and comment-only alignment proof.
Scalability potential: Proof tooling prevents regressions before profiler time is spent; runtime scalability route is unchanged.
Hardware Impact: Editor-only proof path. It protects low-end hardware by catching regressions that would otherwise recreate stalls or allocations.

## Telemetry Honesty: ApplyMicros
Problem: The prompt requests exact Burst worker execution time, but current code can only measure scheduling-side overhead without Unity Profiler/SystemDispatcher timing support.
Solution: `AsyncReadbackCounterDTO.Flags` now sets `FlagApplyMicrosScheduleOnly` whenever `ApplyMicros` is written. Reports state this limitation instead of claiming exact worker-time proof.
Rejected Alternatives: Falsely reporting schedule overhead as Burst execution time.
Scalability potential: No gameplay behavior change; future profiler integration can replace the counter without changing DTO layout.
Hardware Impact: No frame gain. This prevents bad telemetry decisions on constrained hardware.

## Polish Pass: Dispatcher Delta and Job Admission
Problem: A fallback to Unity `Time.fixedDeltaTime` left a non-dispatcher time source in the readback simulation path, and empty sample frames could still schedule a one-lane apply job.
Solution: Resolve fixed delta exclusively from `DispatcherTimingDTO.FixedDelta` with a cached last-valid value and a final literal bootstrap fallback. The readback/mock `_timeSeconds` accumulator advances by that fixed delta, not frame delta. Apply scheduling now returns the incoming `JobHandle` when `max(dispatchCount, completedCount)` is zero, and otherwise schedules only the active lane count.
Rejected Alternatives: Reading Unity `Time` in simulation code or clamping zero work to one Burst lane. The first weakens rollback timing discipline; the second creates exactly the tiny job the doctrine rejects.
Scalability potential: Low quality collapses CPU apply lanes with the actual sample budget and can skip apply entirely on empty frames. Middle/high/ultra scale active lanes continuously with requested/completed samples rather than the maximum configured capacity.
Hardware Impact: Empty frames avoid one scheduler submission; constrained hardware also avoids applying 128 configured lanes when only the low-tier request count is active. Exact microseconds remain profiler-pending.

## Polish Pass: Dead Telemetry Job Removal
Problem: `RecordReadbackTelemetryJob` remained in source after telemetry moved to a direct 64-byte post-simulation write, creating dead Burst surface area and a misleading extra job.
Solution: Deleted the unused job. The black-box route still writes `ReadbackTelemetryEntry[300]` in `PostSimulation` and dumps from `VisualSync` only.
Rejected Alternatives: Keeping unused code for a theoretical future profiler route. Dead jobs are not architecture; future worker timing must integrate with the dispatcher/profiler instead of a stale orphan type.
Scalability potential: No gameplay truth changes. The active route remains fixed-size telemetry with no quality-dependent layout.
Hardware Impact: Runtime behavior is unchanged because the job was not scheduled; source surface and scanner noise are reduced.

## Subagent Audit Closure: Vault Read/Write Fences
Problem: Public reads and owner writes were both opened through generation resolution, which made read accessors capable of returning mutable views and left direct writes without explicit Vault writer ownership.
Solution: Added `ReadVaultBuffer` using `IDataVault.TryReadHandle` and `AcquireVaultWriteBuffer`/`ReleaseVaultWriteBuffer` using `TryAcquireWriteLock`. Direct owner mutations now use short writer windows. Scheduled mock/apply job outputs hold writer locks from schedule through `PostSimulation`, where the dispatcher-owned completion window releases them.
Rejected Alternatives: Continuing to use `TryResolveHandle` everywhere or holding a writer lock across arbitrary user reads. The first blurs authority; the second blocks unrelated proof/read tooling.
Scalability potential: Weak devices avoid lock-fault churn and false write contention; high/ultra devices still scale sample count without changing ownership lanes.
Hardware Impact: Primarily correctness/data-sovereignty. Any microsecond gain is not claimed until profiler timing exists.

## Subagent Audit Closure: Ring Backlog and Teardown
Problem: A saturated three-slot readback ring was treated the same as GPU unavailability, so normal async backlog could inject mock heights. Disabling the runtime disposed GPU buffers without clearing stale active slots.
Solution: `DispatchGpuReadback` now returns `NoWork`, `Dispatched`, `Unavailable`, or `RingBacklog`. Mock readback is enabled only for `Unavailable`; `RingBacklog` keeps cached/dead-reckoned real rows. `ReleaseGpuBuffers` calls `ResetReadbackRingState` to clear requests, counts, frames, active flags, write slots, and mock state.
Rejected Alternatives: Boolean dispatch results and stale metadata after teardown. Both produce false diagnostics and bad force data under pressure.
Scalability potential: Low devices are more likely to accumulate async backlog, so preserving real cached data matters most there. High/ultra still get dense readbacks when the ring drains.
Hardware Impact: Avoids false request drops after re-enable and prevents mock force injection during transient GPU pressure.

## Subagent Audit Closure: Camera AUP Authority
Problem: `Transform.position` is presentation float state and was usable as runtime physics camera input.
Solution: Runtime now consumes owner-published camera AUP through `TryPublishCameraAupSnapshot` or the shift-sequenced `TryQueueSample` overload. The serialized `cameraAupAnchor` exists only under `UNITY_EDITOR` as a fallback for tooling.
Rejected Alternatives: Reading scene Transform in player physics code. That path is not AUP-authoritative and has no shift-generation proof.
Scalability potential: Quality changes still alter only sample density; camera authority and DTO layout remain fixed.
Hardware Impact: Prevents 100km precision drift and false wake UV projection. No frame-time claim.

## Subagent Audit Closure: Upload Helper Boundary
Problem: The runtime depended on `GraphicsBufferUploadUtility`, an internal Core helper. It compiles in the current root assembly but would fail under a future buoyancy asmdef split.
Solution: Added private local `CreateStructuredLockBuffer` and `UploadNativeArrayToGraphicsBuffer` helpers using `GraphicsBuffer.LockBufferForWrite` and `UnsafeUtility.MemCpy`.
Rejected Alternatives: `SetData`, because the scanner bans it for this route, and the internal helper, because it is not a stable contract.
Scalability potential: Same upload path works from 4 samples to 512 capacity without changing authority.
Hardware Impact: Maintains zero-SetData upload and removes a compile-wall trap. Exact microseconds unchanged.

## Subagent Audit Closure: Shader Direction Precompute Deferred
Problem: The shader recomputes lane direction `sincos` per requested point.
Solution: Deferred precomputed direction because the current 64-byte wave DTO ABI is already documented and consumed by the compute kernel. A derived direction buffer can be added only with an ABI route update.
Rejected Alternatives: Repacking wave lanes silently. That would invalidate the payload ledger and risk shader/runtime mismatch.
Scalability potential: Current continuous quality still lowers active wave lanes and sample count; an ABI-approved follow-up can precompute directions for high-density modes.
Hardware Impact: Potential GPU ALU saving is acknowledged but not claimed in this patch.

## Loop 6: Static Compile-Risk Audit Under CPU Gate
Problem: The build gate remains closed because system CPU sampled at 100%, so launching `dotnet build` would violate the batch command discipline.
Solution: Performed a narrowed static compile-risk audit instead: re-read the SHINOBU_264 XML prompt, checked owned runtime/job/contract/compute files for forbidden stall patterns, checked dispatcher/registry/vault interface names against source contracts, and verified the `LockBufferForWrite`/`AsyncGPUReadback.Request(GraphicsBuffer)` route against existing project usage.
Rejected Alternatives: Running a forbidden build under load, or using a broad physics scan as proof when it includes sibling-agent files outside SHINOBU_264 ownership.
Scalability potential: No gameplay behavior changed. The route remains 4-sample low-tier through 128-sample configured high-tier with continuous `GlobalQualityWeight`; static proof keeps the path from regressing into sync readback or binary quality logic.
Hardware Impact: No new microsecond claim. The audit reduces compile/integration risk while preserving the previously claimed stall avoidance until profiler/build verification is legally allowed.

## Loop 6: Generated Project-File Coverage Gap
Problem: The Unity-generated `.csproj` files do not yet include `AsyncBuoyancyReadback*` or the SHINOBU_264 editor proof tools. A `dotnet build Hecton8.Core.csproj` against stale generated project files cannot prove these new Unity assets compile.
Solution: Recorded the gap and left generated `.csproj` files untouched. The next legal proof route is Unity project-file regeneration/import followed by a CPU-gated build or Unity console check.
Rejected Alternatives: Editing `Hecton8.Core.csproj` manually. The file is marked generated/overwritten by Unity, and a manual include would be a false integration artifact outside the authoritative asset pipeline.
Scalability potential: Runtime scalability is unchanged. This protects compile-wall discipline by keeping project generation owned by Unity rather than hand-maintaining stale build manifests.
Hardware Impact: No frame-time impact. It prevents a bad verification claim while CPU is saturated and another `dotnet` build is already running.

## Loop 7: Ready Readback Writer-Lock Retry
Problem: `ConsumeGpuReadbacksNoWait()` cleared a ready readback slot before acquiring the completed-results Vault write lock. If the lock was unavailable, the GPU data was silently abandoned while the system continued on cached/dead-reckoned data.
Solution: Clear the active slot only after either a zero-count request is classified or the ready GPU payload is copied into the completed-results buffer. If the Vault writer lock is unavailable, leave the slot active so the dispatcher can retry without blocking.
Rejected Alternatives: Blocking for the write lock or treating lock contention as GPU failure. Blocking would violate async readback discipline; GPU failure would inject false diagnostics.
Scalability potential: Low-tier devices are more likely to have long dispatcher/Vault contention windows, so retaining the ready slot protects them from avoidable data loss. Higher tiers keep the same three-slot bounded ring and denser sample budgets.
Hardware Impact: No new frame-time claim. The fix avoids an unnecessary cached-data fallback under contention and preserves the nonblocking route.

## Loop 7: Runtime Asmdef Isolation
Problem: The SHINOBU async readback files lived directly under the parent `Hecton8.Core` assembly tree. That works in the current project but weakens the compile-wall proof for this domain.
Solution: Moved only SHINOBU_264 runtime/job/contract files into `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback` and added `Hecton8.Physics.Buoyancy.Runtime.asmdef`. The editor facade now references this runtime assembly explicitly.
Rejected Alternatives: Adding an asmdef at the `Buoyancy` folder root. That would capture existing buoyancy files owned by other agents and risk cross-agent compile breakage. Editing generated `.csproj` files was also rejected because Unity owns them.
Scalability potential: Runtime math and GlobalQualityWeight behavior do not change. Isolation reduces rebuild blast radius once Unity regenerates project files.
Hardware Impact: No frame-time claim. This is iteration-time and dependency-risk reduction.

## Loop 7: Runtime Asmdef Contract Reference
Problem: After the asmdef split, relying on `Hecton8.Core` or `Hecton8.Core.Memory` to transitively expose `Hecton8.Core.Contracts` risks CS0012-style metadata errors because `IDataVault` inherits contract-owned surface.
Solution: Added an explicit `Hecton8.Core.Contracts` reference to `Hecton8.Physics.Buoyancy.Runtime.asmdef`.
Rejected Alternatives: Trusting transitive assembly references. C# compile references are not a stable transitive contract for directly consumed public metadata.
Scalability potential: No runtime behavior change. The assembly remains bounded to Core/Core.Contracts/Core.Memory plus Unity packages.
Hardware Impact: No frame-time claim. This reduces compile-risk after Unity regenerates project files.

## Loop 7: Shared Optimization Report Restoration
Problem: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` had been overwritten by another domain's top-level report and no longer contained SHINOBU_264 proof, while Task 19 requires the shared physics optimization artifact to carry this lane.
Solution: Added a nested `shinobu264AsyncBuoyancyReadbackScanner` section pointing to the dedicated SHINOBU_264 report, runtime asmdef route, async readback proof, Dear Lie proof, and compile limitation.
Rejected Alternatives: Replacing the whole shared report. That would erase other agents' sections and create report churn.
Scalability potential: No runtime behavior change. The shared artifact now reflects continuous sample scaling and async latency hiding without becoming the authority source.
Hardware Impact: No frame-time claim. This is proof artifact integrity.
