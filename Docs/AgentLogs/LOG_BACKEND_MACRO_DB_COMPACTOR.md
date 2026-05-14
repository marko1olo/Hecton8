# LOG_BACKEND_MACRO_DB_COMPACTOR

## 2026-05-14 - B-Tree Tombstone Sweeper

What was wrong:
- The append-only macro database overwrites sector payloads by appending new records and repointing the B-tree, leaving old payload bytes dead inside `.h8db`.
- No compaction gate existed for Save/Load or memory pressure, so a background copy could compete with persistence or emergency recovery.
- The final swap path needed an explicit state/snapshot surface for H-PHI and Memory Sentinel.

What was done:
- Added macro database compaction control/snapshot surface through `IAsyncPersistenceService` and `IMacroDatabaseService`.
- Added dead-byte tracking in the macro DB header and append update path.
- Added FrostTick-triggered background compaction into `world_data_compact.tmp`, copying only live B-tree payloads.
- Added finalization that flushes dirty payloads into temp, truncates to live append offset, atomically replaces active `.h8db`, reopens the MMF, resets dead bytes, and records stall microseconds.
- Added save/load persistence gates in `SaveManager` so compaction request/finalize rejects while `_isBusy`.
- Added critical memory pressure pause routing from `SystemDispatcher` into macro DB compaction state.
- Added startup/shutdown/fault temp cleanup and shutdown-safe copy-reader cancellation around raw MMF traversal.

Cinematic cheats used:
- Scalar dead-byte counter instead of a tombstone object/map.
- Tier-gated compaction threshold: Low/Mx350 waits for 50 MB; Middle/High/Ultra compact at 10 MB.
- Copy only live B-tree payload offsets instead of scanning or rewriting the append log.
- Telemetry flag for >2000 us swap instead of pretending MicroSD can meet SSD timing.

