# Rationale_1414

Agent: 1414
Status: APEX STATIC VERIFIED / DESCRIPTOR GATE ADDED / BUILD THROTTLED

## Decision 000 - Scope And Mandate Selection

Problem: The assignment targets unmanaged arena relocation where stale raw pointers can survive generation handles if a job holds a pointer during arena growth.
Solution: Bound scope to ECHELON 1 Native Arena Allocator and read allocator, native memory/job, zero-GC, ARM64 layout, crash telemetry, execution phase, registry, and performance budget mandates before code.
Rejected Alternatives: Broad gameplay or renderer audits were rejected because they do not own pointer relocation. Direct code mutation before source flow mapping was rejected because H8Memory and GlobalDataVault are foundational.
Scalability potential: Low keeps allocator fail-closed and cheap; Middle/High/Ultra can spend saved stability budget on richer telemetry and visual systems without changing memory truth.
Hardware Impact: On i3/MX350, avoiding use-after-free editor/player crashes prevents full process loss; runtime microsecond gain is not claimed until source proof exists.

## Decision 001 - Missing Ledger Handling

Problem: Required Status_1414.md and Rationale_1414.md files did not exist at session start.
Solution: Treat absence as fresh batch state, create concise ledgers, and record that no stale previous-batch data was read.
Rejected Alternatives: Waiting for manual wipe was rejected because there was no stale data to wipe. Chat-only progress was rejected by batch protocol.
Scalability potential: File-backed state survives context compression without changing runtime code.
Hardware Impact: No runtime cost.

## Decision 002 - Pointer Relocation Free Site

Problem: `H8Memory.ReallocateRaw` copies old arena bytes into a new unmanaged block, then frees `oldPointer` at `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:2970`. A Burst job holding a previously resolved `NativeArray<T>` pointer can write into freed memory if relocation happens while any lock/pin is active.
Solution: Treat the free site as the critical fault. The caller must hold the compaction fence and prove zero active lock mask plus zero external pins before `ReallocateRaw` can execute. The method itself will require a relocation permit so future internal callers cannot bypass the guard by accident.
Rejected Alternatives: Generation-handle validation was rejected because it only protects future resolves, not a raw pointer already handed to a job. Post-copy validation was rejected because the old pointer is already invalid after free.
Scalability potential: Low/Middle/High/Ultra all use the same safety truth. Higher devices may grow larger arenas, but no tier is allowed to weaken pointer relocation guards.
Hardware Impact: Prevents hard editor/player crash on i3/MX350 class devices. Microsecond saving is secondary and not claimed without profiler data.

## Decision 003 - Deferred Growth Atomic Semantics

Problem: Existing `_deferredArenaGrowthBytes` uses an Interlocked max writer, but successful growth clears the field with a blind volatile write. If another request queues a larger target while growth is fenced, the final zero can erase that later request.
Solution: Keep the max-writer loop and replace blind clearing with a compare-exchange loop that clears only when the current arena can satisfy the observed pending target. This makes concurrent larger requests monotonic: they either remain queued or are cleared only when a free block is large enough.
Rejected Alternatives: A managed lock was rejected as hot allocation-path poison. A naked `_deferredArenaGrowthBytes = 0` was rejected because it loses races. A queue of request records was rejected because only the maximum required contiguous size matters.
Scalability potential: Low uses fail-closed no-stall behavior. Middle/High/Ultra can allow larger arena caps through existing `GlobalQualityWeight` capacity limits while preserving the same atomic queue.
Hardware Impact: On i3/MX350 this avoids lock contention and keeps blocked allocation cost to scalar atomic operations plus one bounded block scan in maintenance.

## Decision 004 - Quiescent Phase Selection

Problem: Growth after simulation must occur only after simulation jobs and write locks drain, but before the next frame's allocations need the larger arena.
Solution: Use `SystemDispatcher.RunMasterPostSimulationPhase` after the dispatcher completes the master simulation job fence and after PostSimulation systems run. This is the documented completion/swap window and already records memory blackbox heartbeat nearby.
Rejected Alternatives: PreSimulation defrag was rejected for deferred growth because the allocation denial already happened in the current frame. LateFrame was rejected because presentation reads may already be consuming snapshots.
Scalability potential: All quality tiers share the same phase. Higher tiers may produce more pressure, but the phase remains deterministic.
Hardware Impact: One scalar pending check per PostSimulation frame; expected below 1 us when no pending growth.

## Decision 005 - Sentinel Storage Reality

Problem: The prompt hypothesized `Dictionary<IntPtr,string>` tracking. Current `NativeMemorySentinel` uses fixed managed arrays with string labels and stack traces, so the real defect is not Dictionary churn; it is unsynchronized mutation and non-fatal service shutdown leaks.
Solution: Serialize register/unregister mutations with an Interlocked spin gate and add `FatalMemoryLeakException` on teardown validation. Preserve current arrays to avoid broad caller churn; failure-only message formatting may allocate because the process is already in a fatal leak path.
Rejected Alternatives: Replacing the whole sentinel with NativeHashMap in this pass was rejected because the public API is string-label based across hundreds of call sites and would create avoidable compile risk. Leaving shutdown as `false` return was rejected because it lets leaks pass non-fatally.
Scalability potential: Low avoids registry races. Middle/High/Ultra can add richer leak diagnostics later without changing allocation truth.
Hardware Impact: Register/unregister add one short Interlocked gate. No gameplay-frame cost unless systems register native memory during gameplay, which is already a mandate violation unless explicitly cold.

## Decision 006 - Raw Reallocation Permit

Problem: `H8Memory.ReallocateRaw` was callable with no local proof that all exported pointers had drained before `UnsafeUtility.Free(oldPointer, allocator)`.
Solution: Add `H8RawReallocationGuard` as a 16-byte explicit-layout permit. The guard is created only after `GlobalDataVault` raises `_compactionFence`, acquires the block mutation gate, and observes zero active lock mask plus zero pinned external views. `ReallocateRaw` returns null before allocation/copy/free if the permit is invalid.
Rejected Alternatives: A comment-only contract was rejected because future internal callsites would compile. Post-copy validation was rejected because the old pointer is already freed at the fault point.
Scalability potential: Low/Middle/High/Ultra all share the same relocation truth. Higher tiers can grow larger arenas but cannot weaken the guard.
Hardware Impact: One branch and 16-byte byref guard check per raw reallocation. Reallocation is cold; expected hot-frame cost is 0 us.

