# Rationale 1408 - MAIN_THREAD_SYNCHRONOUS_JOB_BLOCKER_PURGER

Date: 2026-05-28
Status: APEX LOOP 18 STATIC PASS / ABYSSAL TELEMETRY SCALABILITY REPAIR / BUILD TIMEOUT UNKNOWN

## Decision 00 - Task Scope
Problem: The batch targets synchronous Unity Job blockers in `VegetationChunkResidencyDirector.cs` and `VegetationFlowFieldIntegration.cs`; the repository already contains unrelated dirty work from other agents.
Solution: Limit writes to the two target scripts, agent status/rationale/log/report files, and narrowly scoped tests/tools needed for proof. Use `rg`/PowerShell static scans before code changes.
Rejected Alternatives: Broad search-and-replace across `Assets/_Project/Scripts/World` is too risky under concurrent-agent conditions and can mutate unrelated domain ownership.
Scalability potential: Low uses stale snapshots or skipped frames rather than blocking; Middle schedules fixed batches; High and Ultra spend saved main-thread time in `VISUAL_SYNC` for denser flora/flow presentation, not gameplay truth inflation.
Hardware Impact: Expected gain on i3/MX350 is removal of main-thread worker execution stalls; exact microseconds remain static-estimate-only until profiler proof.

## Decision 01 - Mandate Selection
Problem: The task touches dispatcher phases, native job handles, zero-GC hot paths, ARM64 job payloads, streaming residency, abyssal currents, instanced flora, and frame budget policy.
Solution: Read eight mandate files: execution phases, native memory/jobs, zero GC, ARM64 layout, world streaming residency, abyssal flow fields, instanced flora, and performance budgets.
Rejected Alternatives: Reading all `.agents-skills` files would waste context and increase risk of cross-domain contamination.
Scalability potential: The selected mandates cover Low/Middle/High/Ultra behavior without a binary low/high split.
Hardware Impact: Static mandate alignment avoids adding hidden `.Complete()`/allocation paths that would hurt i3/MX350 frame-time stability.

## Decision 02 - Prompt File Name Drift
Problem: The prompt names `VegetationFlowFieldIntegration.cs`, but static `rg --files` shows no such file. The live flow-field implementation is `VegetationFlowFieldIntegrator.cs`.
Solution: Treat `VegetationFlowFieldIntegrator.cs` as the intended target and record the missing prompt path in the JSON ledger.
Rejected Alternatives: Creating a new `VegetationFlowFieldIntegration.cs` facade would invent an unrequested dependency and split ownership.
Scalability potential: Preserving the existing implementation avoids adding a second route for Low/Middle/High/Ultra flow payloads.
Hardware Impact: No runtime cost; static scope correction prevents wasted compile and review time.

## Decision 03 - Raw Completion Strategy
Problem: Four direct `pending.Handle.Complete()` calls exist in target files. They are not `.Run()` calls and are already guarded by `IsCompleted` or forced teardown, but the raw call sites hide the dispatcher-owned completion contract from static gates.
Solution: Replace raw handle completions with `DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete)` or the non-forced equivalent after the existing `IsCompleted` guard. Keep data publish and native release in the existing `LateFrameTick`/teardown flow.
Rejected Alternatives: Removing completion entirely would leak TempJob/native buffers; completing in `SlowTick` would restore a main-thread stall; adding duplicate handle fields would create stale-handle risk.
Scalability potential: Low skips publish until jobs finish; Middle keeps normal cadence; High/Ultra gain room for richer VISUAL_SYNC flora/flow presentation without changing gameplay authority.
Hardware Impact: On i3/MX350 this removes unscoped barrier sites from target files; measured runtime gain still requires Unity profiler proof.

## Decision 04 - DataVault Lock Lifespan
Problem: The prompt demands lock lifespan extension, but the inspected implementation does not hold DataVault writer locks while the worker jobs run. Jobs write TempJob/native snapshots stored in pending structs; DataVault copy occurs only after completion.
Solution: Preserve that safer lifecycle. Complete the handle first through dispatcher fence, then call `TryCopyVegetationMemorySnapshot`, which acquires the DataVault write lock and releases it in `finally`.
Rejected Alternatives: Holding a DataVault writer lock across job execution would increase relocation deadlock risk and violate the vault as owned swap-window storage.
Scalability potential: Low can defer snapshot publish for another frame; Middle keeps current cadence; High/Ultra increase visual payload density only after the post-job copy succeeds.
Hardware Impact: Avoids long-lived writer locks on i3/MX350; memory relocation stalls remain bounded to the completion copy window.

## Decision 05 - Same-Owner Teardown Helpers
Problem: `HectonMapMagicVegetationBridge.cs` contained additional direct `.Complete()` calls in chunk cancellation/teardown and generic native disposal helpers. They are cold/forced paths, but they belong to the same vegetation owner and would pollute broad static gates.
Solution: Route those forced completions through `DispatcherJobSwap.TryComplete` or `DispatcherJobFence.TryComplete` without changing disposal order.
Rejected Alternatives: Leaving raw `.Complete()` in the same partial owner would make audit results ambiguous; removing forced teardown completion would risk disposing live native memory.
Scalability potential: No gameplay truth change across tiers; cleanup remains deterministic under scene unload, AUP shift, and tile cancellation.
Hardware Impact: No new runtime allocation. Forced completion cost remains only in teardown/origin-shift paths.

