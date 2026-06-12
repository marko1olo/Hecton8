# SIGNAL SPSC SCOUT REPORT X_018

Mode: read-only C# audit. Source modifications by X_018: none. `git status` shows `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` modified in the working tree; X_018 did not edit or revert it.

## Files

- `Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs`: SPSC fallback primitive.
- `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`: contains `SignalBusRegistry` and `SignalBus<T>`.
- `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs`: contains `SignalLaneTelemetry`.
- `Library/PackageCache/com.unity.collections@538ace9075bc/Unity.Collections/NativeQueue.cs`: active SignalBus queue primitive source.

## Findings

1. `SignalBusRegistry.cs` does not exist as a standalone file in the active source tree. `SignalBusRegistry` is declared in `SignalBusRuntime.cs:33-37`.

2. `SpscSignalRingBuffer<T>` is not wired into `SignalBus<T>`. Static source scan found only the declaration/constructors in `SpscSignalRingBuffer.cs:11,20,25`. Active `SignalBus<T>` declares `_queue` as `NativeQueue<T>` at `SignalBusRuntime.cs:305` and exposes `NativeQueue<T>.ParallelWriter` at `SignalBusRuntime.cs:395-408` and `421-427`.

3. `PaddedSignalIndex` is source-proven 64 bytes: `SpscSignalRingBuffer.cs:94-105`. Its `Value` field is at offset 0 (`line 97`), `_pad0.._pad6` occupy offsets 8..56 (`lines 98-104`), and bytes 4..7 are implicit padding. Parent `SpscSignalRingBuffer<T>` has no explicit layout or 64-byte alignment guarantee (`line 11`), so false-sharing isolation between `_head` and `_tail` is not byte-perfectly proven.

4. SPSC enqueue sequence:
   - Read tail with `Volatile.Read`: `SpscSignalRingBuffer.cs:64`.
   - Compute `nextTail = (tail + 1) & _mask`: `line 65`.
   - Read head with `Volatile.Read` for full check: `line 66`.
   - Write payload: `line 69`.
   - Publish tail with `Interlocked.Exchange`: `line 70`.

5. SPSC dequeue sequence:
   - Read head with `Volatile.Read`: `SpscSignalRingBuffer.cs:82`.
   - Read tail with `Volatile.Read` for empty check: `line 83`.
   - Read payload: `line 89`.
   - Publish head with `Interlocked.Exchange`: `line 90`.

6. No `Thread.MemoryBarrier`, `Volatile.Write`, or `Interlocked.CompareExchange` exists in `SpscSignalRingBuffer.cs`. The SPSC cursor publication path relies on `Volatile.Read` plus `Interlocked.Exchange`.

7. `SignalBusRegistry.Register` is cold-only by implementation. It writes `_laneDisposeDispatch`, `_laneTelemetryDispatch`, and `_laneDispatch` arrays and increments `_laneCount` / `_dispatchLaneCount` without lock/CAS: `SignalBusRuntime.cs:90-116`. Dispatch reads `_dispatchLaneCount` with `Volatile.Read`: `lines 246` and `263`. Concurrent first-touch registration is not protected in this file.

8. Registry flush sequence:
   - `FlushPreSimulation()` reads `_systemStressMilli` with `Volatile.Read` and reads pause state: `SignalBusRuntime.cs:148-152`.
   - `FlushRegisteredSignalLanes()` reads `_dispatchLaneCount` with `Volatile.Read`: `line 246`.
   - It invokes each registered flush delegate unless null or blocked by simulation pause: `lines 247-258`.

9. Registry clear sequence:
   - `ClearPostSimulationSnapshots()` calls `ClearRegisteredSignalLaneSnapshots()`: `SignalBusRuntime.cs:156-158`.
   - Clear loop reads `_dispatchLaneCount` with `Volatile.Read` and invokes non-null clear delegates: `lines 263-269`.

10. `SignalBus<T>.FlushPreSimulation` drains a `NativeQueue<T>`, not the SPSC ring. It reads `_queue.Count` at `SignalBusRuntime.cs:809`, may drop oldest overflow at `836-845`, then `TryDequeue`s into a frame snapshot at `847-864`.

11. Unity `NativeQueue<T>.Count` source says count traversal walks the internal linked list: `NativeQueue.cs:66-77`. `SignalBus<T>` reads Count at `SignalBusRuntime.cs:597`, `809`, and `847`.

12. `SignalBus<T>` has no local JobHandle fence before drain. `SignalBusRegistry` invokes flush delegates directly (`SignalBusRuntime.cs:244-258`). Any safety between `NativeQueue<T>.ParallelWriter` producers and flush consumers must be enforced by the external dispatcher phase, not by this file.

## Proven Layouts

### PaddedSignalIndex

Path: `Assets/_Project/Scripts/Core/Signals/SpscSignalRingBuffer.cs:94-105`

Size: 64 bytes, explicit.

| Offset | Size | Field |
|---:|---:|---|
| 0 | 4 | `int Value` |
| 4 | 4 | implicit padding |
| 8 | 8 | `ulong _pad0` |
| 16 | 8 | `ulong _pad1` |
| 24 | 8 | `ulong _pad2` |
| 32 | 8 | `ulong _pad3` |
| 40 | 8 | `ulong _pad4` |
| 48 | 8 | `ulong _pad5` |
| 56 | 8 | `ulong _pad6` |

### SignalLaneTelemetry

Path: `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs:179-198`

Size: 32 bytes, explicit.

| Offset | Size | Field |
|---:|---:|---|
| 0 | 4 | `uint LaneHash` |
| 4 | 4 | `int QueuedBeforeFlush` |
| 8 | 4 | `int SnapshotCount` |
| 12 | 4 | `int DroppedCount` |
| 16 | 4 | `int CoalescedCount` |
| 20 | 1 | `byte Flags` |
| 21 | 1 | `byte Reserved0` |
| 22 | 2 | `ushort Reserved1` |
| 24 | 8 | `ulong Reserved2` |

## Proof Boundaries

- Full `SpscSignalRingBuffer<T>` size is not proven from source. It contains `NativeArray<T>` at `SpscSignalRingBuffer.cs:14`; Unity native-container layout changes with runtime/safety defines.
- `SignalLaneDispatch` exact byte size is managed-runtime dependent because it contains delegate references at `SignalBusRuntime.cs:229-231`.
- External PowerShell CLR reflection was attempted against `Library/ScriptAssemblies/Hecton8.Core.dll`; it failed to materialize the nested explicit layout inside the generic SPSC type. That is not a Unity runtime proof and is not used as a size claim.

## Required Future Guardrails

- Do not treat `SignalBus<T>` as SPSC. It is NativeQueue-backed with a legacy MPSC writer surface.
- Do not first-touch/register lanes from parallel workers. Registration table mutation is cold single-thread only.
- Do not flush a lane while jobs can still hold `NativeQueue<T>.ParallelWriter`. The fence must exist before `SignalBusRegistry.FlushPreSimulation()`.
- Do not claim 64-byte false-sharing immunity for `_head`/`_tail` until parent alignment is measured with Unity/Burst/IL2CPP proof.