## Decision 007 - Deferred Growth Race Proof

Problem: A larger deferred growth request could be lost if one growth path blindly cleared `_deferredArenaGrowthBytes` after another thread queued a higher target.
Solution: Preserve monotonic max writes with `Interlocked.CompareExchange`. Clear only when `CanSatisfyContiguousFreeBlock(observed)` is true and only by `CompareExchange(0, observed)`. Interleaving proof: if writer raises target after the reader observed old value, clear CAS fails because current != observed; if writer raises target before observation, the reader tests the larger value; if no writer races, clearing is legal only because an existing contiguous free block satisfies the observed request.
Rejected Alternatives: `Volatile.Write(..., 0)` was rejected as a lost-update race. A managed queue was rejected because the only state needed is maximum required contiguous bytes.
Scalability potential: Low keeps allocation fail-closed and defers growth. Middle/High/Ultra can spend larger arena budgets without adding lock contention.
Hardware Impact: Blocked growth costs a bounded atomic loop. On i3/MX350 the expected saving versus a managed lock is approximately 20 us under contention; no honest claim for uncontended steady state.

## Decision 008 - Quiescent Maintenance Route

Problem: Deferred growth must not run from arbitrary callers because allocations can happen while simulation jobs still hold resolved views.
Solution: Process deferred growth in `SystemDispatcher.RunMasterPostSimulationPhase` after simulation completion and PostSimulation systems. The route reuses the cached `IDataVault` and pattern-matches `GlobalDataVault` without hot registry polling.
Rejected Alternatives: Update-loop polling and allocation-site retry were rejected because they make growth timing nondeterministic and can collide with active views.
Scalability potential: Low pays one scalar pending check per frame. Middle/High/Ultra handle larger deferred targets in the same phase.
Hardware Impact: Pending-zero path is expected below 1 us. Growth path remains cold and bounded by arena size.

## Decision 009 - Sentinel Fatal Leak Gate

Problem: Service shutdown leak checks returned `false`, allowing callers to ignore surviving native allocations. Sentinel record mutation also had no serialization.
Solution: `AssertNoAllocationsAfterServiceShutdown` and subsystem reset now throw `FatalMemoryLeakException` with bounded record details. Register/unregister/refresh paths use an Interlocked mutation gate around fixed-array mutation.
Rejected Alternatives: Editor-only warnings were rejected because leak survival corrupts subsequent domain/service state. A broad NativeHashMap rewrite was rejected as outside the minimum defect and risky under current API surface.
Scalability potential: Low devices fail fast instead of leaking native memory across reload. Higher tiers can add richer postmortem dumps later without changing sentinel truth.
Hardware Impact: Cold-path exception allocates only on fatal failure. Mutation gate adds short Interlocked operations to sentinel registration paths, not arena allocation hot loops.

## Decision 010 - Static Verification Boundary

Problem: The user explicitly forbade build spam and other agents are active in the same workspace.
Solution: First verification batch uses `git diff --check`, `rg` callsite scans, brace-balance scans, and editor static tests. A compiler run is deferred until CPU and compiler-process gates are sampled.
Rejected Alternatives: Immediate dotnet rebuild was rejected because it can steal CPU and the current batch contains many simultaneous agents.
Scalability potential: No runtime effect. Verification cost stays bounded and machine-readable.
Hardware Impact: Static scans avoid expensive Roslyn/MSBuild startup on low-end silicon until one build is justified.

## Decision 011 - Compilation Contention Gate

Problem: Low-level unsafe changes deserve compilation, but the user forbade compiler spam and the host reported `CPU_PERCENT=100.0` with active compiler processes. Initial gate saw `dotnet` PID 66768; final gate still showed `dotnet` PID 40436 and `csc` PID 58904.
Solution: Do not launch `dotnet build`. Mark build verification as blocked by host contention, preserve static evidence, and leave the next compiler attempt for a quiet CPU window.
Rejected Alternatives: Starting another build was rejected because it directly violates the CPU gate and risks corrupting sibling-agent throughput.
Scalability potential: No runtime effect. Verification remains reproducible through the static audit and editor test file.
Hardware Impact: Avoided another MSBuild/Roslyn process on saturated i3/MX350-class conditions.

## Decision 012 - Sentinel Mutation Completion

Problem: After the first gate pass, cold scene/transient leak routines still mutated `_records` by setting `LeakReported` or compacting records through `RemoveAt`.
Solution: Serialize `ReportSceneLifetimeLeaks`, `ReapSceneLifetimeLeaks`, and `AuditLongLivedTransientAllocations` with the same Interlocked mutation gate. Persistent reallocation tracking remains under register/refresh gates.
Rejected Alternatives: Leaving cold mutation ungated was rejected because "thread-safe sentinel" would be only partially true. A broad reader lock was rejected because read-only snapshot copying must stay simple and non-mutating.
Scalability potential: Low tier gains deterministic fatal/leak state. Middle/High/Ultra may emit richer diagnostics without racing the registry.
Hardware Impact: No arena hot path impact. Cold leak audit adds only gate acquire/release around existing loops.

## Decision 013 - Final Proof Artifact

Problem: Chat-only completion is rejected by the batch protocol and cannot survive context compression or objective review.
Solution: Write `Docs/Reports/ARENA_ALLOCATOR_OPTIMIZATION_REPORT_1414.json` with race fixes, verification results, blocked build reason, microsecond estimates, quality scaling, and SHA-256 hashes for source/test/proof files.
Rejected Alternatives: Human prose report only was rejected because it cannot be machine-validated. Including a fake build success was rejected because CPU and active dotnet blocked compilation.
Scalability potential: No runtime effect. Proof artifacts make future high-tier allocator changes auditable without repeating archaeology.
Hardware Impact: No runtime cost.

## Decision 014 - APEX Compaction Fence Queue Fix

Problem: APEX review found `TryEnsureVaultBuffer<T>` queued deferred growth only when `_arenaGrowthInProgress` was set. A caller denied by an active compaction fence before that flag could lose its required contiguous target.
Solution: Queue `requiredBytes` on any active `_compactionFence` before returning false. The Interlocked max queue preserves the largest target and drains only from PostSimulation maintenance.
Rejected Alternatives: Leaving the path as "rare" was rejected because allocator correctness cannot depend on timing. Immediate growth under the fence was rejected because it would re-enter the relocation hazard.
Scalability potential: Low tier fails closed and retries after quiescence. Middle/High/Ultra can request larger capacity through existing quality-derived arena limits without losing pressure signals.
Hardware Impact: One Interlocked max loop only on blocked growth. No steady hot allocation cost is claimed.

