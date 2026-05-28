# Rationale 1426 - CONCURRENCY_AND_PHASE_LIFECYCLE_OVERSEER

Date: 2026-05-28

## Decision 00 - Scope Lock

Problem: Assignment gives cross-domain access to Assets/_Project/Scripts but domain roster places this agent in Core & Memory Infrastructure, with authority over tick dispatcher, phase order, JobHandle fences, DataVault lock lifecycle, and native allocation reset windows.
Solution: Audit globally, mutate narrowly. Prefer dispatcher/fence/accessor fixes and tests over broad domain rewrites unless a concrete same-frame Complete, hidden accessor sync, nested vault lock, or fence vulnerability is proven from source.
Rejected Alternatives: Whole-repository scheduling rewrite; too much public API and integration risk under concurrent-agent conditions. dotnet build; assignment forbids it and CPU contention rule rejects it.
Scalability potential: Low uses skipped optional jobs, deferred snapshots, and cadence reduction; Middle keeps normal cadence with conservative visual sync; High increases snapshot density; Ultra spends saved time only in VISUAL_SYNC visual overkill.
Hardware Impact: Static target is i5-1135G7/MX350. Removing one mid-frame Complete can prevent 1000-8000 us stalls depending on job duration; unverified until Unity profiler proof exists.

## Decision 01 - Mandate Set

Problem: Task spans dispatcher phases, JobHandle dependencies, GlobalDataVault locks, allocator reset windows, zero-GC hot paths, registry purity, and Burst data layout.
Solution: Loaded six mandates: ARCH_Execution_Phases, OPT_Native_Memory_Collections_JobSystem_Protocol, OPT_HectonArenaAllocator_2_0, OPT_Zero_GC_Policy_AllocFree_Mandate, ARCH_Global_Registry_ServiceLocator_DI_Init, DATA_Runtime_Struct_Layout_ARM64.
Rejected Alternatives: Reading rendering/audio/UI mandates first; they are secondary unless scan finds concrete phase violations in those domains.
Scalability potential: Mandates require continuous GlobalQualityWeight and phase-specific load-shed instead of binary low/high switches.
Hardware Impact: Preventing same-frame Schedule/Complete loops preserves worker-thread overlap on low-end CPU; exact gain requires profiler.

## Decision 02 - Central Fence Enforcement

Problem: Runtime scan found only 10 direct `.Complete()` calls outside Editor/Dev, but many systems route completion through `DispatcherJobFence.TryComplete`. Forced completion of an incomplete handle outside a dispatcher swap window can still stall the main thread silently.
Solution: Added a development-build throttled warning in `DispatcherJobFence.TryComplete` when `forceComplete == true`, the handle is not completed, and no dispatcher swap window is active.
Rejected Alternatives: Blocking forced completion outright; teardown and cold bootstrap call sites need a safe drain path. Editing all callers; too broad and unsafe without phase-by-phase proof.
Scalability potential: Low/Middle detect accidental stalls early; High/Ultra preserve extra worker overlap for presentation density instead of burning it on hidden sync.
Hardware Impact: Warning itself is dev-only. Prevented regressions are in the 1000-8000 us range per hidden forced completion depending on job duration; measured proof absent.

## Decision 03 - Vault Compaction Fail-Closed

Problem: `TryResolveHandle`, `TryReadHandle`, and `TryReadOnlyHandle` checked `_compactionFence` before and after view creation. `TryAcquireWriteLock` and `TryLockBuffer` checked before mutation but could return a physical write view or pin if the fence rose after lock mutation.
Solution: Added post-lock `_compactionFence` checks while still inside the mutation-gate window. On fence race, the code records contention, rolls back the writer lock or buffer pin, clears the transient view, and returns false.
Rejected Alternatives: Adding fence checks to every caller wrapper; duplicates policy outside the DataVault owner and leaves gaps. Taking a managed lock; violates native interlock doctrine and risks deadlock under Burst job ownership.
Scalability potential: Low devices avoid rare access-violation stalls under memory pressure; Middle/High/Ultra keep live compaction available without exposing moved arena pointers.
Hardware Impact: Added two volatile reads on lock acquisition paths only, not per-element loops. Expected cost below 1 us per acquisition; failure path prevents crash/stall class under defrag pressure.

