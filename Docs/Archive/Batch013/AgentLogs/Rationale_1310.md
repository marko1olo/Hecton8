# Rationale_1310

Status: IMPLEMENTED PARANOID-SCANNER-PASS-50 PERSISTENT-ALIAS-ADMISSION-FENCE NO-BUILD-REQUESTED
Agent: 1310 CORE_MEMORY_WARDEN_AND_LOCK_SENTRY

## Decision 001 - Scope Boundary

Problem: Prompt grants authority over central unmanaged storage, but project worktree is dirty across hundreds of files.
Solution: Restrict implementation to `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`, `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`, editor/test tooling required by tasks, and 1310-owned docs/reports.
Rejected Alternatives: Broad Core refactor; touching unrelated GlobalRegistry/SignalBus debt; reverting external dirty files.
Scalability potential: Low/Middle/High/Ultra unchanged because this protects memory truth rather than adding gameplay load.
Hardware Impact: i3/MX350 gain is crash/stall avoidance; estimated hot-path added cost target under 5 us per lock path, 0 B GC.

## Decision 002 - Mandate Set

Problem: Task spans native memory, atomics, ARM64 layout, telemetry, and phase boundaries.
Solution: Read seven relevant mandates: native memory/jobs, arena allocator, ARM64 DTO layout, crash telemetry, zero-GC, performance budgets, execution phases.
Rejected Alternatives: Reading all 35 mandates before any source inspection; insufficient and slower for this narrow memory domain.
Scalability potential: Low uses fail-closed fewer relocations; Middle keeps normal cadence; High/Ultra may run richer telemetry without changing gameplay truth.
Hardware Impact: Avoids expensive global locks; expected low-end benefit is avoiding multi-ms stalls from unsafe relocation retries.

## Decision 003 - Race Window Classification

Problem: Current write-lock and buffer-pin paths are partially guarded but publish state in unsafe order for compaction/growth.
Solution: Classify races before editing: writer id before block pin, block pin before active mask, arena growth without active mask, telemetry cursor naked writes.
Rejected Alternatives: Add one managed lock around all allocator operations; rejected because it serializes Core memory and can stall Burst admission on low-end CPUs.
Scalability potential: Low skips relocation/growth under contention; Middle retries next owner phase; High/Ultra can move more bytes only when active masks are zero.
Hardware Impact: i3/MX350 avoids stale-pointer crash class; expected cost is volatile/interlocked operations, target under 10 us per contested lock.

## Decision 004 - DTO Layout Boundary

Problem: Telemetry and allocator metadata must stay ARM64-safe while adding sync evidence.
Solution: Keep existing sizes: `VaultArenaBlock` 32 B, `VaultBufferMeta` 64 B, `MemoryDefragTelemetryEntry` 128 B. Use existing reserved fields for skip counters where possible.
Rejected Alternatives: Enlarge `VaultArenaBlock` to one full cache line; rejected because it doubles block-map memory and changes established ABI without compile/runtime proof.
Scalability potential: Low preserves smaller metadata cache footprint; High/Ultra can spend saved memory on richer presentation telemetry instead of allocator bloat.
Hardware Impact: MX350/i3 keeps block scan cache density; false-sharing risk remains noted for adjacent 32-byte block mutations.

## Decision 005 - Block Mutation Gate

Problem: Writer locks, pin locks, frees, compaction moves, and arena growth all mutate `VaultArenaBlock` state; early active-mask publication created a pending-bit race where a later unlock could clear a bit for a new waiter on the same 32-bit bucket.
Solution: Add `_blockMutationGate`; publish `SetActiveLockBit` only while the gate is held and immediately before `ActiveWriterSystemID` or `Reserved1` publication. Clear active bits under the same gate or through a bounded gated helper.
Rejected Alternatives: Managed `lock`; rejected for hot allocator admission. Pending mask without ref counts; rejected because same-bit collisions remain possible.
Scalability potential: Low skips locks/growth under contention; Middle retries next phase; High/Ultra can compact larger slices only when lock masks are objectively zero.
Hardware Impact: i3/MX350 pays one interlocked gate on lock/pin/free paths, estimated 2-8 us under light contention, in exchange for eliminating stale arena aliases during relocation.

## Decision 006 - Arena Growth Fence

Problem: `H8Memory.ReallocateRaw` frees the old arena pointer; any active NativeArray alias becomes dangling if growth runs while a job owns a buffer view.
Solution: `TryGrowArenaForBytes` and `TryGrowArena` now check `HasActiveBurstLocks(0u)` and `_compactionFence`, queue `_deferredArenaGrowthBytes`, and recheck inside `_blockMutationGate` before `H8Memory.ReallocateRaw`.
Rejected Alternatives: Let `H8Memory.ReallocateRaw` discover aliases; rejected because raw allocator cannot know DataVault job ownership. Blocking until locks drain; rejected as a hang vector.
Scalability potential: Low defers growth and survives with less relocation; Middle retries at dispatcher phase; High/Ultra can grow/relocate once active masks clear.
Hardware Impact: MX350 avoids access violation class; deferred growth costs one volatile long and a telemetry write on blocked pressure.