## Decision 015 - Hot-Path Scan Ambiguity Removal

Problem: The APEX forbidden-term scan counts textual `new ` in modified hot methods. `new VaultBufferMeta { ... }` was a struct initializer, not a managed heap allocation, but it still made proof ambiguous.
Solution: Replace the struct initializer with `VaultBufferMeta meta = default;` and explicit field assignments inside `TryEnsureVaultBuffer<T>`.
Rejected Alternatives: Explaining the exception in prose was rejected because the user requested exact scanning evidence. A Roslyn-only carve-out was rejected because the text proof should remain simple and reproducible.
Scalability potential: No runtime behavior change across tiers. Proof quality improves without changing DTO layout or allocator ownership.
Hardware Impact: No measurable runtime impact expected; generated struct assignment remains stack/local data.

## Decision 016 - APEX JSON Evidence Repair

Problem: The first APEX JSON artifact failed PowerShell `ConvertFrom-Json` because case-insensitive duplicate keys `lowEnd` and `LowEnd` collided.
Solution: Convert the binary switch scan object into an array of `{ term, count }` records and re-run JSON parsing. Current APEX report SHA-256 is `bcd691fd18e52ff61b9e09559838cf79db0cace3e0e9503d51b0f2854570e461`.
Rejected Alternatives: Keeping a JSON file that only some parsers accept was rejected because project evidence must be machine-verifiable on the host shell.
Scalability potential: No runtime effect. Tooling stability improves for later allocator audits.
Hardware Impact: No runtime cost.

## Decision 017 - Final Compilation Throttle Honesty

Problem: A compiler check remains desirable after unsafe memory changes, but the latest gate still shows CPU 100 percent with active `dotnet` PID 40988.
Solution: Do not launch `dotnet build`. Record static verification, exact CPU/process evidence, and mark build/test execution as blocked rather than green.
Rejected Alternatives: Starting another build was rejected because it violates the explicit no-spam rule and the project's >50 percent CPU gate.
Scalability potential: No runtime effect. Protects parallel agent throughput and cheap hardware from avoidable Roslyn load.
Hardware Impact: Avoided one additional MSBuild/Roslyn invocation while host CPU was saturated.

## Decision 018 - Sparse BufferID Write-Lock Completion

Problem: APEX sparse metadata fallback fixed `TryReadFlatMetadata`, but generic `TryAcquireWriteLock<T>`, `ReleaseWriteLock<T>`, and `QueueDeferredRelease` still rejected keys above `_metadataByBufferId.Length` or directly indexed flat metadata. A high sparse `BufferID` could be resolved through map metadata but still fail writer release or deferred release.
Solution: Remove flat-array length rejects from those paths, keep all mutation under the block mutation gate, and route `ActiveWriterSystemID` updates through `WriteMetadata`, which writes flat metadata when possible and sparse map metadata otherwise.
Rejected Alternatives: Increasing `MaxGenerationHandleCapacity` above the sparse enum range was rejected because it would waste native memory for mostly empty IDs. Keeping generic write locks flat-only was rejected because the public handle contract does not state that sparse IDs are read/pin-only.
Scalability potential: Low tier avoids native capacity bloat. Middle/High/Ultra can keep sparse domain IDs without changing DTO layout or memory authority.
Hardware Impact: Saves approximately 14 MB if avoiding a 315736-entry 64-byte metadata array expansion; hot-path scan still reports zero forbidden allocation terms.

## Decision 019 - Deferred Growth Predicate Cleanup

Problem: Concurrent deferred-release edits left `ProcessDeferredArenaGrowth` checking `HasActiveBurstLocks(0u)` twice before testing pinned external views.
Solution: Remove the duplicated predicate and add `GlobalDataVault_DeferredGrowthChecksBurstLocksOnce` to make the regression machine-detectable.
Rejected Alternatives: Leaving the duplicate as harmless was rejected because allocator maintenance runs every PostSimulation frame when pressure exists; redundant native scans are debt in a 0.1 ms suspicious-budget system.
Scalability potential: Low tier removes one redundant lock scan in the pressure path. Middle/High/Ultra preserve the same safety predicate while keeping room for larger arena pressure.
Hardware Impact: Microsecond saving is not claimed without profiler data; static work removed one unnecessary scan call.

## Decision 020 - Sparse BufferID Generation Tombstone Ledger

Problem: Sparse `BufferID` keys above `_metadataByBufferId.Length` have no flat tombstone slot. `RemoveMetadata` removed the live map entry, so `ResolveInitialGenerationForAllocation` could return `1` on recreate and make an old generation-1 handle indistinguishable from a new allocation.
Solution: Add `_metadataGenerationByBufferId` as an unmanaged `UnsafeHashMap<int,uint>` ledger only for sparse keys. `TryAddMetadata` and `WriteMetadata` record live generations, `RemoveMetadata` records tombstone generations, and `ResolveInitialGenerationForAllocation` now reads `ReadMetadataGeneration`.
Rejected Alternatives: Expanding `_metadataByBufferId` to cover sparse enum IDs was rejected because it would allocate mostly empty native metadata. Leaving generation reset as acceptable was rejected because generation handles are the stale-pointer defense after raw memory reuse.
Scalability potential: Low tier keeps sparse memory overhead bounded by active configured capacity. Middle/High/Ultra preserve stable sparse IDs without changing DTO layout or growing flat arrays.
Hardware Impact: Avoids roughly 14 MB metadata expansion for `PlayerHandIkPublishedStates=315736` while preserving stale-handle rejection after release/recreate.

## Decision 021 - Deferred Release Duplicate Pin Gate

Problem: `QueueDeferredRelease` de-duplicated only `DeferredReleaseKindWriter`. If `TryUnlockBuffer` was called repeatedly while the release mutation gate was contended, identical buffer-pin releases could be queued more than once and later double-decrement `Reserved1`.
Solution: De-duplicate all release kinds before reserving a queue slot using `BufferKey`, `OffsetBytes`, `ActiveLockBit`, `LockOwnerSystemId`, and `Kind`.
Rejected Alternatives: Leaving pin releases uncovered was rejected because many callers ignore the `TryUnlockBuffer` return and can retry from retained local masks. Splitting writer/pin queues was rejected because the existing bounded ring already carries `Kind` and needs only a stronger identity check.
Scalability potential: Low tier avoids extra lock-retention and double-release faults under contention. Middle/High/Ultra keep the same bounded ring and can absorb higher parallel pin pressure without changing memory authority.
Hardware Impact: Adds one bounded pending-request scan only on release-gate contention. No steady allocator hot-path cost is claimed.

