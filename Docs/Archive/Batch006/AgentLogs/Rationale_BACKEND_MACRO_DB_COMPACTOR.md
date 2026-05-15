# BACKEND_MACRO_DB_COMPACTOR Rationale

Status: PENDING VERIFICATION

## Decision 0: Domain And Mandate Selection
Problem: The prompt asks for B-Tree tombstone sweeping in the append-only macro database without stopping gameplay or corrupting active save files.
Solution: Bound implementation to Core Database / persistence contracts. Read save, async, native memory, zero-GC, telemetry, registry, performance, persistent registry, and arena mandates before touching source.
Rejected Alternatives: A standalone compactor outside the database owner would duplicate ownership and race active writes. A SaveBinaryStorage-only defrag patch would miss the explicit macro database contract.
Scalability potential: Low uses larger dead-byte threshold and fewer disk writes; Middle/High/Ultra can compact earlier and preserve MicroSD on low hardware while keeping high-tier storage clean.
Hardware Impact: Expected low-end i3/MX350 gain is avoided multi-GB save bloat and fewer long read scans; runtime frame gain is zero by design because copy work stays off the main thread.

## Decision 1: Contract Surface Through Async Persistence
Problem: H-PHI, Memory Sentinel, and save orchestration need compaction status/control without owning the database concrete type.
Solution: Extended `IAsyncPersistenceService` and `IMacroDatabaseService` with request, finalization, persistence gate, memory-pressure pause, and snapshot APIs.
Rejected Alternatives: A direct `H8MacroDatabaseService.Instance` singleton was rejected because 20+ agents are running in parallel and singleton reach-ins would create hidden initialization order dependencies.
Scalability potential: Low tier callers can inspect threshold/state and avoid flash wear; High/Ultra can request tighter housekeeping without changing database ownership.
Hardware Impact: On i3/MX350, a contract call costs single-digit microseconds and avoids reflection or scene search. On high-end hardware, the same API allows more aggressive visual-memory sentinel behavior without DB coupling.

## Decision 2: Header Dead-Byte Counter, Not Tombstone Collection
Problem: Append-only payload replacement leaves dead records, but a per-tombstone map would allocate and grow with playtime.
Solution: Store total dead bytes in the fixed database header and increment it when an overwritten sector receives a new append record; reconcile old files by measuring live tree payload bytes against append offset.
Rejected Alternatives: Managed `Dictionary<ulong,long>` tombstone maps and append-log scans were rejected for GC and cold-load latency.
Scalability potential: Low: compact only at 50 MB to protect MicroSD. Middle/High/Ultra: 10 MB keeps disk footprint tight and leaves more budget for content streaming.
Hardware Impact: i3/MX350 impact is one old-payload header read and one DB-header write on replacement, roughly 1-3 us. Top-tier machines spend the saved disk budget on larger live world payloads rather than dead bytes.

## Decision 3: Background Double Buffer Copy
Problem: Full compaction cannot run on the main thread and cannot mutate the active `.h8db` until the last safe moment.
Solution: Start `Awaitable.BackgroundThreadAsync`, create `world_data_compact.tmp`, traverse the live B-tree with `byte*`, and append only active payload records into the temp database.
Rejected Alternatives: In-place page rewrite was rejected because a crash mid-rewrite would corrupt the authoritative database. Copying the whole MMF was rejected because it preserves tombstones.
Scalability potential: Low tier does fewer compactions and reads only live data. High/Ultra can keep saves compact for larger simulation horizons.
Hardware Impact: Low-end devices pay background IO only above threshold and get bounded main-thread cost. High-end devices can compact more often without visible stalls.

## Decision 4: Save/Load Gate And Dirty Queue Lock
Problem: The database must not compact while an active save/load pipeline is assembling or hydrating state.
Solution: SaveManager sets the macro DB persistence gate immediately after `_isBusy = true` and clears it in the save/load `finally` blocks; request/finalize APIs reject while busy.
Rejected Alternatives: Relying on the next FrostTick to discover `_isBusy` was rejected because the copy worker could already be running.
Scalability potential: Low keeps save/load predictable on weak storage. High/Ultra can still compact between IO bursts.
Hardware Impact: i3/MX350 receives deterministic Save/Load priority; gate transition cost is about 2-4 us.