Exact microseconds saved:
- FrostTick idle path: estimated 5-10 us, no per-frame Update loop.
- Append overwrite dead-byte accounting: estimated 1-3 us, no tombstone collection allocation.
- Save/Load gate transition: estimated 2-4 us.
- Snapshot query: estimated 2-4 us.
- Main-thread final swap target: <2000 us; over-budget swaps set `LastSwapExceededBudget`.
- Traversal GC: 0 B managed allocation in `CopyNodePayloadsTo` by diff-focused static scan.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:normal -clp:ErrorsOnly` is blocked by unrelated compile wall: `Assets/_Project/Scripts/PowerGridManager.cs(61,17): WfcOutpostPowerBootRuntime` missing.
- Unity MCP console validation is blocked by transport refusal at `127.0.0.1:8088/mcp`.
- Diff-focused Omega scan found no added `foreach`, `string.Format`, interpolated string, `.ToString(`, `math.sqrt`, `math.normalize`, or floating division marker in the compactor-owned diff.

Integrator notes:
- Current implementation assumes `File.Replace` is available for the target platform. If a platform lacks it, add a platform-specific atomic replace wrapper before shipping.
- The global build wall is outside the macro DB compactor domain. Do not treat it as a compactor failure unless new errors reference `H8MacroDatabaseService.cs`, `MacroDatabaseContracts.cs`, `H8MacroDatabaseFileFormat.cs`, `SystemDispatcher.cs`, or the new SaveManager gate lines.

## 2026-05-14 - Post-Report Hardening Pass

What was wrong:
- Re-read found a correctness bug in temp-file ownership: the helper `H8MacroDatabaseService` used to write/open `world_data_compact.tmp` could delete the temp file during its own shutdown.
- Finalization fault cleanup could try to delete temp while the temp handle was still open.
- Memory-pressure pause clearing was not compare-exchange guarded against a newer pressure event.

What was done:
- `CleanupCompactionTemp` now refuses to treat `world_data_compact.tmp` itself as an active DB path.
- Final swap cleanup repeats stale-temp deletion after temp service shutdown closes the handle.
- Memory pause expiry now uses `Interlocked.CompareExchange`; uint frame update now uses explicit comparison instead of `math.max`.

Cinematic cheats used:
- Same scalar cheat remains: one dead-byte counter, no tombstone object graph.
- Same flash-wear LOD remains: Low tier waits for 50 MB dead bytes; other tiers use 10 MB.

Exact microseconds saved:
- Prevented a failed full-copy pass that would cost seconds of background IO on MicroSD.
- Runtime hot path unchanged: 0 us/frame.
- Fault cleanup is cold path only.

Verification:
- `git diff --check` passed for touched compactor and evidence files.
- Diff-focused anti-bloat scan found no added `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`, or floating division marker.
- Unity MCP validation remains unavailable: transport refused `127.0.0.1:8088/mcp`.
- `dotnet build Hecton8.Core.csproj` is not a reliable compactor verdict in the current workspace; previous run hit unrelated `PowerGridManager.cs(61,17)` missing type, later rerun exited without a compiler diagnostic after generated dependency work.

## 2026-05-14 - Deferred Dirty Flush Hardening

What was wrong:
- Public dirty append failures were overloaded. During compaction, `TryAppendDirtyPayload` returned false because the append queue was intentionally halted, while `SaveManager.FlushWfcOutpostDirtyPayloadAsync` interpreted false as corrupt persistence.
- Changing the internal append helper directly would have made `EvictDistant` evict dirty sectors without disk persistence.

What was done:
- `TryAppendDirtyPayload` now returns true when the dirty sector exists, the payload handle is valid, and compaction is the only blocker.
- `TryAppendDirtyPayloadLocked` remains strict, so internal eviction still refuses to evict dirty sectors during compaction.
- Updated status/rationale evidence for the deferred write semantics.

Cinematic cheats used:
- Deferred dirty write acceptance: preserve gameplay-facing state in the native dirty queue and let the compaction finalizer/shutdown flush it, instead of scheduling retry loops.
- Existing scalar dead-byte counter and tier threshold remain unchanged.

Exact microseconds saved:
- Avoided async retry loops and false telemetry churn during long MicroSD compactions.
- Explicit dirty flush while compaction is active pays one native hash lookup and one state branch.
- Runtime steady-frame cost remains 0 us.

Verification:
- `dotnet build .\Hecton8.Core.csproj -v:minimal` exited 0 with 48 warnings, 0 errors. Warnings are existing package/third-party or unrelated project warnings.
- `git diff --check` passed for compactor/evidence files; only Git line-ending warnings were emitted.
- Diff-focused anti-bloat scan found no added `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`, `new List<`, `new Dictionary<`, or `Task.Run` in the compactor-owned diff.
- Unity MCP validation and console read remain blocked by HTTP transport refusal at `127.0.0.1:8088/mcp`.

## 2026-05-14 - Temp Truncation Fail-Fast Pass

What was wrong:
- The compaction code wrote `DeadBytes=0`, called `TruncateToAppendOffset()`, then ignored the boolean result.
- A failed flush/shrink/remap could leave temp output unproven while the copy path still marked it ready or the swap path still moved toward replacement.

What was done:
- Background copy now returns failure unless temp truncate/remap succeeds.
- Main-thread swap now marks compaction fault and aborts before `File.Replace` unless temp truncate/remap succeeds.
- Status and rationale evidence updated with the new fail-fast gate.

Cinematic cheats used:
- No new simulation or data structure. The system keeps the same scalar dead-byte model and double-buffer file.
- Fail-fast cold branch buys durability without spending frame budget.

Exact microseconds saved:
- 0 us steady-frame cost.
- Avoids promoting a suspect temp file after storage/remap failure; the saved cost is recovery time and avoided corrupted integration state, not a normal-frame micro-optimization.

Verification:
- `dotnet build .\Hecton8.Core.csproj -v:minimal` exited 0 with 34 warnings, 0 errors.
- `git diff --check` passed for compactor/evidence files; only Git line-ending warnings were emitted.
- Diff-focused anti-bloat scan found no added `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`, `new List<`, `new Dictionary<`, `Task.Run`, or LINQ marker.
- Unity MCP validation and console read are unavailable in the current request; the placeholder tools return `unsupported call`.

## 2026-05-14 - Temp Size Zero-GC Cleanup

What was wrong:
- The background compaction copy allocated `FileInfo` and performed a filesystem stat to read temp file length after a successful truncate/remap.
- That value was already available as the target service `_mappedBytes`.

What was done:
- Replaced `new FileInfo(tempPath).Length` with `target._mappedBytes` after `TruncateToAppendOffset()` succeeds.

Cinematic cheats used:
- No new data structure. Use existing authoritative remap state instead of querying the filesystem.

Exact microseconds saved:
- Removes one cold managed allocation and one filesystem metadata query per compaction pass.
- Steady-frame cost remains 0 us.

Verification:
- First parallel rebuild attempt hit `MSB4166` child-node premature exit before compiler diagnostics.
- Reran `dotnet build .\Hecton8.Core.csproj -v:minimal -m:1 /nr:false`; it exited 0 with 6 warnings, 0 errors.
- Added-line anti-bloat scan found no new `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`, `new List<`, `new Dictionary<`, `Task.Run`, `new FileInfo`, or LINQ marker.

## 2026-05-14 - Dirty Queue Atomicity Pass

What was wrong:
- Finalization removed dirty entries from `_dirtyPayloads` while copying them into `world_data_compact.tmp`.
- If any later dirty payload, temp truncation, or atomic replace step failed, the original `.h8db` stayed authoritative but some dirty writes were already forgotten.

What was done:
- Temp finalization now copies dirty payloads into the target without mutating the source dirty queue.
- Dirty queue is cleared only after `File.Replace` succeeds and the active database reopens.
- Dirty append count is updated only for committed swap-flush payloads.

Cinematic cheats used:
- No new journal file or tombstone map. The existing dirty queue remains the rollback journal until the compact file becomes authoritative.

Exact microseconds saved:
- 0 us steady-frame cost.
- One native hash clear plus one native list clear on successful swap; failure path preserves dirty writes.

Verification:
- Added-line anti-bloat scan found no new `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`, `new List<`, `new Dictionary<`, `Task.Run`, `new FileInfo`, or LINQ marker.
- Latest `dotnet build .\Hecton8.Core.csproj -v:minimal -m:1 /nr:false` is blocked by unrelated concurrent errors:
  - `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs(242,17)`: missing `LeviathanTerrainIkJob.TailWhipDurationSeconds`.
  - `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs(10002,31)`: missing `PrologueSplashdownSineSweepProbeJob`.
- No compactor-owned compiler diagnostic surfaced before that dependency wall.