## Decision 04 - MemorySentinel Phase Split

Problem: `MemorySentinelRuntime` was registered as VISUAL_SYNC and performed vault resolution, target buffer locking, validation job scheduling, and `JobHandle.ScheduleBatchedJobs()` in `VisualSyncTick`. That violates ARCH_Execution_Phases and consumes presentation budget with simulation work.
Solution: Replaced the single direct dispatcher registration with Simulation/PostSimulation bridge systems. `ScheduleSimulation` now schedules `MemorySentinelValidationJob` with the dispatcher `dependsOn` handle, returns the job handle, registers it with H8Memory, and leaves `VisualSyncTick` empty. `PostSimulationTick` finalizes the already-completed handle and releases locked target buffers.
Rejected Alternatives: Keeping a single VisualSync dispatcher lane and hoping `IsCompleted` prevents stalls; it still schedules heavy work in the wrong phase. Registering only Simulation and finalizing next frame; it would hold DataVault pins across a full frame. Rewriting SystemDispatcher multi-phase dispatch; too broad and unnecessary.
Scalability potential: Low skips validation by cadence without touching presentation; Middle runs normal validation in worker time; High/Ultra can increase validation frequency without stealing VisualSync upload time.
Hardware Impact: Moves one validation scheduling site out of VisualSync. Expected low-end saved presentation budget is 150-600 us on frames where target refresh and schedule overhead occur; exact profiler proof not collected.

## Decision 05 - Dependency Weaving Boundary

Problem: A pending MemorySentinel validation job previously had no upstream dispatcher dependency and was not returned to the master simulation handle because scheduling happened in VisualSync.
Solution: Threaded `dependsOn` into `.Schedule(_targetCount, DefaultTargetBatch, dependsOn)` and returned `_validationHandle`; if stale work is still pending, the method returns `JobHandle.CombineDependencies(dependsOn, _validationHandle)` instead of forcing completion.
Rejected Alternatives: Calling `.Complete()` in Simulation to clear stale work; that is the specific stall class being removed. Broad Voxel/Fluid rewrites; static scan showed their known hot chains already pass handles forward or use existing dispatcher fences.
Scalability potential: Low/Middle preserve worker overlap; High/Ultra can spend saved main-thread time on VisualSync fidelity rather than waiting on validation.
Hardware Impact: Avoids one possible main-thread forced wait path. Worst-case avoided stall equals validation job duration; measured value absent, static risk range 1000-8000 us.

## Decision 06 - Obsolete Sync Point Handling

Problem: The scan found `Thread.MemoryBarrier` and `lock` sites across GlobalDataVault, bridge facades, IO/database services, blackbox, watchdog, and editor fuzzers. Removing them blindly would break cold IO safety or native ownership fences.
Solution: Removed the obsolete `JobHandle.ScheduleBatchedJobs()` call from MemorySentinel's former VisualSync path and left deliberate barriers/managed locks untouched. Runtime hot-path proof focuses on the modified dispatcher methods, not cold IO gates.
Rejected Alternatives: Mass deleting barriers/locks by token search; unsafe and outside source-proven bottlenecks. Replacing GlobalDataVault interlock barriers; they are part of the owner memory protocol.
Scalability potential: Low through Ultra benefit from not corrupting cold safety routes; presentation gains come from phase movement, not fake lock deletion.
Hardware Impact: No direct microsecond gain claimed for retained barriers. Removed VisualSync scheduling hint saves only scheduling overhead, estimated below 50 us; the real gain is phase correctness.

## Decision 07 - Regression Proof Surface