## Decision 5: Atomic Swap With Telemetry Budget
Problem: The last 1% must flush dirty payloads, replace the file, and reopen the MMF without a visible hitch.
Solution: Open temp, flush dirty payloads into it, truncate to append offset, close active handles, `File.Replace` temp over active, reopen, reset dead bytes, and record stall microseconds.
Rejected Alternatives: `File.Delete` + `File.Move` was rejected because it opens a no-database window on crash. Holding the lock for the entire copy was rejected because it would halt hydration and writes.
Scalability potential: Low tier records if MicroSD exceeds the 2 ms target and can back off by threshold. High/Ultra can accept more frequent swaps for cleaner disk state.
Hardware Impact: Expected main-thread stall target is 600-1800 us on SSD and may exceed 2000 us on MicroSD; overflow is flagged in telemetry for Sentinel/H-PHI.

## Decision 6: Memory Pressure Pause
Problem: Compaction IO competes with emergency memory recovery and can worsen stalls when the platform is already under pressure.
Solution: SystemDispatcher forwards `CriticalMemoryPressureEvent` to the macro database; compaction pauses for 3000 ms and traversal checks pause state between nodes.
Rejected Alternatives: Checking `GC.GetTotalMemory` or native memory counters from the DB was rejected as duplicate ownership and extra polling.
Scalability potential: Low gets immediate relief from background IO during memory pressure. High/Ultra can resume automatically after the pressure window.
Hardware Impact: On i3/MX350 the pause avoids piling IO on top of memory recovery. On high-end systems the branch is effectively free unless pressure occurs.

## Decision 7: Shutdown Reader Guard
Problem: The background copy intentionally avoids holding `_fileGate` for the full traversal, but shutdown could close the MMF while the copy reader still has raw pointers.
Solution: Added `_compactionCopyActive`, active cancellation checks, and a shutdown wait before releasing file handles.
Rejected Alternatives: Holding `_fileGate` for the whole copy was rejected because it would block gameplay hydration and dirty write staging. Ignoring shutdown was rejected because raw pointer access after MMF close is undefined.
Scalability potential: Low and High share the same safety path; high-end devices do not pay frame cost because the guard only activates during shutdown.
Hardware Impact: No steady-frame cost. Shutdown with active compaction sleeps in 1 ms increments until the worker exits; this is controlled teardown, not gameplay time.

## Decision 8: Compile Wall Classification
Problem: The build does not reach clean compactor verification because `Hecton8.Core.csproj` currently fails on missing unrelated cross-assembly types.
Solution: Captured the build log and classified Task 15 as `[BLOCKED BY DEPENDENCY]` while preserving source-level checks for this domain.
Rejected Alternatives: Editing world/fauna/radar/bootstrap ownership from the macro DB task was rejected as domain breach.
Scalability potential: None at runtime; prevents destabilizing unrelated systems while preserving compactor changes for integration.
Hardware Impact: None at runtime.

## OMEGA POLISH CHANGES
Problem: The polish mandate required an anti-bloat audit after the core checklist was done or blocked, including replacement of honest calculations, scan for managed iteration/string churn, and a final build.
Solution: Extracted `POLISH_MANDATE id="OMEGA_POLISH"` after all 15 tasks were checked/blocked. Ran diff-focused scans for added `foreach`, `string.Format`, interpolated strings, `.ToString(`, `math.sqrt`, `math.normalize`, and floating division markers in the compactor-owned diff. No new offender was found. Reran `dotnet build Hecton8.Core.csproj`; current global wall is `PowerGridManager.cs(61,17): WfcOutpostPowerBootRuntime` missing, outside this domain.
Rejected Alternatives: A broad project scan was rejected as the verdict source because legacy/editor/third-party files and concurrent agents already contain string formatting and foreach usage unrelated to this patch. Editing Power/WFC runtime ownership was rejected as architectural boundary breach.
Scalability potential: The exact cinematic cheat is scalar dead-byte accounting instead of per-tombstone records: Low/Mx350 delays compaction until 50 MB to preserve MicroSD; Middle/High/Ultra compact at 10 MB to keep storage clean for larger worlds. No physical simulation cheat applies to this data-infrastructure task.
Hardware Impact: Low-end i3/MX350 avoids managed GC and flash churn; high-end hardware can spend saved IO budget on larger persistent world payloads. Main-thread swap target remains <2000 us with telemetry flagging over-budget swaps.
Final Git Diff: `H8MacroDatabaseService.cs` gained shutdown-safe compaction copy guards, temp cleanup on fault, and pause-aware live-node traversal. `SaveManager.cs` gained save/load persistence gate notifications. `Status_BACKEND_MACRO_DB_COMPACTOR.md`, `Rationale_BACKEND_MACRO_DB_COMPACTOR.md`, and `LOG_BACKEND_MACRO_DB_COMPACTOR.md` contain the required evidence. Other visible diffs in `SaveManager.cs`/`GlobalRegistryContracts.cs` are concurrent agent work and were not reverted.