## Decision 022 - Queued Writer Release Return Contract

Problem: A recheck found the working tree returning `false` after successfully queuing writer release under mutation-gate contention. Existing callers and retry loops interpret `false` as release failure, even though ownership has been transferred to the deferred release queue.
Solution: Preserve `return QueueDeferredWriterRelease(...)` for `ReleaseWriteLock` and `ReleaseWriterBlockLock`. A queued release is an accepted release request; actual relocation remains blocked until the queue drains and active lock state clears.
Rejected Alternatives: Returning false for philosophical "not drained yet" semantics was rejected because it regresses caller state and can produce failure flags after a correct deferred release. Immediate blocking wait for the gate was rejected because it can stall under allocator pressure.
Scalability potential: Low tier keeps writer release non-blocking under contention. Middle/High/Ultra maintain deterministic deferred drain without forcing caller-specific retry policy.
Hardware Impact: Avoids retry spin in `ReleaseWriteLockWithRetry`-style paths; exact microseconds not claimed without profiler data.

## Decision 023 - Latest Compiler Gate Refresh

Problem: Final proof artifacts must name the current compilation throttle state, not an older CPU/process sample.
Solution: Refresh the final gate to CPU 77 percent with active `dotnet` PIDs 54088 and 55804, keep build/test execution blocked, and update both JSON report hashes.
Rejected Alternatives: Launching `dotnet build` at 77 percent CPU with active dotnet processes was rejected because AGENTS.md forbids build when CPU is above 50 percent or another compiler is running.
Scalability potential: No runtime effect. Protects shared low-end host throughput during parallel agent work.
Hardware Impact: Avoided another MSBuild/Roslyn invocation under active compiler contention.

## Decision 024 - Deferred Release Scan Serialization

Problem: The all-kind pending scan in `QueueDeferredRelease` was necessary but not sufficient. Two callers could both scan before either published `Pending`, then reserve two different slots with identical buffer/key/kind data.
Solution: Add `_deferredReleaseEnqueueGate` and guard scan plus slot reservation with `Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0)`. Release uses `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)` in `finally`, so every successful gate acquire has a single release path.
Rejected Alternatives: Relying on slot-level `CompareExchange` was rejected because it protects slot ownership, not semantic de-duplication across slots. A managed lock was rejected because this path exists specifically to survive allocator pressure.
Scalability potential: Low tier avoids double pin/writer release corruption under contention. Middle/High/Ultra keep the same bounded unmanaged ring and can tolerate more release pressure without changing ownership semantics.
Hardware Impact: Adds one atomic gate only when deferring a release. No steady-state allocation hot-path microsecond saving is claimed.

## Decision 025 - Published Allocation Rollback

Problem: A new buffer allocation could publish `_buffers`, metadata, key registry, and `_allocatedBytes`, then fail `MarkExternalView`. Returning false at that point left state published without completing the requested view and risked retained capacity after a failed call.
Solution: Add `RollbackPublishedAllocation` and call it from the new-allocation external-view failure branch. The helper enters the block mutation gate, removes the buffer map entry, removes metadata, removes the key registry entry, frees the occupied block, decrements `_allocatedBytes`, and releases the gate in `finally`.
Rejected Alternatives: Leaving the state for a later retry was rejected because `Try*` failure must not silently consume capacity after publishing. Blindly calling `TryFreeBlockRollback` after map removal was rejected because it did not prove key/metadata/byte accounting restoration in one audited helper.
Scalability potential: Low devices recover scarce arena space after failed view publication. Middle/High/Ultra avoid accumulating invisible capacity loss during parallel memory pressure.
Hardware Impact: Failure-path only. Expected normal-frame cost is 0 us; static scan of the helper showed zero forbidden GC terms.

## Decision 026 - Final Compilation Gate And Artifact Refresh

Problem: After rollback and release queue fixes, previous JSON hashes were stale and a compiler run still required resource gate proof.
Solution: Regenerate APEX and optimization JSON reports, parse both with `ConvertFrom-Json`, and record current SHA-256 hashes. Latest compiler gate sampled CPU 100 percent with active `csc` PIDs 23596/33620 and `dotnet` PIDs 45864/66816; no build was launched.
Rejected Alternatives: Reporting old hashes was rejected as false evidence. Starting `dotnet build` under CPU 100 percent with active compilers was rejected by the explicit compilation throttling rule.
Scalability potential: No runtime effect. Keeps proof artifacts deterministic for later allocator work.
Hardware Impact: Avoided one MSBuild/Roslyn invocation under saturated host conditions.

## Decision 027 - Counted Pin Release Correction

Problem: My previous all-kind deferred-release de-dup was wrong for buffer pins. `TryLockBuffer` allows repeated same-owner pins by incrementing `Reserved1`, so two valid releases can have identical `BufferKey`, `OffsetBytes`, `ActiveLockBit`, `LockOwnerSystemId`, and `Kind`.
Solution: Keep de-duplication only for `DeferredReleaseKindWriter`, where ownership is unique. Buffer-pin releases remain counted queue events and are allowed to occupy separate deferred slots.
Rejected Alternatives: Treating pin release retries as idempotent was rejected because there is no release token to distinguish a duplicate retry from a second legitimate pin. Adding a new token to the public lock API was rejected as too broad for this allocator pass.
Scalability potential: Low tier avoids retained locks from swallowed pin releases. Middle/High/Ultra preserve the same bounded queue while maintaining correct counted lock semantics under heavier parallel read pressure.
Hardware Impact: Removes a correctness fault. It may enqueue more pin-release records under contention, but only for legitimate counted pins; no microsecond saving is claimed.

## Decision 028 - Single-Gate New Allocation Publish

