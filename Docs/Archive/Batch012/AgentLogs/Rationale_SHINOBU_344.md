# Rationale_SHINOBU_344

Status: POLISH HARDENED / COMPILE BLOCKED BY EXISTING CROSS-DOMAIN DEPENDENCY

## Decision 0 - Preflight Ownership

Problem: Cargo inventory sync can overlap Inventory, Logistics, Docking, Base, UI, and DataVault domains.
Solution: Scope implementation to cargo inventory sync math and isolated artifacts until archaeology identifies the existing owner class. Use partial integration only if an existing runtime owner exists.
Rejected Alternatives: Creating a standalone manager before grep would duplicate ownership and violate one fact -> one owner -> one route.
Scalability potential: Low uses time-sliced 100-slot chunks; Middle uses 300-600; High uses 800; Ultra uses 1000 plus decoupled visual unload overkill.
Hardware Impact: i3/MX350 avoids frame spikes by bounded per-frame slot windows and flat NativeArray scans instead of managed list/object churn.

## Decision 1 - Mandate Set

Problem: Assignment needs inventory SoA, zero GC, native jobs, AUP precision, global authority, and blackbox telemetry.
Solution: Read DATA inventory, zero GC, native jobs, AUP, registry/DI, and telemetry mandates before code.
Rejected Alternatives: Reading every registry mandate would pollute the task context; reading none would produce fake architecture.
Scalability potential: Continuous `GlobalQualityWeight` changes cadence/capacity only, not gameplay truth ownership.
Hardware Impact: DTO padding and NativeArray lanes avoid ARM64 alignment traps and managed heap pressure.

## Decision 2 - Integration Owner

Problem: No `HectonInventoryRuntime` exists; existing SoA ownership lives in `SoaInventoryQueryEngine`.
Solution: Change `SoaInventoryQueryEngine` to partial and add `SoaInventoryQueryEngine.CargoSync.cs` with isolated DTOs, vault accessors, mock data, parser, merge job, telemetry, and signal publishing.
Rejected Alternatives: New `HectonCargoSyncManager` would create a competing owner and require hot registry lookup.
Scalability potential: Low/Middle/High/Ultra all use the same authoritative SoA route; only batch width and presentation change.
Hardware Impact: Avoids one managed object manager and removes scene search from docking transfer.

## Decision 3 - Signal Route

Problem: Cargo completion must notify UI without fragmenting the signal matrix.
Solution: Reuse `InventoryChangedSignal.Load01` for Dear Lie progress and `InventoryDeathLootCacheSignal` for overflow loot cache publication. `DockingCompleteSignal` remains the ingress contract owned elsewhere.
Rejected Alternatives: New `CargoTransferCompleteSignal` would add a lane without payload necessity.
Scalability potential: Low drops progress cadence; Ultra can add visual overkill through existing UI/audio consumers.
Hardware Impact: Reusing configured SignalBus lanes avoids extra NativeQueue capacity and sanitizer paths.

## Decision 4 - Merge Kernel

Problem: Docking freezes come from OOP item transfer patterns and O(N*M) destination scans.
Solution: Burst `ExecuteCargoMergeJob` scans destination hashes with existing AVX2/SSE2/NEON mask helpers, claims source quantities atomically, CAS-adds destination quantities, and reserves empty slots atomically.
Rejected Alternatives: `List.AddRange`, `foreach(ItemData)`, and stable-order item removal all allocate or create data-dependent stalls.
Scalability potential: Low processes 100 slots/frame; Middle 300-600; High 800; Ultra 1000 and spends saved time on visual-only unload effects.
Hardware Impact: i3/MX350 static estimate avoids 7.6-11 ms per 10000-slot OOP transfer. Actual profiler proof remains unavailable in shell.

## Decision 5 - Overflow Loot AUP