## Decision 007 - Telemetry And Dump Route

Problem: Existing blackbox ring did not record locked compaction skips and cursor publication used plain writes; 1310 requires a raw `Dump_1310_MemorySentry.bin` route on sync failure.
Solution: Reuse telemetry offset 76 as `LockedSkipCount`, write entries through `NativeArrayUnsafeUtility.GetUnsafePtr` + `UnsafeUtility.MemCpy`, publish cursor/count with volatile/interlocked operations, and cold-start one background dump worker guarded by `_memorySentryDumpInFlight`.
Rejected Alternatives: `BinaryWriter` for 1310 dump; rejected because it allocates wrappers in the fault path. Spawning `new Thread` from hot fault paths; rejected because it violates hot zero-GC. `Debug.Log` only; rejected because post-mortem data must survive crashes.
Scalability potential: Low records only fixed 300-entry ring; Middle/High/Ultra can interpret the same binary evidence without changing gameplay DTO shape.
Hardware Impact: Hot telemetry remains unmanaged pointer copy; dump is cold and backgrounded. Low-end runtime path stays 0 B GC.

## Decision 008 - Fuzzer And Scanner Boundary

Problem: Runtime proof requires Unity/editor execution, but the CLI session cannot safely run Unity and dotnet build is blocked by existing dotnet processes.
Solution: Add editor-only `OOP_MemorySentryConcurrentRelocationFuzzer` for 100,000-frame concurrent allocation/release/pin/write-lock/compaction stress and add `Tools/OOP_MemorySentry_Scanner.py`; execute scanner now and write `MEMORY_SENTRY_OPTIMIZATION_REPORT_1310.json`.
Rejected Alternatives: Claiming fuzzer runtime success without launching Unity; rejected as fake proof. Running dotnet despite active dotnet processes; rejected by project build law.
Scalability potential: Low uses fail-closed gates; Middle/High/Ultra fuzzer stresses increasing relocation and lock cadence without adding player runtime systems.
Hardware Impact: Scanner is offline; fuzzer is editor-only. No player-frame cost.

## Decision 009 - Release Free vs Rollback Free

Problem: A raw-gated `FreeBlock` can mutate block state between `_compactionFence=1` and the compactor taking `_blockMutationGate`, which violates the compaction-aware release invariant.
Solution: Split normal release and rollback cleanup. `TryFreeBlock` uses `TryEnterBlockMutationGate()` and rejects active compaction. `TryFreeBlockRollback` uses raw gate only for unpublished allocation cleanup where metadata route has not been committed and compactor must be kept out by the gate.
Rejected Alternatives: One free helper for every caller; rejected because normal release and allocation rollback have different safety contracts. Waiting for compaction in release; rejected as stall risk.
Scalability potential: Low fails closed under compaction; Middle retries owner phase; High/Ultra maintain same truth route with higher compaction cadence only when gates clear.
Hardware Impact: Normal release pays the same gate as lock paths; rollback avoids orphaning unpublished blocks under pressure.

## Decision 010 - Paranoid Zero-GC Boundary

Problem: The first scanner proved only fence/reallocation invariants and did not prove absence of managed constructs in hot allocator paths.
Solution: Extend `OOP_MemorySentry_Scanner.py` to scan hot methods for `new`, LINQ, string formatting, foreach, file/thread APIs, and managed IO. Current result: 0 hot managed hits. Cold managed sites are explicitly listed as forensic dump thread/file paths.
Rejected Alternatives: Claiming whole-file zero managed allocation; rejected because forensic dump file IO and mandated background thread are managed cold-path facilities. Hiding them would be false reporting.
Scalability potential: Low/Middle stay 0 B GC in hot allocator paths; High/Ultra may write richer forensic data only on cold failure paths.
Hardware Impact: No added hot GC. Cold dump path remains outside frame budget and is explicitly reported.

## Decision 011 - ARM64/AUP/Assembly Audit

Problem: Explicit DTO offsets were ABI-safe but source declarations hid the required 8/4/2/1 audit order; no AUP or assembly isolation proof existed in the report.
Solution: Reordered explicit DTO declarations without changing offsets, added byte-offset maps to the scanner report, verified direct AUP float-cast count is zero, and recorded using/asmdef isolation.
Rejected Alternatives: Trusting old offset validation only; rejected because review requires human-readable byte maps and assembly boundary evidence.
Scalability potential: DTO ABI unchanged across Low/Middle/High/Ultra. No spatial math added.
Hardware Impact: ARM64 alignment remains exact; no new runtime memory footprint.

## Decision 012 - Partial Lock Rollback Cleanup