## Decision 06 - Dependency Chain Audit
Problem: Converting raw barriers is insufficient if producers and consumers are not chained. The risk is publishing partial flow/threat data or disposing a TempJob buffer while the worker still owns it.
Solution: Keep existing chains: chunk build uses `CombineOptionalHandles` over independent grass, kelp, and floating record jobs; threat voxelization depends on threat propagation; abyssal flow volume depends on thermal grid. Completion occurs only after the final output handle.
Rejected Alternatives: Completing between threat and voxel jobs, or serializing grass/kelp/floating generation through artificial dependencies.
Scalability potential: Low skips unfinished publish; Middle runs steady cadence; High/Ultra keep worker concurrency for richer visual snapshots.
Hardware Impact: Preserves worker parallelism on i3/MX350 and avoids main-thread dependency sleeps.

## Decision 07 - Dry-Run Race Simulation
Problem: A job may cross a frame boundary or the bridge may be disabled during an in-flight chunk/flow solve.
Solution: Frame N `SlowTick` schedules into TempJob/native output arrays and stores the final `JobHandle` in the pending struct. Main thread continues. Frame N `LateFrameTick` attempts non-forced completion; if the handle is incomplete, no publish, no release, no DataVault write lock. Frame N+1 tries again. On `OnDisable`, `OnDestroy`, origin shift, or tile cancellation, forced dispatcher completion drains the handle before `Release*PendingJob` or DataVault buffer release.
Rejected Alternatives: Releasing pending arrays on skip, publishing stale output, or allowing tile cancellation to dispose buffers without a fence.
Scalability potential: Weak devices tolerate one-frame-or-more stale vegetation/flow snapshots. Middle maintains normal cadence. High/Ultra can increase VISUAL_SYNC density after fences clear.
Hardware Impact: i3/MX350 avoids blocking during normal frames; forced blocking remains restricted to teardown/origin-shift/cancellation.

## Decision 08 - Static Editor Proof Instead Of Fabricated Runtime Boot
Problem: Task 16 requests instantiating vegetation directors, but the live code is split across partial owners with private pending structs and depends on project GlobalRegistry/DataVault/MapMagic runtime state. A shallow reflection construction would not prove real job safety and could create false dependency breakage under concurrent-agent work.
Solution: Add `VegetationAsyncJobFence1408EditTests.cs`, an Editor source-contract scanner that verifies dispatcher fences occur before job-output reads, hot scheduling methods contain no raw synchronous tokens, and late-frame/teardown methods are the explicit completion gates.
Rejected Alternatives: Building a synthetic scene bootstrap for MapMagic/DataVault or modifying runtime visibility solely for tests; both would expand domain scope and risk hidden managed allocations.
Scalability potential: Low/Middle/High/Ultra all preserve the same authority route; the test locks the async contract, not a tier-specific behavior.
Hardware Impact: Static Editor scan has no runtime cost on i3/MX350 and catches accidental future reintroduction of main-thread blockers before profiler time is wasted.

## Decision 09 - Teardown Safety Contract
Problem: Asynchronous jobs can remain in flight when Unity unloads a scene, disables the bridge, cancels a tile, or shifts origin. Disposing native buffers without a fence would crash or corrupt state.
Solution: Verify `OnDisable` and `OnDestroy` force `CompleteThreatPropagationJob`, `CompleteFlowFieldJob`, and `CompleteThermalGridJob` before disposal, and same-owner chunk teardown routes through `DispatcherJobSwap.TryComplete` before releasing pending job memory.
Rejected Alternatives: Trusting Unity disposal order or relying on non-forced `IsCompleted` checks during teardown.
Scalability potential: Weak devices can carry jobs across more frames, but teardown still drains deterministically; high-end devices spend normal-frame savings on visual density, not looser memory ownership.
Hardware Impact: i3/MX350 gets normal-frame stall avoidance; forced teardown cost is isolated to scene unload/origin-shift/cancellation.

## Decision 10 - Zero-GC Hot Path Verification
Problem: Moving completion behind a fence can tempt allocation-backed queues, strings, or wrapper objects in `SlowTick`/`LateFrameTick`.
Solution: Keep runtime edits to static helper calls and branch checks. The added tests allocate only in Editor. No managed queues, arrays, LINQ, string formatting, or reference-type construction were added to the touched runtime completion/scheduling paths.
Rejected Alternatives: Managed deferred publish queue or per-frame audit strings.
Scalability potential: Saved CPU/GC headroom scales from stale-snapshot survival on weak devices to richer VISUAL_SYNC flora/flow presentation on high and ultra tiers.
Hardware Impact: i3/MX350 avoids GC spikes in the modified path; exact savings require Unity profiler, but source-level allocation delta is zero.