Problem: Full destination inventories need deterministic overflow placement without 100 km float precision loss.
Solution: Convert dock grid/local to `double3`, add ejection offset in double precision, then pack to `AbsoluteUniversePositionBlit` in `LootCacheDTO`.
Rejected Alternatives: Casting absolute position to `float3` before offset would jitter or bury loot at large coordinates.
Scalability potential: Same math at all tiers; visual scatter radius can scale continuously through tuning.
Hardware Impact: Double math is only used on overflow writes, not every slot scan.

## Decision 6 - Blackbox Telemetry

Problem: "Unknown cargo crash" is rejected by the black box rule.
Solution: Allocate a 300-entry cargo telemetry ring in GlobalDataVault, write frame/source/dest/progress/overflow/conflict/state hash, and dump raw bytes to `Docs/AgentLogs/Dump_SHINOBU_344.bin` on fault request.
Rejected Alternatives: Debug.Log-only diagnostics or managed history lists.
Scalability potential: Fixed 300 frames on all devices; telemetry cadence can be presentation-thinned without changing authoritative state.
Hardware Impact: 19.2 KB ring, fixed footprint, no per-frame managed allocation.

## Decision 7 - Editor Facades

Problem: Designers need live tuning and developers need visibility without modifying gameplay authority.
Solution: Add UI Toolkit tuner for vault-backed tuning DTO and telemetry histogram; add debug gizmo reading raw transaction/progress buffers and drawing AUP-relative line plus seven-segment remaining count.
Rejected Alternatives: Serialized MonoBehaviour settings, string labels in gizmo hot draw, or new signal lane.
Scalability potential: Low devices can tune smaller batches; high devices can raise visual scatter/progress polish while keeping same truth route.
Hardware Impact: Editor-only allocations are outside runtime transfer; gizmo number draw avoids per-frame string label allocation.

## Decision 8 - Static Validator

Problem: OOP cargo merge eradication needs a proof artifact.
Solution: Add `OOP_Cargo_Scanner` scanning Logistics/Vehicles for `AddRange`, foreach item transfer, `TransferItems`, and `Inventory.Sync`, writing `Docs/Reports/LOGISTICS_OPTIMIZATION_REPORT.json`.
Rejected Alternatives: Chat claim or broad third-party scan.
Scalability potential: Prevents reintroduction of managed cargo transfer scripts.
Hardware Impact: Avoids future heap-resize regressions on low-end CPUs.

## Decision 9 - Compile Wall

Problem: Build verification failed outside SHINOBU_344 ownership.
Solution: Ran one allowed build only after CPU <50 and no csc/dotnet; recorded the three errors and stopped instead of editing Airlock/Solar domains.
Rejected Alternatives: Cross-domain DTO repair would violate domain boundary; repeated build attempts would violate CPU/build spam rule.
Scalability potential: No runtime claim until dependency wall clears.
Hardware Impact: Build wall is integration state, not cargo runtime cost.

## Decision 10 - Interlocked.Add Destination Quantity Fence

Problem: The original destination add was CAS-only. It was atomic, but it did not satisfy the explicit SHINOBU_344 requirement for `Interlocked.Add` on the merge quantity path.
Solution: Use the high bit of the 32-bit quantity lane as a transient CAS-owned lock, execute `Interlocked.Add` while the lane is locked, then clear the lock with `Interlocked.Exchange`. Authoritative quantities are clamped to `0..int.MaxValue`; overflow converts to `LootCacheDTO`.
Rejected Alternatives: Raw `uint` add is a data race; `long` counters would change the SoA ABI; a managed lock is illegal in Burst and would freeze docking.
Scalability potential: Low/Middle/High/Ultra all keep the same deterministic integer route; only admitted source rows per slice scale with `GlobalQualityWeight`.
Hardware Impact: i3/MX350 avoids lost updates under concurrent docking writers. The high-bit lock can spin for at most 16 attempts; conflicts are exposed in telemetry instead of blocking the frame.

## Decision 11 - False-Sharing Counter Isolation