Problem: After an unpublished writer lock or buffer pin was established, later post-validation failure could not depend on a second bounded `_blockMutationGate` acquisition. If the second gate failed, a private block pin could remain set.
Solution: Build the returned writer view inside the original mutation gate. Move rollback to `RollbackWriterLockUnlocked` and `RollbackBufferPinUnlocked`, called only while the original gate is still owned. No bounded second-gate cleanup path remains.
Rejected Alternatives: Reusing normal release helpers; rejected because normal release must honor `_compactionFence`. Bounded raw-gate retry after publication; rejected because failure leaks `Reserved1`/`BlockFlagLocked`. Infinite spin; rejected as a hang vector.
Scalability potential: Low/Middle avoid permanent allocator pin leaks under contention; High/Ultra keep higher compaction cadence without changing DTO truth.
Hardware Impact: Normal hot success path creates the view before releasing the gate; rollback has no second-gate spin. Leak risk from bounded cleanup failure is removed.

## Decision 013 - H8Memory Core Dependency Cycle Removal

Problem: `H8Memory.cs` lives in `Hecton8.Core.Memory`, while `DispatcherJobFence` lives in `Hecton8.Core`. The route card `SHINOBU_356_CORE_MEMORY_DISPATCHER_FENCE_BLOCKER_ROUTE_CARD.md` states that Core already references Core.Memory, so a Core.Memory call back into `DispatcherJobFence` creates an asmdef cycle.
Solution: Restore the local cold teardown helper to direct `ownerHandle.Complete(); ownerHandle = default;`. The only call sites are annotated `[BLOCKING_SYNC_POINT]` owner teardown/shutdown paths.
Rejected Alternatives: Add `using Hecton8.Core`; rejected due asmdef cycle. Keep unqualified `DispatcherJobFence`; rejected as compile-risk. Move dispatcher fence into Contracts; rejected by existing route-card because it is mutable runtime state, not a pure contract.
Scalability potential: No Low/Middle/High/Ultra behavior change; this is teardown-only dependency correction.
Hardware Impact: No frame impact. Forced complete remains cold shutdown/scene transition only.

## Decision 014 - H8Memory BufferID Collision Removal

Problem: `SaveVoxelDeltaCompaction*` scratch IDs used `70300-70309`, colliding with existing `Biolum*` IDs in the same `BufferID` enum range. Numeric collisions break one fact -> one owner -> one route and can route unrelated systems into the same vault identity.
Solution: Move `SaveVoxelDeltaCompaction*` and `SaveVoxelDeltaNativeSnapshotScratch` to the contiguous free range `70380-70389`; keep Biolum IDs unchanged. Extend `OOP_MemorySentry_Scanner.py` with a duplicate-value check over `H8Memory.BufferID`.
Rejected Alternatives: Leave names distinct but values duplicated; rejected because enum numeric identity is the runtime authority. Move Biolum IDs; rejected because that would mutate a neighboring domain without need.
Scalability potential: Low/Middle/High/Ultra unchanged; this preserves buffer identity, not visual cadence.
Hardware Impact: No frame impact. Prevents cross-domain buffer aliasing that could cause allocator corruption or wrong scratch reuse under pressure.

## Decision 015 - Arena Growth Relocation Fence

Problem: `TryGrowArena` checked active locks and held `_blockMutationGate`, but `_compactionFence` stayed zero during `H8Memory.ReallocateRaw`. Read/resolve paths could observe fence zero while the old arena pointer was being freed and before `_arenaBase`, metadata, and `_buffers` were republished.
Solution: Raise `_compactionFence` with `Interlocked.CompareExchange` before acquiring the raw mutation gate and before `H8Memory.ReallocateRaw`. Keep the fence active until `_arenaBase`, `_arenaBytes`, relocation records, flat metadata, and moved buffer pointers are refreshed; clear it only in `finally`.
Rejected Alternatives: Rely only on `_blockMutationGate`; rejected because read accessors do not take that gate. Rely only on `ActiveBurstLockMask`; rejected because it does not block new read access during relocation.
Scalability potential: Low/Middle/High/Ultra unchanged; growth still fails closed under active aliases and retries later.
Hardware Impact: Arena growth is cold/pressure path. Hot read/write paths gain a volatile fence check already required by the prompt.

## Decision 016 - Hot Dump Request Zero-GC

Problem: Hot rollback/compaction fault paths called `RequestMemorySentryDump`, and that method allocated `new Thread`. The dump itself is cold forensic IO, but the request path was still transitive managed allocation from hot failure code.
Solution: Cold-start `_memorySentryDumpWorker` and `_memorySentryDumpSignal` during vault initialization. `RequestMemorySentryDump` now only atomically marks `_memorySentryDumpRequested` and signals the existing worker.
Rejected Alternatives: Keep per-fault `new Thread`; rejected under literal hot zero-GC. Synchronous dump from hot fault path; rejected because file IO stalls the caller.
Scalability potential: Low keeps fault path bounded; High/Ultra get the same post-mortem data without changing runtime truth.
Hardware Impact: Fault request hot path becomes atomics + `AutoResetEvent.Set`; thread allocation moves to cold boot.

## Decision 017 - Volatile Fence And H8Memory Dump Hygiene

