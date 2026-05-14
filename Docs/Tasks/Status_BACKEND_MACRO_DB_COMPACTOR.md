# BACKEND_MACRO_DB_COMPACTOR Status

Status: PENDING VERIFICATION
Domain: ECHELON 1 CORE & MEMORY INFRASTRUCTURE / Data Archivist MMF Codec
Prompt: BACKEND_MACRO_DB_COMPACTOR
Task Count: 15

## Mandates Read
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- STRM_Async_Standard.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- STRM_Persistent_Object_Registry.txt
- OPT_HectonArenaAllocator_2_0.txt

## Loop 1: Tasks 1-5
- [x] Task 1: Extend `IAsyncPersistenceService` | Justification: added macro DB compaction request/finalize/snapshot surface through the persistence contract, keeping callers decoupled through `GlobalRegistry`. DOD: interface evidence in `GlobalRegistryContracts.cs` and SaveManager implementation. Alternatives Rejected: direct `H8MacroDatabaseService` singleton calls from H-PHI/Sentinel. Estimate: 4-8 us per cold request.
- [x] Task 2: Consume `CriticalMemoryPressureEvent` to pause compaction | Justification: `SystemDispatcher.DispatchCriticalMemoryPressure` now notifies `IMacroDatabaseService`; DB records pause state and resumes after a bounded 3000 ms cooldown. DOD: static signal path scan. Alternatives Rejected: polling heap state inside DB. Estimate: 2-5 us event-side, 0 us per frame while idle.
- [x] Task 3: ASMDEF isolation `Hecton8.Core.Database` -> Contracts | Justification: `Hecton8.Core.Database.asmdef` references `Hecton8.Core.Contracts`; compaction interfaces live in contracts only. DOD: asmdef source read. Alternatives Rejected: referencing SaveManager or world assemblies from database. Estimate: 0 us runtime.
- [x] Task 4: Track total dead record bytes | Justification: append updates measure old live record bytes, persist `DeadBytes` in the header, and reconcile legacy files by measuring live tree payload bytes. DOD: header offset and append-path scan. Alternatives Rejected: a tombstone list/heap map allocation. Estimate: 1 pointer read plus header write per overwritten sector, roughly 1-3 us.
- [x] Task 5: Trigger background compaction above tier threshold | Justification: `FrostTickCompaction` starts `Awaitable.BackgroundThreadAsync()` when dead bytes exceed threshold and no save/load, memory pause, or existing compaction is active. DOD: request path scan and SaveManager FrostTick registration. Alternatives Rejected: per-frame compaction worker. Estimate: 5-10 us FrostTick idle check.

## Loop 2: Tasks 6-10
- [x] Task 6: Create `world_data_compact.tmp` double-buffer file | Justification: temp path is deterministic beside active DB, cleaned before start and on boot/shutdown/fault. DOD: `CompactionTempFileName` and cleanup path scan. Alternatives Rejected: in-place page rewrite. Estimate: cold file create only; 0 us steady frame cost.
- [x] Task 7: Copy live B-Tree nodes only | Justification: traversal reads B-tree node payload offsets and appends only current live payloads into the temp database. DOD: `CopyNodePayloadsTo` source read. Alternatives Rejected: scanning append log or copying the full MMF. Estimate: background cost proportional to live payload bytes, 0 main-thread traversal cost.
- [x] Task 8: Halt write queue during PRE_SIMULATION finalization | Justification: write queue is rejected while state is Copying/Paused/ReadyToSwap/Swapping; SaveManager also gates compaction during save/load. DOD: `IsCompactionWriteLocked`, `_isBusy` gate scan. Alternatives Rejected: allowing append during swap and reconciling offsets afterward. Estimate: one state branch per append, under 1 us.
- [x] Task 9: Flush, atomic swap, reopen active file | Justification: finalization opens temp, flushes remaining dirty payloads, truncates to append offset, closes active MMF, performs `File.Replace`, then reopens active DB. DOD: swap path source read. Alternatives Rejected: non-atomic delete/move. Estimate: target <2000 us main-thread stall; telemetry records actual microseconds.
- [x] Task 10: Unlock queue with <2 ms target | Justification: after successful swap state returns Idle and dirty queue is empty; stalls over 2000 us set `LastSwapExceededBudget`. DOD: stall timer and flag scan. Alternatives Rejected: blocking gameplay during full copy. Estimate: expected 600-1800 us for close/replace/reopen on SSD, MicroSD may exceed and is flagged.