Problem: `NativeArray<int>` telemetry cursor and overflow counter could share adjacent cache lines with other Vault rows or each other under parallel writes.
Solution: Introduced `CargoAtomicCounterDTO=64` with `Value@0` and explicit 64-byte layout, changed telemetry cursor to that DTO, and added `BufferID.ShinobuCargoOverflowCounter=73143`.
Rejected Alternatives: Padding managed fields in the job struct does not isolate the underlying native data; keeping `int[1]` was cheap but not architecturally proven.
Scalability potential: Low uses fewer reservations; Middle/High/Ultra can issue richer overflow visual rows without MESI thrash on the counter cache line.
Hardware Impact: Static estimate 80-300 us avoided on low-end multi-core contention when overflow and telemetry reservations occur during busy docking frames.

## Decision 12 - Editor Accessor Purity Repair

Problem: `TryResolveTuning` called `EnsureCargoBuffers`, violating the doctrine that `Try/Get/Resolve/Read` accessors must not allocate, grow, or mutate global state.
Solution: Renamed mutating editor routes to `EnsureTuningBufferAvailable`, `EnsureAndAcquireTuningWrite`, and `EnsureCargoBuffersForEditor`. `TryResolveTelemetry` remains pure read-only and no longer creates cargo buffers.
Rejected Alternatives: Leaving the names would hide mutation behind a read-looking API and pollute future code review.
Scalability potential: Editor-only, but it preserves the runtime owner/read separation needed when many agents operate in parallel.
Hardware Impact: No runtime frame cost. Compile-wall risk is reduced because future callers can distinguish read-only telemetry from editor-only buffer creation.

## Decision 13 - Binary Ledger Route Card

Problem: SHINOBU_344 was implemented in task logs but absent from the central binary payload integration ledger.
Solution: Added a concise ledger entry with owner, BufferIDs, ABI sizes, runtime route, scalability route, AUP route, Dear Lie route, and fault telemetry route.
Rejected Alternatives: Chat-only proof or isolated `Status_SHINOBU_344.md` is not sufficient for cross-agent integration.
Scalability potential: The ledger makes it explicit that `GlobalQualityWeight` changes cadence only, not DTO layout or authority.
Hardware Impact: 0 runtime us; reduces integration risk for cheap-device memory budgets by documenting exact fixed buffer ownership.

## Decision 14 - Roslyn AST Cargo Scanner

Problem: Task 19 required an AST parser, but the first validator implementation was a line scanner. It could catch obvious text hits but did not meet the requested proof standard.
Solution: Rewrote `OOP_Cargo_Scanner` to parse `CSharpSyntaxTree`, inspect invocation expressions for `AddRange`, `TransferItems`, and `Inventory.Sync`, inspect `ForEachStatementSyntax` for item transfer loops, and inspect binary expressions for string category filters. Lexical fallback is used only on parser failure.
Rejected Alternatives: Adding broad regexes would keep the false claim alive; semantic compilation would require heavier project-wide analyzer setup and risk another compile wall.
Scalability potential: The validator prevents reintroduction of managed cargo transfer patterns that would freeze low-end devices; high-end visual overkill remains decoupled from inventory truth.
Hardware Impact: 0 runtime us. Editor-only scan trades a small cold Roslyn cost for stronger regression detection.

## Decision 15 - Unity Import Surface Restraint

Problem: New SHINOBU source files could be rejected by Unity import if their `.meta` contract or Roslyn reference route was invented instead of matching the project.
Solution: Compared new SHINOBU metas against existing `SoaInventoryQueryEngine.cs.meta` and first-party script metas, then verified existing Roslyn scanner precedent and `Hecton8.Core.csproj` references to `Assets/Plugins/Roslyn/Microsoft.CodeAnalysis*.dll`.
Rejected Alternatives: Adding MonoImporter blocks to only SHINOBU metas would create inconsistent asset churn; adding a new asmdef would create an unnecessary compile-wall edge; rerunning build would only rediscover the known Airlock/Solar wall.
Scalability potential: No gameplay scaling change. The decision keeps import/iteration cost low so cargo tuning remains cheap across low, middle, high, and ultra development machines.
Hardware Impact: 0 runtime us. Prevents editor-side import/reference churn that would waste developer hardware cycles without improving docking performance.