Problem: A raw token scan found remaining `_compactionFence != 0` reads in alias/sweep/mock paths and a legacy `BinaryWriter` in the H8Memory fatal leak dump path.
Solution: Convert non-reset `_compactionFence` reads to `Volatile.Read(ref _compactionFence)`. Replace H8Memory `BinaryWriter` dump serialization with `FileStream` plus stackalloc little-endian scalar writers and fixed marker bytes.
Rejected Alternatives: Leave raw reads because they are diagnostic/cold; rejected because the compaction fence is the core relocation barrier. Keep `BinaryWriter` because dump is cold; rejected because the user explicitly required surfacing and removing hidden managed wrappers where practical.
Scalability potential: Low/Middle/High/Ultra unchanged; failure evidence format stays binary and allocator truth unchanged.
Hardware Impact: Volatile reads are already required barrier cost. H8Memory dump change affects cold fatal-leak path only and removes one managed wrapper allocation.

## Decision 018 - Prompt Extraction And Zero-GC Evidence Boundary

Problem: The first post-override CLI extraction regex expected `<AGENT_PROMPT id="1310">` exactly and failed against the actual tag `<AGENT_PROMPT id="1310" role="CORE_MEMORY_WARDEN_AND_LOCK_SENTRY" chat_name="1310">`. Broad token scanning also reports existing cold/init/editor managed constructs, which must not be misreported as hot-path GC.
Solution: Use an attribute-tolerant extraction regex `<AGENT_PROMPT\s+[^>]*id="1310"[^>]*>[\s\S]*?</AGENT_PROMPT>` and reconfirm 11 tasks / 10039 chars. Keep the scanner claim scoped to hot allocator/lock/compaction methods: 0 forbidden managed hits. Report cold/init/editor managed sites separately instead of pretending the entire two-file runtime layer contains no managed constructs.
Rejected Alternatives: Treating the extraction failure as missing prompt was rejected because `Select-String` proved the tag at lines 808-867. Claiming whole-file Zero-GC was rejected because `H8Memory.DumpAllocationTableText`, allocator initialization, and fatal exception paths still contain managed constructs outside the 1310 hot path.
Scalability potential: Low/Middle/High/Ultra unchanged; this is evidence hygiene and prevents false compliance claims.
Hardware Impact: No runtime change. It prevents incorrect release certification based on a brittle parser or an overbroad Zero-GC claim.

## Decision 019 - Writer Release Closure And Dump Dispose Recheck

Problem: `ReleaseWriteLock` released the private block lock under `_blockMutationGate`, then dropped the gate before clearing `ActiveWriterSystemID` and `_activeLocks`. The active bit still protected relocation, so this was not an immediate UAF path, but it left an unnecessary post-gate cleanup window and a bounded cleanup dependency. Dispose also computed `_defragBlackBox` release eligibility before stopping the dump worker, so a worker that completed during stop could leave the ring unreleased.
Solution: Move writer-id clear, metadata write, memory barrier, and active-bit clear into the same `_blockMutationGate` critical section as `ReleaseWriterBlockLockUnlocked`. Add scanner check `ReleaseWriteLock_clears_writer_and_active_bit_inside_mutation_gate`. After `StopMemorySentryDumpWorker`, re-check `_memorySentryDumpInFlight`; release `_defragBlackBox` if the worker is actually done, otherwise intentionally leak the ring rather than risking a background read-after-free. Add scanner check `MemorySentry_dispose_rechecks_dump_state_after_worker_stop`.
Rejected Alternatives: Keeping the previous active-bit-after-gate sequence was rejected because the scanner would certify a avoidable synchronization window. Forcing `_defragBlackBox` release after a timed-out dump was rejected because a forensic background writer could still hold a read pointer.
Scalability potential: Low/Middle/High/Ultra unchanged; release path becomes more deterministic and dump teardown avoids needless native leak after normal completion.
Hardware Impact: Writer release removes a second gate acquisition path and shortens contested cleanup. Expected low-end benefit is fewer failed clear attempts under lock churn; cost is zero extra hot allocations.

## Decision 020 - Post-Override Evidence Boundary

Problem: A repeated override required a fresh proof pass, but launching dotnet/build would violate the coordinator instruction and add no value to the static synchronization proof.
Solution: Re-extract the 1310 XML with attribute-tolerant regex, count `Task 01:` through `Task 11:`, re-read the exact runtime line ranges, rerun `OOP_MemorySentry_Scanner.py`, and keep the certification scoped to static code evidence. Current scanner after Decision 021: `checkCount=37`, `failedCount=0`, `hotManagedHits=0`.
Rejected Alternatives: Running dotnet/build despite the explicit build discipline was rejected. Claiming runtime/Unity fuzzer success was rejected because the editor fuzzer was not executed. Claiming whole-file zero managed allocation was rejected because 23 cold/init/forensic managed sites remain and are reported.
Scalability potential: Low keeps skip/defer/fail-closed behavior under contention; Middle retries at owner phase; High/Ultra can compact/grow more often only when active masks are objectively clear.
Hardware Impact: No code cost added in this pass. Static verification cost was offline only; runtime hot-path estimate remains volatile/interlocked/gate overhead with 0 B GC in audited lock/compaction paths.