## Loop 3: Tasks 11-15
- [x] Task 11: Expose compaction state for H-PHI / Memory Sentinel | Justification: `MacroDatabaseCompactionSnapshot`, `MacroDatabaseStats`, and telemetry expose state, flags, temp bytes, dead bytes, pending writes, and last swap microseconds. DOD: contract scan. Alternatives Rejected: opaque DB lock bit. Estimate: 2-4 us snapshot call.
- [x] Task 12: Power loss guard / boot tmp cleanup | Justification: original `.h8db` remains untouched until swap; startup and fault paths delete `world_data_compact.tmp`. DOD: initialize/shutdown/fault cleanup scan. Alternatives Rejected: journaled in-place compaction. Estimate: one cold `File.Exists`/delete on boot.
- [x] Task 13: Low-tier MicroSD threshold 50 MB | Justification: Low/Mx350 resolves to `MacroDatabaseTier.Low`; low tier uses 50 MB dead-byte threshold, other tiers use 10 MB. DOD: tier resolver and threshold scan. Alternatives Rejected: universal 10 MB threshold causing cheap flash churn. Estimate: 0 us after branch.
- [x] Task 14: Zero-GC traversal | Justification: live B-tree traversal uses `byte*`, existing node offsets, and integer loops; no managed collections inside `CopyNodePayloadsTo`. DOD: allocation/static scan. Alternatives Rejected: recursive managed node materialization/listing keys. Estimate: 0 GC allocations; pointer loop cost only.
- [x] Task 15: Awaitable thread lock compile check | Justification: `dotnet build .\Hecton8.Core.csproj -v:minimal` completed with 0 errors after the latest compactor hardening pass. DOD: `Docs/AgentLogs/Build_BACKEND_MACRO_DB_COMPACTOR_latest.txt`. Alternatives Rejected: Unity-MCP-only validation because Unity MCP validation/console tools are unavailable in the current request. Estimate: compile-only check; runtime cost remains 0 us.

## Loop 4: Re-Verification
- [x] Re-read prompt after Tasks 1-15 | Justification: extracted `BACKEND_MACRO_DB_COMPACTOR` from `CURRENT_BATCH.md` with PowerShell regex after implementation. Alternatives Rejected: chat memory only. Estimate: 0 us runtime.
- [x] Verify compaction is blocked during active Save/Load | Justification: SaveManager notifies macro DB gate immediately after `_isBusy = true` and clears after `_isBusy = false` in save/load finally blocks; request/finalize APIs reject while busy. Alternatives Rejected: relying on FrostTick cadence alone. Estimate: 2-4 us per save/load transition.
- [x] Re-check shutdown race | Justification: added `_compactionCopyActive`, active cancellation checks, and shutdown wait before MMF handle release. Alternatives Rejected: holding `_fileGate` for the full copy, which would halt gameplay. Estimate: 0 us steady state; 1 ms sleep loop only during shutdown with active compaction.

## Loop 5: Omega Polish
- [x] Read `<POLISH_MANDATE>` only after all core tasks are done or blocked | Justification: extracted `POLISH_MANDATE id="OMEGA_POLISH"` only after Tasks 1-15 were checked or blocked. Alternatives Rejected: early polish parsing. Estimate: 0 us runtime.
- [x] Final anti-bloat static scan and report append | Justification: diff-only scan found no added `foreach`, `string.Format`, `$"..."`, `.ToString(`, `math.sqrt`, or `math.normalize`; final report appended to `Docs/AgentLogs/LOG_BACKEND_MACRO_DB_COMPACTOR.md`. Alternatives Rejected: broad legacy project scan as pass/fail because SaveManager has pre-existing string interpolation outside this compactor patch. Estimate: 0 B GC in compaction traversal.