## Decision 11 - Build Guard Contention
Problem: Final build is required only if host conditions allow it. The preflight sample reported CPU load at 100% and an existing `dotnet` process PID 62680.
Solution: Do not launch `dotnet build`. Mark Task 15 as build-blocked by contention and rely on static scans, source diff review, and the added Editor test source as the proof artifacts.
Rejected Alternatives: Starting another build under 100% CPU, or claiming compile success without running a compiler.
Scalability potential: The decision preserves coordinator machine stability; runtime scalability claims remain source-level until Unity profiler/build can run under acceptable load.
Hardware Impact: Prevents additional CPU contention on the host. i3/MX350 runtime gain remains a static main-thread-barrier reduction, not a measured profiler number.

## Decision 12 - Expanded Runtime Sweep: Ground Radar
Problem: APEX scan found `GroundPenetratingRadarRuntime.cs:643 job.Run()` in non-Editor World runtime. It was outside the initial vegetation prompt, but still a main-thread synchronous job blocker inside the active World runtime surface.
Solution: Convert GPR raymarch from `Run()` to stored `JobHandle` scheduling. `LateFrameTick` now first attempts `CompleteRadarJob(false)`; if the worker is still active it does not schedule another scan. Loop 10 superseded the first async design: `ScheduleRadarJob` now stores `_radarJobHandle` plus `RadarPendingJob` staging arrays, keeps only SDF/ore read dependencies alive as needed, and no longer exposes live GPR DataVault output buffers to the worker. `CompleteRadarJob` fences through `DispatcherJobFence.TryComplete`, commits staged GPU/readback state, and releases staging arrays in `finally`. `Dispose` force-completes before clearing vault handles.
Rejected Alternatives: Leaving the `.Run()` because it was not in the initial two-file hit list; scheduling over ore read arrays without registering `IWorldResourceSpawnerReadDependencySink`; completing the job in the same frame after `Schedule`.
Scalability potential: Low devices carry a one-frame-or-more stale GPR snapshot instead of blocking. Middle devices keep normal cadence. High and Ultra use the existing continuous `GlobalQualityWeight` ray/step scale for richer fake scan density without changing authority.
Hardware Impact: i3/MX350 avoids synchronous GPR raymarch on the main thread. Runtime microseconds are not claimed without profiler proof.

## Decision 13 - APEX Evidence Closure
Problem: The first report lacked the SHA-256 hash of the JSON report artifact and did not separately enumerate exact zero-GC token counts for the modified hot paths.
Solution: Add `Docs/Reports/APEX_FINAL_VERIFICATION_1408.json` and `.sha256` sidecars. Update `SYNC_BLOCKER_PURGE_REPORT_1408.json` and write its `.sha256`. Record diff-token counts, method-body counts, DataVault routes, quality scalar evidence, and build preflight samples.
Rejected Alternatives: Treating the previous "complete" status as sufficient; running `dotnet build` under 100% CPU with active `csc`/`dotnet`.
Scalability potential: Evidence now distinguishes weak/middle/high/ultra behavior through continuous quality scalars and stale-snapshot tolerance rather than binary tier switches.
Hardware Impact: Verification remains static because compile resources were under contention; no extra host build load was introduced.

## Decision 14 - Abyssal Path Fake Async Removal
Problem: `TryScheduleAbyssalPath` scheduled `StringPullPathJob`, immediately called `ForceCompleteAbyssalPathDependency(ref smoothingHandle)`, then committed `CommitAbyssalPathResult` in the scheduler method. That was fake async and could stall the main thread on a path request.
Solution: Store raw/result NativeLists plus copied smoothing inputs in `AbyssalPathPendingJob`. `TryScheduleAbyssalPath` now returns the scheduled handle. `CompleteAbyssalPathJob(false)` is the only non-forced commit gate and runs from the existing dispatcher late-frame path.
Rejected Alternatives: Keeping same-frame completion; moving commit into `SlowTick`; storing only the handle while disposing TempJob arrays in the scheduler.
Scalability potential: Weak devices can keep the previous abyssal path for extra frames. Middle devices complete normally. High and Ultra can spend the saved main-thread window on richer visual density while keeping the same path authority.
Hardware Impact: i3/MX350 avoids a path-request main-thread sleep. Runtime microseconds are not claimed without Unity profiler proof.

## Decision 15 - Cross-Frame Snapshot Ownership
Problem: Deferring abyssal smoothing would make the job read live passability and threat voxel views after the scheduler returned. That is a race with VoxelDynamicNavGrid and vegetation threat grid writers.
Solution: Copy passability and ecosystem threat voxel views into TempJob NativeArray snapshots before scheduling smoothing. Keep snapshots in `AbyssalPathPendingJob` and release them in `ReleaseAbyssalPathPendingJob` after dispatcher completion.
Rejected Alternatives: Capturing live DataVault views across frames; holding DataVault writer/read locks while a worker job runs; forcing the job to complete immediately to keep borrowed views valid.
Scalability potential: Low and Middle pay bounded snapshot copy only when a path request is scheduled; High and Ultra preserve concurrency and use quality-scaled smoothing budgets.
Hardware Impact: i3/MX350 trades a bounded copy for removal of an unbounded worker wait. This is a safer frame-time shape than blocking on `Complete()`.