## Decision 021 - External View Relocation Closure

Problem: `TryOpenAliasBuffer` created a `NativeArray` view before `BlockFlagExternalView` was published. Between the initial fence read and `MarkExternalView`, live compaction or arena growth could relocate the underlying block and leave the just-created alias with a stale pointer. `FreeBlockLocked` and `TryReallocateBlockLocked` also failed to reject `BlockFlagExternalView`, and the editor fuzzer used numeric `BufferID` ranges already owned by Biolum and Predator Cognition domains.
Solution: Move external-view publication before `H8Memory.CreateNativeArrayView`; make `MarkExternalView` acquire the compaction-aware `_blockMutationGate`; add `HasPinnedExternalViews` and block arena growth before `H8Memory.ReallocateRaw`; reject `BlockFlagExternalView` in free and per-buffer resize paths; move fuzzer IDs to reserved free range `99000-99055`; expand scanner to 37 checks.
Rejected Alternatives: Leaving external-view as compaction-only metadata was rejected because arena growth also invalidates unmanaged aliases. Adding a managed lease object or reference-counted wrapper was rejected because it adds GC and a new authority route. Keeping fuzzer IDs in low real-domain range was rejected because validation tooling must not impersonate production owners.
Scalability potential: Low fails closed and defers growth while any external alias exists; Middle retries at owner phase; High/Ultra can still compact/grow aggressively once aliases are absent, without changing DTO layout or authority identity.
Hardware Impact: Alias acquisition pays one existing mutation gate before view creation. Growth/free/realloc gain a linear block scan only on cold pressure/release/resize paths. Hot lock and telemetry paths remain 0 B GC.

## Decision 022 - Alias Reader Metadata Gate And Full DTO Map

Problem: `MarkAliasReader` wrote `LastAliasRequester` without `_blockMutationGate`, leaving a small unsynchronized metadata write adjacent to the external-view pin route. The scanner also certified only seven GlobalDataVault DTOs and omitted the explicit H8Memory DTOs plus `MacroDatabasePayloadCacheEntry`.
Solution: Gate `MarkAliasReader` with `TryEnterBlockMutationGate`, move the metadata write to `MarkAliasReaderLocked`, and require `BlockFlagExternalView` before `LastAliasRequester` is written. Extend `OOP_MemorySentry_Scanner.py` to audit 11 explicit DTOs across both files, including internal fields and `MacroDatabasePayloadHandle` size. Reorder `BlockDescriptor` and `H8MemoryTelemetryEntry` declarations to preserve 8/4/2/1 source order without changing explicit offsets.
Rejected Alternatives: Leaving alias-reader metadata outside the gate was rejected because it weakens the same relocation metadata route used by `MarkExternalView`. Claiming "all DTOs" from a seven-struct map was rejected as false evidence. Changing offsets or packing was rejected because the existing ABI already validates and only declaration order needed correction.
Scalability potential: Low/Middle/High/Ultra unchanged; this is metadata serialization and evidence coverage, not a gameplay load change.
Hardware Impact: Alias-reader path pays one existing mutation gate. DTO changes are declaration-order only under explicit offsets; runtime layout remains 40/48/64/128-byte as validated.

## Decision 023 - Pure Transient Read View Fence Recheck

Problem: `TryResolveHandle`, `TryReadHandle`, `TryReadOnlyHandle`, and `TryResolveSlice` are pure current-phase accessors, so they cannot publish relocation pins without violating the project law that read accessors must not mutate global state. They still had a narrow window where `_compactionFence` could become active after the pointer view was created but before the accessor returned.
Solution: Keep the accessors pure and non-pinning. Add `Thread.MemoryBarrier()` plus a post-create `Volatile.Read(ref _compactionFence)` after `CreateNativeArrayView`, `CreateReadOnlyNativeArrayView`, and `GetSubArray`; return `false` if relocation/growth started during view construction. Update interface summaries to state current-phase lifetime and cross-phase/job users must take a lock or pinned alias.
Rejected Alternatives: Marking every pure read accessor as `BlockFlagExternalView` was rejected because it mutates global metadata from a read route and, without a release API, can permanently block compaction/growth. Leaving only the pre-create fence was rejected because it does not cover relocation activation during view construction.
Scalability potential: Low/Middle/High/Ultra unchanged; pure transient reads stay cheap while long-lived consumers must use explicit lock/pin routes.
Hardware Impact: Adds one memory barrier and volatile fence read to transient read accessors. No managed allocation, no DTO layout change, no permanent external-view pin.

## Decision 024 - JacobianFoam NativeArray Lock Contract Repair