Problem: `RollbackPublishedAllocation` reacquired the mutation gate after a failed external-view mark. If the original failure was gate contention or compaction-fence timing, the rollback was not mathematically guaranteed.
Solution: Move new allocation publish into `TryAllocatePublishedBuffer<T>`. The helper enters the mutation gate once, allocates the block, clears/sanitizes payload, publishes `_buffers`, metadata, key registry, optional external-view state, and rolls back every partial state inside the same `finally`.
Rejected Alternatives: Bounded spin waiting for a second rollback gate was rejected because it can stall allocator pressure. Queueing a rollback request was rejected because the current deferred release record has no byte-accounting contract and would widen the protocol.
Scalability potential: Low devices recover scarce arena space deterministically. Middle/High/Ultra can handle larger allocation bursts without partial publish leaks.
Hardware Impact: Allocation publish is cold relative to per-frame reads; normal hot path claim remains 0 us. Failure path now does deterministic cleanup under one gate.

## Decision 029 - Pre-Publish Finite Sanitization

Problem: New float payload sanitation happened after metadata publication. A racing resolver could theoretically see newly published metadata before finite sanitation completed.
Solution: `TryAllocatePublishedBuffer<T>` now calls `SanitizeFinitePayload<T>` before `_buffers.TryAdd` and `TryAddMetadata`, so no handle can resolve the payload before sanitation.
Rejected Alternatives: Leaving sanitation after publish was rejected because the new one-gate helper made pre-publish sanitation cheap and mechanically provable. Disabling sanitation for uninitialized float buffers was rejected because existing code explicitly intended finite cleanup.
Scalability potential: Low/Middle/High/Ultra all preserve finite data before publication without changing DTO layout or authority.
Hardware Impact: Same sanitation work, moved earlier. No added normal-frame cost is claimed.

## Decision 030 - Second Final Compilation Gate And Artifact Refresh

Problem: After the pin semantics correction and single-gate publish rewrite, previous JSON hashes were stale and a compiler run still required resource gate proof.
Solution: Regenerate APEX and optimization JSON reports, parse both with `ConvertFrom-Json`, and record current SHA-256 hashes. Latest compiler gate sampled CPU 100 percent with active `dotnet` PID 15108; no build was launched.
Rejected Alternatives: Reporting stale hashes was rejected as false evidence. Starting `dotnet build` under CPU 100 percent with an active dotnet process was rejected by the explicit compilation throttling rule.
Scalability potential: No runtime effect. Keeps proof artifacts current for the next allocator review.
Hardware Impact: Avoided one MSBuild/Roslyn invocation under saturated host conditions.

## Decision 031 - Proof Line Number Reconciliation

Problem: The code was current, but the APEX/optimization JSON still carried pre-helper line numbers for `TryAcquireWriteLock<T>` finally release, deferred growth queue/clear CAS sites, and `ResolveArenaCapacityLimit`. That made the proof artifact weaker than the source.
Solution: Re-open current source with numbered line output, update only the proof artifacts to the actual line numbers, parse both JSON files, and refresh hashes. No runtime code was changed.
Rejected Alternatives: Leaving stale line numbers was rejected because the user explicitly requires exact file paths and line numbers. Re-running compiler was rejected because this was documentation evidence only and the latest compiler gate was already blocked by CPU/process contention.
Scalability potential: No runtime effect. Accurate proof preserves future Low/Middle/High/Ultra allocator review without repeating archaeology.
Hardware Impact: No runtime cost. Avoided one unnecessary MSBuild/Roslyn invocation.

## Decision 032 - Final CPU Gate Refresh

Problem: The final proof still named an older compiler-process sample. A fresh gate is required before any claim about build throttling.
Solution: Sample CPU and active compiler processes again. Latest CPU was 76 percent; `Get-Process dotnet,csc,VBCSCompiler` returned no active compiler processes. Because CPU is still above 50 percent, no `dotnet build` was launched. APEX and optimization reports were updated and re-parsed.
Rejected Alternatives: Launching a build at 76 percent CPU was rejected by the explicit compilation resource throttling rule. Keeping the old `dotnet#15108` sample as the "latest" result was rejected as stale evidence.
Scalability potential: No runtime effect. Protects shared low-end host throughput while preserving exact verification state.
Hardware Impact: Avoided one MSBuild/Roslyn invocation under CPU contention.

## Decision 033 - Source Hash Reconciliation

Problem: The reports still contained a previous SHA-256 for `Assets/_Project/Scripts/Core/SystemDispatcher.cs`, while the workspace file bytes had moved.
Solution: Update the APEX and optimization JSON source hash fields, re-parse both JSON files, and refresh final report hashes. `git status --short -- Assets/_Project/Scripts/Core/SystemDispatcher.cs` reported no local modification in this path, so this was artifact drift, not a new source edit.
Rejected Alternatives: Leaving the stale source hash was rejected because cryptographic proof must match the current file bytes. Editing runtime code to match an old report was rejected as destructive and unrelated.
Scalability potential: No runtime effect. Accurate hashes keep future allocator review anchored to the actual dispatcher hook bytes.
Hardware Impact: No runtime cost.

## Decision 034 - Sentinel Registration Stack Trace Purge

Problem: `NativeMemorySentinel.RegisterPointer` still called `CaptureStackTrace` during normal registration. In Editor/Development persistent lifetimes that helper called `StackTraceUtility.ExtractStackTrace`, which creates a managed string during allocation registration and violates the zero-GC registration mandate.
Solution: Make normal Sentinel stack-trace capture return `string.Empty`, keep owner/label/hash/bytes/lifetime as the fatal leak identity, and replace `NativeAllocationRecord`/`PersistentReallocationRecord` struct initializers with `default` plus field assignments to make the scanner proof unambiguous. Added `NativeMemorySentinel_RegisterPathAvoidsManagedStackTraceCapture`.
Rejected Alternatives: Keeping stack traces for nicer diagnostics was rejected because normal registration must be allocation-free. Rewriting the entire Sentinel to unmanaged `FixedString` storage was rejected in this loop because the public API and fatal message surface are string-based across many call sites; the proven defect was the normal registration stack capture, not a `Dictionary`.
Scalability potential: Low devices avoid editor/development registration string churn; Middle/High/Ultra keep the same fatal owner/label leak identity without adding runtime allocation pressure.
Hardware Impact: Removes one managed stack-trace string allocation from each normal persistent Sentinel registration path in Editor/Development. Runtime microseconds are not claimed without profiler data.

## Decision 035 - Post-Patch Compilation Gate Refresh

