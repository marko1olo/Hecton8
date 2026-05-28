# Rationale 1408 - MAIN_THREAD_SYNCHRONOUS_JOB_BLOCKER_PURGER

Date: 2026-05-28
Status: STATIC PASS / WORLD RUNTIME SYNC CLEAN / BUILD BLOCKED BY CONTENTION

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
Solution: Convert GPR raymarch from `Run()` to stored `JobHandle` scheduling. `LateFrameTick` now first attempts `CompleteRadarJob(false)`; if the worker is still active it does not schedule another scan. `ScheduleRadarJob` stores `_radarJobHandle`, keeps SDF/GPR locks live, and registers ore SoA read dependency. `CompleteRadarJob` fences through `DispatcherJobFence.TryComplete`, commits the GPU/readback state, and releases locks in `finally`. `Dispose` force-completes before clearing vault handles.
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