Problem: Phase regressions are easy to reintroduce by adding `.Schedule()` to any `VisualSyncTick` or `GraphicsBuffer.SetData()` to `PreSimulationTick`.
Solution: Added `ConcurrencyPhaseLifecycle1426EditTests` with source-level guards for VisualSync scheduling, PreSimulation GraphicsBuffer upload, MemorySentinel phase split, DispatcherJobFence forced-completion warning, GlobalDataVault compaction rollback, and a reverse-order mock lock contention test.
Rejected Alternatives: Runtime JSON report or profiler dump; assignment forbids report spam and the failure mode is statically visible in source.
Scalability potential: Prevents future frame-time regressions across Low/Middle/High/Ultra by keeping simulation work out of presentation.
Hardware Impact: Editor-only tests add no runtime cost. Prevented regressions target 150-8000 us depending on violation type.

## Decision 08 - VisualSync Vault Lock Removal

Problem: `VisualPressureAgingRuntime.VisualSyncTick` acquired DataVault buffer pins for aging params, degradation, and runtime DTO while performing GPU upload. That placed lock lifecycle inside VISUAL_SYNC and could stall presentation on contention.
Solution: Removed those `TryLockBuffer` calls from VisualSync. The visual phase now resolves short current-phase views, uploads to double-buffered `GraphicsBuffer` targets, publishes upload timing to private fields, and lets the simulation telemetry job write the runtime DTO next frame through its existing scheduled job.
Rejected Alternatives: Moving GPU upload to PostSimulation; that violates phase ownership for presentation work. Persistent staging NativeArrays; that expands native lifetime and creates another ownership surface.
Scalability potential: Low keeps upload cheap and fail-closed; Middle/High/Ultra keep VISUAL_SYNC free of Vault lock stalls while allowing richer shader payloads.
Hardware Impact: Removes three VisualSync lock acquisitions per dirty upload frame. Expected gain on i3/MX350 is 10-80 us normally, more if contention was present.

## Decision 09 - Analytics Worker Lock Flattening

Problem: `AsynchronousTelemetryExporter` held nine DataVault pins for the background worker lifetime. That is a concrete multi-lock deadlock vector and blocks relocation through many buffer-specific lock bits.
Solution: Replaced the nine worker pins with one `WorkerVaultMutationGuardMask` acquired by `TryAcquireMutationGuard`. `GlobalDataVault.HasActiveBurstLocks` now treats any active mutation guard as a relocation/defrag blocker, and `TryAcquireMutationGuard` rolls back if the compaction fence rises during acquisition.
Rejected Alternatives: Per-access worker `TryLockBuffer`; it would thrash the worker and still risk nested lock windows around compression/handoff. Copying all worker buffers out of DataVault; too large for this patch and would duplicate memory ownership.
Scalability potential: Low avoids worker deadlock and relocation stalls; Middle/High/Ultra keep analytics export isolated without multiplying DataVault lock state.
Hardware Impact: Startup/shutdown removes eighteen lock/unlock calls and failure cleanup paths. Runtime worker views remain zero-GC; compaction now has one guard bit to test instead of nine pinned buffers.

## Decision 10 - Bulkhead Multi-Buffer Writer Flattening

Problem: Bulkhead intent publication, intent consumption, layout-fault telemetry, and editor CSV import had two-buffer mutation windows. The worst cases were intent ring/control, telemetry ring/cursor, and profile/scratch import routes.
Solution: Converted those paired writes to one explicit mutation guard per route: `IntentMutationGuardMask`, `BulkheadTelemetryMutationGuardMask`, and `BulkheadProfileImportMutationGuardMask`. Each route resolves transient DataVault handles only after the guard is acquired and releases the guard in `finally`.
Rejected Alternatives: Keeping paired `TryAcquireWriteLock` calls because they are short-lived; duration does not remove reverse-order deadlock risk. Global DataVault lock semantic rewrites were rejected because other domains currently acquire mutation guards before solver pins and require separate ownership work.
Scalability potential: Low avoids deadlock and relocation stalls during construction/editor import. Middle keeps normal bulkhead cadence. High/Ultra can spend the protected frame budget on visual bulkhead shader payloads, not lock recovery.
Hardware Impact: Removes 2 write-lock acquisitions from layout-fault telemetry, 2 from file import, and 2 from intent ring/control paired access. Expected low-end saving is 5-40 us per cold event; the real gain is eliminating the deadlock vector.