## Decision 16 - Abyssal Path Quality Scalar
Problem: `ResolveAbyssalPathPortalLookAhead` returned `HighTierAbyssalPathPortalLookAhead` unconditionally. That violated the continuous quality scalar rule and wasted smoothing samples on weak devices.
Solution: Add `ResolveAbyssalPathQualityWeight`, `ResolveAbyssalPathQualityBudget`, and `SmoothAbyssalPathQuality`. Portal look-ahead and DDA sample count now scale continuously from low to mid to configured high using `HomeostasisBrain.GlobalQualityWeight` and `math.lerp`.
Rejected Alternatives: Binary `if(isLowEnd)` style switches; preserving hard high-tier budgets; removing smoothing entirely.
Scalability potential: Low uses minimal portal/sample budgets. Middle interpolates toward mid constants. High and Ultra resolve toward configured maximum and buy visual polish with available CPU.
Hardware Impact: i3/MX350 avoids forced high-tier smoothing work; higher devices can still execute visual overkill without altering gameplay truth ownership.

## Decision 17 - Build Throttle Recheck
Problem: A final compile check was desired, but the host reported CPU counter 100%, CIM CPU 100%, and active `dotnet` PID 53704.
Solution: Do not launch `dotnet build`. Record this as `BUILD_BLOCKED_BY_CONTENTION` in `Docs/Reports/APEX_DOMAIN_RECHECK_1408.json` with exact CPU/process evidence.
Rejected Alternatives: Running another build under >50% CPU; claiming compile success from static scans.
Scalability potential: Preserves coordinator machine stability and avoids starving other agents.
Hardware Impact: No additional compile load was introduced.

## Decision 18 - HLOD Read Accessor Purity
Problem: `TryGetVisibleHLODPayload` completed HLOD cull work inside a read accessor, and `RebuildHLODRegistrySnapshot` attempted non-forced completion from `SlowTick`. Even if the current HLOD job flag is effectively dormant, this is a hidden completion route outside the dispatcher-owned swap gate.
Solution: Remove both completion calls. HLOD completion remains centralized in `LateFrameTick` through `CompleteHLODCullJob(forceComplete: false)`, with forced teardown still allowed.
Rejected Alternatives: Keeping no-op completion calls because `_hlodCullScheduled` is currently never set; adding another local guard around read accessor completion.
Scalability potential: Low and Middle consumers read the last published HLOD payload without causing a synchronization side effect. High and Ultra keep the same authority route and can improve HLOD visuals from scheduled owner phases only.
Hardware Impact: i3/MX350 avoids a future hidden readback stall from renderer/consumer polling.

## Decision 19 - Abyssal A* Snapshot Sovereignty And Failure-Path Degradation
Problem: Loop 8 found that `NativeAStarJob` was scheduled asynchronously but still read `VegetationEcosystemThreatGrid` and `VegetationEcosystemThreatVoxel` directly from DataVault-owned arrays. The later smoothing snapshot setup also had failure branches that force-completed `pathSourceHandle`, creating a remaining main-thread stall under degraded memory/compaction conditions.
Solution: Copy A* threat grid and voxel inputs into TempJob snapshots before scheduling `NativeAStarJob`; release those snapshots through `H8Memory.Release(ref ..., pathSourceHandle, OwnerSystemId)`. Remove `ForceCompleteAbyssalPathDependency`; optional smoothing overlays now fail closed to default arrays, preserving the scheduled path chain and allowing `CompleteAbyssalPathJob(false)` to remain the only normal readback gate.
Rejected Alternatives: Registering a new DataVault read-dependency interface in this pass would cross owner boundaries and require broader core contract edits. Keeping force-complete failure cleanup would preserve correctness but still violate the main-thread blocker mandate. Holding DataVault locks across the worker would extend contention and block compaction.
Scalability potential: Low uses A* plus cheap/default smoothing if overlay snapshots are unavailable. Middle keeps normal density/terrain/threat overlays when memory is available. High and Ultra retain full quality-scaled smoothing through `GlobalQualityWeight` without changing gameplay truth ownership.
Hardware Impact: i3/MX350 avoids hidden blocking during memory pressure or compaction windows. Runtime microseconds are not claimed without Unity profiler; static blocker count for `ForceCompleteAbyssalPathDependency` is now zero.