Problem: The cross-domain NativeArray audit found `JacobianFoamGpuRuntime` using `GlobalDataVault.TryResolveHandle` to obtain mutable `Params`, `WakeImpacts`, `Tuning`, and `Telemetry` arrays. Those arrays were then written by `GenerateMockStormStateJob.Run`, direct DTO assignment, and telemetry recording without publishing a vault write lock or read pin. That made the core memory allocator blind to a live unmanaged view and left arena growth/compaction unable to prove stale-pointer safety for this local consumer.
Solution: Treat this as a critical GlobalDataVault contract repair despite being outside the two core files. `Params`, mock `WakeImpacts`, mock `Tuning`, telemetry writes, and default tuning seed now use `TryAcquireWriteLock` through `TryAcquireWriteBuffer`; wake readback, tuning readback, and telemetry dump use `TryLockBuffer` through `TryAcquireReadPin` before resolving the view. Removed private `ResolveParamsArray`, `ResolveTuningArray`, and `ResolveWakeArray` helpers. Extended scanner with `JacobianFoam_vault_nativearray_views_are_locked_or_pinned`.
Rejected Alternatives: Leaving the VFX consumer unchanged was rejected because core stale-pointer protection cannot work if consumers bypass the lock contract. Mutating pure read accessors globally was rejected because it violates read-accessor purity and can permanently block compaction without a release route. Adding a new managed lease object was rejected due GC and new authority route.
Scalability potential: Low/Middle/High/Ultra unchanged in visual truth; low-end devices fail closed by skipping a foam update when the vault is relocating, high-tier devices still execute the same GPU visual overkill when locks are available.
Hardware Impact: Synchronous foam update pays existing vault write/read lock gates around memory views. No new managed allocation. The saved failure class is arena relocation during foam NativeArray use.

## Decision 025 - Changed-File Text Scan Boundary

Problem: The latest override demanded a literal changed-file scan for `new`, string formatting, LINQ, `ToString`, file IO, and NativeArray bypasses. A whole-repository "no `new`" claim would be false: the changed files intentionally contain cold allocator initialization, cold forensic dump IO, editor fuzzer threads/report strings, scanner text literals, and value-type constructors in Jacobian foam rendering.
Solution: Keep the release claim scoped to what the scanner actually proves: `hotManagedHits=0` for allocator/lock/compaction hot methods, `BinaryWriter=0`, `string.Format=0`, `.ToString(`=0, LINQ=0, `foreach=0`, and raw `_compactionFence` reads `0` in the changed runtime files. Report the cold/editor/tool managed sites explicitly instead of erasing them from the evidence. Jacobian `new Vector4`, `new double2`, `new double3`, `new FoamRenderTelemetryEntry`, and `new GenerateMockStormStateJob` are value-type constructions; they do not allocate managed heap but remain visible in text scans.
Rejected Alternatives: Removing cold forensic IO was rejected because the black-box dump is mandated. Removing editor fuzzer threads was rejected because the fuzzer is proof tooling, not runtime player code. Rewriting every value-type constructor only to satisfy a raw text grep was rejected because it adds churn without reducing GC.
Scalability potential: Low/Middle/High/Ultra unchanged; this pass changes evidence hygiene, not gameplay truth or visual cadence.
Hardware Impact: Runtime hot-path impact is unchanged: volatile/interlocked/gate overhead only, with no managed allocations in the audited lock/allocator/compaction paths. Editor/tool allocations remain outside player frame budget.

## Decision 026 - Core Memory Fatal Call-Site Closure

Problem: The strict post-override scan found that normal Core Memory routes still called `FatalMemoryException.Throw*` on bad owner, type mismatch, allocation tracking failure, untracked pointer, and ABI mismatch paths. That is a managed exception allocation route in runtime code, even if the original intent was fail-fast.
Solution: Convert target `GlobalDataVault.cs` and `H8Memory.cs` call sites to fail-closed returns. H8Memory allocation/free/reallocate/alias paths now record `H8MemoryTelemetryFlags.Fault` into the fixed blackbox when tracking state exists, then return `default`, `false`, dependency, or `null` without freeing unverified pointers. GlobalDataVault type and requester validation now returns false/default and dumps the memory sentry ring where available. Scanner added `Core_memory_runtime_fatal_call_sites_removed`.
Rejected Alternatives: Rewriting or deleting the `FatalMemoryException` type was rejected because neighboring domains still call it and changing that behavior would mutate outside 1310 authority. Freeing unknown/untracked pointers was rejected because safe failure is leak-preferential: never corrupt allocator truth to avoid a leak.
Scalability potential: Low/Middle/High/Ultra unchanged; failure semantics become deterministic and do not change DTO layout, save identity, or gameplay truth.
Hardware Impact: Hot normal path unchanged. Fault path saves managed exception allocation; on i3/MX350 the expected gain is not frame time, it is avoiding exception unwinding and preserving a binary fault trail.

## Decision 027 - Vault Bootstrap Fail-Closed Gate