## Decision 9: Temp Service Shutdown Must Not Delete Its Own Output
Problem: A second read found a swap-killing ownership bug: the temporary `H8MacroDatabaseService` created for `world_data_compact.tmp` runs `Shutdown()`, and the generic temp cleanup helper would resolve that same filename and delete the finished compact file before `File.Replace`.
Solution: `CleanupCompactionTemp` now returns immediately when the supplied path is already `world_data_compact.tmp`; active database services still delete stale temp files because their path is the `.h8db`.
Rejected Alternatives: Adding caller-specific shutdown flags was rejected because it spreads temp ownership policy across multiple call sites and is easier to misuse later.
Scalability potential: Low/Mx350 no longer wastes a full background copy only to delete the output before swap; High/Ultra get deterministic compaction completion.
Hardware Impact: Saves one full failed compaction pass on weak flash. This is not a micro-optimization; it prevents a cold-path correctness failure that would cost seconds of background IO.

## Decision 10: Fault Cleanup After Handle Closure
Problem: If finalization opened the temp DB and then failed during dirty flush, cleanup could run while the temp `FileStream` was still open, leaving a stale `.tmp` until boot.
Solution: The finalizer records fault state, shuts down the temp service, then repeats cleanup after the handle is closed.
Rejected Alternatives: Waiting for next boot cleanup was rejected because repeated in-session compaction attempts should not inherit stale temp state.
Scalability potential: Low avoids repeated MicroSD temp-file clutter; higher tiers can retry compaction within the same session.
Hardware Impact: Cold fault path only. Expected normal-frame cost is 0 us.

## Decision 11: Pause Race And Frame Counter Hardening
Problem: The background pause check could clear `_compactionMemoryResumeTickMs` after a newer memory-pressure event wrote a later resume tick; `math.max` on `uint` also added avoidable compile risk.
Solution: Pause clearing now uses `Interlocked.CompareExchange` against the observed tick, and frame index updates use explicit unsigned comparison.
Rejected Alternatives: Leaving unsynchronized worker mutation was rejected because memory pressure is exactly when deterministic throttling matters.
Scalability potential: Low-tier devices under memory pressure keep compaction paused for the newest pressure window; high-end devices pay only cold branch cost.
Hardware Impact: No steady-frame cost. During pressure, the branch prevents IO resumption from racing against newer recovery work.

## Decision 12: Deferred Dirty Append Must Not Masquerade As Corruption
Problem: The public dirty flush path is called by `SaveManager.FlushWfcOutpostDirtyPayloadAsync`; a false return publishes a corrupt-payload warning. During compaction, false meant "append queue intentionally halted, dirty payload still retained", not data corruption. This would spam false telemetry and could cause integrators to chase a nonexistent data-loss path.
Solution: `TryAppendDirtyPayload` now returns true when the dirty sector exists and compaction is the only reason it cannot append immediately. `TryAppendDirtyPayloadLocked` remains strict for internal eviction, so a dirty sector cannot be evicted while compaction owns the write queue.
Rejected Alternatives: A SaveManager retry loop was rejected because it adds async churn and duplicates database state policy outside the owner. Changing the internal append method was rejected because `EvictDistant` depends on false to keep dirty sectors resident.
Scalability potential: Low/Mx350 avoids false warning traffic during long MicroSD compactions; High/Ultra still get immediate disk append when compaction is idle and deterministic final flush when active.
Hardware Impact: Explicit flush attempts pay one native hash lookup and one branch while compaction is active. Steady-frame cost remains 0 us.