## Decision 20 - Threat/Flow Cross-Frame DataVault Snapshot Ownership
Problem: Loop 9 found two remaining cross-frame DataVault read hazards. `ScheduleThreatPropagationJob` passed `currentThreat` from `BufferID.VegetationEcosystemThreatGrid` into `ThreatPropagationJob`, and `ScheduleFlowFieldJob` passed `currentThreatGrid` into `BuildAbyssalFlowFieldJob`. Both jobs can complete in a later dispatcher phase, while GlobalDataVault memory may be republished or compacted.
Solution: Copy `currentThreat` into TempJob `previousThreat` before scheduling threat propagation. Copy `currentThreatGrid` into TempJob `threatGridSnapshot` before scheduling flow solving. Store both snapshots in pending structs and release them in the existing completion `finally` release paths.
Rejected Alternatives: Extending GlobalDataVault with a new read-dependency contract during this task would cross Core ownership and increase integration risk. Holding DataVault locks across worker execution would block compaction and violate the swap-window model. Leaving live views in jobs would keep a race hidden under static sync-token success.
Scalability potential: Low and Middle pay a bounded native copy instead of risking stalls or relocation races. High and Ultra retain full worker concurrency and spend saved main-thread stability on denser visual flow/flora presentation, not gameplay truth changes.
Hardware Impact: i3/MX350 avoids use-after-relocation risk under memory pressure. No measured runtime microseconds are claimed without profiler proof; the tradeoff is deterministic native copy cost for removal of an unbounded correctness hazard.

## Decision 21 - GPR DataVault Staging Sovereignty
Problem: Loop 10 re-audit found the previous GPR async repair still scheduled `GroundRadarRaymarchJob` over live `BufferID.GroundRadar*` DataVault arrays. That removed `Run()` but left public read accessors able to observe buffers while the worker job wrote them, and it pinned DataVault buffers across the job lifetime.
Solution: Add `RadarPendingJob` with H8Memory TempJob arrays for hits, strength, age, ore types, GPU pings, counters, and max signal. `ScheduleRadarJob` copies the current GPR state under a short relocation lock, schedules the raymarch against staging slices, and stores the pending struct. `CompleteRadarJob` fences through `DispatcherJobFence.TryComplete`, calls `CommitCompletedScan(ref pending)`, and releases the staging arrays in `finally`. `TryPublishRadarPendingJob` acquires DataVault write locks for every GPR output buffer, copies staged results, and releases every acquired lock in `finally`.
Rejected Alternatives: Holding DataVault locks across worker execution blocks compaction and still exposes live buffers to readers. Adding a new read-dependency interface for GPR consumers would be broader Core/API work and unnecessary because old snapshots can remain readable until the post-fence publish. Reverting to synchronous `Run()` would solve the race by restoring the original main-thread blocker.
Scalability potential: Low devices can display the previous GPR snapshot while the scheduled raymarch completes. Middle devices keep the normal scan cadence. High and Ultra keep the existing continuous `GlobalQualityWeight` ray/step budget and spend worker time on denser visual fake scan pings without changing gameplay truth.
Hardware Impact: i3/MX350 avoids both synchronous raymarch stalls and DataVault relocation pinning across the worker lifetime. The extra native copy is bounded to 128 pings plus seven small GPR buffers; no measured runtime microseconds are claimed without Unity profiler.

## Decision 22 - Vegetation Read Accessor Purity
Problem: Loop 11 found a doctrine violation that was not a `.Run()`/`.Complete()` blocker: `TryReadVegetationMemoryBuffer` and `TryReadOnlyVegetationMemoryBuffer` recorded vegetation memory telemetry on failure. These helpers are `TryRead*` accessors and therefore must not mutate black-box state, publish diagnostics, grow buffers, or perform any side effect.
Solution: Remove failure-side `RecordVegetationMemoryTelemetry` from both read helpers. They now only validate the vault handle/compaction state/read view and return `false` with default output on failure. Telemetry remains in `TryAcquireVegetationMemoryBuffer` and `TryPublishVegetationMemorySnapshot`, which are writer/publisher paths with explicit ownership.
Rejected Alternatives: Keeping mutation in `TryRead*` because telemetry is useful would violate the Global Systems Doctrine. Renaming every caller to a non-read API would be wider churn under active multi-agent work. Adding a managed failure report queue would violate Zero-GC and increase hot-path risk.
Scalability potential: Low devices avoid hidden telemetry writes during compaction/read-failure churn. Middle devices preserve deterministic read behavior. High and Ultra keep diagnostics on owned writer/publish paths without making polling consumers mutate global state.
Hardware Impact: i3/MX350 loses hidden write pressure in read failure paths. No runtime microseconds are claimed without profiler; the static improvement is removal of two accessor-side telemetry writes and preservation of zero allocations.