## Loop 6: Post-Report Hardening
- [x] Preserve temp file across helper-service shutdown | Justification: re-read found temp `H8MacroDatabaseService` instances would call `CleanupCompactionTemp` on their own `_path` and delete `world_data_compact.tmp` before swap. Fixed cleanup to ignore paths already named `world_data_compact.tmp`. Alternatives Rejected: special-casing every target shutdown caller. Estimate: cold-path only; prevents 100% swap failure after successful copy.
- [x] Close-before-delete fault cleanup | Justification: finalization faults after opening temp could attempt deletion while the temp handle was still open; finalizer now repeats cleanup after target shutdown closes the handle. Alternatives Rejected: leaving stale temp until next boot. Estimate: cold fault path only.
- [x] Harden memory pause race and uint frame update | Justification: replaced `math.max(uint,uint)` with explicit compare and made pause-resume clearing use `Interlocked.CompareExchange` so a stale worker cannot erase a newer memory-pressure pause. Alternatives Rejected: unsynchronized field mutation from worker. Estimate: no steady-frame cost; branch only on compaction pause checks.

## Loop 7: Deferred Dirty Flush Semantics
- [x] Public dirty append reports accepted when compaction owns the queue | Justification: `SaveManager.FlushWfcOutpostDirtyPayloadAsync` treats `TryAppendDirtyPayload=false` as corrupt persistence, but compaction intentionally halts append writes while preserving dirty payloads for final temp flush. Public append now returns true only when the dirty sector exists and compaction is the blocker; the internal eviction path still returns false so dirty sectors are not evicted without disk persistence. Alternatives Rejected: retry loops in SaveManager that would allocate/schedule more Awaitables and still race compaction; changing internal append semantics, which would allow unsafe eviction. Estimate: one hash lookup and state branch only during explicit dirty flush attempts; 0 us steady frame cost.

## Loop 8: Temp Truncation Fail-Fast
- [x] Abort compaction when temp truncate/remap fails | Justification: copy and swap paths now require `TruncateToAppendOffset()` success before temp is treated as ready or used for `File.Replace`. This prevents a partially remapped, untrimmed, or invalid temp file from being promoted after an IO/remap failure. Alternatives Rejected: accepting oversized temp slack as harmless, because a failed remap is not equivalent to a valid compact file. Estimate: cold-path branch only; 0 us steady frame cost.
- [x] Remove temp `FileInfo` length stat | Justification: after truncation/remap the target service already owns the exact `_mappedBytes` value, so the background copy no longer allocates `FileInfo` or performs an extra filesystem metadata query. Alternatives Rejected: keeping `FileInfo` as readability sugar, because compaction is explicitly zero-GC biased. Estimate: removes one cold managed allocation and one file stat per compaction pass.

## Loop 9: Dirty Queue Commit Atomicity
- [x] Preserve dirty queue until swap succeeds | Justification: finalization now copies dirty payloads into temp without removing them from the source queue; the dirty queue is cleared only after `File.Replace` and active DB reopen succeed. This prevents data loss if temp append, truncation, or atomic replace fails after some dirty payloads were copied. Alternatives Rejected: removing entries during temp copy, because temp is not authoritative until swap success. Estimate: no steady-frame cost; finalizer clears two native containers once after successful swap.

## Loop 10: Public Flush Idempotency
- [x] Avoid false corruption warnings after committed compaction flush | Justification: a queued `SaveManager` dirty append can execute after compaction has already copied that dirty payload into temp, swapped the file, and cleared the dirty queue. Public append now returns success when no dirty entry remains but a valid committed payload exists in the B-tree. The internal append path remains strict for eviction safety. Alternatives Rejected: keeping a managed recent-commit list or adding SaveManager retry loops, both of which add state/churn for a cold race. Estimate: one B-tree lookup only for public no-dirty flush calls; 0 us steady frame cost.

## Verification
- Compile: PASS. `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false` exited 0 with 36 warnings, 0 errors after the public flush idempotency patch. Warnings are third-party/package or shared build-output lock retry warnings, not Macro DB diagnostics.
- Unity Console: BLOCKED. Unity MCP validation/console tools are unavailable in this request (`unsupported call` placeholder).
- Runtime / GCMonitor: PENDING VERIFICATION. Static scan confirms traversal has no managed allocation sites.