## Decision 13: Local Compile Wall Cleared, Unity Runtime Still Unverified
Problem: The prior compile wall masked whether the Awaitable/background compaction locks were syntactically valid in the generated Unity C# project.
Solution: Reran `dotnet build .\Hecton8.Core.csproj -v:minimal` after the deferred dirty append fix. The build exited 0 with 48 warnings and 0 errors; warnings are package/third-party or unrelated existing warnings, not compactor-owned failures.
Rejected Alternatives: Treating the successful dotnet build as runtime proof was rejected. Unity import, console, play mode, GCMonitor, and live stall measurements still require a working Unity MCP/editor connection.
Scalability potential: Compile proof allows integrator/runtime validation to focus on IO behavior and tier thresholds instead of syntax/dependency failures.
Hardware Impact: No runtime change. Verification gain is earlier failure detection before MX350/MicroSD playmode profiling.

## Decision 14: Temp Truncation Is A Required Commit Gate
Problem: `TruncateToAppendOffset()` can fail if the temp file cannot flush, shrink, or remap. The previous code ignored that boolean and could still mark the temp file ready or proceed toward replacement.
Solution: Both background copy and main-thread swap now fail-fast if temp truncation/remap fails. Swap records a compaction fault and leaves the authoritative `.h8db` intact.
Rejected Alternatives: Keeping an overlarge temp file was rejected because a failed remap is not just wasted slack; it means the temp handle is not proven valid for promotion.
Scalability potential: Low/MicroSD gets deterministic failure instead of promoting suspect temp output after flaky flash IO. High/Ultra pay only a cold branch.
Hardware Impact: 0 us steady-frame cost. Cold compaction path gains one branch and avoids an invalid swap after storage/remap failure.

## Decision 15: Second Compile Pass After Truncation Gate
Problem: The truncation fail-fast patch touches the Awaitable compaction flow and must not reintroduce a compile wall.
Solution: Reran `dotnet build .\Hecton8.Core.csproj -v:minimal`; it exited 0 with 34 warnings and 0 errors. Warnings remain package/third-party or unrelated existing project warnings.
Rejected Alternatives: Skipping rebuild after a cold-path durability patch was rejected because the prompt explicitly requires Awaitable thread lock verification.
Scalability potential: No runtime change; this keeps integration risk bounded before MicroSD/Steam Deck profiling.
Hardware Impact: No runtime cost. Verification cost only.

## Decision 16: Remove Temp FileInfo Allocation
Problem: The copy worker allocated `FileInfo` and queried filesystem metadata only to obtain the compact temp size after truncation.
Solution: Use `target._mappedBytes`, which is set by the successful `TruncateToAppendOffset()` remap and already represents the authoritative compact file length.
Rejected Alternatives: Keeping `FileInfo` was rejected as unnecessary managed allocation and redundant IO metadata work in a zero-GC-biased compaction system.
Scalability potential: Low/MicroSD avoids an extra filesystem stat during compaction completion; High/Ultra get the same state with less cold overhead.
Hardware Impact: Removes one cold managed allocation and one file stat per compaction pass. Steady-frame impact remains 0 us.

## Decision 17: Node-Reuse-Off Build Verdict
Problem: A normal parallel build attempt hit `MSB4166` child-node premature exit before compiler diagnostics, which is a build-server failure, not a code verdict.
Solution: Reran with `-m:1 /nr:false`; the build exited 0 with 6 warnings and 0 errors. This is the current compiler evidence for the compactor patch.
Rejected Alternatives: Treating `MSB4166` as a compactor failure was rejected because it produced no C# diagnostic and disappeared under single-node no-reuse execution.
Scalability potential: No runtime change. Verification is now deterministic enough for this local pass.
Hardware Impact: No runtime cost.