Problem: The Sentinel patch changes unsafe-adjacent core infrastructure, so compilation is valuable, but the host must obey the explicit build throttle.
Solution: Sample the gate after a 30 second wait. CPU was 94 percent and no `dotnet`/`csc`/`VBCSCompiler` processes were active; because CPU remains above 50 percent, do not launch `dotnet build`. Refresh the APEX and optimization JSON reports with this latest gate.
Rejected Alternatives: Launching `dotnet build` at 94 percent CPU was rejected by AGENTS.md and the user's anti-spam rule. Reporting the older CPU 80 sample was rejected as stale evidence.
Scalability potential: No runtime effect. Protects shared low-end host throughput while preserving exact verification state.
Hardware Impact: Avoided one MSBuild/Roslyn invocation under CPU contention.

## Decision 036 - Sentinel Full-Unmanaged Migration Deferral

Problem: After removing normal registration stack-trace allocation, `NativeMemorySentinel` still has cold managed arrays and string owner/label references. A total `FixedString`/native-storage rewrite would better satisfy the harshest interpretation of "unmanaged sentinel," but it touches many string-based public call sites and fatal diagnostics.
Solution: Record the limitation explicitly in both JSON reports as `SENTINEL_COLD_MANAGED_STORAGE_REMAINS`. Keep the targeted safe patch: normal register/unregister methods now source-scan to zero forbidden terms and no longer call `StackTraceUtility.ExtractStackTrace`.
Rejected Alternatives: Pretending the Sentinel is fully unmanaged was rejected as false. Performing a broad storage/API migration without a legal build gate was rejected because a compile break in core memory infrastructure is higher risk than the remaining cold-storage limitation.
Scalability potential: Low devices receive the immediate per-registration allocation removal. Middle/High/Ultra keep the same leak identity while a future quiet build window can migrate owner/label storage to bounded `FixedString` if required.
Hardware Impact: No additional runtime cost; avoids a risky uncompiled core rewrite under CPU contention.

## Decision 037 - Sentinel Record FixedString Migration

Problem: The previous residual risk was still too broad: `NativeMemorySentinel` record payloads kept managed `Owner`, `Label`, and `StackTrace` references even after normal stack capture was disabled.
Solution: Migrate `NativeAllocationRecord` and `PersistentReallocationRecord` owner/label payloads to `FixedString128Bytes` plus numeric hashes, remove stack-trace storage entirely, and compare records through hash plus byte-for-byte FixedString equality. Fatal leak messages append the fixed strings directly into the failure-path `StringBuilder`.
Rejected Alternatives: Hash-only matching was rejected because hash collisions would make owner/label unregistration ambiguous. A full unmanaged container rewrite was deferred because the remaining managed part is a cold fixed-capacity array container, while this pass removes managed references from the record payload without changing the public string API.
Scalability potential: Low devices avoid retaining managed owner/label string references in the Sentinel registry. Middle/High/Ultra preserve detailed fatal owner/label leak identity without changing call sites or allocator authority.
Hardware Impact: Removes managed object references from tracked allocation records. Runtime microsecond savings are not claimed; the concrete win is lower GC root pressure and cleaner zero-GC proof.

## Decision 038 - FixedString Migration Compilation Gate

Problem: The Sentinel storage migration touches core memory diagnostics and should be compiled, but the build gate must be obeyed.
Solution: Sample CPU and compiler processes before build. Latest gate after a 30 second wait was CPU 62 percent with active `dotnet` PID 62300. Do not launch `dotnet build`; refresh JSON proof with this blocked state.
Rejected Alternatives: Launching a build under CPU > 50 with active dotnet was rejected by AGENTS.md and the user's explicit anti-spam rule.
Scalability potential: No runtime effect. Protects shared host throughput.
Hardware Impact: Avoided one MSBuild/Roslyn invocation during active compile contention.

## Decision 039 - Sentinel Explicit Layout Closure

Problem: After migrating Sentinel owner/label payloads to `FixedString128Bytes`, `NativeAllocationRecord` and `PersistentReallocationRecord` still lacked explicit ARM64 offset contracts and retained flag state as normal bool-field layout concern.
Solution: Convert both records to explicit layout: `NativeAllocationRecord` is 304 bytes with `Pointer@0`, `Bytes@8`, `Owner@16`, `Label@144`, scalar metadata at `272..293`, and named padding through byte 303; `PersistentReallocationRecord` is 288 bytes with fixed strings at `0/128`, `LastBytes@256`, scalar metadata at `264..280`, and named padding through byte 287. Bool-facing properties are byte-backed and do not create runtime bool fields. Updated `NativeMemorySentinel_RegisterPathUsesFixedStringStorage` to assert the layout contract.
Rejected Alternatives: Leaving sequential layout was rejected because the user explicitly demanded mathematical struct-offset proof. Hash-only identity was already rejected because collisions are unacceptable. Migrating the cold managed array containers to native storage was deferred because this pass closed the record payload/layout defect while the compiler gate is still blocked.
Scalability potential: Low devices keep deterministic, compact, scan-friendly sentinel rows without managed owner/label references. Middle/High/Ultra preserve leak diagnostics while allowing future native container migration without changing the record ABI.
Hardware Impact: Runtime microsecond savings are not claimed. Static zero-GC method scan covered 31 methods with 0 forbidden hits in 743531 us. Build was not launched because latest CPU was 90 percent and active `dotnet` PID 6088 existed.

## Decision 040 - Deferred Release Enqueue Gate Source Reconciliation

Problem: The rationale and static editor test already required `_deferredReleaseEnqueueGate`, but the actual `GlobalDataVault.QueueDeferredRelease` source still performed writer duplicate scan and ring-slot reservation without that gate. Two writer-release callers could both scan before either published `Pending`, then reserve separate slots for the same writer release.
Solution: Add `_deferredReleaseEnqueueGate`, reset it in initialization/disposal paths, and wrap writer duplicate scan plus slot reservation in `while (Interlocked.CompareExchange(ref _deferredReleaseEnqueueGate, 1, 0) != 0) Thread.SpinWait(8)` with `Volatile.Write(ref _deferredReleaseEnqueueGate, 0)` in `finally`.
Rejected Alternatives: Relying on the slot-level `CompareExchange(ref request->State, Writing, Empty)` was rejected because it protects physical slot ownership, not semantic de-duplication across slots. Returning false while the enqueue gate is held was rejected because release callers interpret false as an unaccepted release.
Scalability potential: Low tier gets deterministic writer-release acceptance under contention without managed locks. Middle/High/Ultra can absorb heavier release bursts through the same fixed unmanaged ring; buffer-pin releases remain counted events and are not de-duplicated.
Hardware Impact: Adds one tiny atomic spin gate only on deferred-release enqueue paths. No steady allocator hot-path microsecond saving is claimed; the correction removes a race.