Problem: After `H8Memory.Allocate*` was converted to fail-closed returns, `GlobalDataVault.Initialize` could still proceed with default critical `NativeArray` fields; `Create()` also wrote `_latestCreated = vault` unconditionally after failed init.
Solution: Add immediate `IsCreated` gates after every critical H8Memory allocation, add `HasInitializedCriticalNativeStorage`, route every bootstrap storage failure through `AbortInitialize()`, and publish `_latestCreated` only when `vault._initialized` is true.
Rejected Alternatives: Letting default arrays produce zero-length loops was rejected because it hides allocation failure and can publish an unusable vault. Throwing managed exceptions was rejected because this is the exact fail-closed path being removed.
Scalability potential: Low/Middle/High/Ultra unchanged; startup either owns the complete native route or publishes nothing.
Hardware Impact: No hot-frame cost. Startup gains deterministic cleanup on memory pressure; i3/MX350 avoids partial vault boot and downstream invalid NativeArray access.

## Decision 028 - Owner-Bound Buffer Pin Unlock

Problem: `TryLockBuffer(BufferID, SystemID)` took `lockOwner` but did not bind it to the active pin. Any caller with the same `BufferID` could call `TryUnlockBuffer` and decrement `Reserved1`, potentially clearing the relocation blocker while another domain still owns a live `NativeArray` view.
Solution: Require non-unknown owners on the owner-tagged lock/unlock path, write `LastAliasRequester = lockOwner` while holding `_blockMutationGate`, reject different owners, clear it only when the last pin is released, and restore the previous requester on rollback.
Rejected Alternatives: Adding a managed lease/reference object was rejected because it adds GC and another authority route. Leaving the obsolete one-arg overload usable was rejected because it cannot prove owner identity.
Scalability potential: Low/Middle/High/Ultra unchanged; compaction now sees deterministic owner-bound pins instead of anonymous pin counts.
Hardware Impact: Hot pin/unpin adds metadata owner comparisons and one metadata write under the existing gate; expected cost is under 1 us compared to the existing gate and prevents stale-pointer relocation on cheap CPUs.

## Decision 029 - Atomic Alias Publication Rollback

Problem: `PinReadOnlyAlias` still had a two-phase publication route: `TryOpenAliasBuffer` published `BlockFlagExternalView`, then the caller wrote alias-reader metadata through a second mutation-gate call. If the second phase or `NativeArray` alias creation failed after the external-view pin was published, relocation/growth could remain blocked without a returned alias handle.
Solution: Move requester validation, external-view pin, alias-reader metadata, and `H8Memory.CreateNativeArrayView` under the same `_blockMutationGate` in `TryOpenAliasBuffer`. Add `RollbackAliasPublicationLocked` to restore `BlockFlagExternalView`, block version, H8 descriptor state, vault generation, and previous `LastAliasRequester` before releasing the gate. Extend the scanner to prove alias rollback and to broad-scan production calls to obsolete one-arg buffer pin APIs.
Rejected Alternatives: A managed alias lease/ref-count object was rejected because it adds GC and a second authority route. Keeping the two-gate publication was rejected because failure between gates leaves stale relocation metadata. Clearing the external-view flag after releasing the gate was rejected because compaction/growth could observe a false live pin.
Scalability potential: Low fails closed without permanent relocation blockage; Middle retries alias acquisition at the owner phase; High/Ultra can keep aggressive relocation/growth cadence once aliases are absent, without changing DTO layout or gameplay truth.
Hardware Impact: Alias-open pays a longer existing mutation-gate critical section on a pinned read route. Hot allocator/telemetry paths remain 0 B GC. Low-end i3/MX350 gain is avoiding a permanent relocation skip and stale alias metadata after partial alias creation failure.

## Decision 030 - Read-Alias Payload Mutation Removal

Problem: `TryOpenAliasBuffer` still called `SanitizeFinitePayload` after releasing `_blockMutationGate`. That made `PinReadOnlyAlias` a hidden payload writer, violated read-accessor purity, and could race with the real owner write route while only relocation metadata was pinned.
Solution: Remove the post-gate sanitizer call from `TryOpenAliasBuffer`. `SanitizeFinitePayload` now remains only on owner allocation/resize/ensure paths where the owner is establishing or resizing storage. Add scanner check `Read_alias_path_does_not_sanitize_or_mutate_payload` to forbid sanitizer calls in `TryOpenAliasBuffer`, `PinReadOnlyAlias`, `TryResolveHandle`, `TryReadHandle`, `TryReadOnlyHandle`, and `TryResolveSlice`.
Rejected Alternatives: Keeping read-alias sanitizer for NaN repair was rejected because a read API must not rewrite truth data. Moving sanitizer inside the alias mutation gate was rejected because the gate protects relocation metadata, not logical write ownership. Adding a managed lease to track sanitizer authority was rejected due GC and new authority route.
Scalability potential: Low/Middle/High/Ultra unchanged in visual fidelity; read-alias routes remain relocation-safe but do not alter gameplay/data truth. NaN repair remains tied to owner initialization/resizing rather than consumer reads.
Hardware Impact: Alias open removes an O(n) finite scan from the read-alias path. On i3/MX350 this saves scan time proportional to buffer length and removes a data-race class; hot managed allocation remains 0 B in the audited paths.

