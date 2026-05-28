# Rationale_1414

Agent: 1414
Status: APEX STATIC VERIFIED / BUILD BLOCKED BY HOST CONTENTION

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
Solution: Convert the binary switch scan object into an array of `{ term, count }` records and re-run JSON parsing. Current APEX report SHA-256 is `3dccc87eeef4fec2e2cf2d833dbfb84809cb1132003adf348e1cc128e40b3e93`.
Rejected Alternatives: Keeping a JSON file that only some parsers accept was rejected because project evidence must be machine-verifiable on the host shell.
Scalability potential: No runtime effect. Tooling stability improves for later allocator audits.
Hardware Impact: No runtime cost.

## Decision 017 - Final Compilation Throttle Honesty

Problem: A compiler check remains desirable after unsafe memory changes, but the latest gate still shows CPU 66 percent with active `csc` PID 67916 and `dotnet` PID 20440.
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