## Decision 23 - Origin Shift Without Main-Thread Drain
Problem: Loop 12 found `ApplyWorldOffsetToAllChunks` still forced `DisposeAllChunkBuildJobs` and `Complete*Job(forceComplete: true)` outside teardown. An AUP origin shift during active vegetation, flow, thermal, HLOD, path, or pool-defrag work could therefore stall the main thread.
Solution: Split origin shift into `TryApplyWorldOffsetToAllChunks`, `QueuePendingWorldOffset`, `TryApplyPendingWorldOffset`, and `ApplyWorldOffsetToAllChunksImmediate`. If async jobs are active, the offset is accumulated in value-type fields and applied after the dispatcher late-frame completion window. Predator fear node shifting moved into the actual apply path so deferred offset does not double-shift or desync predator fear snapshots.
Rejected Alternatives: Force-completing during origin shift preserves zero-frame visuals but violates the no-normal-phase blocker mandate. Canceling all pending jobs on every shift would throw away valid worker work and increase visual holes. Holding DataVault locks across shift was rejected because it blocks compaction.
Scalability potential: Low devices can carry the old visual offset until the next late-frame completion; Middle gets normal one-frame deferral; High and Ultra keep visual density without main-thread AUP stalls.
Hardware Impact: i3/MX350 avoids an unbounded origin-shift stall. No profiler microseconds are claimed; static proof is removal of the force-complete calls from the origin-shift path.

## Decision 24 - Abyssal Path Invalidation Cancel Flag
Problem: `RebuildAbyssalNavNodeSnapshot` calls `InvalidateAbyssalPathState`, and that method force-completed the active abyssal path job. Nav-node rebuilds can occur during residency refresh, so this was a hidden normal-phase main-thread blocker.
Solution: Add `Cancelled` to `AbyssalPathPendingJob`. `InvalidateAbyssalPathState` marks the pending job canceled and clears public path state. `CompleteAbyssalPathJob(false)` still fences in LateFrame, skips `CommitAbyssalPathResult` when canceled, and releases native lists/snapshots in `finally`.
Rejected Alternatives: Completing immediately to prevent stale path output blocks the main thread. Publishing the completed path after invalidation would commit a path against an obsolete nav graph. Rebuilding nav synchronously before scheduling would move the stall, not remove it.
Scalability potential: Low devices keep no path/previous path while the worker drains. Middle recovers on the next completion window. High and Ultra retain continuous quality smoothing budgets without changing path authority.
Hardware Impact: i3/MX350 avoids path-smoothing stalls during nav graph rebuilds; memory remains bounded by the existing pending native payload until LateFrame release.

## Decision 25 - Continuous Grass LOD And Accessor Naming
Problem: Grass density used a binary `distance <= highDensityRadius ? 0 : 1` tier and a compile-time `_MATH_LOD_LOW` offset branch. `ResolveActiveViewCamera` also performed cache mutation and component lookup under a name reserved for pure accessors.
Solution: Remove `_MATH_LOD_LOW` branch and dead low-math helpers. Encode grass LOD as a 0-255 continuous byte from smoothed distance and `HomeostasisBrain.GlobalQualityWeight`; grass step now interpolates continuously. Rename the camera method to `RefreshActiveViewCameraCache` and update chunk/HLOD call sites.
Rejected Alternatives: Keeping the binary tier because the payload field is a byte would violate the continuous scalability mandate. Changing DTO layout would be a broader authority/save surface risk; encoding continuous state into the existing byte is the lower-risk fix.
Scalability potential: Low uses larger grass steps earlier; Middle interpolates smoothly; High and Ultra keep denser grass farther out without binary popping or gameplay truth changes.
Hardware Impact: i3/MX350 gets cheaper scatter density at distance while strong devices spend saved cycles on visual overkill. Static zero-GC scan reports no reference allocations in the modified methods.

## Decision 26 - Residency Clear Cancellation Instead Of Force Drain
Problem: `ClearAllResidency` is called from normal `RefreshResidency` when tile state or player context is absent. It still called `DisposeAllChunkBuildJobs`, which force-completed active chunk jobs outside teardown. A full residency clear could also let already-scheduled threat/flow/thermal jobs publish stale DataVault output after the clear.
Solution: Route normal clear through `CancelAsyncWorldJobsForResidencyClear`. Chunk jobs are marked `Cancelled`; threat, flow, and thermal pending structs now carry `Cancelled`; their completion paths return before DataVault publish and release native payloads in `finally`. Abyssal path invalidation reuses the existing cancel flag.
Rejected Alternatives: Force-completing chunk jobs preserves immediate visual cleanup but violates the normal-frame blocker purge. Deleting pending jobs without completion would leak or dispose native memory still owned by worker jobs. Publishing completed threat/flow/thermal output after the clear would resurrect stale world state.
Scalability potential: Low devices can drop outdated residency output and show empty/stale visuals until the next valid tile/player context. Middle devices recover on the next LateFrame completion. High and Ultra keep worker concurrency and use continuous quality budgets for visual density rather than blocking for stale correctness.
Hardware Impact: i3/MX350 avoids an unbounded chunk build drain when MapMagic/player context disappears. Runtime microseconds are not claimed without profiler proof; static proof shows the clear/cancel methods add zero reference allocations, zero `string.Format`, zero `.ToString`, zero `foreach`, and zero LINQ-like calls.