## Decision 18: Dirty Queue Clears Only After Authoritative Swap
Problem: Finalization removed dirty entries from the source queue while copying them into temp. If a later temp write, truncation, or `File.Replace` failed, the original `.h8db` would remain authoritative but the dirty queue would have already forgotten some pending writes.
Solution: `FlushDirtyPayloadsIntoTargetLocked` now copies dirty payloads into temp and reports a count without mutating the source dirty queue. The queue is cleared only after `File.Replace` succeeds and the active DB is reopened.
Rejected Alternatives: Keeping the old remove-as-you-copy loop was rejected because temp is not authoritative until atomic swap completes. Retrying only failed tail entries was rejected because it cannot recover entries already removed before a later truncate/replace failure.
Scalability potential: Low/MicroSD gets crash/failure-safe deferred writes during slow compactions; High/Ultra keep the same behavior with no additional frame cost.
Hardware Impact: 0 us steady-frame cost. Swap finalizer performs one native hash clear and one native list clear only after success.

## Decision 19: Latest Compile Wall Is Outside Macro DB
Problem: The post-atomicity build no longer reaches a clean global verdict because concurrent unrelated files now fail: `FaunaKinematicsRuntime.cs` references missing `LeviathanTerrainIkJob.TailWhipDurationSeconds`, and `PlayerCriticalProceduralAudioRenderer.cs` references missing `PrologueSplashdownSineSweepProbeJob`.
Solution: Classified the latest compile as dependency-blocked. The compactor patch is not reverted or broadened into Fauna/Audio ownership.
Rejected Alternatives: Editing Fauna or Audio from the macro DB compactor task was rejected as domain breach and high regression risk.
Scalability potential: No runtime change; this preserves domain isolation while keeping the compactor evidence honest.
Hardware Impact: None.

## Decision 20: Current Compile Wall Remains Outside Macro DB
Problem: The current shared-tree build now fails earlier in `PredatorCognitionDomain.cs(1680,59)` because concurrent Fauna work references missing `AlphaLeviathanTelemetryFlags.NoPlayerTarget`.
Solution: Refreshed the build log and status evidence while leaving Fauna ownership untouched. No Macro DB diagnostic appears before this dependency wall.
Rejected Alternatives: Adding the missing Alpha Leviathan telemetry flag from the backend compactor task was rejected as a domain breach and would hide responsibility from the Fauna/AI owner.
Scalability potential: No runtime change. The compactor remains isolated and ready for integration once the current Fauna compile wall is cleared.
Hardware Impact: None.

## Decision 21: Public Dirty Flush Is Idempotent After Commit
Problem: `SaveManager` queues an async dirty append after `MarkDirty`. If compaction finalization commits that dirty payload and clears the queue before the async append runs, the public append call sees no dirty entry and previously returned false, publishing a false corrupt-payload warning.
Solution: `TryAppendDirtyPayload` now returns success when no dirty entry remains but the sector has a valid committed payload in the B-tree. `TryAppendDirtyPayloadLocked` stays strict so eviction cannot drop a pending dirty sector.
Rejected Alternatives: A managed recent-commit list was rejected for extra state and GC risk. A SaveManager retry loop was rejected because it duplicates database ownership and adds Awaitable churn.
Scalability potential: Low/MicroSD avoids false telemetry during long compactions and slow worker scheduling; High/Ultra keep the same idempotent API semantics with no steady-frame cost.
Hardware Impact: 0 us steady-frame cost. Only stale public flush calls pay one B-tree lookup and payload-header validation.

## Decision 22: Post-Idempotency Build Pass
Problem: The public flush idempotency patch touched unsafe Macro DB code and required a compiler verdict after earlier shared-tree compile walls.
Solution: Reran `dotnet build .\Hecton8.Core.csproj --no-restore --disable-build-servers -v:minimal -m:1 /nr:false /p:UseSharedCompilation=false`; it exited 0 with 36 warnings and 0 errors.
Rejected Alternatives: Treating the earlier dependency-blocked build as sufficient was rejected because the unsafe discard call and B-tree validation branch needed direct compiler evidence.
Scalability potential: No runtime change. The API race fix now has project-level compile evidence for integrator/runtime validation.
Hardware Impact: No runtime cost. Verification-only.