## Decision 031 - Orphan Reclaim Owned-Fence Free

Problem: `SweepOrphanedHandles` raises `_compactionFence` before reclaiming orphaned buffers, but `TryReleaseOrphanedBuffer` still called normal `TryFreeBlock`. Normal free is compaction-aware and rejects while the fence is active, so orphan reclaim could fail under its own exclusion. The method also wrote a metadata tombstone before block free succeeded, which could leave a live block/buffer with invalid metadata after a failed free.
Solution: Add `TryFreeBlockUnderOwnedFence` for callers that already own the relocation exclusion route. It uses the raw `_blockMutationGate`, calls `FreeBlockLocked`, and does not check `_compactionFence` again. `TryReleaseOrphanedBuffer` now frees with this helper first and removes buffer/metadata/key only after free succeeds. `TryFreeBlockRollback` uses the same helper because rollback is an unpublished-allocation cleanup route. Scanner check `TryReleaseOrphanedBuffer_frees_under_owned_fence_before_metadata_tombstone` proves the order and gate type.
Rejected Alternatives: Reusing normal `TryFreeBlock` was rejected because it self-blocks during orphan sweep. Temporarily clearing `_compactionFence` was rejected because it opens a relocation window. Keeping tombstone-before-free was rejected because failed free corrupts metadata truth. Adding a managed recovery list was rejected due GC and a second authority route.
Scalability potential: Low/Middle/High/Ultra unchanged in gameplay truth; orphan reclaim now actually frees native blocks at owner phase instead of silently preserving pressure. High/Ultra can tolerate more aggressive compaction cadence because stale orphan state is not retained.
Hardware Impact: Low-end i3/MX350 avoids leaked orphan blocks and later arena pressure. Cost is one existing raw mutation gate in a fenced orphan-reclaim path; audited hot lock/allocator/compaction paths remain 0 B GC.

## Decision 032 - External View Admission Fence

Problem: `BlockFlagExternalView` blocked compaction, free, and per-buffer reallocation, but writer-lock and owner read-pin admission did not reject it. A persistent read-only alias has no release token; allowing a writer lock while it is published permits mutable access to memory that an external `NativeArray` may still read. Allowing a read pin over the same external view also lets `TryUnlockBuffer` clear `LastAliasRequester` while `BlockFlagExternalView` remains set, erasing alias-owner evidence.
Solution: Make `TryAcquireWriteLock` fail closed when `meta.LastAliasRequester != SystemID.Unknown` or when the occupied block has `BlockFlagExternalView`, both before and inside `_blockMutationGate`. Make `TryLockBuffer` fail closed on `BlockFlagExternalView` before publishing the active lock bit. Make `TryUnlockBuffer` preserve `LastAliasRequester` if `BlockFlagExternalView` is still present. Add scanner check `ExternalView_blocks_writer_and_read_pin_admission`.
Rejected Alternatives: Letting same-owner read pins stack over a persistent alias was rejected because alias lifetime has no release token. Clearing `BlockFlagExternalView` from unlock was rejected because unlock owns only transient read pins, not persistent alias views. Leaving this as "production call count is zero" was rejected because the public API would still contain a latent race route.
Scalability potential: Low/Middle/High/Ultra unchanged in gameplay truth; the allocator now fails closed under persistent alias state instead of mixing mutable and unreleasable read surfaces. High/Ultra compaction cadence remains safe because external views keep relocation blocked until explicit future lease support exists.
Hardware Impact: Cost is one byte-flag check and one metadata-owner check on writer/pin admission. Low-end gain is preventing stale/mixed NativeArray alias access that would cost far more than the branch.

## Decision 033 - Duplicate Persistent Alias Admission Fence

Problem: After PASS-49, `TryOpenAliasBuffer` still allowed a second persistent alias over an existing `BlockFlagExternalView`. Because the alias route has no release token, the second call could overwrite `LastAliasRequester`, hiding the first alias owner and weakening crash evidence even though relocation stayed blocked.
Solution: Make `TryOpenAliasBuffer` fail closed when `hadExternalView` is true or `meta.LastAliasRequester != SystemID.Unknown` before `MarkExternalViewLocked`/`MarkAliasReaderLocked`. Add scanner check `TryOpenAliasBuffer_rejects_existing_persistent_alias` and report field `persistentAliasAdmission`.
Rejected Alternatives: Allowing idempotent same-owner aliases was rejected because there is still no per-alias release count. Adding a managed lease object was rejected due GC and a second authority route. Relying only on production call count zero was rejected because the public API would still carry a latent metadata overwrite route.
Scalability potential: Low/Middle/High/Ultra unchanged in gameplay truth; persistent alias is now a single fail-closed relocation blocker until explicit unmanaged lease support exists.
Hardware Impact: One branch on cold persistent alias open. Low-end i3/MX350 gain is preserving deterministic alias-owner evidence and avoiding mixed stale NativeArray ownership; audited hot allocator/lock paths remain 0 B GC.