## Decision 041 - Sentinel Diagnostic Record Read Gate

Problem: `NativeMemorySentinel.CopySnapshotSources` and `BuildFatalLeakMessage` read `_records[]` without the Sentinel mutation gate. After explicit 304-byte record layout, a concurrent `RemoveAt` swap-back could expose a torn diagnostic snapshot or mixed owner/label/hash fields.
Solution: Gate both diagnostic read loops with `EnterMutationGate()` and release through `finally`. `CopySnapshotSources` now uses `NativeAllocationSnapshotSource snapshot = default` plus field assignments, removing the textual `new` scanner hit from the diagnostic snapshot path.
Rejected Alternatives: Treating these paths as "cold enough" was rejected because diagnostics must be trustworthy during failure analysis. Migrating the remaining cold managed array containers to native storage was again deferred because static storage lifetime cannot be made safer without a quiet compiler/test window.
Scalability potential: Low devices receive deterministic crash/replay evidence. Middle/High/Ultra can keep richer diagnostics without racing the Sentinel record table.
Hardware Impact: Cold diagnostic path only. Static zero-GC method scan now covers 32 methods with 0 forbidden hits in 1152528 us.

## Decision 042 - Final Build Gate And Report Refresh

Problem: After fixing the deferred-release gate and Sentinel diagnostic read race, proof artifacts and hashes were stale. Unsafe-adjacent memory changes still deserve compilation, but build throttle remains mandatory.
Solution: Re-run static scans, parse both JSON reports, refresh SHA-256 hashes, and sample the compilation gate. Latest report gate was CPU 94 percent with no active `dotnet`/`csc`/`VBCSCompiler`; no `dotnet build` was launched because CPU > 50.
Rejected Alternatives: Launching a build at 94 percent CPU was rejected by AGENTS.md and the user's anti-spam rule. Reporting earlier hashes was rejected as false evidence.
Scalability potential: No runtime effect. Accurate proof preserves later Low/Middle/High/Ultra allocator work without repeating archaeology.
Hardware Impact: Avoided one MSBuild/Roslyn invocation under CPU contention.

## Decision 043 - Current Source Drift Reconciliation

Problem: A fresh source/report comparison found `GlobalDataVault.cs` no longer matched the recorded SHA-256 and no longer contained `_deferredReleaseEnqueueGate`, even though the proof artifact still claimed the gate existed.
Solution: Re-apply the enqueue gate to the current working file, re-run the QueueDeferredRelease zero-GC scan, refresh source hashes in both reports, and re-parse both JSON artifacts.
Rejected Alternatives: Trusting the previous report was rejected because the actual working file is the authority. Reverting unrelated concurrent GlobalDataVault edits was rejected because only the missing gate was necessary for this domain fix.
Scalability potential: Low/Middle/High/Ultra all preserve deterministic writer-release de-duplication without managed locks.
Hardware Impact: Static scan covered 32 methods with 0 forbidden hits in 517603 us. Build was not launched because latest CPU was 76 percent.

## Decision 044 - Final Evidence Coordinate Repair

Problem: Final proof review found `Docs/Reports/ARENA_ALLOCATOR_APEX_VERIFICATION_1414.json` still named `_deferredReleaseEnqueueGate` disposal reset as `GlobalDataVault.cs:3544`, but current source places the reset at `GlobalDataVault.cs:3539`.
Solution: Correct the APEX proof line, update the optimization report cross-hash, parse both JSON artifacts, and refresh the task/log hashes. Runtime source was not changed.
Rejected Alternatives: Leaving a stale proof coordinate was rejected because line-level evidence is part of the requested APEX proof. Editing runtime code to match the stale report was rejected as harmful and unrelated.
Scalability potential: No runtime effect; it preserves reliable Low/Middle/High/Ultra allocator audit evidence.
Hardware Impact: No runtime cost and no build invocation.

## Decision 045 - Latest Compiler Gate Refresh

Problem: The previous report gate said CPU 76 percent with no active compiler processes, but the final gate sample found active compiler work.
Solution: Update both JSON reports and logs to the current gate: CPU 76 percent, active `csc` PID 17356 and `dotnet` PID 3212. Do not launch `dotnet build`.
Rejected Alternatives: Building under CPU > 50 percent and active compiler processes was rejected by AGENTS.md and the user's compilation throttling rule. Keeping the older active-process count was rejected as stale evidence.
Scalability potential: No runtime effect; protects shared low-end host throughput while preserving exact verification state.
Hardware Impact: Avoided one MSBuild/Roslyn invocation during active compiler contention.

## Decision 046 - Arena Growth Tail Metadata Preflight

Problem: `TryGrowArenaForBytes` still had a structural blind spot: an occupied tail with exhausted `_blocks` capacity returned `false` before preserving a deferred target, and `ExtendFreeTail` could still discover missing tail metadata capacity after `H8Memory.ReallocateRaw` had already moved and freed the old arena.
Solution: Remove the early occupied-tail/full-capacity return, add `TryPrepareArenaGrowthTailMetadata` inside `TryGrowArena` after the compaction fence and block mutation gate are held but before `H8Memory.ReallocateRaw`, and add `H8Memory.TryReserveBlockDescriptorSlot` so descriptor capacity is prepared or rejected before pointer relocation. `ExtendFreeTail` remains a postcondition check and now uses `VaultArenaBlock freeTail = default` with field assignments.
Rejected Alternatives: Growing first and trusting `ExtendFreeTail` was rejected because failure after raw relocation leaves a grown arena without a representable free-tail block. Reserving a managed lock was rejected by the zero-GC/hot-path mandate. Moving the whole H8Memory tracker to a new lock regime was rejected in this loop because call-site ownership is broader than arena growth and cannot be safely rewritten without a build/test window.
Scalability potential: Low devices fail closed and carry the largest deferred target instead of risking corrupted metadata; Middle/High/Ultra keep the same continuous arena capacity route and can consume larger visual memory budgets once the quiescent phase can grow safely.
Hardware Impact: Adds one bounded metadata-capacity check only on arena growth attempts. Static hot-path scan covered 18 methods with 0 forbidden hits in 218694 us. Build was not launched because CPU was 77 percent and active `dotnet` PID 31496 existed.