## Decision 27 - GPU Readback Classification And Accessor Scanner Correction
Problem: Loop 14 broad World scan found `AsyncGPUReadback.Request` / `GetData` users and one `WaitForCompletion`. A rough accessor scanner also reported `VegetationNavGridSynchronizer.cs:194` as a `TryReadVegetationMemoryBuffer` side-effect, but that line is an invocation inside `TryScheduleAbyssalPath`, not the generic read helper declaration.
Solution: Classify GPU readback paths by exact guards. `VegetationTileCacheResidency.TryFinalizeTileHeightReadback` checks `state.HeightReadbackRequest.done` at line 136 before `GetData<ushort>` at line 157. `GPUScatterDirector.UpdateVisibleCountReadback` checks line 1902 before `GetData<uint>` at line 1908. `HectonIndirectVegetationRenderer.PollCullTelemetryReadback` checks line 3686 before `GetData<uint>` at line 3693. `SargassumMicroFaunaBoids.UpdateParasiteLatchReadback` checks line 7038 before `GetData<int>` at line 7044. The single `WaitForCompletion` at `SargassumMicroFaunaBoids.cs:7983` is called only from `OnDisable`/`OnDestroy` release paths at lines 2030 and 2052. Re-ran a stricter declaration-only scanner including `VegetationMemorySovereigntyRuntime.cs`; result `READ_ACCESSOR_SIDE_EFFECT_MATCHES=0`.
Rejected Alternatives: Removing teardown `WaitForCompletion` would release GPU buffers while Unity may still own an in-flight readback request. Editing Sargassum readback lifetime without that domain owner would be broader GPU lifetime work, not a safe vegetation residency fix. Reporting the false-positive accessor scan as a defect would create noise and risk wrong code churn.
Scalability potential: Low devices keep asynchronous readback polling and stale visual telemetry until the request is done. Middle devices keep normal poll cadence. High and Ultra can use readback telemetry for richer visual debugging without normal-frame blocking; teardown remains deterministic.
Hardware Impact: No profiler microseconds are claimed. Static evidence shows no normal-frame `WaitForCompletion`, zero assigned runtime `.Run(`/raw `.Complete(`, and zero accessor side effects. Build was intentionally blocked because final CPU sample was 68% with active `dotnet` PID 31496. Loop 14 JSON proof hash: `15BCA7F27AE098E9171507842BEF5795EB363CD0059F9770325FBEEA802662F1`.

## Decision 28 - Terrain Height Sync Purge From Vegetation Cache
Problem: `CacheTileMasks` called `terrainData.SyncHeightmap()` immediately before `AsyncGPUReadback.Request`. Official Unity documentation defines `SyncHeightmap` as synchronization for queued heightmap dirty/copy work, and local terrain mutation owners already call it directly after `SetHeightsDelayLOD`. In the vegetation cache path this is a normal-frame synchronization tax with no ownership proof.
Solution: Remove `terrainData.SyncHeightmap()` from `HectonMapMagicVegetationBridge.CacheTileMasks`. Keep async GPU readback of `state.HeightTextureCache`; add an Editor static contract that forbids `SyncHeightmap` in that method and preserves the `heightTexture` -> `AsyncGPUReadback.Request` ordering.
Rejected Alternatives: Keep the call "just in case" and force terrain LOD synchronization during visual cache refresh; move `SyncHeightmap` into readback finalization; add a binary low-end guard. All three hide a normal-frame stall or violate continuous scalability.
Scalability potential: Low devices can accept the previous/stale tile cache until async GPU readback completes. Middle devices keep normal visual cache cadence. High and Ultra can spend saved main-thread budget on denser vegetation visuals without changing terrain authority.
Hardware Impact: i3/MX350 avoids one synchronous terrain heightmap/LOD flush in the vegetation cache path. No profiler microseconds are claimed. Static proof: `CacheTileMasks` now has `newTotal=0`, `referenceNew=0`, `string.Format=0`, `.ToString=0`, `foreach=0`, LINQ=0, and `SyncHeightmap=0`. Final build status is not pass: one guarded build attempt timed out, `dotnet` PID 43740 and then same-command PID 62104 were terminated, and the final compiler-process scan was empty.

## Decision 29 - FixedTileStateMap Naming Purity
Problem: Loop 16 found `FixedTileStateMap.TryGetOrCreate` in `HectonMapMagicVegetationBridge.cs`. The method is local and allocation-prewarmed, but it mutates `_count`, writes `_keys`, and resets `TileRuntimeState` while using a `TryGet*` prefix. That violates the read-accessor purity doctrine at the naming/contract level even though it is not a `.Run()`/`.Complete()` blocker.
Solution: Rename the method to `TryAcquireOrCreate` at `HectonMapMagicVegetationBridge.cs:1326` and update the only call site at `:7979`. Behavior, data layout, tile-state ownership, and preallocated cold arrays are unchanged.
Rejected Alternatives: Leaving the name and adding a comment would keep static scanners noisy. Renaming it to another `TryGet*` form would still conflict with the doctrine. Replacing the fixed map with `Dictionary` would add managed allocation and hash-table churn.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is a contract-hardening fix that keeps hot-path ownership readable while preserving fixed-capacity tile state reuse.
Hardware Impact: No runtime microseconds claimed. The change removes no CPU work; it prevents future read-accessor side effects from hiding under a misleading prefix. Loop 16 scanner proof: `READ_ACCESSOR_SIDE_EFFECT_MATCHES=0`, assigned sync-token matches=0, `TryGetOrCreate` declaration matches=0, `APEX_LOOP16_READ_ACCESSOR_NAMING_RECHECK_1408.json` SHA-256 is `8A262569530EF14D666E5B71B250836DCA464D7D99A48580E02A08ED2369679F`, and aggregate `SYNC_BLOCKER_PURGE_REPORT_1408.json` SHA-256 is `29FF104928FEB46B7FF11FDF4A17AC86DA030ADF53941DBAF620F758CBF2357C`.

