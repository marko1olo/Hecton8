# X_018 Log

What was wrong:
- The task names `SignalBusRegistry.cs`, but the active registry lives inside `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`.
- The active typed signal corridor is not `SpscSignalRingBuffer<T>` backed. It uses `NativeQueue<T>` and exposes a legacy `ParallelWriter`.
- `PaddedSignalIndex` is 64 bytes, but parent cache-line alignment is not proven by source.
- Registry registration mutates managed dispatch tables without lock/CAS and is safe only as cold single-thread setup.
- Flush has no local producer JobHandle fence.

What was done:
- Read `SpscSignalRingBuffer.cs` and the `SignalBusRegistry` / `SignalBus<T>` implementation in `SignalBusRuntime.cs`.
- Read `SignalLaneTelemetry` layout in `HectonSignalLaneContract.cs`.
- Read Unity `NativeQueue<T>` source for Count and ParallelWriter behavior.
- Wrote structured JSON ledger to `Docs/Reports/SIGNAL_SPSC_SCOUT_REPORT_X_018.json`.
- Wrote Markdown report to `Docs/Reports/SIGNAL_SPSC_SCOUT_REPORT_X_018.md`.

Cinematic Cheats used:
- None. Audit-only task; no simulation, rendering, or physical effect was changed.

Exact Microseconds saved:
- 0 us runtime change. No source code changed.
- Future-risk estimate only: avoiding a false SPSC assumption prevents unbounded synchronization bugs; no measured profiler delta exists.

Verification:
- C# source modifications by X_018: none. `git status` shows `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` modified in the working tree; X_018 did not edit or revert it.
- `dotnet build`: not launched; no C# changes and project rule forbids unnecessary rebuilds under possible CPU/build contention.
- Static scan: `SpscSignalRingBuffer<T>` has no first-party call-site outside its own file.

## APEX Re-Audit Addendum - 2026-05-25

What was wrong:
- Initial report gave the correct high-level synchronization shape but did not state the current 64-bit Editor byte offsets for `SpscSignalRingBuffer<T>`.
- Initial report did not separate `SpscSignalRingBuffer<T>.TryEnqueue` from `SignalBus<T>.Push` strongly enough.

What was done:
- Re-read `SpscSignalRingBuffer.cs` with line numbers.
- Re-read `SignalBusRuntime.cs` registry, push, clear, registration, and snapshot-reader ranges with line numbers.
- Checked compiled metadata for `SpscSignalRingBuffer<T>`, `PaddedSignalIndex`, `NativeArray<T>`, `AtomicSafetyHandle`, `Allocator`, and `SystemID`.
- Wrote `Docs/Reports/SIGNAL_SPSC_SCOUT_APEX_ADDENDUM_X_018.md`.

Cinematic Cheats used:
- None. This is synchronization audit only.

Exact Microseconds saved:
- 0 us runtime change. No C# source edits were made.

Hard findings:
- `_head.Value` offset 56, `_tail.Value` offset 120, distance 64 bytes in current 64-bit Unity Editor metadata-derived layout.
- Source does not prove parent 64-byte base alignment; false-sharing prevention is spacing-proof, not full cache-line-alignment proof.
- SPSC enqueue uses `Volatile.Read` and `Interlocked.Exchange`; no `Thread.MemoryBarrier`, no `Volatile.Write`.
- `SignalBus<T>.Push` uses `NativeQueue<T>`, not `SpscSignalRingBuffer<T>`.
- `SignalBusRegistry.ClearPostSimulationSnapshots()` is phase-safe only if dispatcher exclusivity is obeyed; the registry does not internally prevent concurrent registration/snapshot-reader races.