## Decision 047 - Reserved Descriptor Slot Commit Contract

Problem: The arena growth tail metadata preflight prepared descriptor capacity, but capacity is not ownership. Another descriptor registration could consume the prepared slot between preflight and ExtendFreeTail, and the first reservation patch still committed through generic TryUpdateBlockDescriptor without proving the slot remained reserved.
Solution: Add H8BlockState.Reserved, reserve a concrete descriptor row with Bytes = -1L, commit it only through TryCommitReservedBlockDescriptor when current state is still Reserved, and release uncommitted reservations from TryGrowArena finally. ExtendFreeTail now uses the reserved commit path.
Rejected Alternatives: Trusting capacity after EnsureBlockDescriptorCapacity was rejected as TOCTOU. Generic TryUpdateBlockDescriptor was rejected because it lacks Reserved-state proof. A global H8Memory lock rewrite was rejected in this loop because it exceeds the arena-growth domain and requires a green build/test window.
Scalability potential: Low devices fail closed before pointer relocation; Middle/High/Ultra can grow larger arenas using the same reserved metadata route without corrupting descriptor ownership.
Hardware Impact: Growth-only bounded descriptor scan/commit. Hot allocation path remains text-scanned at zero forbidden allocation constructs; no profiler-backed microsecond gain claimed.

## Decision 048 - Throttled Build Attempt Honesty

Problem: Unsafe relocation and descriptor-commit changes warranted compilation, but build throttling forbids compiler spam.
Solution: Sample CPU and compiler processes first: CPU 14 percent, no active compiler processes. Launch exactly one dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1. It timed out after 124020 ms with no compiler output; report as not green, do not retry.
Rejected Alternatives: Claiming compile success was rejected as false. Re-running the build after timeout was rejected as build spam without new evidence.
Scalability potential: No runtime effect; protects shared low-end host throughput.
Hardware Impact: One throttled build attempt consumed time but produced no result; latest post-timeout cleanup stopped own lingering dotnet build PID 3560; final sample is CPU 51 percent with no active compiler processes after stopping lingering build child PIDs 3560, 3392, 8108, and 13080.

## Decision 049 - Loop 21 Hot-Path Text-Proof Closure

Problem: `H8Memory.RegisterPointer` is on the `AllocateRaw` and `ReallocateRaw` registration path and still used value-type `new H8AllocationRecord { ... }` plus `new BlockDescriptor { ... }`. These were struct initializers, not managed heap allocations, but they weakened the exact text-scanning proof demanded by APEX.
Solution: Replace both initializers with `default` plus explicit field assignments, then call `RegisterBlockDescriptorNoInit(in blockDescriptor)`. Also replace the cold `GlobalDataVault.Initialize` `new VaultArenaBlock { ... }` initializer with `default` plus fields so file-level `rg` evidence no longer reports that ambiguity.
Rejected Alternatives: Explaining that these were structs was rejected because the mandate requires machine-readable evidence. Running another build after the solution graph failure was rejected when the immediate core-only gate sampled CPU 100 percent.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The allocator proof is cleaner while the existing continuous `GlobalQualityWeight` arena-capacity route remains the scalability lever.
Hardware Impact: Runtime microsecond gain is not claimed. Static hot-path scan covered 24 methods with 0 forbidden hits. The latest full solution build failed in the MSBuild project graph before core code diagnostics; no core-only retry was launched at CPU 100. Lingering compiler processes from the solution build attempt were stopped: dotnet PID 68208, VBCSCompiler PID 6836, late child dotnet PID 40660, late child dotnet PID 12612, and orphaned csc PID 2104. A later host check showed CPU 6 with external compile-medic dotnet PID 58372 and csc PID 5024 writing `Docs/Reports/BUILD_COMPILE_MEDIC_HECTON8_EDITOR_FINAL_AFTER_BUCKETER_*`; not owned by 1414 and not stopped.

## Decision 050 - H8Memory Descriptor Mutation Gate

Problem: After the reserved descriptor-slot patch, `_blockDescriptors` still had multiple writer helpers (`TryUpdateBlockDescriptor`, reservation, commit, release, register, free, owner-key update, and capacity growth) with no H8Memory-local serialization. A vault growth path could reserve a concrete descriptor row, but a concurrent non-vault H8Memory descriptor writer could still mutate the same table between reserve and commit.
Solution: Add `_blockDescriptorMutationGate` in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`. `EnterBlockDescriptorMutationGate` uses `Interlocked.CompareExchange(ref _blockDescriptorMutationGate, 1, 0)` and `Thread.SpinWait(8)`; every added descriptor writer releases through `finally` via `ReleaseBlockDescriptorMutationGate`, which performs `Thread.MemoryBarrier()` then `Volatile.Write(..., 0)`. Reservation body moved into `TryReserveBlockDescriptorSlotNoLock`, callable only from the gated wrapper.
Rejected Alternatives: A broad H8Memory allocation-table lock rewrite was rejected in this loop because it would touch `Allocate<T>`/`NativeArray<T>` construction paths and needs a quiet compiler window. Locking `TryGetBlockDescriptor` was rejected because project doctrine requires read accessors to stay pure; writer serialization is proven, 40-byte diagnostic read atomicity is not claimed.
Scalability potential: Low tier gets fail-closed deterministic descriptor metadata without managed locks. Middle/High/Ultra keep the same continuous `GlobalQualityWeight` arena-capacity route; this patch does not change gameplay truth or buffer layout.
Hardware Impact: Adds a tiny Interlocked spin gate only around descriptor metadata writer paths. Runtime microsecond gain is not claimed. Loop 22 static scan covered 39 methods with `new_ref_text=0`, `string_format=0`, `tostring=0`, `foreach=0`, `linq_query=0`, `linq_methods=0`. Build was not launched in Loop 22 because the final report gate sampled CPU 100 with active `dotnet build Hecton8.slnx` PID 65020 parented by codex.exe. Final report hashes: APEX `74849c07100727a028688cfab91b71c8f35755be87cc7b4de8e7a015e2bcb9fe`, optimization `cf2c765526f4c4ffce64e32d0d7c3a5deddb1f5dc5d6cb5b1c64c10cc01a7afa`.