## Decision 30 - GPR Ping Upload Buffer Acquire Naming
Problem: Loop 17 expanded the read-accessor scanner to include `TryResolve*` and compound field mutation. It found `GroundPenetratingRadarRuntime.TryResolveGprPingWriteBuffer`, which toggled `_gprUploadBufferIndex` inside a method named as a pure resolver. This is a write/acquire path because it selects the next GPU upload buffer and advances the double-buffer index.
Solution: Rename the method to `TryAcquireGprPingWriteBuffer` at `GroundPenetratingRadarRuntime.cs:1555` and update the call site at `:918`. Add an Editor source contract at `VegetationAsyncJobFence1408EditTests.cs:256-265` so the old `TryResolveGprPingWriteBuffer` name cannot return silently.
Rejected Alternatives: Leaving the name because it is private would preserve a doctrine breach. Splitting the buffer flip into a separate caller would increase churn and risk changing GPU upload ordering. Removing double buffering would be a rendering regression.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The system still uses the existing staged GPR visual fake; high quality keeps denser ray/step budgets through `HomeostasisBrain.GlobalQualityWeight`, while weak hardware can use stale GPR pings without blocking.
Hardware Impact: No profiler microseconds claimed. Runtime work is unchanged. Static proof after the rename: `READ_ACCESSOR_FIELD_MUTATION_MATCHES=0`, assigned sync-token matches=0, `TryAcquireGprPingWriteBuffer` has `newTotal=0`, `referenceNew=0`, `string.Format=0`, `.ToString=0`, `foreach=0`, LINQ-like calls=0. Loop 17 report SHA-256 is `1C19BF1FB281877C5FDEF805ED4DF39470E2CFFD26DF5ED572D79DE29CC82BB0`; aggregate report SHA-256 is `A6047BE919F219725FA6535B1501D9DE16E6C78DAE5F1B9808E20D10ECF40193`.

## Decision 31 - Abyssal Telemetry Binary Flag Removal
Problem: Loop 18 found a remaining scalability doctrine leak in `VegetationNavGridSynchronizer.RecordAbyssalPathTelemetry`: the telemetry flag path encoded `_lastAbyssalPathPortalLookAhead <= LowTierAbyssalPathPortalLookAhead` as a binary low-tier marker. The runtime budget functions already scale continuously through `HomeostasisBrain.GlobalQualityWeight`, and `AbyssalPathTelemetryEntry` already stores exact `PortalLookAhead` and `MaxDdaSamples`, so the flag was redundant and misleading.
Solution: Remove the low-tier telemetry flag at `VegetationNavGridSynchronizer.cs:2103-2107`. Keep only empty-output and non-finite fault bits. Add a static test assertion at `VegetationAsyncJobFence1408EditTests.cs:311` to reject reintroduction of the binary predicate.
Rejected Alternatives: Keeping the flag as a diagnostic shortcut would make telemetry lie about a continuous budget. Replacing it with another low/high flag would keep the same defect. Changing the explicit 64-byte telemetry DTO layout was rejected because numeric budget fields already provide the needed evidence and layout churn would raise ARM64/Burst risk without value.
Scalability potential: Low records cheap numeric budgets and can keep stale path output if jobs lag. Middle interpolates toward mid portal/sample budgets. High and Ultra interpolate toward configured high budgets through `math.lerp` and can spend CPU on smoother visual path presentation. No gameplay authority or DTO route changes.
Hardware Impact: No profiler microseconds claimed. Runtime work decreases by one branch/flag OR in telemetry only; the real gain is contract clarity. Static proof: assigned sync-token scan=0, `READ_ACCESSOR_FIELD_MUTATION_OR_SYNC_MATCHES=0`, `SCHEDULE_READBACK_COLOCATED_METHODS=0`, `RecordAbyssalPathTelemetry` `referenceNew=0` with only value `new AbyssalPathTelemetryEntry`, and Loop 18 report SHA-256 `88FB8E14D0B9B88F453AA63C0883C8B21956AC23E799A4FBAECD722DB4E3ABF5`. Build status is `UNKNOWN_TIMEOUT_NOT_PASS` after one throttled 603584 ms build attempt; external compile-medic PIDs 46892/28228 appeared later and were not killed; dump: `Docs/AgentLogs/Dump_1408_BUILD_TIMEOUT_LOOP18_20260528.txt`.
